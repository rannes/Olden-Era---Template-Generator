using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OldenEra.TemplateEditor.Tests.Services.AutoUpdate;

internal sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;
    public int CallCount { get; private set; }
    public HttpRequestMessage? LastRequest { get; private set; }

    public StubHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        _responder = responder;
    }

    public StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : this((req, _) => Task.FromResult(responder(req)))
    {
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest = request;
        return _responder(request, cancellationToken);
    }
}
