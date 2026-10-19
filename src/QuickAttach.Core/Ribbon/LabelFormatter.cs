using System.Globalization;
using QuickAttach.Core.Files;

namespace QuickAttach.Core.Ribbon;

public static class LabelFormatter
{
    /// <summary>Gallery item label, e.g. "report.pdf  ·  19.10. 14:05".</summary>
    public static string Format(FileItem item) =>
        item.Name + "  ·  " + item.LastWrite.ToString("dd.MM. HH:mm", CultureInfo.InvariantCulture);
}
