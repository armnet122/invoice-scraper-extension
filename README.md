# Invoice Scraper (v3.1.0)

Browser extension (`extension/`) plus Rust tooling:

- `installer/` - native setup binary (Windows `.exe`, static Linux ELF). Writes browser policy
  so Chrome, Edge, Brave, Chromium and Vivaldi force-install the extension. Run as Administrator / `sudo`.
  `setup uninstall` removes it.
- `crx-pack/` - dev tool: signs `extension/` into `releases/invoice-scraper-<ver>.crx` and regenerates `updates.xml`.

## Tray icon (Windows)
Setup copies itself to `Program Files\InvoiceScraper`, starts a tray icon and adds a logon task. Right-click menu:
status line, **Target domains…** (Notepad list, applied on close), extension on/off, toast / auto-Enter / floating-input
toggles, **Reload settings into browsers**, Start with Windows, open settings folder, Exit.
Settings reach the extension through browser policy (managed storage), so no browser restart is needed.
A newly added domain needs one click on **Allow <domain>** in the extension popup (browser permission rule).
Linux has no tray: edit `/etc/InvoiceScraper/config.json`, then `sudo ./invoice-scraper-setup-linux apply`.

## Install
- Windows: run `InvoiceScraper-Setup-windows.exe` (UAC prompt), restart browsers.
- Linux: `sudo ./invoice-scraper-setup-linux`, restart browsers.
- Check `chrome://policy` and `chrome://extensions`.

## Release an update (browsers pull it automatically within hours)
1. Bump `version` in `extension/manifest.json`.
2. `cargo run -p crx-pack` (needs `keys/extension.pem`, never commit it; back it up, losing it changes the extension ID).
3. Commit `releases/` + `updates.xml`, push to `main`.

## Build installers
```
cargo build --release -p invoice-scraper-setup
RUSTFLAGS="-Clinker=rust-lld -Clinker-flavor=ld.lld" cargo build --release -p invoice-scraper-setup --target x86_64-unknown-linux-musl
```
