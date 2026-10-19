using System.Globalization;
using QuickAttach.Core.Files;
using QuickAttach.Core.Ribbon;

namespace QuickAttach.Core.Tests;

public class LabelFormatterTests
{
    private static readonly FileItem Item = new(Path.Combine("files", "anna", "Report_0042.pdf"), new DateTime(2026, 3, 7, 9, 5, 0));

    [Fact]
    public void Describe_ShowsFolderAndShortDateTimeOfCulture()
    {
        var folder = Path.Combine("files", "anna");

        Assert.Equal(folder + "\u00A0·\u00A007.03.2026\u00A009:05", LabelFormatter.Describe(Item, CultureInfo.GetCultureInfo("de-DE")));
    }

    [Fact]
    public void Describe_KeepsTimestampOnOneLine()
    {
        Assert.DoesNotContain(" ", LabelFormatter.Describe(Item, CultureInfo.GetCultureInfo("en-US")));
    }
}
