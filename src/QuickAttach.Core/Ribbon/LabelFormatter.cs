using System.Globalization;
using System.IO;
using QuickAttach.Core.Files;

namespace QuickAttach.Core.Ribbon;

public static class LabelFormatter
{
    /// <summary>
    /// Second menu line, e.g. "\\filer01\files\anna · 19.10. 14:05". Non-breaking spaces keep the timestamp
    /// on the same line as the folder.
    /// </summary>
    public static string Describe(FileItem item) =>
        Path.GetDirectoryName(item.FullPath) + "\u00A0·\u00A0" + item.LastWrite.ToString("dd.MM.\u00A0HH:mm", CultureInfo.InvariantCulture);
}
