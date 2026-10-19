using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ScanAttach.Core.Config;

/// <summary>
/// Merges configuration layers value by value. Layers are ordered highest priority first;
/// the first layer that holds a usable value wins. Layer keys should be case-insensitive.
/// </summary>
public static class ConfigResolver
{
    /// <param name="layers">Policy, central users[name], central defaults, install, in that order.</param>
    /// <param name="userLayer">User override layer, inserted at index 1 when AllowUserOverride resolves to true.</param>
    public static ScanAttachConfig Resolve(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> layers,
        IReadOnlyDictionary<string, object?>? userLayer = null)
    {
        var effective = layers.ToList();
        var allowUserOverride = (bool?)Find(effective, "AllowUserOverride", ParseBool) ?? false;
        if (allowUserOverride && userLayer != null)
        {
            effective.Insert(Math.Min(1, effective.Count), userLayer);
        }

        var defaults = new ScanAttachConfig();
        return new ScanAttachConfig
        {
            ScanPath = Environment.ExpandEnvironmentVariables((string?)Find(effective, "ScanPath", ParseString) ?? defaults.ScanPath),
            AdditionalPaths = ((IReadOnlyList<string>?)Find(effective, "AdditionalPaths", ParseList) ?? defaults.AdditionalPaths)
                .Select(Environment.ExpandEnvironmentVariables)
                .ToArray(),
            GroupLabel = (string?)Find(effective, "GroupLabel", ParseString) ?? defaults.GroupLabel,
            GalleryLabel = (string?)Find(effective, "GalleryLabel", ParseString) ?? defaults.GalleryLabel,
            GalleryScreentip = (string?)Find(effective, "GalleryScreentip", ParseString) ?? defaults.GalleryScreentip,
            TargetTab = (string?)Find(effective, "TargetTab", ParseString) ?? defaults.TargetTab,
            MaxItems = Clamp((int?)Find(effective, "MaxItems", ParseInt) ?? defaults.MaxItems, 1, 30),
            MaxAgeDays = Math.Max(0, (int?)Find(effective, "MaxAgeDays", ParseInt) ?? defaults.MaxAgeDays),
            FileFilter = (IReadOnlyList<string>?)Find(effective, "FileFilter", ParseList) ?? defaults.FileFilter,
            ShowThumbnails = (bool?)Find(effective, "ShowThumbnails", ParseBool) ?? defaults.ShowThumbnails,
            AllowUserOverride = allowUserOverride,
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
