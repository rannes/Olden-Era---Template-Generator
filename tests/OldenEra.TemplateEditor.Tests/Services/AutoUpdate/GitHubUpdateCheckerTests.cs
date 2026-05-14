using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using OldenEra.TemplateEditor.Services.AutoUpdate;
using Xunit;

namespace OldenEra.TemplateEditor.Tests.Services.AutoUpdate;

public class GitHubUpdateCheckerTests
{
    private const string TestUrl = "https://example.test/latest";

    private static GitHubUpdateChecker MakeChecker(StubHttpHandler handler)
        => new GitHubUpdateChecker(new HttpClient(handler), TestUrl);

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    [Fact]
    public async Task ReturnsNull_whenLatestSameAsCurrent()
    {
        var handler = new StubHttpHandler(_ => Json("""
            {"tag_name":"v0.7.0","assets":[]}
        """));
        var info = await MakeChecker(handler).CheckAsync(new Version(0, 7, 0));
        Assert.Null(info);
    }

    [Fact]
    public async Task ReturnsNull_whenLatestOlderThanCurrent()
    {
        var handler = new StubHttpHandler(_ => Json("""
            {"tag_name":"v0.6.0","assets":[]}
        """));
        var info = await MakeChecker(handler).CheckAsync(new Version(0, 7, 0));
        Assert.Null(info);
    }

    [Fact]
    public async Task ReturnsInfo_whenNewerVersionWithMatchingAsset()
    {
        var handler = new StubHttpHandler(_ => Json("""
            {
              "tag_name":"v0.8.0",
              "assets":[
                {"name":"OldenEraTemplates-v0.8.0.exe",
                 "browser_download_url":"https://example.test/download.exe",
                 "size":12345}
              ]
            }
        """));
        var info = await MakeChecker(handler).CheckAsync(new Version(0, 7, 0));
        Assert.NotNull(info);
        Assert.Equal(new Version(0, 8, 0), info!.Version);
        Assert.Equal("https://example.test/download.exe", info.AssetUrl);
        Assert.Equal("OldenEraTemplates-v0.8.0.exe", info.AssetName);
        Assert.Equal(12345, info.AssetSize);
    }

    [Fact]
    public async Task ReturnsInfoWithNullAssetUrl_whenNewerVersionButNoMatchingAsset()
    {
        var handler = new StubHttpHandler(_ => Json("""
            {
              "tag_name":"v0.8.0",
              "assets":[
                {"name":"OldenEraTemplates-v0.8.0.zip",
                 "browser_download_url":"https://example.test/download.zip",
                 "size":12345}
              ]
            }
        """));
        var info = await MakeChecker(handler).CheckAsync(new Version(0, 7, 0));
        Assert.NotNull(info);
        Assert.Equal(new Version(0, 8, 0), info!.Version);
        Assert.Null(info.AssetUrl);
    }

    [Fact]
    public async Task ReturnsNull_whenHttpError()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var info = await MakeChecker(handler).CheckAsync(new Version(0, 7, 0));
        Assert.Null(info);
    }

    [Fact]
    public async Task ReturnsNull_whenTagUnparseable()
    {
        var handler = new StubHttpHandler(_ => Json("""
            {"tag_name":"not-a-version","assets":[]}
        """));
        var info = await MakeChecker(handler).CheckAsync(new Version(0, 7, 0));
        Assert.Null(info);
    }

    [Fact]
    public async Task SendsUserAgentHeader()
    {
        var handler = new StubHttpHandler(_ => Json("""{"tag_name":"v0.6.0","assets":[]}"""));
        await MakeChecker(handler).CheckAsync(new Version(0, 7, 0));
        Assert.NotNull(handler.LastRequest);
        Assert.Contains(handler.LastRequest!.Headers.UserAgent,
            p => p.Product?.Name == "OldenEraTemplates");
    }

    [Fact]
    public async Task CancellationIsHonored()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var handler = new StubHttpHandler((_, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Json("""{"tag_name":"v0.6.0","assets":[]}"""));
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => MakeChecker(handler).CheckAsync(new Version(0, 7, 0), cts.Token));
    }
}
