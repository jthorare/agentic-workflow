using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.UiDriver.Core;
using Aw.UiDriver.Core.Protocol;

namespace Aw.UiDriver.Avalonia;

/// <summary>
/// The loopback HTTP server: POST /v1/&lt;verb&gt; with a JSON body answers JSON (a screenshot answers image/png), GET /v1/ping answers {"ok":true}. Every call
/// carries the run's secret in X-Ui-Driver-Token. Calls are served one at a time, so the UI sees one verb after another. Bound to 127.0.0.1 on a free port.
/// </summary>
internal sealed class DriverServer : IAsyncDisposable
{
    private const int Attempts = 5;

    private readonly HttpListener _listener;
    private readonly DriverHost _host;
    private readonly byte[] _secret;
    private readonly CancellationTokenSource _stop = new();
    private Task _loop = Task.CompletedTask;

    private DriverServer(HttpListener listener, string origin, DriverHost host, string secret)
    {
        _listener = listener;
        _host = host;
        Origin = origin;
        _secret = Encoding.UTF8.GetBytes(secret);
    }

    internal string Origin { get; }

    /// <summary>Starts listening on a free loopback port.</summary>
    /// <param name="host">The verbs.</param>
    /// <param name="secret">The run's secret.</param>
    /// <returns>The running server, or null when no port could be bound.</returns>
    internal static DriverServer? Start(DriverHost host, string secret)
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var port = FreePort();
            var listener = new HttpListener();
            listener.Prefixes.Add(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/"));
            try
            {
                listener.Start();
            }
            catch (HttpListenerException)
            {
                listener.Close();
                continue;
            }

            var server = new DriverServer(listener, string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}"), host, secret);
            server._loop = Task.Run(server.ServeAsync, server._stop.Token);
            return server;
        }

        return null;
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        _listener.Close();
        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch (Exception e) when (e is HttpListenerException or ObjectDisposedException or OperationCanceledException)
        {
            // Closing the listener ends the pending accept.
        }

        _stop.Dispose();
    }

    internal static Reply Failure(Error error) =>
        new(
            error.Kind switch
            {
                ErrorKind.Invalid => 400,
                ErrorKind.Forbidden => 401,
                ErrorKind.NotFound => 404,
                ErrorKind.Timeout => 408,
                ErrorKind.Conflict or ErrorKind.RateLimited or ErrorKind.BudgetExceeded or ErrorKind.Unavailable => 500,
                _ => 500,
            },
            "application/json",
            AwJson.Serialize(new DriverFailure(error.Code, error.Message), UiDriverJson.Options));

    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    private async Task ServeAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception e) when (e is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            try
            {
                await AnswerAsync(context).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // why: nothing a request does may end the accept loop; a failure is logged and the next call is served
            catch (Exception e) when (e is not OperationCanceledException)
#pragma warning restore CA1031
            {
                Console.Error.WriteLine("aw-ui-driver: a call failed after its answer: " + e.Message.ReplaceLineEndings(" "));
            }
        }
    }

    private async Task AnswerAsync(HttpListenerContext context)
    {
        try
        {
            var reply = await AnswerOrFailureAsync(context.Request).ConfigureAwait(false);
            context.Response.StatusCode = reply.Status;
            context.Response.ContentType = reply.ContentType;
            context.Response.ContentLength64 = reply.Body.Length;
            await context.Response.OutputStream.WriteAsync(reply.Body, _stop.Token).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or HttpListenerException or OperationCanceledException or ObjectDisposedException)
        {
            // The caller went away mid-answer.
        }
        finally
        {
            context.Response.Close();
        }
    }

    // A verb that throws (an exception on the UI thread, an answer that won't serialize) is AW-UIDRIVER-007 for that call, never the end of the server.
    private async Task<Reply> AnswerOrFailureAsync(HttpListenerRequest request)
    {
        try
        {
            return await RouteAsync(request).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // why: see above, every failure of one call becomes that call's answer
        catch (Exception e) when (e is not OperationCanceledException)
#pragma warning restore CA1031
        {
            var message = e.Message.ReplaceLineEndings(" ");
            Console.Error.WriteLine("aw-ui-driver: " + request.Url?.AbsolutePath + " failed: " + message);
            return Failure(UiDriverErrors.Failed.With(message));
        }
    }

    private async Task<Reply> RouteAsync(HttpListenerRequest request)
    {
        if (!SecretOk(request.Headers[DriverEndpoint.TokenHeader]))
        {
            return Failure(UiDriverErrors.Unauthorized.With("the token is missing or wrong"));
        }

        var path = request.Url?.AbsolutePath ?? string.Empty;
        if (request.HttpMethod == "GET" && path == "/v1/ping")
        {
            return new Reply(200, "application/json", "{\"ok\":true}"u8.ToArray());
        }

        if (request.HttpMethod != "POST" || !path.StartsWith("/v1/", StringComparison.Ordinal))
        {
            return Failure(UiDriverErrors.Invalid.With("POST /v1/<verb> with a JSON body"));
        }

        using var body = new MemoryStream();
        await request.InputStream.CopyToAsync(body, _stop.Token).ConfigureAwait(false);
        return await Verbs.RunAsync(path["/v1/".Length..], body.ToArray(), _host, _stop.Token).ConfigureAwait(false);
    }

    private bool SecretOk(string? given) => given is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), _secret);
}
