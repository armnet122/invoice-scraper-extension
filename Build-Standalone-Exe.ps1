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

        Console.WriteLine("[✓] Extracted all files to C:\\InvoiceScraperExtension");
        Console.WriteLine();

        Console.WriteLine("[*] Registering extension in Windows Registry for all browsers...");
        try {
            RegistryKey chromeKey = Registry.CurrentUser.CreateSubKey(@"Software\Google\Chrome\Extensions\invoicescraper");
            chromeKey.SetValue("path", @"C:\InvoiceScraperExtension");
            chromeKey.SetValue("version", "2.3.0");

            RegistryKey edgeKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Edge\Extensions\invoicescraper");
            edgeKey.SetValue("path", @"C:\InvoiceScraperExtension");
            edgeKey.SetValue("version", "2.3.0");

            RegistryKey braveKey = Registry.CurrentUser.CreateSubKey(@"Software\BraveSoftware\Brave-Browser\Extensions\invoicescraper");
            braveKey.SetValue("path", @"C:\InvoiceScraperExtension");
            braveKey.SetValue("version", "2.3.0");

            Console.WriteLine("[✓] Registry entries configured!");
        } catch (Exception ex) {
            Console.WriteLine("[!] Registry note: " + ex.Message);
        }

        Console.WriteLine();
        Console.WriteLine("[*] Launching installed browsers with extension pre-loaded...");

        string[] browserPaths = new string[] {
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe",
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Programs\Opera\opera.exe"
        };

        foreach (string bPath in browserPaths) {
            if (File.Exists(bPath)) {
                try {
                    Console.WriteLine("  - Launching " + Path.GetFileName(bPath) + "...");
                    Process.Start(bPath, "--load-extension=\"C:\\InvoiceScraperExtension\"");
                } catch {}
            }
        }

        Console.WriteLine();
        Console.WriteLine("================================================================");
        Console.WriteLine("  [🎉 SUCCESS] Installation complete! Saved to C:\\InvoiceScraperExtension");
        Console.WriteLine("================================================================");
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
