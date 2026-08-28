# Multi-PC Remote Auto-Updating Extension (v2.2.0)

A modern, Manifest V3 browser extension configured for GitHub user **armnet122** that extracts **Invoice #** values, copies them to your clipboard, and automatically updates across all your PCs remotely.

---

## 🌐 How Remote Multi-PC Auto-Updating Works

### GitHub Repository & Releases (Configured for `armnet122`)
1. Create a repository on GitHub named `invoice-scraper-extension` under user `armnet122`.
2. Push this folder to GitHub:
   ```bash
   git init
   git remote add origin https://github.com/armnet122/invoice-scraper-extension.git
   git add .
   git commit -m "Initial commit v2.2.0"
   git push -u origin main
   ```
3. Whenever you update code on one PC:
   - Run `.\publish-update.ps1 -NewVersion "2.3.0" -ReleaseNotes "Added new feature"`
   - Run `git add . ; git commit -m "Release v2.3.0" ; git push origin main`
4. All installed instances across all your PCs will automatically detect the new version (`https://raw.githubusercontent.com/armnet122/invoice-scraper-extension/main/version.json`) and notify you with a 1-click update notice!

---

## 🚀 Quick Setup Instructions for Other PCs

1. Clone or download your repository on any PC:
   ```bash
   git clone https://github.com/armnet122/invoice-scraper-extension.git
   ```
2. Open `chrome://extensions` or `edge://extensions`.
3. Enable **Developer mode** -> click **Load unpacked** -> select `invoice-scraper-extension`.
4. The extension will automatically pull & alert you whenever you push updates on your primary PC!
