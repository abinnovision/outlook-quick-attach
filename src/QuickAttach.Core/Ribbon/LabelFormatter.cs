using System.Globalization;
using System.IO;
using QuickAttach.Core.Files;

namespace QuickAttach.Core.Ribbon;

public static class LabelFormatter
{
    /// <summary>
    /// Second menu line, e.g. "\\filer01\files\anna · 19.10.2026 14:05" with the short date and time of the culture
    /// (<see cref="CultureInfo.CurrentCulture"/> by default). Non-breaking spaces keep the timestamp on the same line
    /// as the folder.
    /// </summary>
    public static string Describe(FileItem item, CultureInfo? culture = null) =>
        Path.GetDirectoryName(item.FullPath) + "\u00A0·\u00A0" + item.LastWrite.ToString("g", culture ?? CultureInfo.CurrentCulture).Replace(' ', '\u00A0');
}
