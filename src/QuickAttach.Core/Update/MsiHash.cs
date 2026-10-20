using System;
using System.IO;
using System.Security.Cryptography;

namespace QuickAttach.Core.Update;

/// <summary>SHA-256 pin for the update MSI (update.MsiSha256).</summary>
public static class MsiHash
{
    /// <summary>Returns the SHA-256 of the file as lowercase hex.</summary>
    public static string Compute(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
    }

    /// <summary>True when a hash is pinned and equals the actual one (case-insensitive). A missing pin never allows.</summary>
    public static bool IsAllowed(string? expected, string actual) =>
        !string.IsNullOrWhiteSpace(expected) && string.Equals(expected!.Trim(), actual, StringComparison.OrdinalIgnoreCase);
}
