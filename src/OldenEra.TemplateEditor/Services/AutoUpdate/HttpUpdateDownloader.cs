using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OldenEra.TemplateEditor.Services.AutoUpdate;

public sealed class HttpUpdateDownloader : IUpdateDownloader
{
    private readonly HttpClient _http;

    public HttpUpdateDownloader(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    public async Task<string> DownloadAsync(
        UpdateInfo info,
        string targetDirectory,
        IProgress<double>? progress,
        CancellationToken cancellationToken = default)
    {
        if (info is null) throw new ArgumentNullException(nameof(info));
        if (string.IsNullOrWhiteSpace(info.AssetUrl))
            throw new InvalidOperationException("UpdateInfo has no asset URL.");
        if (string.IsNullOrWhiteSpace(info.AssetName))
            throw new InvalidOperationException("UpdateInfo has no asset name.");

        Directory.CreateDirectory(targetDirectory);
        string finalPath   = Path.Combine(targetDirectory, info.AssetName);
        string partialPath = finalPath + ".partial";

        if (File.Exists(partialPath)) File.Delete(partialPath);
        if (File.Exists(finalPath))   File.Delete(finalPath);

        try
        {
            using var response = await _http
                .GetAsync(info.AssetUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            long? expectedTotal = response.Content.Headers.ContentLength ?? info.AssetSize;
            await using var http = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (var output = new FileStream(partialPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true))
            {
                var buffer = new byte[64 * 1024];
                long received = 0;
                double lastReported = -1;
                int read;
                while ((read = await http.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    received += read;
                    if (progress != null && expectedTotal is > 0)
                    {
                        double frac = Math.Min(1.0, (double)received / expectedTotal.Value);
                        if (frac - lastReported >= 0.005 || frac >= 1.0)
                        {
                            progress.Report(frac);
                            lastReported = frac;
                        }
                    }
                }
                if (progress != null) progress.Report(1.0);
            }

            File.Move(partialPath, finalPath);
            return finalPath;
        }
        catch
        {
            TryDelete(partialPath);
            TryDelete(finalPath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* swallow */ }
    }
}
