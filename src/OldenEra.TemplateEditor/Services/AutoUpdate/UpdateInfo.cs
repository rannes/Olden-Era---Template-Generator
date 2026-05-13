namespace OldenEra.TemplateEditor.Services.AutoUpdate;

public sealed record UpdateInfo(System.Version Version, string? AssetUrl, string? AssetName, long? AssetSize);
