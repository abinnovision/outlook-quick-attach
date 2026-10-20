using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace QuickAttach.Core.Config;

/// <summary>Central config.json: defaults, per-user overrides and update settings.</summary>
public sealed class CentralConfig
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyLayer =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    private static int _centralUnreachable;

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
        string? json)
    {
        _json = json;
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

    /// <summary>Returns the users[name] layer (case-insensitive), or an empty layer.</summary>
    public IReadOnlyDictionary<string, object?> ForUser(string userName) =>
        _users.TryGetValue(userName, out var layer) ? layer : EmptyLayer;

    /// <summary>True when both configs were parsed from the same JSON text, or both are empty.</summary>
    public bool HasSameContent(CentralConfig other) => _json == other._json;

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
        return (cached == null ? null : TryParse(cached, fromCache: true, cachePath)) ?? CreateEmpty(readFailed: true);
    }

    /// <summary>Reads only the local cache, or returns <see cref="Empty"/> when it is missing or invalid.</summary>
    public static CentralConfig LoadCache(string cachePath)
    {
        string json;
        try
        {
            json = File.ReadAllText(cachePath);
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            return Empty;
        }

        return TryParse(json, fromCache: true, cachePath) ?? Empty;
    }

    public static CentralConfig Parse(string json) => Parse(json, fromCache: false);

    private static CentralConfig Parse(string json, bool fromCache)
    {
        var root = JObject.Parse(json);
        var users = new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.OrdinalIgnoreCase);
        if (root["users"] is JObject usersObject)
        {
            foreach (var user in usersObject.Properties())
            {
                users[user.Name] = ToLayer(user.Value as JObject);
            }
        }

        var update = root["update"] as JObject;
        return new CentralConfig(
            ToLayer(root["defaults"] as JObject),
            users,
            (string?)update?["MsiPath"],
            (string?)update?["MinVersion"],
            (string?)update?["MsiSha256"],
            fromCache,
            readFailed: fromCache,
            json);
    }

    private static CentralConfig CreateEmpty(bool readFailed) => new(
        EmptyLayer,
        new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.OrdinalIgnoreCase),
        null,
        null,
        null,
        fromCache: false,
        readFailed,
        json: null);

    private static CentralConfig? TryParse(string json, bool fromCache, string path)
    {
        try
        {
            return Parse(json, fromCache);
        }
        catch (Exception e)
        {
            Log.Error("Config", "Invalid config " + path, e);
            return null;
        }
    }

    private static string? TryRead(string path, TimeSpan timeout)
    {
        var read = Task.Run(() => File.ReadAllText(path));
        try
        {
            return read.Wait(timeout) ? read.Result : null;
        }
        catch (AggregateException)
        {
            return null;
        }
    }

    private static void TryWriteCache(string cachePath, string json)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            File.WriteAllText(cachePath, json);
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
