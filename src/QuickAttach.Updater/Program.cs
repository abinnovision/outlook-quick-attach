using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
            if (IsOutlookRunning())
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

            if (string.IsNullOrWhiteSpace(central.MsiSha256))
            {
                Log.Info(Source, "update.MsiSha256 missing, update skipped.");
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
            // The hash is checked before msi.dll opens the package.
            var sharePath = Environment.ExpandEnvironmentVariables(central.MsiPath);
            var msiPath = Path.Combine(Path.GetTempPath(), "QuickAttach-update-" + Guid.NewGuid().ToString("N") + ".msi");
            File.Copy(sharePath, msiPath);
            if (!MsiHash.IsAllowed(central.MsiSha256, MsiHash.Compute(msiPath)))
            {
                Log.Info(Source, "MSI hash does not match update.MsiSha256, update skipped: " + sharePath);
                return 0;
            }

            var msiVersion = MsiPackage.ReadProductVersion(msiPath);
            var installed = typeof(Program).Assembly.GetName().Version;
            if (!VersionPolicy.IsUpdateAvailable(installed, msiVersion))
            {
                return 0;
            }

            // Outlook may have started while the MSI was copied and checked.
            if (IsOutlookRunning())
            {
                Log.Info(Source, "Outlook is running, update skipped.");
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

    /// <summary>True when Outlook runs in the current session; other users on the same machine do not count.</summary>
    private static bool IsOutlookRunning()
    {
        using var current = Process.GetCurrentProcess();
        var session = current.SessionId;
        var processes = Process.GetProcessesByName("OUTLOOK");
        try
        {
            return processes.Any(p => p.SessionId == session);
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }
}
