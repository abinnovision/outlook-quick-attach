using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace ScanAttach;

/// <summary>Reads one HKCU key into a configuration layer.</summary>
internal static class RegistrySource
{
    public const string RootKey = @"Software\abi\ScanAttach";
    public const string PolicyKey = @"Software\Policies\abi\ScanAttach";
    public const string UserKey = RootKey + @"\User";
    public const string InstallKey = RootKey + @"\Install";

    public static IReadOnlyDictionary<string, object?> Read(string subKey)
    {
        var layer = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        using var key = Registry.CurrentUser.OpenSubKey(subKey);
        if (key != null)
        {
            foreach (var name in key.GetValueNames())
            {
                layer[name] = key.GetValue(name);
            }
        }

        return layer;
    }

    public static string? ReadConfigPath()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RootKey);
        return key?.GetValue("ConfigPath") as string;
    }
}
