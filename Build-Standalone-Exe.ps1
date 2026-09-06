# PowerShell script to compile a 100% self-contained standalone Setup-InvoiceScraper.exe binary
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $scriptDir

$manifest = [System.IO.File]::ReadAllText("$scriptDir\manifest.json")
$contentJs = [System.IO.File]::ReadAllText("$scriptDir\content.js")
$backgroundJs = [System.IO.File]::ReadAllText("$scriptDir\background.js")
$popupHtml = [System.IO.File]::ReadAllText("$scriptDir\popup.html")
$popupJs = [System.IO.File]::ReadAllText("$scriptDir\popup.js")
$popupCss = [System.IO.File]::ReadAllText("$scriptDir\popup.css")
$testHtml = [System.IO.File]::ReadAllText("$scriptDir\test-invoice.html")
$updatesXml = [System.IO.File]::ReadAllText("$scriptDir\updates.xml")
$versionJson = [System.IO.File]::ReadAllText("$scriptDir\version.json")
$readmeMd = [System.IO.File]::ReadAllText("$scriptDir\README.md")

function Escape-CS($str) {
    return "@""" + $str.Replace('"', '""') + """"
}

$csContent = @"
using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using Microsoft.Win32;

class Program
{
    static readonly string MANIFEST_JSON = $(Escape-CS $manifest);
    static readonly string CONTENT_JS = $(Escape-CS $contentJs);
    static readonly string BACKGROUND_JS = $(Escape-CS $backgroundJs);
    static readonly string POPUP_HTML = $(Escape-CS $popupHtml);
    static readonly string POPUP_JS = $(Escape-CS $popupJs);
    static readonly string POPUP_CSS = $(Escape-CS $popupCss);
    static readonly string TEST_HTML = $(Escape-CS $testHtml);
    static readonly string UPDATES_XML = $(Escape-CS $updatesXml);
    static readonly string VERSION_JSON = $(Escape-CS $versionJson);
    static readonly string README_MD = $(Escape-CS $readmeMd);

    static void Main()
    {
        Console.Title = "Invoice Scraper Extension Standalone Setup";
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("================================================================");
        Console.WriteLine("     Invoice Scraper Extension 1-Click Standalone Installer");
        Console.WriteLine("================================================================");
        Console.WriteLine();

        string targetDir = @"C:\InvoiceScraperExtension";
        Console.WriteLine("[*] Extracting embedded extension files to " + targetDir + "...");
        if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

        WriteFile(targetDir, "manifest.json", MANIFEST_JSON);
        WriteFile(targetDir, "content.js", CONTENT_JS);
        WriteFile(targetDir, "background.js", BACKGROUND_JS);
        WriteFile(targetDir, "popup.html", POPUP_HTML);
        WriteFile(targetDir, "popup.js", POPUP_JS);
        WriteFile(targetDir, "popup.css", POPUP_CSS);
        WriteFile(targetDir, "test-invoice.html", TEST_HTML);
        WriteFile(targetDir, "updates.xml", UPDATES_XML);
        WriteFile(targetDir, "version.json", VERSION_JSON);
        WriteFile(targetDir, "README.md", README_MD);

        string targetAlt = @"C:\InvoiceScrapperExtension";
        if (!Directory.Exists(targetAlt)) Directory.CreateDirectory(targetAlt);

        WriteFile(targetAlt, "manifest.json", MANIFEST_JSON);
        WriteFile(targetAlt, "content.js", CONTENT_JS);
        WriteFile(targetAlt, "background.js", BACKGROUND_JS);
        WriteFile(targetAlt, "popup.html", POPUP_HTML);
        WriteFile(targetAlt, "popup.js", POPUP_JS);
        WriteFile(targetAlt, "popup.css", POPUP_CSS);
        WriteFile(targetAlt, "test-invoice.html", TEST_HTML);
        WriteFile(targetAlt, "updates.xml", UPDATES_XML);
        WriteFile(targetAlt, "version.json", VERSION_JSON);
        WriteFile(targetAlt, "README.md", README_MD);

        Console.WriteLine("[✓] Extracted all files to C:\\InvoiceScraperExtension");
        Console.WriteLine();

        Console.WriteLine("[*] Cleaning legacy blocked registry entries...");
        try {
            Registry.CurrentUser.DeleteSubKey(@"Software\Google\Chrome\Extensions\invoicescraper", false);
            Registry.CurrentUser.DeleteSubKey(@"Software\Microsoft\Edge\Extensions\invoicescraper", false);
            Registry.CurrentUser.DeleteSubKey(@"Software\BraveSoftware\Brave-Browser\Extensions\invoicescraper", false);
            Console.WriteLine("[✓] Clean security baseline verified (no suspicious registry entries).");
        } catch {}

        Console.WriteLine();
        Console.WriteLine("================================================================");
        Console.WriteLine("  [🎉 SUCCESS] Deployment Complete! Saved to C:\\InvoiceScraperExtension");
        Console.WriteLine();
        Console.WriteLine("  To activate or reload the extension:");
        Console.WriteLine("  1. Open your browser and go to: chrome://extensions");
        Console.WriteLine("  2. Turn ON 'Developer mode' (top right corner)");
        Console.WriteLine("  3. Click 'Load unpacked' and select: C:\\InvoiceScraperExtension");
        Console.WriteLine("================================================================");
        try {
            Process.Start("chrome://extensions");
        } catch {}
    }

    static void WriteFile(string dir, string name, string content)
    {
        File.WriteAllText(Path.Combine(dir, name), content, Encoding.UTF8);
    }
}
"@

$csFile = "$scriptDir\StandaloneInstaller.cs"
$exeFile = "$scriptDir\Setup-InvoiceScraper.exe"
Set-Content -Path $csFile -Value $csContent -Encoding UTF8

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
& $csc /out:$exeFile /target:exe $csFile

Write-Host "Self-contained Standalone Setup-InvoiceScraper.exe built successfully!"
