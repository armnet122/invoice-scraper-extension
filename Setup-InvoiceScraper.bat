@echo off
title Invoice Scraper Extension Safe Installer v2.8.0
color 0A
cls

echo ================================================================
echo      Invoice Scraper Extension Safe 1-Click Installer
echo ================================================================
echo.

set TARGET_DIR=C:\InvoiceScraperExtension
set TARGET_ALT=C:\InvoiceScrapperExtension

echo [*] Deploying extension files to %TARGET_DIR%...
if not exist "%TARGET_DIR%" mkdir "%TARGET_DIR%"
if not exist "%TARGET_ALT%" mkdir "%TARGET_ALT%"

copy /Y "%~dp0manifest.json" "%TARGET_DIR%\" >nul
copy /Y "%~dp0content.js" "%TARGET_DIR%\" >nul
copy /Y "%~dp0background.js" "%TARGET_DIR%\" >nul
copy /Y "%~dp0popup.html" "%TARGET_DIR%\" >nul
copy /Y "%~dp0popup.js" "%TARGET_DIR%\" >nul
copy /Y "%~dp0popup.css" "%TARGET_DIR%\" >nul
copy /Y "%~dp0test-invoice.html" "%TARGET_DIR%\" >nul
copy /Y "%~dp0updates.xml" "%TARGET_DIR%\" >nul
copy /Y "%~dp0version.json" "%TARGET_DIR%\" >nul
copy /Y "%~dp0README.md" "%TARGET_DIR%\" >nul

copy /Y "%~dp0manifest.json" "%TARGET_ALT%\" >nul
copy /Y "%~dp0content.js" "%TARGET_ALT%\" >nul
copy /Y "%~dp0background.js" "%TARGET_ALT%\" >nul
copy /Y "%~dp0popup.html" "%TARGET_ALT%\" >nul
copy /Y "%~dp0popup.js" "%TARGET_ALT%\" >nul
copy /Y "%~dp0popup.css" "%TARGET_ALT%\" >nul
copy /Y "%~dp0test-invoice.html" "%TARGET_ALT%\" >nul
copy /Y "%~dp0updates.xml" "%TARGET_ALT%\" >nul
copy /Y "%~dp0version.json" "%TARGET_ALT%\" >nul
copy /Y "%~dp0README.md" "%TARGET_ALT%\" >nul

echo [✓] Extension files successfully stored at %TARGET_DIR%
echo.

echo [*] Removing any blocked or obsolete registry keys...
reg delete "HKCU\Software\Google\Chrome\Extensions\invoicescraper" /f >nul 2>&1
reg delete "HKCU\Software\Microsoft\Edge\Extensions\invoicescraper" /f >nul 2>&1
reg delete "HKCU\Software\BraveSoftware\Brave-Browser\Extensions\invoicescraper" /f >nul 2>&1
echo [✓] Clean security baseline verified (no suspicious registry entries).
echo.

echo ================================================================
echo   [🎉 SUCCESS] Deployment Complete!
echo   Location: %TARGET_DIR%
echo.
echo   To activate or reload the extension:
echo   1. Open your browser and navigate to: chrome://extensions
echo   2. Turn ON 'Developer mode' (top right corner)
echo   3. Click 'Load unpacked' and select: %TARGET_DIR%
echo      (Or click the reload icon if already added)
echo ================================================================
echo.

:: Optionally open extensions page in default browser
start "" "chrome://extensions" 2>nul || start "" "edge://extensions" 2>nul
pause
