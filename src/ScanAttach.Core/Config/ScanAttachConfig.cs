using System.Collections.Generic;

namespace ScanAttach.Core.Config;

/// <summary>Effective add-in configuration after all layers are merged.</summary>
public sealed class ScanAttachConfig
{
    public static readonly IReadOnlyList<string> DefaultFileFilter = new[] { "*.pdf", "*.jpg", "*.png", "*.tif" };

    public string ScanPath { get; set; } = "";
    public IReadOnlyList<string> AdditionalPaths { get; set; } = new string[0];
    public string GroupLabel { get; set; } = "Scans";
    public string GalleryLabel { get; set; } = "Letzte Scans";
    public string GalleryScreentip { get; set; } = "Neueste Dateien aus dem Scan-Ordner anhängen";
    public string TargetTab { get; set; } = "Message";
    public int MaxItems { get; set; } = 10;
    public int MaxAgeDays { get; set; }
    public IReadOnlyList<string> FileFilter { get; set; } = DefaultFileFilter;
    public bool ShowThumbnails { get; set; } = true;
    public bool AllowUserOverride { get; set; }
}
