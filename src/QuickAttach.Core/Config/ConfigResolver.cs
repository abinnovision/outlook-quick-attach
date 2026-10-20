using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace QuickAttach.Core.Config;

/// <summary>
/// Merges configuration layers value by value. Layers are ordered highest priority first;
/// the first layer that holds a usable value wins. Layer keys should be case-insensitive.
/// Unknown keys and unusable values are reported in <see cref="QuickAttachConfig.Warnings"/>, prefixed with the layer name.
/// </summary>
public static class ConfigResolver
{
    private const int MaxAgeDaysLimit = 36500;

    private static readonly HashSet<string> KnownKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "FolderPath", "AdditionalPaths", "GroupLabel", "GalleryLabel", "GalleryScreentip",
        "TargetTab", "MaxItems", "MaxAgeDays", "FileFilter", "ShowThumbnails",
    };

    private static readonly Regex AbsolutePath = new(@"^(\\\\|[A-Za-z]:\\)", RegexOptions.CultureInvariant);

    /// <param name="layers">Central users[name], central defaults, user settings, install, in that order.</param>
    public static QuickAttachConfig Resolve(IReadOnlyList<(string Name, IReadOnlyDictionary<string, object?> Values)> layers)
    {
        var warnings = new List<string>();
        foreach (var (name, values) in layers)
        {
            warnings.AddRange(values.Keys.Where(k => !KnownKeys.Contains(k)).Select(k => name + ": unknown key \"" + k + "\" ignored"));
        }

        object? Find(string key, Func<object, Action<string>, object?> parse)
        {
            foreach (var (name, values) in layers)
            {
                if (!values.TryGetValue(key, out var raw) || raw == null || raw is string s && s.Trim().Length == 0)
                {
                    continue;
                }

                var warned = false;
                var value = parse(raw, message =>
                {
                    warned = true;
                    warnings.Add(name + ": " + key + " " + message);
                });
                if (value != null)
                {
                    return value;
                }

                if (!warned)
                {
                    warnings.Add(name + ": " + key + " has an invalid value, ignored");
                }
            }

            return null;
        }

        var defaults = new QuickAttachConfig();
        return new QuickAttachConfig
        {
            FolderPath = (string?)Find("FolderPath", ParsePath) ?? defaults.FolderPath,
            AdditionalPaths = (IReadOnlyList<string>?)Find("AdditionalPaths", ParsePaths) ?? defaults.AdditionalPaths,
            GroupLabel = (string?)Find("GroupLabel", ParseString) ?? defaults.GroupLabel,
            GalleryLabel = (string?)Find("GalleryLabel", ParseString) ?? defaults.GalleryLabel,
            GalleryScreentip = (string?)Find("GalleryScreentip", ParseString) ?? defaults.GalleryScreentip,
            TargetTab = (string?)Find("TargetTab", ParseTargetTab) ?? defaults.TargetTab,
            MaxItems = (int?)Find("MaxItems", (raw, warn) => Clamp(ParseInt(raw, warn), 1, 30, warn)) ?? defaults.MaxItems,
            MaxAgeDays = (int?)Find("MaxAgeDays", (raw, warn) => Clamp(ParseInt(raw, warn), 0, MaxAgeDaysLimit, warn)) ?? defaults.MaxAgeDays,
            FileFilter = (IReadOnlyList<string>?)Find("FileFilter", ParseList) ?? defaults.FileFilter,
            ShowThumbnails = (bool?)Find("ShowThumbnails", ParseBool) ?? defaults.ShowThumbnails,
            Warnings = warnings,
        };
    }

    private static object? ParseString(object raw, Action<string> warn) => raw switch
    {
        string s => s,
        // Arrays (REG_MULTI_SZ, JSON lists) and byte[] (REG_BINARY) are not strings.
        IEnumerable => null,
        _ => Convert.ToString(raw, CultureInfo.InvariantCulture),
    };

    private static object? ParsePath(object raw, Action<string> warn) =>
        ParseString(raw, warn) is string s ? CheckPath(Environment.ExpandEnvironmentVariables(s.Trim()), warn) : null;

    private static object? ParsePaths(object raw, Action<string> warn)
    {
        var paths = ((string[]?)ParseList(raw, warn) ?? new string[0])
            .Select(p => CheckPath(Environment.ExpandEnvironmentVariables(p), warn))
            .OfType<string>()
            .ToArray();
        return paths.Length == 0 ? null : paths;
    }

    private static string? CheckPath(string path, Action<string> warn)
    {
        if (AbsolutePath.IsMatch(path))
        {
            return path;
        }

        warn("entry \"" + path + "\" is not an absolute path (\\\\server\\share or X:\\), ignored");
        return null;
    }

    private static object? ParseTargetTab(object raw, Action<string> warn)
    {
        var value = (string?)ParseString(raw, warn);
        if (value == null || value.Equals("Message", StringComparison.OrdinalIgnoreCase) || value.Equals("Insert", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        warn("\"" + value + "\" is not Message or Insert, ignored");
        return null;
    }

    private static object? ParseInt(object raw, Action<string> warn) => raw switch
    {
        int i => i,
        long l => (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, l)),
        string s when int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => null,
    };

    private static object? ParseBool(object raw, Action<string> warn) => raw switch
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

    private static object? ParseList(object raw, Action<string> warn)
    {
        IEnumerable<string>? items = raw switch
        {
            string s => s.Split(';'),
            string[] a => a,
            _ => null,
        };
        var list = items?.Select(i => i.Trim()).Where(i => i.Length > 0).ToArray();
        return list == null || list.Length == 0 ? null : list;
    }

    private static object? Clamp(object? value, int min, int max, Action<string> warn)
    {
        if (value is not int i || i >= min && i <= max)
        {
            return value;
        }

        var clamped = Math.Max(min, Math.Min(max, i));
        warn(i.ToString(CultureInfo.InvariantCulture) + " is outside " + min.ToString(CultureInfo.InvariantCulture) + " to "
            + max.ToString(CultureInfo.InvariantCulture) + ", using " + clamped.ToString(CultureInfo.InvariantCulture));
        return clamped;
    }
}
