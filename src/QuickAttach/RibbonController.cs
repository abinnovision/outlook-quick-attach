using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
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
    private FileResult _files = new(new FileItem[0], unreachable: false);
    private volatile bool _updateAvailable;

    protected void Start(SynchronizationContext ui)
    {
        _ui = ui;
        // Start from the local cache; the share is read in the background.
        _central = ConfigLoader.LoadCachedCentral();
        _centralCheckedAt = DateTime.UtcNow;
        _centralRefreshing = 1;
        _watcher = new ConfigWatcher(() => ui.Post(
            _ =>
            {
                _centralCheckedAt = DateTime.MinValue;
                Invalidate();
            },
            null));
        Task.Run(() =>
        {
            Guard("RefreshCentral", RefreshCentral);
            Guard("Startup", () =>
            {
                KeepAddinEnabled();
                CheckForUpdate();
            });
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
        () => Current().GalleryScreentip + (_updateAvailable ? Environment.NewLine + Strings.UpdateAtNextSignIn : ""),
        "");

    public bool GetGalleryEnabled(IRibbonControl control) => Guard("GetGalleryEnabled", () => !IsBlocked(), true);

    public string GetMenuContent(IRibbonControl control)
    {
        return Guard("GetMenuContent", () =>
        {
            var config = Current();
            _files = IsBlocked() ? new FileResult(new FileItem[0], unreachable: false) : _repository.GetLatest(config);
            return FileMenu.Build(_files, config.ShowThumbnails, config.AllowUserOverride);
        }, FileMenu.Build(new FileResult(new FileItem[0], unreachable: true), showIcons: false, showSettings: false));
    }

    public object? GetFileImage(IRibbonControl control) =>
        Guard("GetFileImage", () => FileIconProvider.Get(_files.Items[int.Parse(control.Tag, CultureInfo.InvariantCulture)].FullPath), null);

    public void OnFileClick(IRibbonControl control)
    {
        Guard("OnFileClick", () =>
        {
            var index = int.Parse(control.Tag, CultureInfo.InvariantCulture);
            if (index < 0 || index >= _files.Items.Count)
            {
                return;
            }

            var file = _files.Items[index];
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
                    attachment = attachments.Add(file.FullPath);
                }
                catch (Exception e)
                {
                    Log.Error("OnFileClick", "Could not attach " + file.FullPath, e);
                    MessageBox.Show(Strings.AttachFailed(file.Name), Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            var path = Current().FolderPath;
            if (path.Length > 0)
            {
                Process.Start("explorer.exe", "\"" + path + "\"");
            }
        });
    }

    public void OnSettings(IRibbonControl control)
    {
        Guard("OnSettings", () =>
        {
            var config = Current();
            using var form = new SettingsForm(config, name => ConfigLoader.IsLocked(_central, name));
            if (form.ShowDialog() == System.Windows.Forms.DialogResult.OK)
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
                    _ui?.Post(_ => Invalidate(), null);
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

    private void Invalidate()
    {
        _loadedAt = DateTime.MinValue;
        foreach (var ribbon in _ribbons)
        {
            ribbon.Invalidate();
        }
    }

    private void CheckForUpdate()
    {
        var msiPath = _central.MsiPath;
        if (string.IsNullOrWhiteSpace(msiPath))
        {
            return;
        }

        var msiVersion = MsiPackage.ReadProductVersion(Environment.ExpandEnvironmentVariables(msiPath));
        _updateAvailable = VersionPolicy.IsUpdateAvailable(InstalledVersion, msiVersion);
        if (_updateAvailable)
        {
            _ui?.Post(_ => Invalidate(), null);
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
}
