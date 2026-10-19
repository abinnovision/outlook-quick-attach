using QuickAttach.Core.Files;
using QuickAttach.Core.Ribbon;

namespace QuickAttach.Core.Tests;

public class LabelFormatterTests
{
    [Fact]
    public void Format_ShowsNameAndShortTimestamp()
    {
        var item = new FileItem(Path.Combine("files", "Report_0042.pdf"), new DateTime(2026, 3, 7, 9, 5, 0));

        Assert.Equal("Report_0042.pdf  ·  07.03. 09:05", LabelFormatter.Format(item));
    }
}
