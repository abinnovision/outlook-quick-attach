using System;
using System.Windows.Forms;
using Microsoft.Win32;
using ScanAttach.Core.Config;

namespace ScanAttach;

/// <summary>User settings, written to HKCU\Software\abi\ScanAttach\User. Values set by policy or central config are read-only.</summary>
internal sealed class SettingsForm : Form
{
    private readonly TextBox _scanPath = new() { Width = 320 };
    private readonly TextBox _groupLabel = new() { Width = 320 };
    private readonly TextBox _galleryLabel = new() { Width = 320 };
    private readonly NumericUpDown _maxItems = new() { Minimum = 1, Maximum = 30, Width = 80 };
    private readonly Func<string, bool> _isLocked;

    public SettingsForm(ScanAttachConfig config, Func<string, bool> isLocked)
    {
        _isLocked = isLocked;
        Text = "ScanAttach Einstellungen";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        _scanPath.Text = config.ScanPath;
        _groupLabel.Text = config.GroupLabel;
        _galleryLabel.Text = config.GalleryLabel;
        _maxItems.Value = config.MaxItems;

        var browse = new Button { Text = "…", Width = 32 };
        browse.Click += (_, _) => BrowseFolder();
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK };
        ok.Click += (_, _) => Save();
        var cancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel };
        AcceptButton = ok;
        CancelButton = cancel;

        var layout = new TableLayoutPanel { ColumnCount = 3, AutoSize = true, Padding = new Padding(10) };
        AddRow(layout, "Scan-Ordner", _scanPath, "ScanPath", browse);
        AddRow(layout, "Gruppenname", _groupLabel, "GroupLabel");
        AddRow(layout, "Beschriftung", _galleryLabel, "GalleryLabel");
        AddRow(layout, "Anzahl Einträge", _maxItems, "MaxItems");
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
        buttons.Controls.AddRange(new Control[] { cancel, ok });
        layout.Controls.Add(buttons);
        layout.SetColumnSpan(buttons, 3);
        Controls.Add(layout);
    }

    private void AddRow(TableLayoutPanel layout, string label, Control input, string name, Control? extra = null)
    {
        var locked = _isLocked(name);
        input.Enabled = !locked;
        if (extra != null)
        {
            extra.Enabled = !locked;
        }

        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left });
        layout.Controls.Add(input);
        layout.Controls.Add(extra ?? new Label());
    }

    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog { SelectedPath = _scanPath.Text };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _scanPath.Text = dialog.SelectedPath;
        }
    }

    private void Save()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegistrySource.UserKey);
        if (!_isLocked("ScanPath"))
        {
            key.SetValue("ScanPath", _scanPath.Text, RegistryValueKind.ExpandString);
        }

        if (!_isLocked("GroupLabel"))
        {
            key.SetValue("GroupLabel", _groupLabel.Text);
        }

        if (!_isLocked("GalleryLabel"))
        {
            key.SetValue("GalleryLabel", _galleryLabel.Text);
        }

        if (!_isLocked("MaxItems"))
        {
            key.SetValue("MaxItems", (int)_maxItems.Value, RegistryValueKind.DWord);
        }
    }
}
