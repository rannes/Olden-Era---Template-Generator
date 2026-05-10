using System.IO.Compression;
using System.Reflection;
using System.Text;
using OldenEra.Generator.Constants;

namespace OldenEra.Web.Services;

public static class InstallerPackager
{
    public static byte[] BuildPlainZip(string templateName, byte[] rmgJson, byte[] png)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, $"{templateName}.rmg.json", rmgJson);
            WriteEntry(archive, $"{templateName}.png", png);
        }
        return ms.ToArray();
    }

    public static byte[] BuildInstallerZip(string templateName, byte[] rmgJson, byte[] png)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, $"{templateName}.rmg.json", rmgJson);
            WriteEntry(archive, $"{templateName}.png", png);
            WriteEntry(archive, "install.bat", LoadResource("install.bat", templateName));
            WriteEntry(archive, "install.ps1", LoadResource("install.ps1", templateName));
            WriteEntry(archive, "README.txt", LoadResource("README.txt", templateName));
        }
        return ms.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string name, byte[] data)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var s = entry.Open();
        s.Write(data, 0, data.Length);
    }

    private static byte[] LoadResource(string fileName, string templateName)
    {
        var asm = typeof(InstallerPackager).Assembly;
        string resourceName = $"OldenEra.Web.Resources.Installer.{fileName}";
        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        string text = reader.ReadToEnd()
            .Replace("{TEMPLATE_NAME}", templateName)
            .Replace("{STEAM_APP_ID}", OldenEraSteamInfo.AppId)
            .Replace("{STEAM_FOLDER_NAME}", OldenEraSteamInfo.SteamFolderName)
            .Replace("{TEMPLATES_SUBPATH}", OldenEraSteamInfo.TemplatesSubpath);
        return Encoding.UTF8.GetBytes(text);
    }
}
