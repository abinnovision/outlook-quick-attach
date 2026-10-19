using System;
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

    public static QuickAttachConfig Resolve(CentralConfig central) => ConfigResolver.Resolve(
        new[]
        {
            central.ForUser(Environment.UserName),
            central.Defaults,
            RegistrySource.Read(RegistrySource.UserKey),
            RegistrySource.Read(RegistrySource.InstallKey),
        });

    /// <summary>True when config.json sets the value, so the settings dialog cannot change it.</summary>
    public static bool IsLocked(CentralConfig central, string name) =>
        central.ForUser(Environment.UserName).ContainsKey(name) || central.Defaults.ContainsKey(name);
}
