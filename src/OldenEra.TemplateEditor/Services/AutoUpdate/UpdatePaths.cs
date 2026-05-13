using System;
using System.IO;

namespace OldenEra.TemplateEditor.Services.AutoUpdate;

public static class UpdatePaths
{
    public const string AppFolderName = "OldenEraTemplateGenerator";

    public static string AppDataFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName);

    public static string LogFile         => Path.Combine(AppDataFolder, "update.log");
    public static string PreferencesFile => Path.Combine(AppDataFolder, "preferences.json");
    public static string DownloadFolder  => Path.Combine(AppDataFolder, "update");
}
