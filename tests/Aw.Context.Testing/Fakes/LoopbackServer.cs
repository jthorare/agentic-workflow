using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Aw.Context.Testing.Fakes;

/// <summary>
/// Kestrel on 127.0.0.1 and an ephemeral port, running one handler. Framework logs go to <see cref="Log"/> only; the
/// request log records method, path, operation and status, never a header (spec §14 S-L2).
/// </summary>
internal sealed class LoopbackServer : IAsyncDisposable
{
    private static readonly JsonWriterOptions _writer = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, Indented = true };
    private readonly WebApplication _app;
    private readonly ConcurrentQueue<string> _log;
    private readonly ConcurrentQueue<RecordedRequest> _requests = new();
    private readonly Func<FakeHttpRequest, FakeHttpReply> _handler;

    private LoopbackServer(WebApplication app, ConcurrentQueue<string> log, Func<FakeHttpRequest, FakeHttpReply> handler)
    {
        _app = app;
        _log = log;
        _handler = handler;
    }

    public Uri BaseUrl { get; private set; } = new("http://127.0.0.1/");

    public IReadOnlyList<RecordedRequest> Requests => [.. _requests];

    public IReadOnlyList<string> Log => [.. _log];

    public static async Task<LoopbackServer> Start(string name, Func<FakeHttpRequest, FakeHttpReply> handler)
    {
        var log = new ConcurrentQueue<string>();
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = [], ApplicationName = name, ContentRootPath = AppContext.BaseDirectory });
        _ = builder.Logging.ClearProviders().AddProvider(new ListLoggerProvider(log)).SetMinimumLevel(LogLevel.Trace);
        _ = builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, 0));
        var server = new LoopbackServer(builder.Build(), log, handler);
        server._app.Run(server.Serve);
        await server._app.StartAsync().ConfigureAwait(false);
        server.BaseUrl = new Uri(server._app.Urls.Single().TrimEnd('/') + "/");
        return server;
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync(CancellationToken.None).ConfigureAwait(false);
        await _app.DisposeAsync().ConfigureAwait(false);
    }

    private async Task Serve(HttpContext context)
    {
        var request = await Read(context.Request).ConfigureAwait(false);
        var reply = Answer(request);
        var target = request.Path + context.Request.QueryString.Value;
        _requests.Enqueue(new RecordedRequest(request.Method, target, reply.Operation, reply.Status));
        _log.Enqueue(string.Create(CultureInfo.InvariantCulture, $"{request.Method} {target} -> {reply.Status} {reply.Operation}"));
        context.Response.StatusCode = reply.Status;
        context.Response.ContentType = "application/json; charset=utf-8";
        await using var json = new Utf8JsonWriter(context.Response.Body, _writer);
        reply.Body.WriteTo(json);
        await json.FlushAsync(context.RequestAborted).ConfigureAwait(false);
    }

    private FakeHttpReply Answer(FakeHttpRequest request)
    {
        try
        {
            return _handler(request);
        }
        catch (Exception e)
        {
            var name = e.GetType().Name;
            return new FakeHttpReply(500, new JsonObject { ["error"] = $"The fake server failed on this request ({name}): {e.Message}" }, "error: " + name);
        }
    }

    private static async Task<FakeHttpRequest> Read(HttpRequest request)
    {
        using var reader = new StreamReader(request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(request.HttpContext.RequestAborted).ConfigureAwait(false);
        var query = request.Query.ToDictionary(q => q.Key, q => q.Value.ToString(), StringComparer.Ordinal);
        var authorization = request.Headers.Authorization.Count == 0 ? null : request.Headers.Authorization.ToString();
        return new FakeHttpRequest(request.Method, request.Path.Value ?? "/", query, authorization, body);
    }
}
