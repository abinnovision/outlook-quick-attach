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
    public void LoadCache_ReadsOnlyTheCache()
    {
        using var dir = new TempDir();
        var cachePath = dir.Combine("config.cache.json");
        File.WriteAllText(cachePath, Json);

        var config = CentralConfig.LoadCache(cachePath);

        Assert.True(config.FromCache);
        Assert.Equal("/files", config.Defaults["FolderPath"]);
        Assert.True(config.HasSameContent(CentralConfig.Parse(Json)));
    }

    [Fact]
    public void LoadCache_MissingOrInvalid_ReturnsEmpty()
    {
        using var dir = new TempDir();
        var cachePath = dir.Combine("config.cache.json");

        Assert.Same(CentralConfig.Empty, CentralConfig.LoadCache(cachePath));

        File.WriteAllText(cachePath, "{ not json");
        Assert.Same(CentralConfig.Empty, CentralConfig.LoadCache(cachePath));
    }

    [Fact]
    public void HasSameContent_ComparesJsonText()
    {
        Assert.True(CentralConfig.Parse(Json).HasSameContent(CentralConfig.Parse(Json)));
        Assert.False(CentralConfig.Parse(Json).HasSameContent(CentralConfig.Parse("{}")));
        Assert.False(CentralConfig.Parse(Json).HasSameContent(CentralConfig.Empty));
    }

    [Fact]
    public void ForUser_UnknownUser_ReturnsEmptyLayer()
    {
        var config = CentralConfig.Parse(Json);

        Assert.Empty(config.ForUser("unknown"));
    }

    [Theory]
    [InlineData("""{ "defaults": [] }""", "config.json: defaults is not an object, ignored")]
    [InlineData("""{ "users": "anna" }""", "config.json: users is not an object, ignored")]
    [InlineData("""{ "update": 1 }""", "config.json: update is not an object, ignored")]
    [InlineData("""{ "users": { "anna": [] } }""", "config.json: users.anna is not an object, ignored")]
    [InlineData("""{ "default": {} }""", "config.json: unknown key \"default\" ignored")]
    [InlineData("""{ "update": { "MsiPath": ["a"] } }""", "config.json: update.MsiPath is not a string, ignored")]
    [InlineData("""{ "update": { "MinVersion": 1.2 } }""", "config.json: update.MinVersion is not a string, ignored")]
    public void Parse_WarnsAboutStructuralProblems(string json, string warning)
    {
        var config = CentralConfig.Parse(json);

        Assert.Equal([warning], config.Warnings);
    }

    [Fact]
    public void Parse_ValidFile_HasNoWarnings()
    {
        Assert.Empty(CentralConfig.Parse(Json).Warnings);
    }

    [Fact]
    public void Parse_IgnoresNonStringUpdateValueButKeepsTheRest()
    {
        var config = CentralConfig.Parse("""{ "defaults": { "GroupLabel": "x" }, "update": { "MsiPath": 1, "MinVersion": "1.0.0" } }""");

        Assert.Null(config.MsiPath);
        Assert.Equal("1.0.0", config.MinVersion);
        Assert.Equal("x", config.Defaults["GroupLabel"]);
    }

    [Fact]
    public void Parse_KeepsDateLikeStringsAsStrings()
    {
        var config = CentralConfig.Parse("""{ "defaults": { "GroupLabel": "2026-10-19T12:00:00Z" }, "update": { "MinVersion": "2026-10-19" } }""");

        Assert.Equal("2026-10-19T12:00:00Z", config.Defaults["GroupLabel"]);
        Assert.Equal("2026-10-19", config.MinVersion);
    }

    [Fact]
    public void Parse_StripsByteOrderMark()
    {
        Assert.Equal("/files", CentralConfig.Parse("\uFEFF" + Json).Defaults["FolderPath"]);
    }

    [Fact]
    public void Parse_RejectsTrailingContent()
    {
        Assert.ThrowsAny<Exception>(() => CentralConfig.Parse("{} {}"));
    }

    [Fact]
    public void LoadWithCache_CentralFileInvalid_AddsWarning()
    {
        using var dir = new TempDir();
        var configPath = dir.Combine("config.json");
        var cachePath = dir.Combine("config.cache.json");
        File.WriteAllText(configPath, "{ not json");
        File.WriteAllText(cachePath, Json);

        var config = CentralConfig.LoadWithCache(configPath, cachePath, Timeout);

        Assert.Equal(["config.json: invalid JSON, using the cached copy"], config.Warnings);
        Assert.False(config.HasSameContent(CentralConfig.LoadCache(cachePath)));
    }

    [Fact]
    public void LoadWithCache_SkipsFilesLargerThanOneMegabyte()
    {
        using var dir = new TempDir();
        var configPath = dir.Combine("config.json");
        var cachePath = dir.Combine("config.cache.json");
        File.WriteAllText(configPath, "{ \"defaults\": { \"GroupLabel\": \"" + new string('x', 1024 * 1024) + "\" } }");
        File.WriteAllText(cachePath, Json);

        var config = CentralConfig.LoadWithCache(configPath, cachePath, Timeout);

        Assert.True(config.FromCache);
        Assert.Equal(Json, File.ReadAllText(cachePath));
    }

    [Fact]
    public void LoadWithCache_ReplacesExistingCache()
    {
        using var dir = new TempDir();
        var configPath = dir.Combine("config.json");
        var cachePath = dir.Combine("config.cache.json");
        File.WriteAllText(cachePath, "{}");
        File.WriteAllText(configPath, Json);

        CentralConfig.LoadWithCache(configPath, cachePath, Timeout);

        Assert.Equal(Json, File.ReadAllText(cachePath));
        Assert.False(File.Exists(cachePath + ".tmp"));
    }
}
