using System;

namespace QuickAttach.Core.Files;

public sealed class FileItem
{
    public FileItem(string fullPath, DateTime lastWrite)
    {
        FullPath = fullPath;
        LastWrite = lastWrite;
    }

    public string FullPath { get; }

    public string Name => System.IO.Path.GetFileName(FullPath);

    /// <summary>Last write time in local time.</summary>
    public DateTime LastWrite { get; }
}
