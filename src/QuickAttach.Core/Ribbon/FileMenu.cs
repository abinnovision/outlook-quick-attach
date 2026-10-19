using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using QuickAttach.Core.Files;

namespace QuickAttach.Core.Ribbon;

/// <summary>Builds the dynamicMenu content: one button per file, then the folder and settings actions.</summary>
public static class FileMenu
{
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/office/2009/07/customui";

    public static string Build(FileResult files, bool showIcons, bool showSettings)
    {
        var menu = new XElement(Ns + "menu", new XAttribute("itemSize", "large"));
        if (files.Unreachable || files.Items.Count == 0)
        {
            menu.Add(Button(files.Unreachable ? "btnUnreachable" : "btnEmpty", files.Unreachable ? Strings.FolderNotReachable : Strings.NoFilesFound, new XAttribute("enabled", "false")));
        }
        else
        {
            menu.Add(files.Items.Select((item, index) => Button(
                "btnFile" + index.ToString(CultureInfo.InvariantCulture),
                Mnemonic(item.Name),
                new XAttribute("description", Mnemonic(LabelFormatter.Describe(item))),
                new XAttribute("tag", index.ToString(CultureInfo.InvariantCulture)),
                showIcons ? new XAttribute("getImage", "GetFileImage") : null,
                new XAttribute("onAction", "OnFileClick"))));
        }

        menu.Add(new XElement(Ns + "menuSeparator", new XAttribute("id", "sepActions")));
        // Same icons as Outlook's "Browse This PC…" and File > Options.
        menu.Add(Button("btnOpenFolder", Strings.OpenFolder, new XAttribute("imageMso", "FileOpen"), new XAttribute("onAction", "OnOpenFolder")));
        if (showSettings)
        {
            menu.Add(Button("btnSettings", Strings.Settings, new XAttribute("imageMso", "ApplicationOptionsDialog"), new XAttribute("onAction", "OnSettings")));
        }

        return menu.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>Office reads "&amp;" as an access key marker; "&amp;&amp;" shows a literal ampersand.</summary>
    private static string Mnemonic(string text) => text.Replace("&", "&&");

    private static XElement Button(string id, string label, params XAttribute?[] attributes) =>
        new(Ns + "button", new XAttribute("id", id), new XAttribute("label", label), attributes);
}
