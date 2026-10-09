//! Windows: close running Chromium-family browsers and reopen them with their tabs restored.
use std::{os::windows::process::CommandExt, process::Command, thread, time::Duration};

const NAMES: &[&str] = &["chrome", "msedge", "brave", "vivaldi", "chromium"];
const NO_WINDOW: u32 = 0x0800_0000;

fn quiet(program: &str) -> Command {
    let mut c = Command::new(program);
    c.creation_flags(NO_WINDOW);
    c
}

fn running_paths() -> Vec<String> {
    let script = format!("Get-Process {} -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Path -Unique", NAMES.join(","));
    let out = quiet("powershell").args(["-NoProfile", "-Command", &script]).output().map(|o| o.stdout).unwrap_or_default();
    String::from_utf8_lossy(&out).lines().map(|l| l.trim().to_string()).filter(|l| !l.is_empty()).collect()
}

fn kill(force: bool) {
    for n in NAMES {
        let mut c = quiet("taskkill");
        c.arg("/IM").arg(format!("{n}.exe"));
        if force {
            c.arg("/F");
        }
        let _ = c.output();
    }
}

/// Returns how many browsers were restarted.
pub fn restart() -> usize {
    let paths = running_paths();
    if paths.is_empty() {
        return 0;
    }
    kill(false); // polite close first so each browser saves its session
    for _ in 0..30 {
        if running_paths().is_empty() {
            break;
        }
        thread::sleep(Duration::from_millis(500));
    }
    if !running_paths().is_empty() {
        kill(true); // background-mode leftovers
        thread::sleep(Duration::from_secs(2));
    }
    for p in &paths {
        // trustlevel 0x20000 = relaunch as the normal user, not elevated like this process
        let _ = quiet("runas").args(["/trustlevel:0x20000", &format!("\"{p}\" --restore-last-session")]).spawn();
    }
    paths.len()
}
