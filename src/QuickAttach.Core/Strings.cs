using System.Globalization;

namespace QuickAttach.Core;

/// <summary>Fixed UI texts in English and German, chosen by the Windows display language.</summary>
public static class Strings
{
    public const string AppName = "Quick Attach";

    private static bool German => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de";

    public static string OpenFolder => German ? "Ordner öffnen…" : "Open folder…";

    public static string Settings => German ? "Einstellungen…" : "Settings…";

    public static string NoFilesFound => German ? "Keine Dateien gefunden" : "No files found";

    public static string FolderNotReachable => German ? "Ordner nicht erreichbar" : "Folder not reachable";

    public static string MenuError => German ? "Fehler, siehe Protokoll" : "Error, see log";

    public static string ConfigProblem => German ? "Konfigurationsproblem, siehe Protokoll" : "Configuration problem, see log";

    public static string InstallUpdate => German ? "Bitte installieren Sie das Update" : "Please install the update";

    public static string UpdateAtNextSignIn =>
        German ? "Ein Update wird bei der nächsten Anmeldung installiert" : "An update will be installed at next sign-in";

    public static string SettingsTitle => German ? "Quick Attach Einstellungen" : "Quick Attach Settings";

    public static string Folder => German ? "Ordner" : "Folder";

    public static string GroupName => German ? "Gruppenname" : "Group name";

    public static string ButtonLabel => German ? "Beschriftung" : "Button label";

    public static string NumberOfFiles => German ? "Anzahl Dateien" : "Number of files";

    public static string Cancel => German ? "Abbrechen" : "Cancel";

    public static string SettingsSaveFailed =>
        German ? "Die Einstellungen konnten nicht gespeichert werden." : "The settings could not be saved.";

    public static string AttachFailed(string fileName) => German
        ? "\"" + fileName + "\" konnte nicht angehängt werden. Die Datei wurde möglicherweise verschoben, gelöscht oder wird gerade verwendet."
        : "\"" + fileName + "\" could not be attached. The file may have been moved, deleted or be in use.";
}
