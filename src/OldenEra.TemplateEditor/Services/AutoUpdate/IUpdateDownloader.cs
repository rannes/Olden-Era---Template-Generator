using System;
using System.Threading;
using System.Threading.Tasks;

namespace OldenEra.TemplateEditor.Services.AutoUpdate;

public interface IUpdateDownloader
{
    /// <summary>
    /// Downloads the asset to a fresh path under <paramref name="targetDirectory"/>.
    /// Returns the absolute path of the completed file.
    /// </summary>
    Task<string> DownloadAsync(
        UpdateInfo info,
        string targetDirectory,
        IProgress<double>? progress,
        CancellationToken cancellationToken = default);
}
