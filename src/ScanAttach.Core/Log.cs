using System;
using System.Globalization;
using System.IO;

namespace ScanAttach.Core;

/// <summary>Appends to %LocalAppData%\abi\ScanAttach\log.txt. Never throws.</summary>
public static class Log
{
    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "abi", "ScanAttach", "log.txt");

    public static void Info(string source, string message) => Write(source, message);

    public static void Error(string source, Exception exception) => Write(source, exception.ToString());

    private static void Write(string source, string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " [" + source + "] " + message;
            File.AppendAllText(FilePath, line + Environment.NewLine);
        }
        catch (Exception)
        {
            // Logging is best effort.
        }
    }
}
