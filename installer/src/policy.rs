//! Config file + browser policy writer. The policy force-installs the extension and
//! passes it settings through Chrome's managed-storage (`3rdparty`) policy.
use serde::{Deserialize, Serialize};
use std::{fs, io, path::PathBuf};

pub const EXT_ID: &str = "glhjedpaiamdajihaejimgfpnlagdpej";
pub const UPDATE_URL: &str = "https://raw.githubusercontent.com/armnet122/invoice-scraper-extension/main/updates.xml";

#[derive(Serialize, Deserialize, Clone)]
#[serde(default)]
pub struct Config {
    pub enabled: bool,
    pub domains: Vec<String>,
    pub show_toast: bool,
    pub auto_enter: bool,
    pub show_floating_input: bool,
    pub autostart: bool,
}

impl Default for Config {
    fn default() -> Self {
        Self {
            enabled: true,
            domains: ["dvla.gov.gh", "genesys.com", "genesyscloud.com"].map(String::from).to_vec(),
            show_toast: true,
            auto_enter: true,
            show_floating_input: true,
            autostart: true,
        }
    }
}

pub fn config_dir() -> PathBuf {
    #[cfg(windows)]
    let base = PathBuf::from(std::env::var("ProgramData").unwrap_or_else(|_| r"C:\ProgramData".into()));
    #[cfg(not(windows))]
    let base = PathBuf::from("/etc");
    base.join("InvoiceScraper")
}

fn config_path() -> PathBuf {
    config_dir().join("config.json")
}

pub fn load() -> Config {
    fs::read(config_path()).ok().and_then(|b| serde_json::from_slice(&b).ok()).unwrap_or_default()
}

pub fn save(cfg: &Config) -> io::Result<()> {
    fs::create_dir_all(config_dir())?;
    fs::write(config_path(), serde_json::to_string_pretty(cfg).unwrap())
}

/// Bare host name such as `example.com` (no scheme, path or wildcard).
pub fn valid_domain(d: &str) -> bool {
    let labels: Vec<&str> = d.split('.').collect();
    labels.len() >= 2
        && labels.iter().all(|l| {
            !l.is_empty() && !l.starts_with('-') && !l.ends_with('-') && l.bytes().all(|b| b.is_ascii_alphanumeric() || b == b'-')
        })
}

/// One domain per line; `#` comments allowed. Accepts pasted URLs and strips them to the host.
pub fn parse_domains(text: &str) -> Result<Vec<String>, String> {
    let mut out: Vec<String> = Vec::new();
    for line in text.lines().map(str::trim).filter(|l| !l.is_empty() && !l.starts_with('#')) {
        let host = line.split("://").last().unwrap().split(['/', ':']).next().unwrap();
        let host = host.trim_start_matches("*.").to_lowercase();
        if !valid_domain(&host) {
            return Err(format!("not a valid domain: {line}"));
        }
        if !out.contains(&host) {
            out.push(host);
        }
    }
    if out.is_empty() {
        return Err("add at least one domain".into());
    }
    Ok(out)
}

fn entry() -> String {
    format!("{EXT_ID};{UPDATE_URL}")
}

#[cfg(windows)]
mod os {
    use super::*;
    use winreg::{enums::*, RegKey};

    const BROWSERS: &[(&str, &str)] = &[
        ("Google Chrome", r"SOFTWARE\Policies\Google\Chrome"),
        ("Microsoft Edge", r"SOFTWARE\Policies\Microsoft\Edge"),
        ("Brave", r"SOFTWARE\Policies\BraveSoftware\Brave"),
        ("Chromium", r"SOFTWARE\Policies\Chromium"),
        ("Vivaldi", r"SOFTWARE\Policies\Vivaldi"),
    ];

    pub fn apply(cfg: &Config) -> io::Result<Vec<&'static str>> {
        let hklm = RegKey::predef(HKEY_LOCAL_MACHINE);
        let mut done = Vec::new();
        for (name, path) in BROWSERS {
            let (list, _) = hklm.create_subkey(format!(r"{path}\ExtensionInstallForcelist"))?;
            let ours: Vec<String> = list
                .enum_values()
                .filter_map(Result::ok)
                .filter(|(_, v)| v.to_string().starts_with(EXT_ID))
                .map(|(n, _)| n)
                .collect();
            for n in ours {
                list.delete_value(n)?;
            }
            let base = format!(r"{path}\3rdparty\extensions\{EXT_ID}");
            let _ = hklm.delete_subkey_all(&base);
            if cfg.enabled {
                let slot = (1..).map(|i| i.to_string()).find(|n| list.get_value::<String, _>(n).is_err()).unwrap();
                list.set_value(&slot, &entry())?;
                let (pol, _) = hklm.create_subkey(format!(r"{base}\policy"))?;
                pol.set_value("showToast", &(cfg.show_toast as u32))?;
                pol.set_value("autoEnter", &(cfg.auto_enter as u32))?;
                pol.set_value("showFloatingInput", &(cfg.show_floating_input as u32))?;
                let (doms, _) = pol.create_subkey("domains")?;
                for (i, d) in cfg.domains.iter().enumerate() {
                    doms.set_value((i + 1).to_string(), d)?;
                }
            }
            done.push(*name);
        }
        Ok(done)
    }
}

#[cfg(not(windows))]
mod os {
    use super::*;
    use std::path::Path;

    const FILE: &str = "invoice-scraper.json";
    const BROWSERS: &[(&str, &str)] = &[
        ("Google Chrome", "/etc/opt/chrome/policies/managed"),
        ("Chromium", "/etc/chromium/policies/managed"),
        ("Chromium (Debian/Ubuntu)", "/etc/chromium-browser/policies/managed"),
        ("Microsoft Edge", "/etc/opt/edge/policies/managed"),
        ("Brave", "/etc/brave/policies/managed"),
    ];

    pub fn apply(cfg: &Config) -> io::Result<Vec<&'static str>> {
        let body = serde_json::json!({
            "ExtensionInstallForcelist": [entry()],
            "3rdparty": { "extensions": { EXT_ID: {
                "domains": cfg.domains,
                "showToast": cfg.show_toast,
                "autoEnter": cfg.auto_enter,
                "showFloatingInput": cfg.show_floating_input,
            }}}
        });
        let mut done = Vec::new();
        for (name, dir) in BROWSERS {
            let file = Path::new(dir).join(FILE);
            if cfg.enabled {
                fs::create_dir_all(dir)?;
                fs::write(&file, serde_json::to_string_pretty(&body).unwrap())?;
            } else {
                let _ = fs::remove_file(&file);
            }
            done.push(*name);
        }
        Ok(done)
    }
}

/// Writes (or, when `cfg.enabled` is false, removes) the policy for every supported browser.
pub use os::apply;

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parses_urls_and_rejects_junk() {
        assert_eq!(parse_domains("# c\nhttps://App.DVLA.gov.gh/x\n*.genesys.com\ngenesys.com").unwrap(), ["app.dvla.gov.gh", "genesys.com"]);
        assert!(parse_domains("localhost").is_err());
        assert!(parse_domains("a b.com").is_err());
        assert!(parse_domains("# nothing").is_err());
    }
}
