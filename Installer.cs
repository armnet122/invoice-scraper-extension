using System;
using System.IO;
using System.Diagnostics;
using Microsoft.Win32;

class Program
{
    static void Main()
    {
        Console.Title = "Invoice Scraper Extension 1-Click Auto-Installer";
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("================================================================");
        Console.WriteLine("     Invoice Scraper Extension 1-Click Auto-Installer");
        Console.WriteLine("================================================================");
        Console.WriteLine();

        string targetDir = @"C:\InvoiceScraperExtension";
        Console.WriteLine("[*] Creating target directory at " + targetDir + "...");
        Directory.CreateDirectory(targetDir);

        string currentDir = AppDomain.CurrentDomain.BaseDirectory;
        string[] files = new string[] {
            "manifest.json", "content.js", "background.js", "popup.html", 
            "popup.js", "popup.css", "test-invoice.html", "updates.xml", 
            "version.json", "README.md", "Setup-InvoiceScraper.bat"
        };

        foreach (string file in files)
        {
            string src = Path.Combine(currentDir, file);
            if (File.Exists(src))
            {
                File.Copy(src, Path.Combine(targetDir, file), true);
            }
        }

        Console.WriteLine("[✓] Extension files stored successfully at C:\\InvoiceScraperExtension");
        Console.WriteLine();

        Console.WriteLine("[*] Applying Windows Registry keys for Chrome, Edge, Brave...");
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

            Console.WriteLine("[✓] Registry keys registered successfully!");
        } catch (Exception ex) {
            Console.WriteLine("[!] Registry note: " + ex.Message);
        }

        Console.WriteLine();
        Console.WriteLine("[*] Launching installed browsers with extension pre-loaded...");

        string chromePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
        if (File.Exists(chromePath)) Process.Start(chromePath, "--load-extension=\"C:\\InvoiceScraperExtension\"");

        string edgePath = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";
        if (!File.Exists(edgePath)) edgePath = @"C:\Program Files\Microsoft\Edge\Application\msedge.exe";
        if (File.Exists(edgePath)) Process.Start(edgePath, "--load-extension=\"C:\\InvoiceScraperExtension\"");

        string bravePath = @"C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe";
        if (File.Exists(bravePath)) Process.Start(bravePath, "--load-extension=\"C:\\InvoiceScraperExtension\"");

        Console.WriteLine();
        Console.WriteLine("================================================================");
        Console.WriteLine("  [🎉 SUCCESS] Setup complete! Saved to C:\\InvoiceScraperExtension");
        Console.WriteLine("================================================================");
    }
}
