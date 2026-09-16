# Multi-PC & Multi-Platform Remote Auto-Updating Extension (v2.7.0)

A modern, Manifest V3 browser extension configured for GitHub user **armnet122** that extracts **Invoice #** values, copies them to your clipboard, and automatically updates across all your PCs remotely (Windows & Linux Mint/Ubuntu).

---

## 🐧 Linux Mint Setup Instructions

### Quick Installation on Linux Mint / Ubuntu:
1. Open Terminal (`Ctrl+Alt+T`) and clone the repository:
   ```bash
   git clone https://github.com/armnet122/invoice-scraper-extension.git ~/InvoiceScraperExtension
   ```
2. Open Google Chrome, Brave, Microsoft Edge, or Chromium on Linux Mint.
3. Open `chrome://extensions` (or `brave://extensions` / `edge://extensions`).
4. Enable **Developer mode** (toggle switch in the top-right corner).
5. Click **Load unpacked** (top-left) and select the `~/InvoiceScraperExtension` folder.
6. Done! The extension is ready to use on Linux Mint!

---

## 💻 Windows Setup Instructions

### 1-Click Standalone Installer:
- Double-click [`Setup-InvoiceScraper.exe`](file:///C:/InvoiceScraperExtension/Setup-InvoiceScraper.exe) to extract to `C:\InvoiceScraperExtension` and automatically configure all installed browsers!

---

## 🌐 Remote Multi-PC Auto-Updating

Whenever you publish updates on your primary PC:
1. Run `.\Build-Standalone-Exe.ps1`
2. Push to GitHub:
   ```bash
   git add . ; git commit -m "Release vX.Y.Z" ; git push origin main
   ```
3. All your Windows & Linux Mint PCs will automatically receive a desktop notification with a 1-click update button!
