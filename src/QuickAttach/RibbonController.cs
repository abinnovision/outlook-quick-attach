using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Office.Core;
using Microsoft.Win32;
using QuickAttach.Core;
using QuickAttach.Core.Config;
using QuickAttach.Core.Files;
using QuickAttach.Core.Ribbon;
using QuickAttach.Core.Update;
using Outlook = Microsoft.Office.Interop.Outlook;

namespace QuickAttach;

/// <summary>Ribbon XML and callbacks. Office invokes the callbacks by name on the add-in object.</summary>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDispatch)]
public class RibbonController : IRibbonExtensibility
{
    private const string AddinProgId = "Abigroup.QuickAttach";
    private static readonly TimeSpan ConfigCacheWindow = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CentralRetryWindow = TimeSpan.FromSeconds(60);
    private static readonly Version InstalledVersion = typeof(RibbonController).Assembly.GetName().Version;

    private readonly List<IRibbonUI> _ribbons = new();
    private readonly FileRepository _repository = new();
    private SynchronizationContext? _ui;
    private ConfigWatcher? _watcher;
    private volatile CentralConfig _central = CentralConfig.Empty;
    private volatile bool _centralFailed;
    private int _centralRefreshing;
    private DateTime _centralCheckedAt = DateTime.MinValue;
    private QuickAttachConfig _config = new();
    private DateTime _loadedAt = DateTime.MinValue;
    private volatile bool _updateAvailable;
    private string _loggedWarnings = "";

    protected void Start(SynchronizationContext ui)
    {
        _ui = ui;
        // Start from the local cache; the share is read in the background.
        _central = ConfigLoader.LoadCachedCentral();
        _centralCheckedAt = DateTime.UtcNow;
        _centralRefreshing = 1;
        _watcher = new ConfigWatcher(() => ui.Post(
            _ => Guard("ConfigChanged", () =>
            {
                _centralCheckedAt = DateTime.MinValue;
                Invalidate();
            }),
            null));
        Task.Run(() =>
        {
            Guard("RefreshCentral", RefreshCentral);
            Guard("KeepAddinEnabled", KeepAddinEnabled);
            Guard("CheckForUpdate", CheckForUpdate);
        });
    }

    protected void Stop() => _watcher?.Dispose();

    public string? GetCustomUI(string ribbonId)
    {
        return Guard("GetCustomUI", () => ribbonId switch
        {
            // TargetTab is evaluated once here; Office keeps the ribbon XML until Outlook restarts.
            "Microsoft.Outlook.Mail.Compose" => ReadRibbon("Compose").Replace(
                "{TabIdMso}",
                string.Equals(Current().TargetTab, "Insert", StringComparison.OrdinalIgnoreCase) ? "TabInsert" : "TabNewMailMessage"),
            "Microsoft.Outlook.Explorer" => ReadRibbon("Explorer"),
            _ => null,
        }, null);
    }

    public void OnRibbonLoad(IRibbonUI ribbon) => _ribbons.Add(ribbon);

    public string GetGroupLabel(IRibbonControl control) => Guard("GetGroupLabel", () => Current().GroupLabel, "Quick Attach");

    public string GetGalleryLabel(IRibbonControl control) =>
        Guard("GetGalleryLabel", () => IsBlocked() ? Strings.InstallUpdate : Current().GalleryLabel, "Recent Files");

    public string GetGalleryScreentip(IRibbonControl control) => Guard(
        "GetGalleryScreentip",
        () =>
        {
            var config = Current();
            return config.GalleryScreentip
                + (_updateAvailable ? Environment.NewLine + Strings.UpdateAtNextSignIn : "")
                + (config.Warnings.Count > 0 ? Environment.NewLine + Strings.ConfigProblem + ": " + Log.FilePath : "");
        },
        "");

    public bool GetGalleryEnabled(IRibbonControl control) => Guard("GetGalleryEnabled", () => !IsBlocked(), true);

    public string GetMenuContent(IRibbonControl control)
    {
        return Guard("GetMenuContent", () =>
        {
            var config = Current();
            var files = IsBlocked() ? new FileResult(new FileItem[0], unreachable: false) : _repository.GetLatest(config);
            return FileMenu.Build(files, config.ShowThumbnails);
        }, FileMenu.BuildError());
    }

    /// <summary>The tag holds the full path of the file.</summary>
    public object? GetFileImage(IRibbonControl control) => Guard("GetFileImage", () => FileIconProvider.Get(control.Tag), null);

