using System;

namespace ScanAttach.Core.Scans;

public sealed class ScanItem
{
    public ScanItem(string fullPath, DateTime lastWrite)
    {
        FullPath = fullPath;
        LastWrite = lastWrite;
    }

    public string FullPath { get; }

    public string Name => System.IO.Path.GetFileName(FullPath);

    /// <summary>Last write time in local time.</summary>
    public DateTime LastWrite { get; }
}
