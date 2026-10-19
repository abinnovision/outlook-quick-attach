using System.Globalization;
using ScanAttach.Core.Scans;

namespace ScanAttach.Core.Ribbon;

public static class LabelFormatter
{
    /// <summary>Gallery item label, e.g. "scan.pdf  ·  19.10. 14:05".</summary>
    public static string Format(ScanItem item) =>
        item.Name + "  ·  " + item.LastWrite.ToString("dd.MM. HH:mm", CultureInfo.InvariantCulture);
}
