using QuickAttach.Core.Config;

namespace QuickAttach.Core.Tests;

public class CentralConfigTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    private const string Json = """
        {
          "defaults": { "FolderPath": "/files", "MaxItems": 12, "AllowUserOverride": false },
          "users": { "mmueller": { "FolderPath": "/files/buchhaltung" } },
          "update": { "MsiPath": "/it/QuickAttach.msi", "MinVersion": "1.2.0", "MsiSha256": "ABC123" }
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
        Assert.Equal("/files", config.Defaults["FolderPath"]);
        Assert.Equal("/files/buchhaltung", config.ForUser("mmueller")["FolderPath"]);
        Assert.Equal("/it/QuickAttach.msi", config.MsiPath);
        Assert.Equal("1.2.0", config.MinVersion);
        Assert.Equal("ABC123", config.MsiSha256);
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
        Assert.Equal("/files", config.Defaults["FolderPath"]);
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
