using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using ScanAttach.Core;

namespace ScanAttach;

/// <summary>
/// Signals configuration changes: polls the central config.json every 60 s (SMB change events are unreliable)
/// and waits for changes of the HKCU keys on background threads.
/// </summary>
internal sealed class ConfigWatcher : IDisposable
{
    private const int NotifyChangeName = 0x1;
    private const int NotifyChangeLastSet = 0x4;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

    private readonly Action _changed;
    private readonly Timer _timer;
    private DateTime _lastWrite;
    private volatile bool _disposed;

    public ConfigWatcher(Action changed)
    {
        _changed = changed;
        _lastWrite = ReadLastWrite();
        _timer = new Timer(_ => Poll(), null, PollInterval, PollInterval);
        foreach (var subKey in new[] { RegistrySource.RootKey, RegistrySource.PolicyKey })
        {
            new Thread(() => WatchKey(subKey)) { IsBackground = true, Name = "ScanAttach registry watcher" }.Start();
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Dispose();
    }

    private void Poll()
    {
        try
        {
            var current = ReadLastWrite();
            if (current != _lastWrite)
            {
                _lastWrite = current;
                _changed();
            }
        }
        catch (Exception e)
        {
            Log.Error("ConfigWatcher", e);
        }
    }

    private void WatchKey(string subKey)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(subKey);
            while (key != null && !_disposed)
            {
                if (RegNotifyChangeKeyValue(key.Handle, true, NotifyChangeName | NotifyChangeLastSet, IntPtr.Zero, false) != 0 || _disposed)
                {
                    return;
                }

                _changed();
            }
        }
        catch (Exception e)
        {
            Log.Error("ConfigWatcher", e);
        }
    }

    private static DateTime ReadLastWrite()
    {
        var path = RegistrySource.ReadConfigPath();
        return string.IsNullOrWhiteSpace(path) ? DateTime.MinValue : File.GetLastWriteTimeUtc(path);
    }

    [DllImport("advapi32.dll")]
    private static extern int RegNotifyChangeKeyValue(SafeRegistryHandle key, bool watchSubtree, int notifyFilter, IntPtr eventHandle, bool asynchronous);
}
