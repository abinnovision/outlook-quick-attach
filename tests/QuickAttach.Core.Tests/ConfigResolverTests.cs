using QuickAttach.Core.Config;

namespace QuickAttach.Core.Tests;

public class ConfigResolverTests
{
    private static (string, IReadOnlyDictionary<string, object?>) Layer(params (string Key, object? Value)[] values) => Layer("test", values);

    private static (string, IReadOnlyDictionary<string, object?>) Layer(string name, params (string Key, object? Value)[] values)
    {
        var layer = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in values)
        {
            layer[key] = value;
        }

        return (name, layer);
    }

    [Fact]
    public void Resolve_AppliesPriorityAcrossAllLayers()
    {
        var central = CentralConfig.Parse("""
            {
              "defaults": { "GroupLabel": "defaults", "GalleryLabel": "defaults", "TargetTab": "Insert" },
              "users": { "anna": { "GroupLabel": "anna" } }
            }
            """);
        var user = Layer(("GroupLabel", "user"), ("GalleryLabel", "user"), ("TargetTab", "Message"), ("GalleryScreentip", "user"));
        var install = Layer(("GroupLabel", "install"), ("GalleryScreentip", "install"), ("FolderPath", @"\\filer01\install"));

        var config = ConfigResolver.Resolve([("users", central.ForUser("anna")), ("defaults", central.Defaults), user, install]);

        Assert.Equal("anna", config.GroupLabel);
        Assert.Equal("defaults", config.GalleryLabel);
        Assert.Equal("Insert", config.TargetTab);
        Assert.Equal("user", config.GalleryScreentip);
        Assert.Equal(@"\\filer01\install", config.FolderPath);
        Assert.Equal(10, config.MaxItems);
        Assert.Empty(config.Warnings);
    }

    [Fact]
    public void Resolve_IgnoresUnknownKeysWithWarning()
    {
        var central = CentralConfig.Parse("""{ "defaults": { "AllowUserOverride": false, "groupLABEL": "central" } }""");

        var config = ConfigResolver.Resolve([("config.json defaults", central.Defaults), Layer(("GalleryLabel", "user"))]);

        Assert.Equal("central", config.GroupLabel);
        Assert.Equal("user", config.GalleryLabel);
        Assert.Equal(["config.json defaults: unknown key \"AllowUserOverride\" ignored"], config.Warnings);
    }

    [Fact]
    public void Resolve_WithoutLayers_ReturnsDefaults()
    {
        var config = ConfigResolver.Resolve([]);

        Assert.Equal("", config.FolderPath);
        Assert.Empty(config.AdditionalPaths);
        Assert.Equal("Quick Attach", config.GroupLabel);
        Assert.Equal("Recent Files", config.GalleryLabel);
        Assert.Equal("Attach recent files from the configured folder", config.GalleryScreentip);
        Assert.Equal("Message", config.TargetTab);
        Assert.Equal(10, config.MaxItems);
        Assert.Equal(0, config.MaxAgeDays);
        Assert.Equal(["*.pdf", "*.jpg", "*.png", "*.tif"], config.FileFilter);
        Assert.True(config.ShowThumbnails);
        Assert.Empty(config.Warnings);
    }

    [Fact]
    public void Resolve_MergesPerValueNotPerLayer()
    {
        var central = CentralConfig.Parse("""
            {
              "defaults": { "FolderPath": "\\\\filer01\\default", "GroupLabel": "Documents", "MaxItems": 12 },
              "users": { "mmueller": { "FolderPath": "\\\\filer01\\buchhaltung" } }
            }
            """);

        var config = ConfigResolver.Resolve([("users", central.ForUser("mmueller")), ("defaults", central.Defaults)]);

        Assert.Equal(@"\\filer01\buchhaltung", config.FolderPath);
        Assert.Equal("Documents", config.GroupLabel);
        Assert.Equal(12, config.MaxItems);
    }

    [Fact]
    public void Resolve_UsesUsersOverrideCaseInsensitively()
    {
        var central = CentralConfig.Parse("""{ "defaults": { "FolderPath": "C:\\a" }, "users": { "MMueller": { "folderpath": "C:\\b" } } }""");

        var config = ConfigResolver.Resolve([("users", central.ForUser("mmueller")), ("defaults", central.Defaults)]);

        Assert.Equal(@"C:\b", config.FolderPath);
    }

    [Fact]
    public void Resolve_ExpandsUsernameInFolderPathAndAdditionalPaths()
    {
        var previous = Environment.GetEnvironmentVariable("USERNAME");
        Environment.SetEnvironmentVariable("USERNAME", "testuser");
        try
        {
            var layer = Layer(("FolderPath", @"\\filer01\files\%USERNAME%"), ("AdditionalPaths", @"D:\extra\%USERNAME%"));

            var config = ConfigResolver.Resolve([layer]);

            Assert.Equal(@"\\filer01\files\testuser", config.FolderPath);
            Assert.Equal([@"D:\extra\testuser"], config.AdditionalPaths);
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

    [Theory]
    [InlineData(-3, 0)]
    [InlineData(7, 7)]
    [InlineData(int.MaxValue, 36500)]
    public void Resolve_ClampsMaxAgeDays(int value, int expected)
    {
        var config = ConfigResolver.Resolve([Layer(("MaxAgeDays", value))]);

        Assert.Equal(expected, config.MaxAgeDays);
    }

    [Fact]
    public void Resolve_InvalidValue_FallsThroughToNextLayer()
    {
        var config = ConfigResolver.Resolve([Layer(("MaxItems", "many"), ("GroupLabel", " ")), Layer(("MaxItems", 5))]);

        Assert.Equal(5, config.MaxItems);
        Assert.Equal("Quick Attach", config.GroupLabel);
    }

    [Fact]
    public void Resolve_BlankValues_AreNotSetWithoutWarning()
    {
        var config = ConfigResolver.Resolve([Layer(("FolderPath", ""), ("GroupLabel", "  "), ("MaxItems", null))]);

        Assert.Equal("", config.FolderPath);
        Assert.Empty(config.Warnings);
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
        var registry = Layer(("AdditionalPaths", new[] { @"\\filer01\a", @"\\filer01\b" }));

        var config = ConfigResolver.Resolve([registry, ("defaults", central.Defaults)]);

        Assert.Equal(["*.pdf", "*.png"], config.FileFilter);
        Assert.Equal([@"\\filer01\a", @"\\filer01\b"], config.AdditionalPaths);
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

    public static TheoryData<string, object, string> WarningCases => new()
    {
        { "Unknown", 1, "src: unknown key \"Unknown\" ignored" },
        { "GroupLabel", new[] { "a", "b" }, "src: GroupLabel has an invalid value, ignored" },
        { "GalleryLabel", new byte[] { 1, 2 }, "src: GalleryLabel has an invalid value, ignored" },
        { "MaxItems", "many", "src: MaxItems has an invalid value, ignored" },
        { "MaxItems", 2.5, "src: MaxItems has an invalid value, ignored" },
        { "MaxItems", 0, "src: MaxItems 0 is outside 1 to 30, using 1" },
        { "MaxItems", 31, "src: MaxItems 31 is outside 1 to 30, using 30" },
        { "MaxAgeDays", -1, "src: MaxAgeDays -1 is outside 0 to 36500, using 0" },
        { "MaxAgeDays", 40000, "src: MaxAgeDays 40000 is outside 0 to 36500, using 36500" },
        { "ShowThumbnails", "yes", "src: ShowThumbnails has an invalid value, ignored" },
        { "FileFilter", 5, "src: FileFilter has an invalid value, ignored" },
        { "TargetTab", "Home", "src: TargetTab \"Home\" is not Message or Insert, ignored" },
        { "FolderPath", "files", @"src: FolderPath entry ""files"" is not an absolute path (\\server\share or X:\), ignored" },
        { "FolderPath", "/files", @"src: FolderPath entry ""/files"" is not an absolute path (\\server\share or X:\), ignored" },
        { "FolderPath", new[] { @"\\filer01\a" }, "src: FolderPath has an invalid value, ignored" },
        { "AdditionalPaths", @"\\filer01\a;relative", @"src: AdditionalPaths entry ""relative"" is not an absolute path (\\server\share or X:\), ignored" },
    };

    [Theory]
    [MemberData(nameof(WarningCases))]
    public void Resolve_WarnsAboutUnusableValues(string key, object value, string warning)
    {
        var config = ConfigResolver.Resolve([Layer("src", (key, value))]);

        Assert.Equal([warning], config.Warnings);
    }

    [Fact]
    public void Resolve_InvalidEntries_AreIgnored()
    {
        var config = ConfigResolver.Resolve([
            Layer("src", ("FolderPath", "relative"), ("AdditionalPaths", new[] { "relative", @"X:\extra" }), ("TargetTab", "Home")),
            Layer("next", ("FolderPath", @"\\filer01\files")),
        ]);

        Assert.Equal(@"\\filer01\files", config.FolderPath);
        Assert.Equal([@"X:\extra"], config.AdditionalPaths);
        Assert.Equal("Message", config.TargetTab);
        Assert.Equal(3, config.Warnings.Count);
    }
}
