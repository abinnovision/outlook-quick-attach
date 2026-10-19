using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using ScanAttach.Core;
using ScanAttach.Core.Config;
using ScanAttach.Core.Update;

namespace ScanAttach.Updater;

/// <summary>Runs at logon, before Outlook locks the add-in DLL, and installs a newer MSI from the share.</summary>
internal static class Program
{
    private const string Source = "Updater";

    private static int Main()
    {
        try
        {
            if (Process.GetProcessesByName("OUTLOOK").Length > 0)
            {
                Log.Info(Source, "Outlook is running, update skipped.");
                return 0;
            }

            using var key = Registry.CurrentUser.OpenSubKey(@"Software\abi\ScanAttach");
            var central = CentralConfig.LoadWithCache(key?.GetValue("ConfigPath") as string, CentralConfig.DefaultCachePath, TimeSpan.FromSeconds(10));
            if (string.IsNullOrWhiteSpace(central.MsiPath))
            {
                return 0;
            }

            var msiPath = Environment.ExpandEnvironmentVariables(central.MsiPath);
            var msiVersion = MsiPackage.ReadProductVersion(msiPath);
            var installed = typeof(Program).Assembly.GetName().Version;
            if (!VersionPolicy.IsUpdateAvailable(installed, msiVersion))
            {
                return 0;
            }

            // The MSI replaces this executable, so start msiexec and exit without waiting.
            var logPath = Path.Combine(Path.GetTempPath(), "ScanAttach-update.log");
            Log.Info(Source, "Installing " + msiVersion + " over " + installed + " from " + msiPath + ", log: " + logPath);
            Process.Start(new ProcessStartInfo("msiexec.exe", "/i \"" + msiPath + "\" /qn /norestart REBOOT=ReallySuppress /l*v \"" + logPath + "\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            })?.Dispose();
        }
        catch (Exception e)
        {
            Log.Error(Source, e);
        }

        return 0;
    }
}
