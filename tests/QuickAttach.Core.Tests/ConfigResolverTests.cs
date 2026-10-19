using QuickAttach.Core.Config;

namespace QuickAttach.Core.Tests;

public class ConfigResolverTests
{
    private static Dictionary<string, object?> Layer(params (string Key, object? Value)[] values)
    {
        var layer = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in values)
        {
            layer[key] = value;
        }

        return layer;
    }

    [Fact]
    public void Resolve_AppliesPriorityAcrossAllFiveLayers()
    {
        var policy = Layer(("GroupLabel", "policy"), ("AllowUserOverride", true));
        var user = Layer(("GroupLabel", "user"), ("GalleryLabel", "user"));
        var central = CentralConfig.Parse("""
            {
              "defaults": { "GroupLabel": "defaults", "GalleryLabel": "defaults", "GalleryScreentip": "defaults", "TargetTab": "Insert" },
              "users": { "anna": { "GroupLabel": "anna", "GalleryLabel": "anna", "GalleryScreentip": "anna" } }
            }
            """);
        var install = Layer(("GroupLabel", "install"), ("TargetTab", "install"), ("FolderPath", "/install"));

        var config = ConfigResolver.Resolve([policy, central.ForUser("anna"), central.Defaults, install], user);

        Assert.Equal("policy", config.GroupLabel);
        Assert.Equal("user", config.GalleryLabel);
        Assert.Equal("anna", config.GalleryScreentip);
        Assert.Equal("Insert", config.TargetTab);
        Assert.Equal("/install", config.FolderPath);
        Assert.Equal(10, config.MaxItems);
    }

    [Fact]
    public void Resolve_WithoutLayers_ReturnsDefaults()
    {
        var config = ConfigResolver.Resolve([]);

        Assert.Equal("", config.FolderPath);
        Assert.Empty(config.AdditionalPaths);
        Assert.Equal("Schnell anhängen", config.GroupLabel);
        Assert.Equal("Letzte Dateien", config.GalleryLabel);
        Assert.Equal("Neueste Dateien aus dem Ordner anhängen", config.GalleryScreentip);
        Assert.Equal("Message", config.TargetTab);
        Assert.Equal(10, config.MaxItems);
        Assert.Equal(0, config.MaxAgeDays);
        Assert.Equal(["*.pdf", "*.jpg", "*.png", "*.tif"], config.FileFilter);
        Assert.True(config.ShowThumbnails);
        Assert.False(config.AllowUserOverride);
    }

    [Fact]
    public void Resolve_MergesPerValueNotPerLayer()
    {
        var central = CentralConfig.Parse("""
            {
              "defaults": { "FolderPath": "/files/default", "GroupLabel": "Ablage", "MaxItems": 12 },
              "users": { "mmueller": { "FolderPath": "/files/buchhaltung" } }
            }
            """);

        var config = ConfigResolver.Resolve([central.ForUser("mmueller"), central.Defaults]);

        Assert.Equal("/files/buchhaltung", config.FolderPath);
        Assert.Equal("Ablage", config.GroupLabel);
        Assert.Equal(12, config.MaxItems);
    }

    [Fact]
    public void Resolve_UsesUsersOverrideCaseInsensitively()
    {
        var central = CentralConfig.Parse("""{ "defaults": { "FolderPath": "/a" }, "users": { "MMueller": { "folderpath": "/b" } } }""");

        var config = ConfigResolver.Resolve([central.ForUser("mmueller"), central.Defaults]);

        Assert.Equal("/b", config.FolderPath);
    }

    [Fact]
    public void Resolve_AllowUserOverrideFalse_IgnoresUserLayer()
    {
        var user = Layer(("GroupLabel", "user"), ("AllowUserOverride", true));
        var defaults = Layer(("GroupLabel", "central"), ("AllowUserOverride", false));

        var config = ConfigResolver.Resolve([defaults], user);

        Assert.Equal("central", config.GroupLabel);
        Assert.False(config.AllowUserOverride);
    }

    [Fact]
    public void Resolve_AllowUserOverrideTrue_UserLayerBelowPolicy()
    {
        var policy = Layer(("MaxItems", 3));
        var user = Layer(("MaxItems", 7), ("GroupLabel", "user"));
        var defaults = Layer(("GroupLabel", "central"), ("AllowUserOverride", 1));

        var config = ConfigResolver.Resolve([policy, defaults], user);

        Assert.Equal(3, config.MaxItems);
        Assert.Equal("user", config.GroupLabel);
        Assert.True(config.AllowUserOverride);
    }

    [Fact]
    public void Resolve_ExpandsUsernameInFolderPathAndAdditionalPaths()
    {
        var previous = Environment.GetEnvironmentVariable("USERNAME");
        Environment.SetEnvironmentVariable("USERNAME", "testuser");
        try
        {
            var layer = Layer(("FolderPath", @"\\filer01\files\%USERNAME%"), ("AdditionalPaths", "/extra/%USERNAME%"));

            var config = ConfigResolver.Resolve([layer]);

            Assert.Equal(@"\\filer01\files\testuser", config.FolderPath);
            Assert.Equal(["/extra/testuser"], config.AdditionalPaths);
        }
        finally
        {
            Environment.SetEnvironmentVariable("USERNAME", previous);
        }
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(12, 12)]
    [InlineData(31, 30)]
    [InlineData(1000L, 30)]
    [InlineData("25", 25)]
    public void Resolve_ClampsMaxItems(object value, int expected)
    {
        var config = ConfigResolver.Resolve([Layer(("MaxItems", value))]);

        Assert.Equal(expected, config.MaxItems);
    }

    [Fact]
    public void Resolve_InvalidValue_FallsThroughToNextLayer()
    {
        var config = ConfigResolver.Resolve([Layer(("MaxItems", "many"), ("GroupLabel", " ")), Layer(("MaxItems", 5))]);

        Assert.Equal(5, config.MaxItems);
        Assert.Equal("Schnell anhängen", config.GroupLabel);
    }

    [Fact]
    public void Resolve_ParsesListFromSemicolonString()
    {
        var config = ConfigResolver.Resolve([Layer(("FileFilter", "*.pdf; *.jpg;;"))]);

        Assert.Equal(["*.pdf", "*.jpg"], config.FileFilter);
    }

    [Fact]
    public void Resolve_ParsesListFromJsonArrayAndMultiString()
    {
        var central = CentralConfig.Parse("""{ "defaults": { "FileFilter": ["*.pdf", "*.png"] } }""");
        var registry = Layer(("AdditionalPaths", new[] { "/a", "/b" }));

        var config = ConfigResolver.Resolve([registry, central.Defaults]);

        Assert.Equal(["*.pdf", "*.png"], config.FileFilter);
        Assert.Equal(["/a", "/b"], config.AdditionalPaths);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData("true", true)]
    [InlineData("False", false)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    public void Resolve_ParsesBooleans(object value, bool expected)
    {
        var config = ConfigResolver.Resolve([Layer(("ShowThumbnails", value))]);

        Assert.Equal(expected, config.ShowThumbnails);
    }

    [Fact]
    public void Resolve_ClampsNegativeMaxAgeDaysToZero()
    {
        var config = ConfigResolver.Resolve([Layer(("MaxAgeDays", -3))]);

        Assert.Equal(0, config.MaxAgeDays);
    }
}
