using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Aw.Connectors.Testing.Slack;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aw.Connectors.Testing.Server;

/// <summary>
/// Kestrel on 127.0.0.1 serving one <see cref="FakeSlackServer"/> (messages spec §8.1): a request to http://127.0.0.1:&lt;port&gt;/api/&lt;method&gt; reaches the fake
/// as https://slack.com/api/&lt;method&gt; with its query, form body, Authorization and User-Agent, and the fake's status, headers (Retry-After, x-oauth-scopes)
/// and body come back. Only loopback is bound: no configuration source is read (so ASPNETCORE_URLS, --urls and appsettings cannot add a listener), hosting URLs
/// are not preferred, and Kestrel listens on <see cref="IPAddress.Loopback"/> only. A request the fake refuses (an unknown method, a channel that is not allowed)
/// answers 500 with the fake's sentence; a scripted transport failure drops the connection. Nothing is logged, so no header reaches a log.
/// </summary>
public sealed class FakeSlackHost : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly HttpMessageInvoker _invoker;

    private FakeSlackHost(WebApplication app, FakeSlackServer server)
    {
        _app = app;
        Server = server;
        _invoker = new HttpMessageInvoker(server, disposeHandler: false);
    }

    /// <summary>The fake it serves, for seeding and scripting while it runs.</summary>
    public FakeSlackServer Server { get; }

    /// <summary>The base address, http://127.0.0.1:&lt;port&gt;/; Slack's methods are under api/.</summary>
    public Uri BaseUrl { get; private set; } = new("http://127.0.0.1/");

    /// <summary>Every address Kestrel listens on.</summary>
    public IReadOnlyList<Uri> Addresses { get; private set; } = [];

    /// <summary>Starts the host.</summary>
    /// <param name="server">The fake to serve.</param>
    /// <param name="port">The loopback port; 0 picks a free one.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>The running host.</returns>
    public static async Task<FakeSlackHost> Start(FakeSlackServer server, int port = 0, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentOutOfRangeException.ThrowIfNegative(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, IPEndPoint.MaxPort);

        // why: no appsettings reload watcher (each one is an inotify instance, #3459); Sources.Clear() then drops every source, so no
        // environment variable or argument can name another listener.
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            Args = ["--hostBuilder:reloadConfigOnChange=false"],
            EnvironmentName = Environments.Production,
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.Configuration.Sources.Clear();
        _ = builder.Configuration.AddInMemoryCollection();
        _ = builder.WebHost.PreferHostingUrls(preferHostingUrls: false);
        _ = builder.WebHost.UseKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, port));
        _ = builder.Logging.ClearProviders();

        var host = new FakeSlackHost(builder.Build(), server);
        host._app.Run(host.Serve);
        await host._app.StartAsync(cancellationToken).ConfigureAwait(false);
        host.Addresses = [.. host._app.Urls.Select(static url => new Uri(url))];
        host.BaseUrl = new Uri(host.Addresses.Single().GetLeftPart(UriPartial.Authority) + "/");
        return host;
    }

    /// <summary>Waits until the process is asked to stop (Ctrl+C or SIGTERM).</summary>
    /// <param name="cancellationToken">Stops the wait early.</param>
    /// <returns>A task that ends at shutdown.</returns>
    public Task WaitForShutdown(CancellationToken cancellationToken = default) => _app.WaitForShutdownAsync(cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync(CancellationToken.None).ConfigureAwait(false);
        await _app.DisposeAsync().ConfigureAwait(false);
        _invoker.Dispose();
    }

    private async Task Serve(HttpContext context)
    {
        using var request = await ToSlack(context.Request, context.RequestAborted).ConfigureAwait(false);
        HttpResponseMessage answer;
        try
        {
            answer = await _invoker.SendAsync(request, context.RequestAborted).ConfigureAwait(false);
        }
        catch (InvalidOperationException refused)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsync(refused.Message, context.RequestAborted).ConfigureAwait(false);
            return;
        }
        catch (HttpRequestException)
        {
            context.Abort();
            return;
        }

        using (answer)
        {
            await Write(answer, context.Response, context.RequestAborted).ConfigureAwait(false);
        }
    }

    private static async Task<HttpRequestMessage> ToSlack(HttpRequest incoming, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(new HttpMethod(incoming.Method), new Uri("https://slack.com" + incoming.Path.ToUriComponent() + incoming.QueryString.ToUriComponent()));
        if (incoming.Headers.Authorization.ToString() is { Length: > 0 } authorization)
        {
            _ = request.Headers.TryAddWithoutValidation("Authorization", authorization);
        }

        if (incoming.Headers.UserAgent.ToString() is { Length: > 0 } agent)
        {
            _ = request.Headers.TryAddWithoutValidation("User-Agent", agent);
        }

        if (HasBody(incoming))
        {
            using var reader = new StreamReader(incoming.Body, Encoding.UTF8);
            request.Content = new StringContent(await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false), Encoding.UTF8);
            // why: no Content-Type stays none, so the fake reads the body as form fields the way Slack does
            request.Content.Headers.ContentType = incoming.ContentType is { Length: > 0 } type ? MediaTypeHeaderValue.Parse(type) : null;
        }

        return request;
    }

    // A body with or without a Content-Type: a length above 0, or chunked (no length).
    private static bool HasBody(HttpRequest incoming)
        => incoming.ContentLength is > 0 || (incoming.ContentLength is null && incoming.Headers.TransferEncoding.Count > 0) || incoming.ContentType is { Length: > 0 };

    private static async Task Write(HttpResponseMessage answer, HttpResponse response, CancellationToken cancellationToken)
    {
        response.StatusCode = (int)answer.StatusCode;
        foreach (var (name, values) in answer.Headers)
        {
            response.Headers[name] = values.ToArray();
        }

        if (answer.Content is { } content)
        {
            response.ContentType = content.Headers.ContentType?.ToString();
            await content.CopyToAsync(response.Body, cancellationToken).ConfigureAwait(false);
        }
    }
}