    /// <summary>Attaches the file whose full path is in the tag, so a list changed in another window cannot pick a different file.</summary>
    public void OnFileClick(IRibbonControl control)
    {
        Guard("OnFileClick", () =>
        {
            var path = control.Tag;
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            object? item = null;
            Outlook.Attachments? attachments = null;
            Outlook.Attachment? attachment = null;
            try
            {
                item = control.Context switch
                {
                    Outlook.Inspector inspector => inspector.CurrentItem,
                    Outlook.Explorer explorer => explorer.ActiveInlineResponse,
                    _ => null,
                };
                if (item is not Outlook.MailItem mail)
                {
                    return;
                }

                attachments = mail.Attachments;
                try
                {
                    attachment = attachments.Add(path);
                }
                catch (Exception e)
                {
                    Log.Error("OnFileClick", "Could not attach " + path, e);
                    MessageBox.Show(OwnerOf(control), Strings.AttachFailed(Path.GetFileName(path)), Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            finally
            {
                Release(attachment);
                Release(attachments);
                Release(item);
            }
        });
    }

    public void OnOpenFolder(IRibbonControl control)
    {
        Guard("OnOpenFolder", () =>
        {
            var config = Current();
            var paths = new[] { config.FolderPath }.Concat(config.AdditionalPaths).ToArray();
            // Directory.Exists can block on an unreachable share, so the check runs in the background.
            Task.Run(() => Guard("OnOpenFolder", () =>
            {
                // A quote would end the quoted argument early.
                var path = paths.FirstOrDefault(p => p.Length > 0 && p.IndexOf('"') < 0 && Directory.Exists(p));
                if (path != null)
                {
                    Process.Start("explorer.exe", "\"" + path + "\"")?.Dispose();
                }
            }));
        });
    }

    public void OnSettings(IRibbonControl control)
    {
        Guard("OnSettings", () =>
        {
            var config = Current();
            using var form = new SettingsForm(config, name => ConfigLoader.IsLocked(_central, name));
            if (form.ShowDialog(OwnerOf(control)) == System.Windows.Forms.DialogResult.OK)
            {
                Invalidate();
            }
        });
    }

    /// <summary>
    /// Resolves from the in-memory central config and the registry; never reads the share. When the central config
    /// is stale (longer after a failed read), one background refresh starts.
    /// </summary>
    private QuickAttachConfig Current()
    {
        var now = DateTime.UtcNow;
        if (now - _centralCheckedAt > (_centralFailed ? CentralRetryWindow : ConfigCacheWindow)
            && Interlocked.CompareExchange(ref _centralRefreshing, 1, 0) == 0)
        {
            _centralCheckedAt = now;
            Task.Run(() => Guard("RefreshCentral", RefreshCentral));
        }

        // Several callbacks fire per ribbon refresh; reuse the resolved config briefly.
        if (now - _loadedAt > ConfigCacheWindow)
        {
            _config = ConfigLoader.Resolve(_central);
            _loadedAt = now;
            LogWarnings(_config.Warnings);
        }

        return _config;
    }

    /// <summary>Reads the central config on a background thread and refreshes the ribbon when its content changed.</summary>
    private void RefreshCentral()
    {
        try
        {
            var central = ConfigLoader.LoadCentral();
            _centralFailed = central.ReadFailed;
            if (!central.ReadFailed || central.FromCache)
            {
                // Without file and cache, the last good config stays in use.
                var previous = _central;
                _central = central;
                if (!central.HasSameContent(previous))
                {
                    PostInvalidate();
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _centralRefreshing, 0);
        }
    }

    private bool IsBlocked()
    {
        Current();
        return VersionPolicy.IsBlocked(InstalledVersion, _central.MinVersion);
    }

    /// <summary>Logs the full warning list once per change.</summary>
    private void LogWarnings(IReadOnlyList<string> warnings)
    {
        var joined = string.Join(Environment.NewLine, warnings);
        if (joined != _loggedWarnings)
        {
            _loggedWarnings = joined;
            if (joined.Length > 0)
            {
                Log.Info("Config", "Configuration problems:" + Environment.NewLine + joined);
            }
        }
    }

    private void PostInvalidate() => _ui?.Post(_ => Guard("Invalidate", Invalidate), null);

    /// <summary>Refreshes every loaded ribbon; a ribbon that fails (its window closed) is dropped.</summary>
    private void Invalidate()
    {
        _loadedAt = DateTime.MinValue;
        foreach (var ribbon in _ribbons.ToArray())
        {
            try
            {
                ribbon.Invalidate();
            }
            catch (Exception e)
            {
                Log.Error("Invalidate", e);
                _ribbons.Remove(ribbon);
            }
        }
    }

    /// <summary>Shows the update hint only for an MSI that matches update.MsiSha256, so msi.dll never opens another file.</summary>
    private void CheckForUpdate()
    {
        var central = _central;
        if (string.IsNullOrWhiteSpace(central.MsiPath) || string.IsNullOrWhiteSpace(central.MsiSha256))
        {
            return;
        }

        var msiPath = Environment.ExpandEnvironmentVariables(central.MsiPath);
        if (!MsiHash.IsAllowed(central.MsiSha256, MsiHash.Compute(msiPath)))
        {
            return;
        }

        _updateAvailable = VersionPolicy.IsUpdateAvailable(InstalledVersion, MsiPackage.ReadProductVersion(msiPath));
        if (_updateAvailable)
        {
            PostInvalidate();
        }
    }

    /// <summary>The Outlook window of the ribbon, as owner for dialogs, or null.</summary>
    private static IWin32Window? OwnerOf(IRibbonControl control)
    {
        try
        {
            return control.Context is IOleWindow window ? new WindowHandle(window.GetWindow()) : null;
        }
        catch (Exception e) when (e is COMException || e is InvalidCastException)
        {
            return null;
        }
    }

    private static void KeepAddinEnabled()
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Office\16.0\Outlook\Resiliency\DoNotDisableAddinList");
        key.SetValue(AddinProgId, 1, RegistryValueKind.DWord);
    }

    private static void Release(object? comObject)
    {
        if (comObject != null && Marshal.IsComObject(comObject))
        {
            Marshal.ReleaseComObject(comObject);
        }
    }

    private static string ReadRibbon(string name)
    {
        using var stream = typeof(RibbonController).Assembly.GetManifestResourceStream("QuickAttach.Ribbon." + name + ".xml")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static T Guard<T>(string callback, Func<T> action, T fallback)
    {
        try
        {
            return action();
        }
        catch (Exception e)
        {
            Log.Error(callback, e);
            return fallback;
        }
    }

    private static void Guard(string callback, Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            Log.Error(callback, e);
        }
    }

    private sealed class WindowHandle : IWin32Window
    {
        public WindowHandle(IntPtr handle) => Handle = handle;

        public IntPtr Handle { get; }
    }

    [ComImport]
    [Guid("00000114-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IOleWindow
    {
        IntPtr GetWindow();

        void ContextSensitiveHelp(bool enterMode);
    }
}
