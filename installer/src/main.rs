//! `InvoiceScraper` setup + tray controller.
//!   (no args)  install: copy self, write browser policy, autostart tray (Windows), restart browsers
//!   tray       Windows system-tray controller
//!   apply      re-apply /etc or ProgramData config to the browsers (Linux: edit config.json first)
//!   uninstall  remove policy, autostart task, Apps-list entry and config
#[cfg(windows)]
mod browsers;
mod policy;
#[cfg(windows)]
mod tray;

use std::io::{self, Write};

#[cfg(windows)]
const UNINSTALL_KEY: &str = r"Software\Microsoft\Windows\CurrentVersion\Uninstall\InvoiceScraper";
#[cfg(windows)]
const NO_WINDOW: u32 = 0x0800_0000;

fn confirm(question: &str, default_yes: bool) -> bool {
    print!("\n{question} {} ", if default_yes { "[Y/n]" } else { "[y/N]" });
    let _ = io::stdout().flush();
    let mut s = String::new();
    let _ = io::stdin().read_line(&mut s);
    match s.trim().to_lowercase().as_str() {
        "" => default_yes,
        a => a.starts_with('y'),
    }
}

/// Closes and reopens running browsers so the new policy and extension load now.
fn restart_browsers() {
    #[cfg(windows)]
    {
        println!("Restarting browsers...");
        println!("[ok] {} browser(s) restarted with tabs restored", browsers::restart());
    }
    #[cfg(not(windows))]
    println!("Restart your browsers to load the change.");
}

fn install() -> io::Result<()> {
    let cfg = policy::load();
    policy::save(&cfg)?;
    for b in policy::apply(&cfg)? {
        println!("[ok] {b}");
    }
    #[cfg(windows)]
    {
        use winreg::{enums::*, RegKey};
        let dir = std::path::PathBuf::from(std::env::var("ProgramFiles").unwrap_or_else(|_| r"C:\Program Files".into())).join("InvoiceScraper");
        std::fs::create_dir_all(&dir)?;
        let exe = dir.join("InvoiceScraper.exe");
        if std::env::current_exe()? != exe {
            std::fs::copy(std::env::current_exe()?, &exe)?;
        }
        // Run from the installed copy so the scheduled task path stays valid.
        tray::set_autostart(true);
        std::process::Command::new(&exe).arg("tray").spawn()?;
        println!("[ok] tray icon started (right-click it for settings)");

        let (k, _) = RegKey::predef(HKEY_LOCAL_MACHINE).create_subkey(UNINSTALL_KEY)?;
        k.set_value("DisplayName", &"Invoice Scraper")?;
        k.set_value("DisplayVersion", &env!("CARGO_PKG_VERSION"))?;
        k.set_value("Publisher", &"armnet122")?;
        k.set_value("UninstallString", &format!("\"{}\" uninstall", exe.display()))?;
        k.set_value("NoModify", &1u32)?;
    }
    if confirm("Restart your browsers now so the extension loads? Open tabs are restored.", true) {
        restart_browsers();
    }
    Ok(())
}

fn uninstall() -> io::Result<()> {
    if !confirm("Remove Invoice Scraper from all browsers and this PC?", false) {
        println!("Cancelled.");
        return Ok(());
    }
    let off = policy::Config { enabled: false, ..policy::load() };
    for b in policy::apply(&off)? {
        println!("[removed] {b}");
    }
    let _ = std::fs::remove_dir_all(policy::config_dir());
    #[cfg(windows)]
    {
        use std::{os::windows::process::CommandExt, process::Command};
        tray::set_autostart(false);
        let _ = winreg::RegKey::predef(winreg::enums::HKEY_LOCAL_MACHINE).delete_subkey_all(UNINSTALL_KEY);
        // stop the tray (not this process), then delete the install folder once we have exited
        let _ = Command::new("taskkill")
            .args(["/F", "/IM", "InvoiceScraper.exe", "/FI"])
            .arg(format!("PID ne {}", std::process::id()))
            .creation_flags(NO_WINDOW)
            .output();
        if let Some(dir) = std::env::current_exe()?.parent().filter(|d| d.ends_with("InvoiceScraper")) {
            let _ = Command::new("cmd")
                .args(["/C", "ping -n 4 127.0.0.1 >nul & rmdir /s /q"])
                .arg(dir)
                .creation_flags(NO_WINDOW)
                .spawn();
        }
    }
    if confirm("Restart your browsers now to finish removing the extension?", true) {
        restart_browsers();
    }
    Ok(())
}

fn main() {
    let cmd = std::env::args().nth(1).unwrap_or_default();
    #[cfg(windows)]
    if cmd == "tray" {
        return tray::run();
    }
    let res = match cmd.trim_start_matches('-') {
        "uninstall" => uninstall(),
        "apply" => policy::apply(&policy::load()).map(|b| b.iter().for_each(|n| println!("[ok] {n}"))),
        _ => install(),
    };
    match res {
        Ok(()) => println!("\nDone. Check chrome://policy and chrome://extensions."),
        Err(e) if e.kind() == io::ErrorKind::PermissionDenied => {
            eprintln!("\nPermission denied: run as Administrator (Windows) or with sudo (Linux).")
        }
        Err(e) => eprintln!("\nError: {e}"),
    }
    #[cfg(windows)]
    {
        println!("\nPress Enter to close.");
        let _ = io::stdin().read_line(&mut String::new());
    }
}
