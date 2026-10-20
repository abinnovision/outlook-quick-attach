using System;
using System.Globalization;
using System.IO;

namespace QuickAttach.Core;

/// <summary>
/// Appends to %LocalAppData%\abigroup\QuickAttach\log.txt. Above 1 MB the file moves to log.old.txt,
/// replacing the previous one. Never throws.
/// </summary>
public static class Log
{
    private const long MaxSize = 1024 * 1024;
    private static readonly object Gate = new();

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "abigroup", "QuickAttach", "log.txt");

    private static string OldFilePath { get; } = Path.Combine(Path.GetDirectoryName(FilePath)!, "log.old.txt");

    public static void Info(string source, string message) => Write(source, message);

    public static void Error(string source, Exception exception) => Write(source, exception.ToString());

    public static void Error(string source, string message, Exception exception) => Write(source, message + ": " + exception);

    private static void Write(string source, string message)
    {
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                Rotate();
                var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " [" + source + "] " + message;
                File.AppendAllText(FilePath, line + Environment.NewLine);
            }
            catch (Exception)
            {
                // Logging is best effort.
            }
        }
    }

    private static void Rotate()
    {
        try
        {
            var file = new FileInfo(FilePath);
            if (file.Exists && file.Length > MaxSize)
            {
                File.Delete(OldFilePath);
                File.Move(FilePath, OldFilePath);
            }
        }
        catch (Exception)
        {
            // Another process may hold the file; rotate on a later write.
        }
    }
}
