using ScanAttach.Core.Config;

namespace ScanAttach.Core.Tests;

public class CentralConfigTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    private const string Json = """
        {
          "defaults": { "ScanPath": "/scans", "MaxItems": 12, "AllowUserOverride": false },
          "users": { "mmueller": { "ScanPath": "/scans/buchhaltung" } },
          "update": { "MsiPath": "/it/ScanAttach.msi", "MinVersion": "1.2.0" }
        }
        """;

    [Fact]
    public void LoadWithCache_ReadsCentralFileAndWritesCache()
    {
        using var dir = new TempDir();
        var configPath = dir.Combine("config.json");
        var cachePath = dir.Combine("cache", "config.cache.json");
        File.WriteAllText(configPath, Json);

        var config = CentralConfig.LoadWithCache(configPath, cachePath, Timeout);

        Assert.False(config.FromCache);
        Assert.False(config.ReadFailed);
        Assert.Equal("/scans", config.Defaults["ScanPath"]);
        Assert.Equal("/scans/buchhaltung", config.ForUser("mmueller")["ScanPath"]);
        Assert.Equal("/it/ScanAttach.msi", config.MsiPath);
        Assert.Equal("1.2.0", config.MinVersion);
        Assert.Equal(Json, File.ReadAllText(cachePath));
    }

    [Fact]
    public void LoadWithCache_CentralFileMissing_UsesCache()
    {
        using var dir = new TempDir();
        var cachePath = dir.Combine("config.cache.json");
        File.WriteAllText(cachePath, Json);

        var config = CentralConfig.LoadWithCache(dir.Combine("missing", "config.json"), cachePath, Timeout);

        Assert.True(config.FromCache);
        Assert.True(config.ReadFailed);
        Assert.Equal("/scans", config.Defaults["ScanPath"]);
        Assert.Equal("1.2.0", config.MinVersion);
    }

    [Fact]
    public void LoadWithCache_CentralFileInvalid_UsesCacheAndKeepsIt()
    {
        using var dir = new TempDir();
        var configPath = dir.Combine("config.json");
        var cachePath = dir.Combine("config.cache.json");
        File.WriteAllText(configPath, "{ not json");
        File.WriteAllText(cachePath, Json);

        var config = CentralConfig.LoadWithCache(configPath, cachePath, Timeout);

        Assert.True(config.FromCache);
        Assert.Equal(Json, File.ReadAllText(cachePath));
    }

    [Fact]
    public void LoadWithCache_NothingReadable_ReturnsEmpty()
    {
        using var dir = new TempDir();

        var config = CentralConfig.LoadWithCache(dir.Combine("config.json"), dir.Combine("config.cache.json"), Timeout);

        Assert.True(config.ReadFailed);
        Assert.Empty(config.Defaults);
        Assert.Empty(config.ForUser("anyone"));
        Assert.Null(config.MinVersion);
    }

    [Fact]
    public void LoadWithCache_NoConfigPath_ReturnsEmptyWithoutCache()
    {
        using var dir = new TempDir();
        var cachePath = dir.Combine("config.cache.json");
        File.WriteAllText(cachePath, Json);

        var config = CentralConfig.LoadWithCache(null, cachePath, Timeout);

        Assert.Same(CentralConfig.Empty, config);
    }

    [Fact]
    public void ForUser_UnknownUser_ReturnsEmptyLayer()
    {
        var config = CentralConfig.Parse(Json);

        Assert.Empty(config.ForUser("unknown"));
    }
}
