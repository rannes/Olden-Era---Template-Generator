using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using OldenEra.TemplateEditor.Services.AutoUpdate;
using Xunit;

namespace OldenEra.TemplateEditor.Tests.Services.AutoUpdate;

public class HttpUpdateDownloaderTests : IDisposable
{
    private readonly string _dir;

    public HttpUpdateDownloaderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "oetg-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* swallow */ }
    }

    private static UpdateInfo Info(long size = 100)
        => new(new Version(0, 8, 0),
               AssetUrl: "https://example.test/file.exe",
               AssetName: "OldenEraTemplates-v0.8.0.exe",
               AssetSize: size);

    private static HttpResponseMessage OkBytes(byte[] body)
    {
        var content = new ByteArrayContent(body);
        content.Headers.ContentLength = body.Length;
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    [Fact]
    public async Task Download_writesFileAndReportsProgress()
    {
        var bytes = new byte[200_000];
        new Random(42).NextBytes(bytes);
        var handler = new StubHttpHandler(_ => OkBytes(bytes));
        var downloader = new HttpUpdateDownloader(new HttpClient(handler));
        var reports = new List<double>();
        var progress = new SyncProgress<double>(p => reports.Add(p));

        string path = await downloader.DownloadAsync(Info(bytes.Length), _dir, progress);

        Assert.True(File.Exists(path));
        Assert.Equal(bytes.Length, new FileInfo(path).Length);
        Assert.NotEmpty(reports);
        Assert.Equal(1.0, reports[^1], 3);
        for (int i = 1; i < reports.Count; i++)
            Assert.True(reports[i] >= reports[i - 1], "progress should be monotonic");
    }

    [Fact]
    public async Task Download_renamesPartialToFinal()
    {
        var handler = new StubHttpHandler(_ => OkBytes(new byte[10]));
        var downloader = new HttpUpdateDownloader(new HttpClient(handler));

        string path = await downloader.DownloadAsync(Info(10), _dir, progress: null);

        Assert.EndsWith(".exe", path);
        Assert.False(File.Exists(path + ".partial"));
    }

    [Fact]
    public async Task Download_cleansUpPartial_onCancellation()
    {
        // Slow stream so cancellation interrupts mid-read.
        var slowStream = new SlowStream(new byte[1024 * 1024], delayPerReadMs: 50);
        var handler = new StubHttpHandler((req, ct) =>
        {
            var content = new StreamContent(slowStream);
            content.Headers.ContentLength = slowStream.Length;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        });
        var downloader = new HttpUpdateDownloader(new HttpClient(handler));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(75));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => downloader.DownloadAsync(Info(slowStream.Length), _dir, progress: null, cts.Token));

        var leftover = Directory.GetFiles(_dir);
        Assert.Empty(leftover);
    }

    [Fact]
    public async Task Download_cleansUpPartial_onHttpError()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var downloader = new HttpUpdateDownloader(new HttpClient(handler));

        await Assert.ThrowsAnyAsync<HttpRequestException>(
            () => downloader.DownloadAsync(Info(10), _dir, progress: null));

        var leftover = Directory.GetFiles(_dir);
        Assert.Empty(leftover);
    }

    private sealed class SyncProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;
        public SyncProgress(Action<T> handler) { _handler = handler; }
        public void Report(T value) => _handler(value);
    }

    private sealed class SlowStream : Stream
    {
        private readonly byte[] _data;
        private readonly int _delayMs;
        private long _pos;

        public SlowStream(byte[] data, int delayPerReadMs)
        {
            _data = data;
            _delayMs = delayPerReadMs;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _data.Length;
        public override long Position { get => _pos; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long o, SeekOrigin so) => throw new NotSupportedException();
        public override void SetLength(long v) => throw new NotSupportedException();
        public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
            => ReadAsync(buffer, offset, count, default).GetAwaiter().GetResult();

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        {
            await Task.Delay(_delayMs, ct).ConfigureAwait(false);
            int remaining = (int)Math.Min(count, _data.Length - _pos);
            if (remaining <= 0) return 0;
            int chunk = Math.Min(remaining, 4096);
            Array.Copy(_data, _pos, buffer, offset, chunk);
            _pos += chunk;
            return chunk;
        }
    }
}
