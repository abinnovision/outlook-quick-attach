using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using QuickAttach.Core.Files;

namespace QuickAttach.Core.Ribbon;

/// <summary>Builds the dynamicMenu content: one button per file, then the folder and settings actions.</summary>
public static class FileMenu
{
    /// <summary>Ribbon limit for the tag attribute, which holds the full path of the file.</summary>
    public const int MaxTagLength = 1024;

    private const int MaxTextLength = 1000;
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/office/2009/07/customui";

    public static string Build(FileResult files, bool showIcons)
    {
        // The tag must hold the exact path, so files whose path does not fit are left out.
        var items = files.Items.Where(i => i.FullPath.Length <= MaxTagLength && Clean(i.FullPath) == i.FullPath).ToArray();
        if (files.Unreachable || items.Length == 0)
        {
            return Build(Button(files.Unreachable ? "btnUnreachable" : "btnEmpty", files.Unreachable ? Strings.FolderNotReachable : Strings.NoFilesFound, new XAttribute("enabled", "false")));
        }

        return Build(items.Select((item, index) => Button(
            "btnFile" + index.ToString(CultureInfo.InvariantCulture),
            Mnemonic(Text(item.Name)),
            new XAttribute("description", Text(LabelFormatter.Describe(item))),
            new XAttribute("tag", item.FullPath),
            showIcons ? new XAttribute("getImage", "GetFileImage") : null,
            new XAttribute("onAction", "OnFileClick"))).ToArray());
    }

    /// <summary>Menu shown when building the file list failed.</summary>
    public static string BuildError() => Build(Button("btnError", Strings.MenuError, new XAttribute("enabled", "false")));

    private static string Build(params XElement[] entries)
    {
        var menu = new XElement(Ns + "menu", new XAttribute("itemSize", "large"), entries);
        menu.Add(new XElement(Ns + "menuSeparator", new XAttribute("id", "sepActions")));
        // Same icons as Outlook's "Browse This PC…" and File > Options.
        menu.Add(Button("btnOpenFolder", Strings.OpenFolder, new XAttribute("imageMso", "FileOpen"), new XAttribute("onAction", "OnOpenFolder")));
        menu.Add(Button("btnSettings", Strings.Settings, new XAttribute("imageMso", "ApplicationOptionsDialog"), new XAttribute("onAction", "OnSettings")));

        return menu.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>Office reads "&amp;" as an access key marker in labels; "&amp;&amp;" shows a literal ampersand.</summary>
    private static string Mnemonic(string text) => text.Replace("&", "&&");

    /// <summary>Removes characters that XML cannot hold and shortens the text to 1000 characters.</summary>
    private static string Text(string text)
    {
        var clean = Clean(text);
        if (clean.Length <= MaxTextLength)
        {
            return clean;
        }

        var length = char.IsHighSurrogate(clean[MaxTextLength - 1]) ? MaxTextLength - 1 : MaxTextLength;
        return clean.Substring(0, length);
    }

    private static string Clean(string text)
    {
        var result = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (i + 1 < text.Length && XmlConvert.IsXmlSurrogatePair(text[i + 1], text[i]))
            {
                result.Append(text[i]).Append(text[i + 1]);
                i++;
            }
            else if (XmlConvert.IsXmlChar(text[i]))
            {
                result.Append(text[i]);
            }
        }

        return result.ToString();
    }

    private static XElement Button(string id, string label, params XAttribute?[] attributes) =>
        new(Ns + "button", new XAttribute("id", id), new XAttribute("label", label), attributes);
}
