using System;
using ScanAttach.Core.Config;

namespace ScanAttach;

/// <summary>Builds the effective configuration from registry layers and the central config.json.</summary>
internal static class ConfigLoader
{
    private static readonly TimeSpan CentralTimeout = TimeSpan.FromSeconds(2);

    public static CentralConfig LoadCentral() =>
        CentralConfig.LoadWithCache(RegistrySource.ReadConfigPath(), CentralConfig.DefaultCachePath, CentralTimeout);

    public static ScanAttachConfig Resolve(CentralConfig central) => ConfigResolver.Resolve(
        new[]
        {
            RegistrySource.Read(RegistrySource.PolicyKey),
            central.ForUser(Environment.UserName),
            central.Defaults,
            RegistrySource.Read(RegistrySource.InstallKey),
        },
        RegistrySource.Read(RegistrySource.UserKey));

    /// <summary>True when policy or central config sets the value, so the user cannot change it.</summary>
    public static bool IsLocked(CentralConfig central, string name) =>
        RegistrySource.Read(RegistrySource.PolicyKey).ContainsKey(name)
        || central.ForUser(Environment.UserName).ContainsKey(name)
        || central.Defaults.ContainsKey(name);
}
