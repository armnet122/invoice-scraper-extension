//! `InvoiceScraper` setup + tray controller.
//!   (no args)  install: copy self, write browser policy, autostart tray (Windows), launch it
//!   tray       Windows system-tray controller
//!   apply      re-apply /etc or ProgramData config to the browsers (Linux: edit config.json first)
//!   uninstall  remove policy, autostart task and config
mod policy;
#[cfg(windows)]
mod tray;

use std::io;

fn install() -> io::Result<()> {
    let cfg = policy::load();
    policy::save(&cfg)?;
    for b in policy::apply(&cfg)? {
        println!("[ok] {b}");
    }
    #[cfg(windows)]
    {
        let dir = std::path::PathBuf::from(std::env::var("ProgramFiles").unwrap_or_else(|_| r"C:\Program Files".into())).join("InvoiceScraper");
        std::fs::create_dir_all(&dir)?;
        let exe = dir.join("InvoiceScraper.exe");
        if std::env::current_exe()? != exe {
            std::fs::copy(std::env::current_exe()?, &exe)?;
        }
        // Run from the installed copy so the scheduled task path stays valid.
        let _ = std::process::Command::new("schtasks")
            .args(["/Create", "/TN", tray::TASK_NAME, "/SC", "ONLOGON", "/RL", "HIGHEST", "/F", "/TR"])
            .arg(format!("\"{}\" tray", exe.display()))
            .output();
        std::process::Command::new(&exe).arg("tray").spawn()?;
        println!("[ok] tray icon started (right-click it for settings)");
    }
    Ok(())
}

fn uninstall() -> io::Result<()> {
    let off = policy::Config { enabled: false, ..policy::load() };
    for b in policy::apply(&off)? {
        println!("[removed] {b}");
    }
    let _ = std::fs::remove_dir_all(policy::config_dir());
    #[cfg(windows)]
    tray::set_autostart(false);
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
        Ok(()) => println!("\nDone. Browsers pick the policy up within a minute (check chrome://policy)."),
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
