using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ScanAttach.Core.Config;

namespace ScanAttach.Core.Scans;

/// <summary>Lists the newest matching files of the configured folders, with a timeout and a short cache.</summary>
public sealed class ScanRepository
{
    private static readonly TimeSpan CacheWindow = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MinFileAge = TimeSpan.FromSeconds(1);

    private readonly TimeSpan _timeout;
    private readonly Func<DateTime> _utcNow;
    private readonly object _lock = new();
    private string? _cacheKey;
    private DateTime _cachedAt;
    private ScanResult? _cached;

    /// <param name="timeout">Maximum time for one lookup, 2 s by default.</param>
    /// <param name="utcNow">Clock returning UTC time, <see cref="DateTime.UtcNow"/> by default.</param>
    public ScanRepository(TimeSpan? timeout = null, Func<DateTime>? utcNow = null)
    {
        _timeout = timeout ?? TimeSpan.FromSeconds(2);
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public ScanResult GetLatest(ScanAttachConfig config)
    {
        var paths = new[] { config.ScanPath }
            .Concat(config.AdditionalPaths)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();
        var key = string.Join("|", paths) + "|" + string.Join(";", config.FileFilter) + "|" + config.MaxItems + "|" + config.MaxAgeDays;
        var now = _utcNow();

        lock (_lock)
        {
            if (_cached != null && _cacheKey == key && now - _cachedAt < CacheWindow)
            {
                return _cached;
            }
        }

        var result = Scan(paths, config, now);

        lock (_lock)
        {
            _cacheKey = key;
            _cachedAt = now;
            _cached = result;
        }

        return result;
    }

    private ScanResult Scan(string[] paths, ScanAttachConfig config, DateTime now)
    {
        if (paths.Length == 0)
        {
            return new ScanResult(new ScanItem[0], unreachable: false);
        }

        var lookup = Task.Run(() => Enumerate(paths, config, now));
        try
        {
            return lookup.Wait(_timeout) ? lookup.Result : new ScanResult(new ScanItem[0], unreachable: true);
        }
        catch (AggregateException)
        {
            return new ScanResult(new ScanItem[0], unreachable: true);
        }
    }

    private static ScanResult Enumerate(string[] paths, ScanAttachConfig config, DateTime now)
    {
        var filters = config.FileFilter.Select(ToRegex).ToArray();
        var excludeHidden = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var files = new List<FileInfo>();
        var failed = 0;

        foreach (var path in paths)
        {
            try
            {
                files.AddRange(new DirectoryInfo(path).EnumerateFiles());
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                failed++;
            }
        }

        var items = files
            .Where(f => filters.Length == 0 || filters.Any(r => r.IsMatch(f.Name)))
            .Where(f => !f.Name.StartsWith("~$", StringComparison.Ordinal))
            .Where(f => !f.Name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
            .Where(f => !excludeHidden || (f.Attributes & FileAttributes.Hidden) == 0)
            .Where(f => now - f.LastWriteTimeUtc >= MinFileAge)
            .Where(f => config.MaxAgeDays <= 0 || now - f.LastWriteTimeUtc <= TimeSpan.FromDays(config.MaxAgeDays))
            .GroupBy(f => Path.GetFullPath(f.FullName), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Take(config.MaxItems)
            .Select(f => new ScanItem(f.FullName, f.LastWriteTime))
            .ToArray();

        return new ScanResult(items, unreachable: failed == paths.Length);
    }

    private static Regex ToRegex(string pattern) => new(
        "^" + Regex.Escape(pattern.Trim()).Replace(@"\*", ".*").Replace(@"\?", ".") + "$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
