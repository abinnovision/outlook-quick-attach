using System.Diagnostics;
using QuickAttach.Core.Config;
using QuickAttach.Core.Files;

namespace QuickAttach.Core.Tests;

public class FileRepositoryTests
{
    private static readonly DateTime Now = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    private static FileRepository Repository(Func<DateTime>? clock = null, TimeSpan? timeout = null) =>
        new(timeout, clock ?? (() => Now));

    private static string[] Names(FileResult result) => result.Items.Select(i => i.Name).ToArray();

    [Fact]
    public void GetLatest_FiltersByPatternCaseInsensitively()
    {
        using var dir = new TempDir();
        dir.WriteFile("REPORT.PDF", Now.AddMinutes(-1));
        dir.WriteFile("a.Jpg", Now.AddMinutes(-2));
        dir.WriteFile("notes.docx", Now.AddMinutes(-3));
        dir.WriteFile("pdf", Now.AddMinutes(-4));

        var result = Repository().GetLatest(new QuickAttachConfig { FolderPath = dir.Path, FileFilter = ["*.pdf", "*.jpg"] });

        Assert.False(result.Unreachable);
        Assert.Equal(["REPORT.PDF", "a.Jpg"], Names(result));
    }

    [Fact]
    public void GetLatest_SortsNewestFirstAndTakesMaxItems()
    {
        using var dir = new TempDir();
        dir.WriteFile("old.pdf", Now.AddHours(-3));
        dir.WriteFile("newest.pdf", Now.AddMinutes(-1));
        dir.WriteFile("middle.pdf", Now.AddHours(-1));

        var result = Repository().GetLatest(new QuickAttachConfig { FolderPath = dir.Path, MaxItems = 2 });

        Assert.Equal(["newest.pdf", "middle.pdf"], Names(result));
    }

    [Fact]
    public void GetLatest_AppliesMaxAgeDays()
    {
        using var dir = new TempDir();
        dir.WriteFile("recent.pdf", Now.AddDays(-2));
        dir.WriteFile("stale.pdf", Now.AddDays(-8));

        var result = Repository().GetLatest(new QuickAttachConfig { FolderPath = dir.Path, MaxAgeDays = 7 });

        Assert.Equal(["recent.pdf"], Names(result));
    }

    [Fact]
    public void GetLatest_MaxAgeDaysZero_KeepsOldFiles()
    {
        using var dir = new TempDir();
        dir.WriteFile("ancient.pdf", Now.AddYears(-3));

        var result = Repository().GetLatest(new QuickAttachConfig { FolderPath = dir.Path });

        Assert.Equal(["ancient.pdf"], Names(result));
    }

    [Fact]
    public void GetLatest_ExcludesTempLockAndYoungFiles()
    {
        using var dir = new TempDir();
        dir.WriteFile("done.pdf", Now.AddSeconds(-5));
        dir.WriteFile("~$lock.pdf", Now.AddMinutes(-1));
        dir.WriteFile("upload.TMP", Now.AddMinutes(-1));
        dir.WriteFile("writing.pdf", Now.AddMilliseconds(-500));

        var result = Repository().GetLatest(new QuickAttachConfig { FolderPath = dir.Path, FileFilter = ["*"] });

        Assert.Equal(["done.pdf"], Names(result));
    }

    [Fact]
    public void GetLatest_IncludesAdditionalPathsAndDedupesByFullPath()
    {
        using var main = new TempDir();
        using var extra = new TempDir();
        main.WriteFile("main.pdf", Now.AddMinutes(-2));
        extra.WriteFile("extra.pdf", Now.AddMinutes(-1));

        var result = Repository().GetLatest(new QuickAttachConfig
        {
            FolderPath = main.Path,
            AdditionalPaths = [extra.Path, main.Path],
        });

        Assert.Equal(["extra.pdf", "main.pdf"], Names(result));
        Assert.Equal(main.Combine("main.pdf"), result.Items[1].FullPath);
    }

    [Fact]
    public void GetLatest_ReturnsCachedResultWithinWindow()
    {
        using var dir = new TempDir();
        var now = Now;
        var repository = Repository(() => now);
        var config = new QuickAttachConfig { FolderPath = dir.Path };
        dir.WriteFile("first.pdf", Now.AddMinutes(-1));
        Assert.Equal(["first.pdf"], Names(repository.GetLatest(config)));

        dir.WriteFile("second.pdf", Now.AddSeconds(-30));
        now = Now.AddSeconds(4);
        Assert.Equal(["first.pdf"], Names(repository.GetLatest(config)));

        now = Now.AddSeconds(6);
        Assert.Equal(["second.pdf", "first.pdf"], Names(repository.GetLatest(config)));
    }

    [Fact]
    public void GetLatest_ChangedConfig_BypassesCache()
    {
        using var dir = new TempDir();
        var repository = Repository();
        dir.WriteFile("a.pdf", Now.AddMinutes(-1));
        dir.WriteFile("b.jpg", Now.AddMinutes(-2));

        repository.GetLatest(new QuickAttachConfig { FolderPath = dir.Path, FileFilter = ["*.pdf"] });
        var result = repository.GetLatest(new QuickAttachConfig { FolderPath = dir.Path, FileFilter = ["*.jpg"] });

        Assert.Equal(["b.jpg"], Names(result));
    }

