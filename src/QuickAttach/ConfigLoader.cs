using System;
using System.Linq;
using QuickAttach.Core.Config;

namespace QuickAttach;

/// <summary>Builds the effective configuration from registry layers and the central config.json.</summary>
internal static class ConfigLoader
{
    private static readonly TimeSpan CentralTimeout = TimeSpan.FromSeconds(2);

    public static CentralConfig LoadCentral() =>
        CentralConfig.LoadWithCache(RegistrySource.ReadConfigPath(), CentralConfig.DefaultCachePath, CentralTimeout);

    /// <summary>Local cache only, without touching the share.</summary>
    public static CentralConfig LoadCachedCentral() =>
        string.IsNullOrWhiteSpace(RegistrySource.ReadConfigPath()) ? CentralConfig.Empty : CentralConfig.LoadCache(CentralConfig.DefaultCachePath);

    /// <summary>Merges all layers; the warnings of the central config come first.</summary>
    public static QuickAttachConfig Resolve(CentralConfig central)
    {
        var config = ConfigResolver.Resolve(
            new[]
            {
                ("config.json users." + Environment.UserName, central.ForUser(Environment.UserName)),
                ("config.json defaults", central.Defaults),
                (@"HKCU\" + RegistrySource.UserKey, RegistrySource.Read(RegistrySource.UserKey)),
                (@"HKCU\" + RegistrySource.InstallKey, RegistrySource.Read(RegistrySource.InstallKey)),
            });
        config.Warnings = central.Warnings.Concat(config.Warnings).ToArray();
        return config;
    }

    /// <summary>True when config.json sets the value, so the settings dialog cannot change it.</summary>
    public static bool IsLocked(CentralConfig central, string name) =>
        central.ForUser(Environment.UserName).ContainsKey(name) || central.Defaults.ContainsKey(name);
}
