using QuickAttach.Core.Files;
using QuickAttach.Core.Ribbon;

namespace QuickAttach.Core.Tests;

public class LabelFormatterTests
{
    [Fact]
    public void Describe_ShowsFolderAndShortTimestamp()
    {
        var folder = Path.Combine("files", "anna");
        var item = new FileItem(Path.Combine(folder, "Report_0042.pdf"), new DateTime(2026, 3, 7, 9, 5, 0));

        Assert.Equal(folder + "\u00A0·\u00A007.03.\u00A009:05", LabelFormatter.Describe(item));
    }
}
