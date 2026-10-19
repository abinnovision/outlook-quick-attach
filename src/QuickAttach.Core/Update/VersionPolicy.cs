using System;

namespace QuickAttach.Core.Update;

/// <summary>Version checks for the MinVersion kill switch and available MSI updates.</summary>
public static class VersionPolicy
{
    /// <summary>True when the installed version is below MinVersion. Missing or invalid MinVersion never blocks.</summary>
    public static bool IsBlocked(Version installed, string? minVersion) =>
        TryParse(minVersion) is { } min && Normalize(installed) < min;

    /// <summary>True when the MSI version is above the installed version.</summary>
    public static bool IsUpdateAvailable(Version installed, string? msiVersion) =>
        TryParse(msiVersion) is { } msi && msi > Normalize(installed);

    /// <summary>Parses "1.2", "1.2.3" or "v1.2.3"; missing parts count as 0.</summary>
    public static Version? TryParse(string? value)
    {
        var text = value?.Trim().TrimStart('v', 'V');
        return Version.TryParse(text, out var version) ? Normalize(version) : null;
    }

    private static Version Normalize(Version v) =>
        new(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision));
}
