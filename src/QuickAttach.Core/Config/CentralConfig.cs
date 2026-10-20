using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace QuickAttach.Core.Config;

/// <summary>Central config.json: defaults, per-user overrides and update settings.</summary>
public sealed class CentralConfig
{
    private const string Source = "config.json";
    private const long MaxFileSize = 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, object?> EmptyLayer =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] TopLevelKeys = { "defaults", "users", "update" };

    // Hash of the last invalid content per path, so a broken file is logged once per change.
    private static readonly ConcurrentDictionary<string, int> InvalidLogged = new(StringComparer.OrdinalIgnoreCase);

    private static int _centralUnreachable;
    private static int _tooLargeLogged;

    private readonly Dictionary<string, IReadOnlyDictionary<string, object?>> _users;
    private readonly string? _json;

    private CentralConfig(
        IReadOnlyDictionary<string, object?> defaults,
        Dictionary<string, IReadOnlyDictionary<string, object?>> users,
        string? msiPath,
        string? minVersion,
        string? msiSha256,
        bool fromCache,
        bool readFailed,
        string? json,
        IReadOnlyList<string> warnings)
    {
        _json = json;
        Warnings = warnings;
        Defaults = defaults;
        _users = users;
        MsiPath = msiPath;
        MinVersion = minVersion;
        MsiSha256 = msiSha256;
        FromCache = fromCache;
        ReadFailed = readFailed;
    }

    public static CentralConfig Empty { get; } = CreateEmpty(readFailed: false);

    /// <summary>Default location of the local cache: %LocalAppData%\abigroup\QuickAttach\config.cache.json.</summary>
    public static string DefaultCachePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "abigroup", "QuickAttach", "config.cache.json");

    public IReadOnlyDictionary<string, object?> Defaults { get; }

    public string? MsiPath { get; }

    public string? MinVersion { get; }

    /// <summary>Expected SHA-256 of the MSI at <see cref="MsiPath"/>, as hex.</summary>
    public string? MsiSha256 { get; }

    /// <summary>True when the central file could not be read and the local cache was used.</summary>
    public bool FromCache { get; }

    /// <summary>True when a config path is set but the central file could not be read, with or without cache.</summary>
    public bool ReadFailed { get; }

    /// <summary>Structural problems of the file, and a note when the central file is invalid and the cache is used.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>Returns the users[name] layer (case-insensitive), or an empty layer.</summary>
    public IReadOnlyDictionary<string, object?> ForUser(string userName) =>
        _users.TryGetValue(userName, out var layer) ? layer : EmptyLayer;

    /// <summary>True when both configs were parsed from the same JSON text with the same warnings, or both are empty.</summary>
    public bool HasSameContent(CentralConfig other) => _json == other._json && Warnings.SequenceEqual(other.Warnings);

    /// <summary>
    /// Reads the central file within the timeout and refreshes the cache on success.
    /// Falls back to the cache when the file is unreachable or invalid, and to <see cref="Empty"/> without either.
    /// </summary>
    public static CentralConfig LoadWithCache(string? configPath, string cachePath, TimeSpan timeout)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            return Empty;
        }

        var json = TryRead(configPath!, timeout);
        if (json == null)
        {
            // Log only the change from reachable to unreachable, not every retry.
            if (Interlocked.Exchange(ref _centralUnreachable, 1) == 0)
            {
                Log.Info("Config", "Central config not reachable: " + configPath);
            }
        }
        else
        {
            Interlocked.Exchange(ref _centralUnreachable, 0);
        }

        var config = json == null ? null : TryParse(json, fromCache: false, configPath!);
        if (config != null)
        {
            TryWriteCache(cachePath, json!);
            return config;
        }

        var cached = TryRead(cachePath, timeout);
        var fallback = (cached == null ? null : TryParse(cached, fromCache: true, cachePath)) ?? CreateEmpty(readFailed: true);
        return json == null ? fallback : fallback.WithWarning(Source + ": invalid JSON" + (fallback.FromCache ? ", using the cached copy" : ""));
    }

    /// <summary>Reads only the local cache, or returns <see cref="Empty"/> when it is missing or invalid.</summary>
    public static CentralConfig LoadCache(string cachePath)
    {
        string? json;
        try
        {
            json = ReadText(cachePath);
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            return Empty;
        }

        return (json == null ? null : TryParse(json, fromCache: true, cachePath)) ?? Empty;
    }

    public static CentralConfig Parse(string json) => Parse(json, fromCache: false);

    private static CentralConfig Parse(string json, bool fromCache)
    {
        // Date-like strings stay strings.
        using var reader = new JsonTextReader(new StringReader(json.TrimStart('\uFEFF'))) { DateParseHandling = DateParseHandling.None };
        var root = JObject.Load(reader);
        while (reader.Read())
        {
            if (reader.TokenType != JsonToken.Comment)
            {
                throw new JsonReaderException("Additional text after the JSON object.");
            }
        }

        var warnings = root.Properties()
            .Where(p => !TopLevelKeys.Contains(p.Name))
            .Select(p => Source + ": unknown key \"" + p.Name + "\" ignored")
            .ToList();
        var users = new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.OrdinalIgnoreCase);
        foreach (var user in Section(root, "users", warnings)?.Properties() ?? Enumerable.Empty<JProperty>())
        {
            if (user.Value is JObject userObject)
            {
                users[user.Name] = ToLayer(userObject);
            }
            else
            {
                warnings.Add(Source + ": users." + user.Name + " is not an object, ignored");
            }
        }

        var defaults = ToLayer(Section(root, "defaults", warnings));
        var update = Section(root, "update", warnings);
        return new CentralConfig(
            defaults,
            users,
            UpdateValue(update, "MsiPath", warnings),
            UpdateValue(update, "MinVersion", warnings),
            UpdateValue(update, "MsiSha256", warnings),
            fromCache,
            readFailed: fromCache,
            json,
            warnings);
    }

    private static JObject? Section(JObject root, string name, List<string> warnings)
    {
        var token = root[name];
        if (token is JObject section)
        {
            return section;
        }

        if (token != null && token.Type != JTokenType.Null)
        {
            warnings.Add(Source + ": " + name + " is not an object, ignored");
        }

        return null;
    }

    private static string? UpdateValue(JObject? update, string name, List<string> warnings)
    {
        var token = update?[name];
        if (token == null || token.Type == JTokenType.Null)
        {
            return null;
        }

        if (token.Type == JTokenType.String)
        {
            return (string?)token;
        }

        warnings.Add(Source + ": update." + name + " is not a string, ignored");
        return null;
    }

    private CentralConfig WithWarning(string warning) =>
        new(Defaults, _users, MsiPath, MinVersion, MsiSha256, FromCache, ReadFailed, _json, Warnings.Concat(new[] { warning }).ToArray());

    private static CentralConfig CreateEmpty(bool readFailed) => new(
        EmptyLayer,
        new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.OrdinalIgnoreCase),
        null,
        null,
        null,
        fromCache: false,
        readFailed,
        json: null,
        new string[0]);

    private static CentralConfig? TryParse(string json, bool fromCache, string path)
    {
        try
        {
            var config = Parse(json, fromCache);
            InvalidLogged.TryRemove(path, out _);
            return config;
        }
        catch (Exception e)
        {
            var hash = json.GetHashCode();
            if (!InvalidLogged.TryGetValue(path, out var logged) || logged != hash)
            {
                InvalidLogged[path] = hash;
                Log.Error("Config", "Invalid config " + path, e);
            }

            return null;
        }
    }

    private static string? TryRead(string path, TimeSpan timeout)
    {
        var read = Task.Run(() => ReadText(path));
        try
        {
            return read.Wait(timeout) ? read.Result : null;
        }
        catch (AggregateException)
        {
            return null;
        }
    }

    /// <summary>Reads the file, or returns null when it is larger than 1 MB.</summary>
    private static string? ReadText(string path)
    {
        if (new FileInfo(path).Length > MaxFileSize)
        {
            if (Interlocked.Exchange(ref _tooLargeLogged, 1) == 0)
            {
                Log.Info("Config", "Config file larger than 1 MB, skipped: " + path);
            }

            return null;
        }

        return File.ReadAllText(path);
    }

    private static void TryWriteCache(string cachePath, string json)
    {
        try
        {
            // Write a temporary file first, so a reader never sees a partial cache.
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            var temp = cachePath + ".tmp";
            File.WriteAllText(temp, json);
            if (File.Exists(cachePath))
            {
                File.Replace(temp, cachePath, null);
            }
            else
            {
                File.Move(temp, cachePath);
            }
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            // The cache is best effort.
        }
    }

    private static IReadOnlyDictionary<string, object?> ToLayer(JObject? obj)
    {
        var layer = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (obj == null)
        {
            return layer;
        }

        foreach (var property in obj.Properties())
        {
            layer[property.Name] = property.Value switch
            {
                JArray array => array.Select(item => item.ToString()).ToArray(),
                JValue value => value.Value,
                _ => null,
            };
        }

        return layer;
    }
}
