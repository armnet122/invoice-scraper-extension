# PowerShell script to compile Setup-InvoiceScraper.exe using Windows IExpress compiler
param(
    [string]$OutputDir = "C:\Users\PC\.gemini\antigravity\scratch"
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $scriptDir

$exePath = Join-Path $OutputDir "Setup-InvoiceScraper.exe"
$sedPath = Join-Path $scriptDir "installer.sed"

Write-Host "🚀 Compiling Setup-InvoiceScraper.exe executable installer..." -ForegroundColor Green

# Prepare IExpress SED file
$sedContent = @"
[Version]
Class=IExpress
SEDVersion=3.0
[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=0
HideExtractAnimation=1
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=%InstallPrompt%
DisplayLicense=%DisplayLicense%
FinishMessage=%FinishMessage%
TargetName=%TargetName%
FriendlyName=%FriendlyName%
AppLaunched=%AppLaunched%
PostInstallCmd=%PostInstallCmd%
AdminQuietInstCmd=%AdminQuietInstCmd%
UserQuietInstCmd=%UserQuietInstCmd%
SourceFiles=SourceFiles
[Strings]
InstallPrompt=
DisplayLicense=
FinishMessage=
TargetName=$exePath
FriendlyName=Invoice Scraper Extension Auto-Installer
AppLaunched=cmd.exe /c Setup-InvoiceScraper.bat
PostInstallCmd=<None>
AdminQuietInstCmd=
UserQuietInstCmd=
[SourceFiles]
SourceFiles0=$scriptDir\
[SourceFiles0]
%FILE0%=
%FILE1%=
%FILE2%=
%FILE3%=
%FILE4%=
%FILE5%=
%FILE6%=
%FILE7%=
%FILE8%=
%FILE9%=
%FILE10%=
[Strings]
FILE0="manifest.json"
FILE1="content.js"
FILE2="background.js"
FILE3="popup.html"
FILE4="popup.js"
FILE5="popup.css"
FILE6="test-invoice.html"
FILE7="updates.xml"
FILE8="version.json"
FILE9="README.md"
FILE10="Setup-InvoiceScraper.bat"
"@

Set-Content -Path $sedPath -Value $sedContent -Encoding UTF8

# Invoke Windows built-in iexpress.exe
$iexpress = "C:\Windows\System32\iexpress.exe"
if (Test-Path $iexpress) {
    Start-Process -FilePath $iexpress -ArgumentList "/N /Q $sedPath" -Wait
    Write-Host "🎉 Executable Installer created successfully at: $exePath" -ForegroundColor Green
} else {
    Write-Error "IExpress compiler not found at $iexpress"
}
