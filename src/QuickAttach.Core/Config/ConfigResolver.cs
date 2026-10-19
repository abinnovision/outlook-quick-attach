using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace QuickAttach.Core.Config;

/// <summary>
/// Merges configuration layers value by value. Layers are ordered highest priority first;
/// the first layer that holds a usable value wins. Layer keys should be case-insensitive.
/// </summary>
public static class ConfigResolver
{
    /// <param name="layers">Central users[name], central defaults, user settings, install, in that order.</param>
    public static QuickAttachConfig Resolve(IReadOnlyList<IReadOnlyDictionary<string, object?>> layers)
    {
        var defaults = new QuickAttachConfig();
        return new QuickAttachConfig
        {
            FolderPath = Environment.ExpandEnvironmentVariables((string?)Find(layers, "FolderPath", ParseString) ?? defaults.FolderPath),
            AdditionalPaths = ((IReadOnlyList<string>?)Find(layers, "AdditionalPaths", ParseList) ?? defaults.AdditionalPaths)
                .Select(Environment.ExpandEnvironmentVariables)
                .ToArray(),
            GroupLabel = (string?)Find(layers, "GroupLabel", ParseString) ?? defaults.GroupLabel,
            GalleryLabel = (string?)Find(layers, "GalleryLabel", ParseString) ?? defaults.GalleryLabel,
            GalleryScreentip = (string?)Find(layers, "GalleryScreentip", ParseString) ?? defaults.GalleryScreentip,
            TargetTab = (string?)Find(layers, "TargetTab", ParseString) ?? defaults.TargetTab,
            MaxItems = Clamp((int?)Find(layers, "MaxItems", ParseInt) ?? defaults.MaxItems, 1, 30),
            MaxAgeDays = Math.Max(0, (int?)Find(layers, "MaxAgeDays", ParseInt) ?? defaults.MaxAgeDays),
            FileFilter = (IReadOnlyList<string>?)Find(layers, "FileFilter", ParseList) ?? defaults.FileFilter,
            ShowThumbnails = (bool?)Find(layers, "ShowThumbnails", ParseBool) ?? defaults.ShowThumbnails,
        };
    }

    private static object? Find(IEnumerable<IReadOnlyDictionary<string, object?>> layers, string key, Func<object, object?> parse)
    {
        foreach (var layer in layers)
        {
            if (layer.TryGetValue(key, out var raw) && raw != null && parse(raw) is { } value)
            {
                return value;
            }
        }

        return null;
    }

    private static object? ParseString(object raw)
    {
        var value = Convert.ToString(raw, CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static object? ParseInt(object raw) => raw switch
    {
        int i => i,
        long l => (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, l)),
        string s when int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => null,
    };

    private static object? ParseBool(object raw) => raw switch
    {
        bool b => b,
        int i => i != 0,
        long l => l != 0,
        string s => s.Trim().ToLowerInvariant() switch
        {
            "true" or "1" => true,
            "false" or "0" => false,
            _ => null,
        },
        _ => null,
    };

    private static object? ParseList(object raw)
    {
        IEnumerable<string> items = raw switch
        {
            string s => s.Split(';'),
            IEnumerable e => e.Cast<object?>().Select(o => Convert.ToString(o, CultureInfo.InvariantCulture) ?? ""),
            _ => Enumerable.Empty<string>(),
        };
        var list = items.Select(i => i.Trim()).Where(i => i.Length > 0).ToArray();
        return list.Length == 0 ? null : list;
    }

    private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
}
