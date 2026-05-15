using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OldenEra.Generator.Services;

namespace OldenEra.TemplateEditor.Services;

/// <summary>
/// Filesystem-backed <see cref="IUserPresetStorage"/> for the WPF host. T-807.
///
/// Each preset is one file under <see cref="DirectoryPath"/>:
/// <c>%LocalAppData%/OldenEraTemplates/UserPresets/&lt;encoded&gt;.oetgs</c>.
/// The encoded filename round-trips arbitrary user names (including spaces and
/// punctuation) by hex-escaping characters that aren't filesystem-safe — this
/// keeps the on-disk layout independent of OS path rules while preserving the
/// exact display name the user typed.
///
/// Kept WPF-free on purpose: <see cref="OldenEra.TemplateEditor.Tests"/>
/// references this file by path so tests run on Mac/Linux CI.
/// </summary>
public sealed class FileSystemUserPresetStorage : IUserPresetStorage
{
    public const string Extension = ".oetgs";

    private readonly string _directory;

    public string DirectoryPath => _directory;

    public FileSystemUserPresetStorage() : this(DefaultDirectory()) { }

    public FileSystemUserPresetStorage(string directory)
    {
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
    }

    public static string DefaultDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OldenEraTemplates",
            "UserPresets");

    public Task<IReadOnlyList<string>> ListNamesAsync()
    {
        if (!Directory.Exists(_directory))
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        var names = Directory.EnumerateFiles(_directory, "*" + Extension)
            .Select(p => Path.GetFileNameWithoutExtension(p))
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(Decode)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();
        return Task.FromResult<IReadOnlyList<string>>(names);
    }

    public async Task<string?> ReadAsync(string name)
    {
        var path = PathFor(name);
        if (!File.Exists(path)) return null;
        return await File.ReadAllTextAsync(path);
    }

    public async Task WriteAsync(string name, string json)
    {
        Directory.CreateDirectory(_directory);
        var path = PathFor(name);
        await File.WriteAllTextAsync(path, json);
    }

    public Task DeleteAsync(string name)
    {
        var path = PathFor(name);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    internal string PathFor(string name) =>
        Path.Combine(_directory, Encode(name) + Extension);

    // --- name <-> filename encoding ----------------------------------------
    // Hex-escape anything not in [A-Za-z0-9 _-]. Stable, reversible, no clashes.

    internal static string Encode(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (char c in name)
        {
            if (IsSafe(c)) sb.Append(c);
            else
            {
                // UTF-8 escape for non-ASCII; ASCII reserved chars get %XX directly.
                foreach (byte b in Encoding.UTF8.GetBytes(new[] { c }))
                    sb.Append('%').Append(b.ToString("X2"));
            }
        }
        return sb.ToString();
    }

    internal static string Decode(string encoded)
    {
        var bytes = new List<byte>(encoded.Length);
        for (int i = 0; i < encoded.Length; i++)
        {
            char c = encoded[i];
            if (c == '%' && i + 2 < encoded.Length
                && byte.TryParse(encoded.AsSpan(i + 1, 2),
                                 System.Globalization.NumberStyles.HexNumber,
                                 System.Globalization.CultureInfo.InvariantCulture,
                                 out byte b))
            {
                bytes.Add(b);
                i += 2;
            }
            else
            {
                foreach (byte bb in Encoding.UTF8.GetBytes(new[] { c }))
                    bytes.Add(bb);
            }
        }
        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    private static bool IsSafe(char c) =>
        (c >= 'A' && c <= 'Z') ||
        (c >= 'a' && c <= 'z') ||
        (c >= '0' && c <= '9') ||
        c == ' ' || c == '_' || c == '-';
}
