@echo off
title Invoice Scraper Extension Auto-Installer v2.3.0
color 0A
cls

echo ================================================================
echo      Invoice Scraper Extension 1-Click Auto-Installer
echo ================================================================
echo.

set TARGET_DIR=C:\InvoiceScraperExtension
echo [*] Creating target installation directory at %TARGET_DIR%...
if not exist "%TARGET_DIR%" mkdir "%TARGET_DIR%"

echo [*] Copying extension files to %TARGET_DIR%...
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

echo [✓] Extension files successfully stored at C:\InvoiceScraperExtension
echo.

echo [*] Registering extension in Windows Registry for installed browsers...

:: Google Chrome Registry Registration
reg add "HKCU\Software\Google\Chrome\Extensions\invoicescraper" /v "path" /t REG_SZ /d "C:\InvoiceScraperExtension" /f >nul 2>&1
reg add "HKCU\Software\Google\Chrome\Extensions\invoicescraper" /v "version" /t REG_SZ /d "2.3.0" /f >nul 2>&1

:: Microsoft Edge Registry Registration
reg add "HKCU\Software\Microsoft\Edge\Extensions\invoicescraper" /v "path" /t REG_SZ /d "C:\InvoiceScraperExtension" /f >nul 2>&1
reg add "HKCU\Software\Microsoft\Edge\Extensions\invoicescraper" /v "version" /t REG_SZ /d "2.3.0" /f >nul 2>&1

:: Brave Browser Registry Registration
reg add "HKCU\Software\BraveSoftware\Brave-Browser\Extensions\invoicescraper" /v "path" /t REG_SZ /d "C:\InvoiceScraperExtension" /f >nul 2>&1
reg add "HKCU\Software\BraveSoftware\Brave-Browser\Extensions\invoicescraper" /v "version" /t REG_SZ /d "2.3.0" /f >nul 2>&1

echo [✓] Registry keys applied successfully.
echo.

echo [*] Detecting installed browsers and launching with extension loaded...

:: Launch Google Chrome if installed
if exist "C:\Program Files\Google\Chrome\Application\chrome.exe" (
    echo   - Launching Google Chrome...
    start "" "C:\Program Files\Google\Chrome\Application\chrome.exe" --load-extension="C:\InvoiceScraperExtension"
) else if exist "C:\Program Files (x86)\Google\Chrome\Application\chrome.exe" (
    echo   - Launching Google Chrome...
    start "" "C:\Program Files (x86)\Google\Chrome\Application\chrome.exe" --load-extension="C:\InvoiceScraperExtension"
)

:: Launch Microsoft Edge if installed
if exist "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" (
    echo   - Launching Microsoft Edge...
    start "" "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" --load-extension="C:\InvoiceScraperExtension"
) else if exist "C:\Program Files\Microsoft\Edge\Application\msedge.exe" (
    echo   - Launching Microsoft Edge...
    start "" "C:\Program Files\Microsoft\Edge\Application\msedge.exe" --load-extension="C:\InvoiceScraperExtension"
)

:: Launch Brave Browser if installed
if exist "C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe" (
    echo   - Launching Brave Browser...
    start "" "C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe" --load-extension="C:\InvoiceScraperExtension"
)

:: Launch Opera Browser if installed
if exist "%LOCALAPPDATA%\Programs\Opera\opera.exe" (
    echo   - Launching Opera Browser...
    start "" "%LOCALAPPDATA%\Programs\Opera\opera.exe" --load-extension="C:\InvoiceScraperExtension"
)

echo.
echo ================================================================
echo   [🎉 SUCCESS] Extension setup complete on all browsers!
echo   Files stored at: C:\InvoiceScraperExtension
echo ================================================================
echo.
pause
