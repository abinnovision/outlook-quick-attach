using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using QuickAttach.Core.Config;

namespace QuickAttach.Core.Files;

/// <summary>
/// Lists the newest matching files of the configured folders, with a timeout and a short cache. An unreachable
/// result is kept for longer and re-checked in the background, so the caller does not wait on every lookup.
/// When a lookup times out after a reachable result, that result is kept while the background check runs.
/// </summary>
public sealed class FileRepository
{
    private const int MaxSkippedEntries = 1000;

    private static readonly TimeSpan CacheWindow = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan UnreachableWindow = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MinFileAge = TimeSpan.FromSeconds(1);

    // Shortcuts and shell files that point elsewhere or run code when opened.
    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".lnk", ".url", ".scf", ".library-ms", ".searchConnector-ms",
    };

    private readonly TimeSpan _timeout;
    private readonly Func<DateTime> _utcNow;
    private readonly object _lock = new();
    private string? _cacheKey;
    private DateTime _cachedAt;
    private FileResult? _cached;
    private bool _stale;
    private int _probing;

    /// <param name="timeout">Maximum time for one lookup, 2 s by default.</param>
    /// <param name="utcNow">Clock returning UTC time, <see cref="DateTime.UtcNow"/> by default.</param>
    public FileRepository(TimeSpan? timeout = null, Func<DateTime>? utcNow = null)
    {
        _timeout = timeout ?? TimeSpan.FromSeconds(2);
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public FileResult GetLatest(QuickAttachConfig config)
    {
        var paths = new[] { config.FolderPath }
            .Concat(config.AdditionalPaths)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();
        var key = string.Join("|", paths) + "|" + string.Join(";", config.FileFilter) + "|" + config.MaxItems + "|" + config.MaxAgeDays;
        var now = _utcNow();

        lock (_lock)
        {
            var recheck = _cached != null && (_cached.Unreachable || _stale);
            if (_cached != null && _cacheKey == key && now - _cachedAt < (recheck ? UnreachableWindow : CacheWindow))
            {
                if (recheck)
                {
                    Probe(key, paths, config, now);
                }

                return _cached;
            }
        }

        var result = Search(paths, config, now);

        lock (_lock)
        {
            var timedOut = result == null;
            if (timedOut && _cached != null && _cacheKey == key && !_cached.Unreachable)
            {
                result = _cached;
            }

            _cacheKey = key;
            _cachedAt = now;
            _cached = result ?? new FileResult(new FileItem[0], unreachable: true);
            _stale = timedOut;
            return _cached;
        }
    }

    /// <summary>Looks up the folders in the background, at most once at a time, and caches a reachable result.</summary>
    private void Probe(string key, string[] paths, QuickAttachConfig config, DateTime now)
    {
        if (Interlocked.CompareExchange(ref _probing, 1, 0) != 0)
        {
            return;
        }

        Task.Run(() => Enumerate(paths, config, now)).ContinueWith(
            probe =>
            {
                if (probe.Status == TaskStatus.RanToCompletion && !probe.Result.Unreachable)
                {
                    lock (_lock)
                    {
                        _cacheKey = key;
                        _cachedAt = _utcNow();
                        _cached = probe.Result;
                        _stale = false;
                    }
                }

                Interlocked.Exchange(ref _probing, 0);
            },
            TaskScheduler.Default);
    }

    /// <summary>Returns null when the lookup timed out or failed.</summary>
    private FileResult? Search(string[] paths, QuickAttachConfig config, DateTime now)
    {
        if (paths.Length == 0)
        {
            return new FileResult(new FileItem[0], unreachable: false);
        }

        var lookup = Task.Run(() => Enumerate(paths, config, now));
        try
        {
            return lookup.Wait(_timeout) ? lookup.Result : null;
        }
        catch (AggregateException)
        {
            return null;
        }
    }

    private static FileResult Enumerate(string[] paths, QuickAttachConfig config, DateTime now)
    {
        var filters = config.FileFilter.Select(ToRegex).ToArray();
        var excludeHidden = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var files = new List<FileInfo>();
        var failed = 0;

        foreach (var path in paths)
        {
            try
            {
                AddFiles(path, files);
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
            .Where(f => !BlockedExtensions.Contains(f.Extension))
            .Where(f => !excludeHidden || (f.Attributes & FileAttributes.Hidden) == 0)
            // A file dated in the future (clock skew) is not treated as still being written.
            .Where(f => !(now - f.LastWriteTimeUtc >= TimeSpan.Zero && now - f.LastWriteTimeUtc < MinFileAge))
            .Where(f => config.MaxAgeDays <= 0 || now - f.LastWriteTimeUtc <= TimeSpan.FromDays(config.MaxAgeDays))
            .GroupBy(f => Path.GetFullPath(f.FullName), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Take(config.MaxItems)
            .Select(f => new FileItem(f.FullName, f.LastWriteTime))
            .ToArray();

        return new FileResult(items, unreachable: failed == paths.Length);
    }

    /// <summary>Adds the files of one folder, skipping entries whose path is too long.</summary>
    private static void AddFiles(string path, List<FileInfo> files)
    {
        using var entries = new DirectoryInfo(path).EnumerateFiles().GetEnumerator();
        for (var skipped = 0; skipped < MaxSkippedEntries;)
        {
            try
            {
                if (!entries.MoveNext())
                {
                    return;
                }

                files.Add(entries.Current);
            }
            catch (PathTooLongException)
            {
                skipped++;
            }
        }
    }

    private static Regex ToRegex(string pattern) => new(
        "^" + Regex.Escape(pattern.Trim()).Replace(@"\*", ".*").Replace(@"\?", ".") + "$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
