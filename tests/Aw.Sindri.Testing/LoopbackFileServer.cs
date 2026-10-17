using System.Net;
using System.Net.Sockets;

namespace Aw.Sindri.Testing;

/// <summary>
/// A small HTTP server on 127.0.0.1 serving a path-to-bytes map, so an HTTP adapter can be tested against real sockets: a missing path is a
/// 404, a path can answer with a redirect, and every answer can be delayed. Disposing stops it.
/// </summary>
public sealed class LoopbackFileServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;
    private readonly List<string> _requests = [];

    /// <summary>Initializes and starts a server on a free loopback port.</summary>
    /// <param name="files">The bytes to serve, by absolute request path such as /abc/deployment/x.</param>
    public LoopbackFileServer(IReadOnlyDictionary<string, byte[]> files)
    {
        Files = new Dictionary<string, byte[]>(files, StringComparer.Ordinal);
        var port = FreePort();
        BaseUri = new Uri($"http://127.0.0.1:{port.ToString(System.Globalization.CultureInfo.InvariantCulture)}/");
        _listener.Prefixes.Add(BaseUri.AbsoluteUri);
        _listener.Start();
        _loop = Task.Run(Serve, _stop.Token);
    }

    /// <summary>Gets the server's base address, ending in a slash.</summary>
    public Uri BaseUri { get; }

    /// <summary>Gets the served files by absolute request path; add to it before requesting.</summary>
    public IDictionary<string, byte[]> Files { get; }

    /// <summary>Gets the redirects by absolute request path to the Location they answer with (302).</summary>
    public IDictionary<string, string> Redirects { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets the status codes to answer with instead of serving the file, by absolute request path (an empty body).</summary>
    public IDictionary<string, int> Statuses { get; } = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>Gets or sets how long every answer waits before it is sent.</summary>
    public TimeSpan Delay { get; set; }

    /// <summary>Gets the request paths seen, in order.</summary>
    public IReadOnlyList<string> Requests
    {
        get
        {
            lock (_requests)
            {
                return [.. _requests];
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stop.Cancel();
        _listener.Close();
        try
        {
            _ = _loop.Wait(TimeSpan.FromSeconds(5), CancellationToken.None);
        }
        catch (AggregateException)
        {
            // the loop ends on the listener closing
        }

        _stop.Dispose();
    }

    /// <summary>Finds a loopback port nothing listens on right now.</summary>
    /// <returns>The port.</returns>
    public static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    private async Task Serve()
    {
        while (!_stop.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            _ = Task.Run(() => Answer(context), _stop.Token);
        }
    }

    private async Task Answer(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath ?? "";
        lock (_requests)
        {
            _requests.Add(path);
        }

        try
        {
            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, _stop.Token).ConfigureAwait(false);
            }

            await Reply(context.Response, path).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is OperationCanceledException or HttpListenerException or IOException or ObjectDisposedException)
        {
            // the client gave up or the server stopped
        }
        finally
        {
            context.Response.Close();
        }
    }

    private async Task Reply(HttpListenerResponse response, string path)
    {
        if (Redirects.TryGetValue(path, out var location))
        {
            response.StatusCode = 302;
            response.RedirectLocation = location;
        }
        else if (Statuses.TryGetValue(path, out var status))
        {
            response.StatusCode = status;
        }
        else if (Files.TryGetValue(path, out var bytes))
        {
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes, _stop.Token).ConfigureAwait(false);
        }
        else
        {
            response.StatusCode = 404;
        }
    }
}
