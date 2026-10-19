using System.Collections.Generic;

namespace ScanAttach.Core.Scans;

public sealed class ScanResult
{
    public ScanResult(IReadOnlyList<ScanItem> items, bool unreachable)
    {
        Items = items;
        Unreachable = unreachable;
    }

    public IReadOnlyList<ScanItem> Items { get; }

    /// <summary>True when the lookup timed out or no configured folder could be read.</summary>
    public bool Unreachable { get; }
}
