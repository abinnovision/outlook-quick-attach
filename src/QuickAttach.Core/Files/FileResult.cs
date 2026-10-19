using System.Collections.Generic;

namespace QuickAttach.Core.Files;

public sealed class FileResult
{
    public FileResult(IReadOnlyList<FileItem> items, bool unreachable)
    {
        Items = items;
        Unreachable = unreachable;
    }

    public IReadOnlyList<FileItem> Items { get; }

    /// <summary>True when the lookup timed out or no configured folder could be read.</summary>
    public bool Unreachable { get; }
}
