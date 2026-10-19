namespace ScanAttach.Core.Tests;

/// <summary>Real temporary directory, deleted on dispose.</summary>
internal sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "scanattach-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    public string WriteFile(string name, DateTime lastWriteUtc, string content = "x")
    {
        var file = Combine(name);
        File.WriteAllText(file, content);
        File.SetLastWriteTimeUtc(file, lastWriteUtc);
        return file;
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
