# 1-Click Remote Update Publisher Script for Invoice Scraper Extension
# Run this script whenever you update code on one PC to publish updates remotely to all your PCs!

param(
    [string]$NewVersion = "",
    [string]$ReleaseNotes = "Remote auto-update release."
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $scriptDir

$manifestPath = Join-Path $scriptDir "manifest.json"
$manifestJson = Get-Content $manifestPath -Raw | ConvertFrom-Json

if ([string]::IsNullOrWhiteSpace($NewVersion)) {
    $currentVersion = [version]$manifestJson.version
    $NewVersion = "$($currentVersion.Major).$($currentVersion.Minor).$($currentVersion.Build + 1)"
}

Write-Host "🚀 Publishing Remote Extension Update v$NewVersion for armnet122..." -ForegroundColor Green

# 1. Update manifest.json version
$manifestJson.version = $NewVersion
$manifestJson | ConvertTo-Json -Depth 10 | Set-Content $manifestPath -Encoding UTF8
Write-Host "  [✓] Updated manifest.json to v$NewVersion" -ForegroundColor Cyan

# 2. Update version.json
$versionJsonPath = Join-Path $scriptDir "version.json"
$versionObj = @{
    version = $NewVersion
    downloadUrl = "https://raw.githubusercontent.com/armnet122/invoice-scraper-extension/main/invoice-scraper-extension-v$NewVersion.zip"
    notes = $ReleaseNotes
    releaseDate = (Get-Date -Format "yyyy-MM-dd")
}
$versionObj | ConvertTo-Json | Set-Content $versionJsonPath -Encoding UTF8
Write-Host "  [✓] Updated version.json" -ForegroundColor Cyan

# 3. Update updates.xml
$updatesXmlPath = Join-Path $scriptDir "updates.xml"
$updatesXmlContent = @"
<?xml version="1.0" encoding="UTF-8"?>
<gupdate xmlns="http://www.google.com/update2/response" protocol="2.0">
  <app appid="invoice-scraper-extension">
    <updatecheck codebase="https://raw.githubusercontent.com/armnet122/invoice-scraper-extension/main/invoice-scraper-extension-v$NewVersion.zip" version="$NewVersion" />
  </app>
</gupdate>
"@
Set-Content -Path $updatesXmlPath -Value $updatesXmlContent -Encoding UTF8
Write-Host "  [✓] Updated updates.xml" -ForegroundColor Cyan

# 4. Generate production release Zip
$zipPath = Join-Path (Split-Path $scriptDir -Parent) "invoice-scraper-extension-v$NewVersion.zip"
Compress-Archive -Path "$scriptDir\manifest.json", "$scriptDir\content.js", "$scriptDir\background.js", "$scriptDir\popup.html", "$scriptDir\popup.js", "$scriptDir\popup.css", "$scriptDir\test-invoice.html", "$scriptDir\updates.xml", "$scriptDir\version.json", "$scriptDir\README.md" -DestinationPath $zipPath -Force
Write-Host "  [✓] Created release zip: $zipPath" -ForegroundColor Cyan

Write-Host "`n🎉 Extension v$NewVersion packaged successfully!" -ForegroundColor Green
Write-Host "To publish remotely to all your PCs, run:" -ForegroundColor Yellow
Write-Host "  git add . ; git commit -m 'Release v$NewVersion' ; git push origin main" -ForegroundColor Gray
