using System.Xml.Linq;
using QuickAttach.Core.Files;
using QuickAttach.Core.Ribbon;

namespace QuickAttach.Core.Tests;

public class FileMenuTests
{
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/office/2009/07/customui";

    [Fact]
    public void Build_ListsFilesWithIndexTagsAndActions()
    {
        var files = new FileResult(
            [new FileItem(Path.Combine("files", "a&b <1>.pdf"), new DateTime(2026, 3, 7, 9, 5, 0)), new FileItem(Path.Combine("files", "c.txt"), DateTime.Now)],
            unreachable: false);

        var menu = XElement.Parse(FileMenu.Build(files, showIcons: true, showSettings: true));
        var buttons = menu.Elements(Ns + "button").ToList();

        Assert.Equal("large", (string?)menu.Attribute("itemSize"));
        Assert.Equal(["btnFile0", "btnFile1", "btnOpenFolder", "btnSettings"], buttons.Select(b => (string?)b.Attribute("id")));
        Assert.Equal("a&&b <1>.pdf", (string?)buttons[0].Attribute("label"));
        Assert.Equal("files\u00A0·\u00A007.03.\u00A009:05", (string?)buttons[0].Attribute("description"));
        Assert.Equal("1", (string?)buttons[1].Attribute("tag"));
        Assert.Equal("GetFileImage", (string?)buttons[0].Attribute("getImage"));
        Assert.Equal("OnFileClick", (string?)buttons[0].Attribute("onAction"));
        Assert.Single(menu.Elements(Ns + "menuSeparator"));
        Assert.Equal("FileOpen", (string?)buttons[2].Attribute("imageMso"));
        Assert.Equal("ApplicationOptionsDialog", (string?)buttons[3].Attribute("imageMso"));
    }

    [Fact]
    public void Build_OmitsIconsAndSettingsWhenDisabled()
    {
        var files = new FileResult([new FileItem(Path.Combine("files", "c.txt"), DateTime.Now)], unreachable: false);

        var menu = XElement.Parse(FileMenu.Build(files, showIcons: false, showSettings: false));

        Assert.Null(menu.Element(Ns + "button")!.Attribute("getImage"));
        Assert.DoesNotContain(menu.Elements(Ns + "button"), b => (string?)b.Attribute("id") == "btnSettings");
    }

    [Theory]
    [InlineData(true, "btnUnreachable", FileMenu.UnreachableLabel)]
    [InlineData(false, "btnEmpty", FileMenu.EmptyLabel)]
    public void Build_ShowsDisabledPlaceholderWithoutFiles(bool unreachable, string id, string label)
    {
        var menu = XElement.Parse(FileMenu.Build(new FileResult([], unreachable), showIcons: true, showSettings: false));
        var first = menu.Elements(Ns + "button").First();

        Assert.Equal(id, (string?)first.Attribute("id"));
        Assert.Equal(label, (string?)first.Attribute("label"));
        Assert.Equal("false", (string?)first.Attribute("enabled"));
    }
}
