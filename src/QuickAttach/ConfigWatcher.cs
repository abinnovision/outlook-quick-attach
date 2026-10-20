using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using QuickAttach.Core;

namespace QuickAttach;

/// <summary>
/// Signals configuration changes: polls the central config.json every 60 s (SMB change events are unreliable)
/// and waits for changes of the HKCU keys on a background thread. The parent key is watched, and created when
/// missing, because the QuickAttach keys may not exist yet.
/// </summary>
internal sealed class ConfigWatcher : IDisposable
{
    private const int NotifyChangeName = 0x1;
    private const int NotifyChangeLastSet = 0x4;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

    private readonly Action _changed;
    private readonly Timer _timer;
    private readonly ManualResetEvent _stop = new(false);
    private DateTime? _lastWrite;
    private volatile bool _disposed;

    public ConfigWatcher(Action changed)
    {
        _changed = changed;
        // One-shot timer, re-armed after each poll so polls never overlap. The first poll runs at once
        // on the timer thread and only records the baseline.
        _timer = new Timer(_ => Poll(), null, Timeout.Infinite, Timeout.Infinite);
        _timer.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        new Thread(() => WatchKey(@"Software\abigroup")) { IsBackground = true, Name = "QuickAttach registry watcher" }.Start();
    }

    public void Dispose()
    {
        _disposed = true;
        _stop.Set();
        _timer.Dispose();
    }

    private void Poll()
    {
        try
        {
            var current = ReadLastWrite();
            var previous = _lastWrite;
            _lastWrite = current;
            if (previous != null && previous != current)
            {
                _changed();
            }
        }
        catch (Exception e)
        {
            Log.Error("ConfigWatcher", e);
        }
        finally
        {
            Rearm();
        }
    }

    private void Rearm()
    {
        try
        {
            if (!_disposed)
            {
                _timer.Change(PollInterval, Timeout.InfiniteTimeSpan);
            }
        }
        catch (ObjectDisposedException)
        {
            // Disposed between the check and the call.
        }
    }

    private void WatchKey(string subKey)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(subKey);
            using var changed = new AutoResetEvent(false);
            var handles = new WaitHandle[] { changed, _stop };
            while (!_disposed)
            {
                // Asynchronous, so Dispose can end the wait through the stop event.
                if (RegNotifyChangeKeyValue(key.Handle, true, NotifyChangeName | NotifyChangeLastSet, changed.SafeWaitHandle, true) != 0
                    || WaitHandle.WaitAny(handles) != 0)
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
    private static extern int RegNotifyChangeKeyValue(SafeRegistryHandle key, bool watchSubtree, int notifyFilter, SafeWaitHandle eventHandle, bool asynchronous);
}
