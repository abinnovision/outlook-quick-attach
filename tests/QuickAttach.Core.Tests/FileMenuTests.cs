using System.Xml.Linq;
using QuickAttach.Core.Files;
using QuickAttach.Core.Ribbon;

namespace QuickAttach.Core.Tests;

public class FileMenuTests
{
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/office/2009/07/customui";

    [Fact]
    public void Build_ListsFilesWithPathTagsAndActions()
    {
        var files = new FileResult(
            [new FileItem(Path.Combine("files", "a&b <1>.pdf"), new DateTime(2026, 3, 7, 9, 5, 0)), new FileItem(Path.Combine("files", "c.txt"), DateTime.Now)],
            unreachable: false);

        var menu = XElement.Parse(FileMenu.Build(files, showIcons: true));
        var buttons = menu.Elements(Ns + "button").ToList();

        Assert.Equal("large", (string?)menu.Attribute("itemSize"));
        Assert.Equal(["btnFile0", "btnFile1", "btnOpenFolder", "btnSettings"], buttons.Select(b => (string?)b.Attribute("id")));
        Assert.Equal("a&&b <1>.pdf", (string?)buttons[0].Attribute("label"));
        Assert.Equal(LabelFormatter.Describe(files.Items[0]), (string?)buttons[0].Attribute("description"));
        Assert.Equal(files.Items[1].FullPath, (string?)buttons[1].Attribute("tag"));
        Assert.Equal("GetFileImage", (string?)buttons[0].Attribute("getImage"));
        Assert.Equal("OnFileClick", (string?)buttons[0].Attribute("onAction"));
        Assert.Single(menu.Elements(Ns + "menuSeparator"));
        Assert.Equal("FileOpen", (string?)buttons[2].Attribute("imageMso"));
        Assert.Equal("ApplicationOptionsDialog", (string?)buttons[3].Attribute("imageMso"));
    }

    [Fact]
    public void Build_OmitsIconsWhenDisabled()
    {
        var files = new FileResult([new FileItem(Path.Combine("files", "c.txt"), DateTime.Now)], unreachable: false);

        var menu = XElement.Parse(FileMenu.Build(files, showIcons: false));

        Assert.Null(menu.Element(Ns + "button")!.Attribute("getImage"));
    }

    [Theory]
    [InlineData(true, "btnUnreachable")]
    [InlineData(false, "btnEmpty")]
    public void Build_ShowsDisabledPlaceholderWithoutFiles(bool unreachable, string id)
    {
        var menu = XElement.Parse(FileMenu.Build(new FileResult([], unreachable), showIcons: true));
        var first = menu.Elements(Ns + "button").First();

        Assert.Equal(id, (string?)first.Attribute("id"));
        Assert.Equal(unreachable ? Strings.FolderNotReachable : Strings.NoFilesFound, (string?)first.Attribute("label"));
        Assert.Equal("false", (string?)first.Attribute("enabled"));
    }

    [Fact]
    public void Build_EscapesMnemonicsOnlyInLabel()
    {
        var item = new FileItem(Path.Combine("a&b", "c&d.pdf"), DateTime.Now);

        var button = XElement.Parse(FileMenu.Build(new FileResult([item], unreachable: false), showIcons: false)).Element(Ns + "button")!;

        Assert.Equal("c&&d.pdf", (string?)button.Attribute("label"));
        Assert.StartsWith("a&b\u00A0", (string?)button.Attribute("description"));
    }

    [Fact]
    public void Build_KeepsSurrogatePairsAndTruncatesLongLabels()
    {
        var item = new FileItem(Path.Combine("f", "\uD83D\uDE00" + new string('x', 1010) + ".pdf"), DateTime.Now);

        var button = XElement.Parse(FileMenu.Build(new FileResult([item], unreachable: false), showIcons: false)).Element(Ns + "button")!;
        var label = (string)button.Attribute("label")!;

        Assert.StartsWith("\uD83D\uDE00x", label);
        Assert.Equal(1000, label.Length);
        Assert.Equal(item.FullPath, (string?)button.Attribute("tag"));
    }

    [Fact]
    public void Build_SkipsFilesWhosePathDoesNotFitTheTag()
    {
        var files = new FileResult(
            [new FileItem(Path.Combine("files", new string('x', FileMenu.MaxTagLength) + ".pdf"), DateTime.Now), new FileItem(Path.Combine("files", "a\uD800.pdf"), DateTime.Now)],
            unreachable: false);

        var first = XElement.Parse(FileMenu.Build(files, showIcons: false)).Element(Ns + "button")!;

        Assert.Equal("btnEmpty", (string?)first.Attribute("id"));
    }

    [Fact]
    public void BuildError_ShowsDisabledErrorEntryAndActions()
    {
        var buttons = XElement.Parse(FileMenu.BuildError()).Elements(Ns + "button").ToList();

        Assert.Equal(["btnError", "btnOpenFolder", "btnSettings"], buttons.Select(b => (string?)b.Attribute("id")));
        Assert.Equal(Strings.MenuError, (string?)buttons[0].Attribute("label"));
        Assert.Equal("false", (string?)buttons[0].Attribute("enabled"));
    }
}
