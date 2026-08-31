#!/bin/bash
# 1-Click Linux Installer Script for Invoice Scraper Extension (Linux Mint / Ubuntu / Debian / Fedora)

set -e

echo "================================================================"
echo "    Invoice Scraper Extension - Linux Auto-Setup"
echo "================================================================"
echo ""

TARGET_DIR="$HOME/InvoiceScraperExtension"

echo "[*] Creating target installation directory at $TARGET_DIR..."
mkdir -p "$TARGET_DIR"

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

echo "[*] Copying extension files..."
cp -f "$SCRIPT_DIR/manifest.json" "$TARGET_DIR/" 2>/dev/null || true
cp -f "$SCRIPT_DIR/content.js" "$TARGET_DIR/" 2>/dev/null || true
cp -f "$SCRIPT_DIR/background.js" "$TARGET_DIR/" 2>/dev/null || true
cp -f "$SCRIPT_DIR/popup.html" "$TARGET_DIR/" 2>/dev/null || true
cp -f "$SCRIPT_DIR/popup.js" "$TARGET_DIR/" 2>/dev/null || true
cp -f "$SCRIPT_DIR/popup.css" "$TARGET_DIR/" 2>/dev/null || true
cp -f "$SCRIPT_DIR/test-invoice.html" "$TARGET_DIR/" 2>/dev/null || true
cp -f "$SCRIPT_DIR/updates.xml" "$TARGET_DIR/" 2>/dev/null || true
cp -f "$SCRIPT_DIR/version.json" "$TARGET_DIR/" 2>/dev/null || true
cp -f "$SCRIPT_DIR/README.md" "$TARGET_DIR/" 2>/dev/null || true

echo "[✓] Files stored at: $TARGET_DIR"
echo ""
echo "================================================================"
echo "  [🎉 SUCCESS] Setup complete!"
echo ""
echo "  To complete setup in Chrome, Brave, Edge, or Chromium:"
echo "  1. Open browser -> Go to chrome://extensions"
echo "  2. Toggle 'Developer mode' ON (top-right)"
echo "  3. Click 'Load unpacked' and select folder: $TARGET_DIR"
echo "================================================================"
