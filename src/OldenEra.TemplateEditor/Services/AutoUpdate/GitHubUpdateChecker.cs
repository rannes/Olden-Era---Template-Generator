using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace OldenEra.TemplateEditor.Services.AutoUpdate;

public sealed class GitHubUpdateChecker : IUpdateChecker
{
    public const string DefaultLatestReleaseUrl =
        "https://api.github.com/repos/rannes/Olden-Era---Template-Generator/releases/latest";

    private readonly HttpClient _http;
    private readonly string _latestReleaseUrl;

    public GitHubUpdateChecker(HttpClient http, string? latestReleaseUrl = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _latestReleaseUrl = latestReleaseUrl ?? DefaultLatestReleaseUrl;
    }

    public async Task<UpdateInfo?> CheckAsync(Version current, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, _latestReleaseUrl);
        request.Headers.UserAgent.Clear();
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue(
            "OldenEraTemplateGenerator", current?.ToString() ?? "0"));

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var release = await JsonSerializer.DeserializeAsync<GitHubReleaseDto>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (release?.TagName == null) return null;

        var latest = UpdateAssetSelection.ParseTag(release.TagName);
        if (latest == null) return null;
        if (current != null && latest <= current) return null;

        var names = release.Assets?.Select(a => a.Name ?? "") ?? Enumerable.Empty<string>();
        var picked = UpdateAssetSelection.SelectAsset(names, latest);
        if (picked == null) return new UpdateInfo(latest, AssetUrl: null, AssetName: null, AssetSize: null);

        var asset = release.Assets!.First(a => string.Equals(a.Name, picked, StringComparison.Ordinal));
        return new UpdateInfo(latest, asset.BrowserDownloadUrl, asset.Name, asset.Size);
    }

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
        [JsonPropertyName("assets")]   public List<GitHubAssetDto>? Assets { get; set; }
    }

    private sealed class GitHubAssetDto
    {
        [JsonPropertyName("name")]                 public string? Name { get; set; }
        [JsonPropertyName("browser_download_url")] public string? BrowserDownloadUrl { get; set; }
        [JsonPropertyName("size")]                 public long? Size { get; set; }
    }
}
