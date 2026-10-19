using System.Collections.Generic;

namespace QuickAttach.Core.Config;

/// <summary>Effective add-in configuration after all layers are merged.</summary>
public sealed class QuickAttachConfig
{
    public static readonly IReadOnlyList<string> DefaultFileFilter = new[] { "*.pdf", "*.jpg", "*.png", "*.tif" };

    public string FolderPath { get; set; } = "";
    public IReadOnlyList<string> AdditionalPaths { get; set; } = new string[0];
    public string GroupLabel { get; set; } = "Quick Attach";
    public string GalleryLabel { get; set; } = "Recent Files";
    public string GalleryScreentip { get; set; } = "Attach recent files from the configured folder";
    public string TargetTab { get; set; } = "Message";
    public int MaxItems { get; set; } = 10;
    public int MaxAgeDays { get; set; }
    public IReadOnlyList<string> FileFilter { get; set; } = DefaultFileFilter;
    public bool ShowThumbnails { get; set; } = true;
    public bool AllowUserOverride { get; set; }
}
