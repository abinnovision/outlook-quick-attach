using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Office.Core;
using Microsoft.Win32;
using ScanAttach.Core;
using ScanAttach.Core.Config;
using ScanAttach.Core.Ribbon;
using ScanAttach.Core.Scans;
using ScanAttach.Core.Update;
using Outlook = Microsoft.Office.Interop.Outlook;

namespace ScanAttach;

/// <summary>Ribbon XML and callbacks. Office invokes the callbacks by name on the add-in object.</summary>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDispatch)]
public class RibbonController : IRibbonExtensibility
{
    private const string AddinProgId = "Abi.ScanAttach";
    private const string UnreachableLabel = "Scan-Ordner nicht erreichbar";
    private const string BlockedLabel = "Bitte Update installieren";
    private const string UpdateHint = "Update wird beim nächsten Anmelden installiert";
    private static readonly TimeSpan ConfigCacheWindow = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CentralRetryWindow = TimeSpan.FromSeconds(60);
    private static readonly Version InstalledVersion = typeof(RibbonController).Assembly.GetName().Version;

    private readonly List<IRibbonUI> _ribbons = new();
    private readonly ScanRepository _repository = new();
    private SynchronizationContext? _ui;
    private ConfigWatcher? _watcher;
    private CentralConfig _central = CentralConfig.Empty;
    private ScanAttachConfig _config = new();
    private DateTime _loadedAt = DateTime.MinValue;
    private DateTime _centralLoadedAt = DateTime.MinValue;
    private bool _centralFailed;
    private ScanResult _scans = new(new ScanItem[0], unreachable: false);
    private volatile bool _updateAvailable;

    protected void Start(SynchronizationContext ui)
    {
        _ui = ui;
        _watcher = new ConfigWatcher(() => ui.Post(_ => Invalidate(), null));
        Task.Run(() =>
        {
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

    public string GetGroupLabel(IRibbonControl control) => Guard("GetGroupLabel", () => Current().GroupLabel, "Scans");

    public string GetGalleryLabel(IRibbonControl control) =>
        Guard("GetGalleryLabel", () => IsBlocked() ? BlockedLabel : Current().GalleryLabel, "Letzte Scans");

    public string GetGalleryScreentip(IRibbonControl control) => Guard(
        "GetGalleryScreentip",
        () => Current().GalleryScreentip + (_updateAvailable ? Environment.NewLine + UpdateHint : ""),
        "");

    public bool GetGalleryEnabled(IRibbonControl control) => Guard("GetGalleryEnabled", () => !IsBlocked(), true);

    public int GetItemCount(IRibbonControl control)
    {
        return Guard("GetItemCount", () =>
        {
            var config = Current();
            if (IsBlocked())
            {
                return 0;
            }

            _scans = _repository.GetLatest(config);
            return _scans.Unreachable ? 1 : _scans.Items.Count;
        }, 0);
    }

    public string GetItemLabel(IRibbonControl control, int index) =>
        Guard("GetItemLabel", () => _scans.Unreachable ? UnreachableLabel : LabelFormatter.Format(_scans.Items[index]), "");

    public string GetItemScreentip(IRibbonControl control, int index) =>
        Guard("GetItemScreentip", () => _scans.Unreachable ? _config.ScanPath : _scans.Items[index].FullPath, "");

    public object? GetItemImage(IRibbonControl control, int index) => Guard(
        "GetItemImage",
        () => _scans.Unreachable || !_config.ShowThumbnails ? null : ThumbnailProvider.Get(_scans.Items[index].FullPath, 64),
        null);

    public void OnScanSelected(IRibbonControl control, string selectedId, int selectedIndex)
    {
        Guard("OnScanSelected", () =>
        {
            if (_scans.Unreachable || selectedIndex < 0 || selectedIndex >= _scans.Items.Count)
            {
                return;
            }

            var mail = control.Context switch
            {
                Outlook.Inspector inspector => inspector.CurrentItem as Outlook.MailItem,
                Outlook.Explorer explorer => explorer.ActiveInlineResponse as Outlook.MailItem,
                _ => null,
            };
            mail?.Attachments.Add(_scans.Items[selectedIndex].FullPath);
        });
    }

    public void OnOpenFolder(IRibbonControl control)
    {
        Guard("OnOpenFolder", () =>
        {
            var path = Current().ScanPath;
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

    public bool GetSettingsVisible(IRibbonControl control) => Guard("GetSettingsVisible", () => Current().AllowUserOverride, false);

    private ScanAttachConfig Current()
    {
        // Several callbacks fire per refresh; reuse the loaded config briefly. After a failed
        // central read, keep the last result for longer so the UI thread does not wait on every refresh.
        var now = DateTime.UtcNow;
        if (now - _loadedAt > ConfigCacheWindow)
        {
            if (now - _centralLoadedAt > (_centralFailed ? CentralRetryWindow : ConfigCacheWindow))
            {
                var central = ConfigLoader.LoadCentral();
                _centralFailed = central.ReadFailed;
                if (!central.ReadFailed || central.FromCache)
                {
                    // Without file and cache, the last good config stays in use.
                    _central = central;
                }

                _centralLoadedAt = now;
            }

            _config = ConfigLoader.Resolve(_central);
            _loadedAt = now;
        }

        return _config;
    }

    private bool IsBlocked()
    {
        Current();
        return VersionPolicy.IsBlocked(InstalledVersion, _central.MinVersion);
    }

    private void Invalidate()
    {
        _loadedAt = DateTime.MinValue;
        _centralLoadedAt = DateTime.MinValue;
        foreach (var ribbon in _ribbons)
        {
            ribbon.Invalidate();
        }
    }

    private void CheckForUpdate()
    {
        var msiPath = ConfigLoader.LoadCentral().MsiPath;
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

    private static string ReadRibbon(string name)
    {
        using var stream = typeof(RibbonController).Assembly.GetManifestResourceStream("ScanAttach.Ribbon." + name + ".xml")!;
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
