using System;
using System.IO;
using System.Text.Json;

namespace OldenEra.TemplateEditor.Services.AutoUpdate;

public sealed record AppPreferences
{
    public bool CheckForUpdatesOnStartup { get; init; } = true;
}

public interface IAppPreferencesStore
{
    AppPreferences Load();
    void Save(AppPreferences prefs);
}

public sealed class JsonAppPreferencesStore : IAppPreferencesStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _path;
    private readonly IUpdateLog? _log;

    public JsonAppPreferencesStore(string? path = null, IUpdateLog? log = null)
    {
        _path = path ?? UpdatePaths.PreferencesFile;
        _log = log;
    }

    public AppPreferences Load()
    {
        try
        {
            if (!File.Exists(_path)) return new AppPreferences();
            var text = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<AppPreferences>(text, Json) ?? new AppPreferences();
        }
        catch (Exception ex)
        {
            _log?.Warn("Failed to load preferences; using defaults.", ex);
            return new AppPreferences();
        }
    }

    public void Save(AppPreferences prefs)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var text = JsonSerializer.Serialize(prefs, Json);
            File.WriteAllText(_path, text);
        }
        catch (Exception ex)
        {
            _log?.Warn("Failed to save preferences.", ex);
        }
    }
}
