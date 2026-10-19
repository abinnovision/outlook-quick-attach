using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using QuickAttach.Core;
using QuickAttach.Core.Config;
using QuickAttach.Core.Update;

namespace QuickAttach.Updater;

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

            using var key = Registry.CurrentUser.OpenSubKey(@"Software\abigroup\QuickAttach");
            var central = CentralConfig.LoadWithCache(key?.GetValue("ConfigPath") as string, CentralConfig.DefaultCachePath, TimeSpan.FromSeconds(10));
            if (string.IsNullOrWhiteSpace(central.MsiPath))
            {
                return 0;
            }

            // Remove copies left by earlier runs.
            foreach (var old in Directory.GetFiles(Path.GetTempPath(), "QuickAttach-update-*.msi"))
            {
                try
                {
                    File.Delete(old);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // Still in use by msiexec; a later run removes it.
                }
            }

            // Check and install a local copy, so the file on the share cannot change after verification.
            var sharePath = Environment.ExpandEnvironmentVariables(central.MsiPath);
            var msiPath = Path.Combine(Path.GetTempPath(), "QuickAttach-update-" + Guid.NewGuid().ToString("N") + ".msi");
            File.Copy(sharePath, msiPath);
            var msiVersion = MsiPackage.ReadProductVersion(msiPath);
            var installed = typeof(Program).Assembly.GetName().Version;
            if (!VersionPolicy.IsUpdateAvailable(installed, msiVersion))
            {
                return 0;
            }

            if (!MsiHash.IsAllowed(central.MsiSha256, MsiHash.Compute(msiPath)))
            {
                Log.Info(Source, "MSI hash does not match update.MsiSha256, update skipped: " + sharePath);
                return 0;
            }

            // The MSI replaces this executable, so start msiexec and exit without waiting.
            var logPath = Path.Combine(Path.GetTempPath(), "QuickAttach-update.log");
            Log.Info(Source, "Installing " + msiVersion + " over " + installed + " from " + sharePath + ", log: " + logPath);
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
