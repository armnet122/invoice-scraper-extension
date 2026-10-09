//! Installs / removes the browser policy that force-installs the Invoice Scraper
//! extension from our self-hosted update feed. The browser then updates it by itself.
use std::io;

const EXT_ID: &str = "glhjedpaiamdajihaejimgfpnlagdpej";
const UPDATE_URL: &str = "https://raw.githubusercontent.com/armnet122/invoice-scraper-extension/main/updates.xml";

fn entry() -> String {
    format!("{EXT_ID};{UPDATE_URL}")
}

#[cfg(windows)]
mod platform {
    use super::*;
    use winreg::{enums::*, RegKey};

    // Chromium-family browsers that read HKLM policy.
    const BROWSERS: &[(&str, &str)] = &[
        ("Google Chrome", r"SOFTWARE\Policies\Google\Chrome"),
        ("Microsoft Edge", r"SOFTWARE\Policies\Microsoft\Edge"),
        ("Brave", r"SOFTWARE\Policies\BraveSoftware\Brave"),
        ("Chromium", r"SOFTWARE\Policies\Chromium"),
        ("Vivaldi", r"SOFTWARE\Policies\Vivaldi"),
    ];

    pub fn install() -> io::Result<()> {
        let hklm = RegKey::predef(HKEY_LOCAL_MACHINE);
        for (name, path) in BROWSERS {
            let (key, _) = hklm.create_subkey(format!(r"{path}\ExtensionInstallForcelist"))?;
            let existing = key
                .enum_values()
                .filter_map(Result::ok)
                .find(|(_, v)| v.to_string().starts_with(EXT_ID));
            let slot = match existing {
                Some((n, _)) => n,
                None => (1..)
                    .map(|i| i.to_string())
                    .find(|n| key.get_value::<String, _>(n).is_err())
                    .unwrap(),
            };
            key.set_value(&slot, &entry())?;
            println!("[ok] {name}");
        }
        Ok(())
    }

    pub fn uninstall() -> io::Result<()> {
        let hklm = RegKey::predef(HKEY_LOCAL_MACHINE);
        for (name, path) in BROWSERS {
            if let Ok(key) = hklm.open_subkey_with_flags(format!(r"{path}\ExtensionInstallForcelist"), KEY_ALL_ACCESS) {
                let ours: Vec<String> = key
                    .enum_values()
                    .filter_map(Result::ok)
                    .filter(|(_, v)| v.to_string().starts_with(EXT_ID))
                    .map(|(n, _)| n)
                    .collect();
                for n in ours {
                    key.delete_value(n)?;
                    println!("[removed] {name}");
                }
            }
        }
        Ok(())
    }

    pub fn pause() {
        println!("\nPress Enter to close.");
        let _ = io::stdin().read_line(&mut String::new());
    }
}

#[cfg(not(windows))]
mod platform {
    use super::*;
    use std::{fs, path::Path};

    const FILE: &str = "invoice-scraper.json";
    const BROWSERS: &[(&str, &str)] = &[
        ("Google Chrome", "/etc/opt/chrome/policies/managed"),
        ("Chromium", "/etc/chromium/policies/managed"),
        ("Chromium (Debian/Ubuntu)", "/etc/chromium-browser/policies/managed"),
        ("Microsoft Edge", "/etc/opt/edge/policies/managed"),
        ("Brave", "/etc/brave/policies/managed"),
    ];

    pub fn install() -> io::Result<()> {
        let body = serde_json::json!({ "ExtensionInstallForcelist": [entry()] });
        for (name, dir) in BROWSERS {
            fs::create_dir_all(dir)?;
            fs::write(Path::new(dir).join(FILE), serde_json::to_string_pretty(&body).unwrap())?;
            println!("[ok] {name}");
        }
        Ok(())
    }

    pub fn uninstall() -> io::Result<()> {
        for (name, dir) in BROWSERS {
            if fs::remove_file(Path::new(dir).join(FILE)).is_ok() {
                println!("[removed] {name}");
            }
        }
        Ok(())
    }

    pub fn pause() {}
}

fn main() {
    let uninstall = std::env::args().nth(1).is_some_and(|a| a == "uninstall" || a == "--uninstall");
    println!("Invoice Scraper extension {}\n", if uninstall { "removal" } else { "setup" });
    let res = if uninstall { platform::uninstall() } else { platform::install() };
    match res {
        Ok(()) if uninstall => println!("\nDone. Restart your browsers."),
        Ok(()) => println!(
            "\nDone. Restart your browsers; the extension installs and updates itself.\nVerify at chrome://policy and chrome://extensions."
        ),
        Err(e) if e.kind() == io::ErrorKind::PermissionDenied => {
            eprintln!("\nPermission denied: run as Administrator (Windows) or with sudo (Linux).")
        }
        Err(e) => eprintln!("\nError: {e}"),
    }
    platform::pause();
}
