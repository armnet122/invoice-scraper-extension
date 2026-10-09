//! Windows: keeps the unpacked extension folder current from the GitHub release feed.
use std::{
    fs,
    io::{self, Cursor},
    os::windows::process::CommandExt,
    path::Path,
    process::Command,
};

/// Folder the user loads with "Load unpacked". Files are only added/overwritten, never deleted.
pub const EXT_DIR: &str = r"C:\InvoiceScraperExtension\extension";
const FEED: &str = "https://raw.githubusercontent.com/armnet122/invoice-scraper-extension/main/updates.xml";

pub struct Release {
    pub version: String,
    pub url: String,
}

fn download(url: &str) -> io::Result<Vec<u8>> {
    // curl.exe ships with Windows 10+, which saves us an HTTP client dependency
    let out = Command::new("curl.exe").args(["-sfL", "--max-time", "60", url]).creation_flags(0x0800_0000).output()?;
    if out.status.success() {
        Ok(out.stdout)
    } else {
        Err(io::Error::new(io::ErrorKind::Other, format!("download failed: {url}")))
    }
}

fn attr<'a>(xml: &'a str, name: &str) -> Option<&'a str> {
    let rest = &xml[xml.find(&format!("{name}='"))? + name.len() + 2..];
    Some(&rest[..rest.find('\'')?])
}

pub fn latest() -> io::Result<Release> {
    let all = String::from_utf8_lossy(&download(FEED)?).into_owned();
    let xml = &all[all.find("<updatecheck").unwrap_or(0)..]; // skip the <?xml version='1.0'?> prolog
    match (attr(xml, "version"), attr(xml, "codebase")) {
        (Some(v), Some(u)) => Ok(Release { version: v.into(), url: u.into() }),
        _ => Err(io::Error::new(io::ErrorKind::InvalidData, "unreadable update feed")),
    }
}

pub fn local_version() -> Option<String> {
    let m: serde_json::Value = serde_json::from_slice(&fs::read(Path::new(EXT_DIR).join("manifest.json")).ok()?).ok()?;
    m["version"].as_str().map(String::from)
}

pub fn is_newer(remote: &str, local: &str) -> bool {
    let n = |s: &str| s.split('.').map(|p| p.parse::<u32>().unwrap_or(0)).collect::<Vec<_>>();
    n(remote) > n(local)
}

/// Downloads the release CRX and unpacks its files into `EXT_DIR`.
pub fn install(rel: &Release) -> io::Result<()> {
    let crx = download(&rel.url)?;
    if crx.len() < 12 || &crx[..4] != b"Cr24" {
        return Err(io::Error::new(io::ErrorKind::InvalidData, "not a CRX file"));
    }
    let header = u32::from_le_bytes(crx[8..12].try_into().unwrap()) as usize;
    let mut zip = zip::ZipArchive::new(Cursor::new(&crx[12 + header..])).map_err(|e| io::Error::new(io::ErrorKind::InvalidData, e))?;
    fs::create_dir_all(EXT_DIR)?;
    for i in 0..zip.len() {
        let mut f = zip.by_index(i).map_err(|e| io::Error::new(io::ErrorKind::InvalidData, e))?;
        // flat extension: take the bare file name only, so a crafted entry can't escape the folder
        let Some(name) = f.enclosed_name().and_then(|p| p.file_name().map(|n| n.to_owned())) else { continue };
        if f.is_file() {
            io::copy(&mut f, &mut fs::File::create(Path::new(EXT_DIR).join(name))?)?;
        }
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    #[ignore = "needs network; writes EXT_DIR"]
    fn live_feed_and_install() {
        let r = latest().unwrap();
        assert!(r.url.ends_with(".crx"));
        install(&r).unwrap();
        assert_eq!(local_version().as_deref(), Some(r.version.as_str()));
    }

    #[test]
    #[ignore = "shows a real toast"]
    fn toast_shows() {
        register_app_id();
        Toast::new(APP_ID).title("Invoice Scraper").text1("test toast").add_button("Update", "update").on_activated(|_| Ok(())).show().unwrap();
    }

    #[test]
    fn feed_and_versions() {
        let x = "<updatecheck codebase='https://x/y.crx' version='3.1.1' />";
        assert_eq!((attr(x, "version"), attr(x, "codebase")), (Some("3.1.1"), Some("https://x/y.crx")));
        assert!(is_newer("3.10.0", "3.9.9") && is_newer("3.1.1", "3.1.0") && !is_newer("3.1.1", "3.1.1"));
    }
}

// ---- notifications & scheduling ----
use std::sync::Arc;
use tauri_winrt_notification::{Duration as ToastDuration, Toast};

const APP_ID: &str = "InvoiceScraper";

/// Lets Windows show our name on toasts (unpackaged apps need an AppUserModelID entry).
pub fn register_app_id() {
    use winreg::{enums::HKEY_CURRENT_USER, RegKey};
    if let Ok((k, _)) = RegKey::predef(HKEY_CURRENT_USER).create_subkey(r"Software\Classes\AppUserModelId\InvoiceScraper") {
        let _ = k.set_value("DisplayName", &"Invoice Scraper");
    }
}

pub fn notify(title: &str, text: &str) {
    let _ = Toast::new(APP_ID).title(title).text1(text).show();
}

fn apply(rel: &Release) {
    match install(rel) {
        Ok(()) => {
            let _ = Toast::new(APP_ID)
                .title("Invoice Scraper updated")
                .text1(&format!("Files in the extension folder are now v{}. Restart your browsers to load them.", rel.version))
                .add_button("Restart browsers", "restart")
                .on_activated(|a| {
                    if a.as_deref() == Some("restart") {
                        crate::browsers::restart();
                    }
                    Ok(())
                })
                .show();
        }
        Err(e) => notify("Update failed", &e.to_string()),
    }
}

/// `manual` also reports "up to date" / failures; scheduled checks stay silent in those cases.
pub fn check(manual: bool) {
    let Ok(rel) = latest() else {
        if manual {
            notify("Update check failed", "Could not reach GitHub.");
        }
        return;
    };
    let local = local_version();
    if local.as_deref().is_some_and(|l| !is_newer(&rel.version, l)) {
        if manual {
            notify("Up to date", &format!("Extension v{} is current.", rel.version));
        }
        return;
    }
    let text = format!("Version {} is available (installed: {}).", rel.version, local.as_deref().unwrap_or("none"));
    let rel = Arc::new(rel);
    let _ = Toast::new(APP_ID)
        .title("Invoice Scraper update available")
        .text1(&text)
        .duration(ToastDuration::Long)
        .add_button("Update", "update")
        .on_activated(move |a| {
            if a.as_deref() == Some("update") {
                apply(&rel);
            }
            Ok(())
        })
        .show();
}
