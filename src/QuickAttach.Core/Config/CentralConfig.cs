using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace QuickAttach.Core.Config;

/// <summary>Central config.json: defaults, per-user overrides and update settings.</summary>
public sealed class CentralConfig
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyLayer =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, IReadOnlyDictionary<string, object?>> _users;

    private CentralConfig(
        IReadOnlyDictionary<string, object?> defaults,
        Dictionary<string, IReadOnlyDictionary<string, object?>> users,
        string? msiPath,
        string? minVersion,
        bool fromCache,
        bool readFailed)
    {
        Defaults = defaults;
        _users = users;
        MsiPath = msiPath;
        MinVersion = minVersion;
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

    /// <summary>True when the central file could not be read and the local cache was used.</summary>
    public bool FromCache { get; }

    /// <summary>True when a config path is set but the central file could not be read, with or without cache.</summary>
    public bool ReadFailed { get; }

    /// <summary>Returns the users[name] layer (case-insensitive), or an empty layer.</summary>
    public IReadOnlyDictionary<string, object?> ForUser(string userName) =>
        _users.TryGetValue(userName, out var layer) ? layer : EmptyLayer;

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
        var config = json == null ? null : TryParse(json, fromCache: false);
        if (config != null)
        {
            TryWriteCache(cachePath, json!);
            return config;
        }

        var cached = TryRead(cachePath, timeout);
        return (cached == null ? null : TryParse(cached, fromCache: true)) ?? CreateEmpty(readFailed: true);
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
            fromCache,
            readFailed: fromCache);
    }

    private static CentralConfig CreateEmpty(bool readFailed) => new(
        EmptyLayer,
        new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.OrdinalIgnoreCase),
        null,
        null,
        fromCache: false,
        readFailed);

    private static CentralConfig? TryParse(string json, bool fromCache)
    {
        try
        {
            return Parse(json, fromCache);
        }
        catch (Exception)
        {
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
