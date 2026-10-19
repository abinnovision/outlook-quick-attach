using ScanAttach.Core.Ribbon;
using ScanAttach.Core.Scans;

namespace ScanAttach.Core.Tests;

public class LabelFormatterTests
{
    [Fact]
    public void Format_ShowsNameAndShortTimestamp()
    {
        var item = new ScanItem(Path.Combine("scans", "Scan_0042.pdf"), new DateTime(2026, 3, 7, 9, 5, 0));

        Assert.Equal("Scan_0042.pdf  ·  07.03. 09:05", LabelFormatter.Format(item));
    }
}