    [Fact]
    public void GetLatest_UnreachablePath_ReturnsWithinTimeout()
    {
        using var dir = new TempDir();
        var repository = Repository(timeout: TimeSpan.FromMilliseconds(200));

        var stopwatch = Stopwatch.StartNew();
        var result = repository.GetLatest(new QuickAttachConfig { FolderPath = dir.Combine("filer01", "files") });
        stopwatch.Stop();

        Assert.True(result.Unreachable);
        Assert.Empty(result.Items);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(300), $"took {stopwatch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void GetLatest_UnreachablePath_ReturnsCachedResultAndRecoversInBackground()
    {
        using var dir = new TempDir();
        var missing = dir.Combine("filer01", "files");
        var now = Now;
        var repository = Repository(() => now);
        var config = new QuickAttachConfig { FolderPath = missing };
        Assert.True(repository.GetLatest(config).Unreachable);

        Directory.CreateDirectory(missing);
        File.WriteAllText(Path.Combine(missing, "report.pdf"), "x");
        File.SetLastWriteTimeUtc(Path.Combine(missing, "report.pdf"), Now.AddMinutes(-1));
        now = Now.AddSeconds(10);

        // The first call returns the cached result and starts the background probe.
        Assert.True(repository.GetLatest(config).Unreachable);
        Assert.True(
            SpinWait.SpinUntil(() => !repository.GetLatest(config).Unreachable, TimeSpan.FromSeconds(5)),
            "background probe did not recover");
        Assert.Equal(["report.pdf"], Names(repository.GetLatest(config)));
    }

    [Fact]
    public void GetLatest_UnreachablePath_LooksUpAgainAfterWindow()
    {
        using var dir = new TempDir();
        var missing = dir.Combine("filer01", "files");
        var now = Now;
        var repository = Repository(() => now);
        var config = new QuickAttachConfig { FolderPath = missing };
        Assert.True(repository.GetLatest(config).Unreachable);

        Directory.CreateDirectory(missing);
        File.WriteAllText(Path.Combine(missing, "report.pdf"), "x");
        File.SetLastWriteTimeUtc(Path.Combine(missing, "report.pdf"), Now.AddMinutes(-1));
        now = Now.AddSeconds(31);

        Assert.Equal(["report.pdf"], Names(repository.GetLatest(config)));
    }

    [Fact]
    public void GetLatest_OneOfSeveralPathsUnreachable_ReturnsReachableItems()
    {
        using var dir = new TempDir();
        dir.WriteFile("report.pdf", Now.AddMinutes(-1));

        var result = Repository().GetLatest(new QuickAttachConfig
        {
            FolderPath = dir.Combine("missing"),
            AdditionalPaths = [dir.Path],
        });

        Assert.False(result.Unreachable);
        Assert.Equal(["report.pdf"], Names(result));
    }

    [Fact]
    public void GetLatest_NoPathConfigured_ReturnsEmptyAndReachable()
    {
        var result = Repository().GetLatest(new QuickAttachConfig());

        Assert.False(result.Unreachable);
        Assert.Empty(result.Items);
    }

    [Fact]
    public void GetLatest_SkipsShortcutsRegardlessOfFilter()
    {
        using var dir = new TempDir();
        dir.WriteFile("report.pdf", Now.AddMinutes(-1));
        foreach (var name in new[] { "a.lnk", "b.URL", "c.scf", "d.library-ms", "e.searchConnector-ms" })
        {
            dir.WriteFile(name, Now.AddMinutes(-1));
        }

        var result = Repository().GetLatest(new QuickAttachConfig { FolderPath = dir.Path, FileFilter = ["*"] });

        Assert.Equal(["report.pdf"], Names(result));
    }

    [Fact]
    public void GetLatest_KeepsFilesDatedInTheFuture()
    {
        using var dir = new TempDir();
        dir.WriteFile("future.pdf", Now.AddMinutes(5));

        var result = Repository().GetLatest(new QuickAttachConfig { FolderPath = dir.Path, MaxAgeDays = 7 });

        Assert.Equal(["future.pdf"], Names(result));
    }

    [Fact]
    public void GetLatest_TimeoutAfterReachableResult_KeepsThatResult()
    {
        using var dir = new TempDir();
        dir.WriteFile("report.pdf", Now.AddMinutes(-1));
        var now = Now;
        // Every foreground lookup times out, so only the background probe can find files.
        var repository = Repository(() => now, TimeSpan.Zero);
        var config = new QuickAttachConfig { FolderPath = dir.Path };
        repository.GetLatest(config);
        now = Now.AddSeconds(1);
        Assert.True(
            SpinWait.SpinUntil(() => !repository.GetLatest(config).Unreachable, TimeSpan.FromSeconds(5)),
            "background probe did not recover");

        now = Now.AddSeconds(10);
        var result = repository.GetLatest(config);

        Assert.False(result.Unreachable);
        Assert.Equal(["report.pdf"], Names(result));
    }
}
