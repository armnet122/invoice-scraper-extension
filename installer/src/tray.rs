//! Windows system-tray controller (`InvoiceScraper.exe tray`).
use crate::policy::{self, Config};
use std::{
    net::TcpListener,
    process::Command,
    sync::{Arc, Mutex},
    time::{Duration, Instant},
};
use tao::event_loop::{ControlFlow, EventLoopBuilder};
use tray_icon::{
    menu::{CheckMenuItem, Menu, MenuEvent, MenuItem, PredefinedMenuItem},
    Icon, TrayIconBuilder,
};

pub const TASK_NAME: &str = "InvoiceScraperTray";

fn icon() -> Icon {
    let (w, h) = (32u32, 32u32);
    let mut px = Vec::with_capacity((w * h * 4) as usize);
    for y in 0..h {
        for x in 0..w {
            let (dx, dy) = (x as i32 - 16, y as i32 - 16);
            let rgba = if dx * dx + dy * dy > 15 * 15 {
                [0, 0, 0, 0]
            } else if (14..18).contains(&x) && (9..23).contains(&y) {
                [255, 255, 255, 255]
            } else {
                [79, 70, 229, 255]
            };
            px.extend_from_slice(&rgba);
        }
    }
    Icon::from_rgba(px, w, h).expect("icon")
}

pub fn set_autostart(on: bool) {
    let mut cmd = Command::new("schtasks");
    if on {
        let exe = std::env::current_exe().unwrap();
        cmd.args(["/Create", "/TN", TASK_NAME, "/SC", "ONLOGON", "/RL", "HIGHEST", "/F", "/TR"])
            .arg(format!("\"{}\" tray", exe.display()));
    } else {
        cmd.args(["/Delete", "/TN", TASK_NAME, "/F"]);
    }
    let _ = cmd.output();
}

/// Opens the domain list in Notepad; applies it when Notepad closes.
fn edit_domains(cfg: Arc<Mutex<Config>>, refresh: impl Fn() + Send + 'static) {
    std::thread::spawn(move || {
        let file = policy::config_dir().join("domains.txt");
        let mut text = format!(
            "# One domain per line (subdomains are included). Save and close Notepad to apply.\n{}\n",
            cfg.lock().unwrap().domains.join("\n")
        );
        loop {
            if std::fs::write(&file, &text).is_err() {
                return;
            }
            let _ = Command::new("notepad").arg(&file).status();
            let edited = std::fs::read_to_string(&file).unwrap_or_default();
            match policy::parse_domains(&edited) {
                Ok(domains) => {
                    let mut c = cfg.lock().unwrap();
                    c.domains = domains;
                    let _ = policy::save(&c).and_then(|_| policy::apply(&c));
                    drop(c);
                    refresh();
                    return;
                }
                Err(e) if edited.trim() == text.trim() => {
                    let _ = e; // unchanged and still invalid: stop re-opening
                    return;
                }
                Err(e) => text = format!("# ERROR: {e}\n{}", edited.trim_start_matches("# ERROR").lines().skip(1).collect::<Vec<_>>().join("\n")),
            }
        }
    });
}

pub fn run() {
    // Single instance: the port doubles as a lock.
    let Ok(_lock) = TcpListener::bind("127.0.0.1:47613") else { return };
    unsafe { windows_sys::Win32::System::Console::FreeConsole() };

    crate::update::register_app_id();
    std::thread::spawn(|| loop {
        crate::update::check(false);
        std::thread::sleep(Duration::from_secs(4 * 3600));
    });

    let cfg = Arc::new(Mutex::new(policy::load()));
    let c = cfg.lock().unwrap().clone();

    let status = MenuItem::new("", false, None);
    let edit = MenuItem::new("Target domains…", true, None);
    let enabled = CheckMenuItem::new("Extension enabled", true, c.enabled, None);
    let toast = CheckMenuItem::new("Show toast messages", true, c.show_toast, None);
    let auto_enter = CheckMenuItem::new("Auto-press Enter after paste", true, c.auto_enter, None);
    let floating = CheckMenuItem::new("Show floating input", true, c.show_floating_input, None);
    let autostart = CheckMenuItem::new("Start with Windows", true, c.autostart, None);
    let reload = MenuItem::new("Reload settings into browsers", true, None);
    let folder = MenuItem::new("Open settings folder", true, None);
    let restart = MenuItem::new("Restart browsers", true, None);
    let check = MenuItem::new("Check for updates now", true, None);
    let uninstall = MenuItem::new("Uninstall…", true, None);
    let exit = MenuItem::new("Exit", true, None);

    let menu = Menu::new();
    menu.append_items(&[
        &status,
        &PredefinedMenuItem::separator(),
        &edit,
        &enabled,
        &toast,
        &auto_enter,
        &floating,
        &PredefinedMenuItem::separator(),
        &reload,
        &restart,
        &check,
        &autostart,
        &folder,
        &uninstall,
        &PredefinedMenuItem::separator(),
        &exit,
    ])
    .unwrap();

    let label = |c: &Config| format!("Invoice Scraper v{} - {}, {} domains", env!("CARGO_PKG_VERSION"), if c.enabled { "on" } else { "off" }, c.domains.len());
    status.set_text(label(&c));

    let _tray = TrayIconBuilder::new()
        .with_menu(Box::new(menu))
        .with_tooltip("Invoice Scraper")
        .with_icon(icon())
        .build()
        .expect("tray icon");

    let event_loop = EventLoopBuilder::<()>::with_user_event().build();
    let proxy = event_loop.create_proxy();
    let apply = |c: &Config| {
        let _ = policy::save(c).and_then(|_| policy::apply(c));
    };
    apply(&c); // re-assert policy at every login

    event_loop.run(move |event, _, flow| {
        *flow = ControlFlow::WaitUntil(Instant::now() + Duration::from_millis(200));
        if let tao::event::Event::UserEvent(()) = event {
            status.set_text(label(&cfg.lock().unwrap()));
        }
        while let Ok(ev) = MenuEvent::receiver().try_recv() {
            let mut c = cfg.lock().unwrap();
            if ev.id == *exit.id() {
                *flow = ControlFlow::Exit;
                return;
            } else if ev.id == *edit.id() {
                drop(c);
                let p = proxy.clone();
                edit_domains(cfg.clone(), move || {
                    let _ = p.send_event(());
                });
                continue;
            } else if ev.id == *enabled.id() {
                c.enabled = enabled.is_checked();
            } else if ev.id == *toast.id() {
                c.show_toast = toast.is_checked();
            } else if ev.id == *auto_enter.id() {
                c.auto_enter = auto_enter.is_checked();
            } else if ev.id == *floating.id() {
                c.show_floating_input = floating.is_checked();
            } else if ev.id == *autostart.id() {
                c.autostart = autostart.is_checked();
                set_autostart(c.autostart);
            } else if ev.id == *restart.id() {
                drop(c);
                std::thread::spawn(|| {
                    crate::browsers::restart();
                });
                continue;
            } else if ev.id == *check.id() {
                drop(c);
                std::thread::spawn(|| crate::update::check(true));
                continue;
            } else if ev.id == *uninstall.id() {
                // runs in its own console window and asks for confirmation
                let _ = Command::new(std::env::current_exe().unwrap()).arg("uninstall").spawn();
                continue;
            } else if ev.id == *folder.id() {
                let _ = Command::new("explorer").arg(policy::config_dir()).spawn();
                continue;
            } // `reload` falls through: just re-apply
            apply(&c);
            status.set_text(label(&c));
        }
    });
}
