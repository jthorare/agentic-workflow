using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>
/// An HttpListener on a free 127.0.0.1 port serving fixed pages by path, 302 redirects by path, and 404 otherwise, until
/// disposed.
/// </summary>
internal sealed class LocalHttpServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly IReadOnlyDictionary<string, string> _pages;
    private readonly IReadOnlyDictionary<string, string> _redirects;

    internal LocalHttpServer(IReadOnlyDictionary<string, string> pages, IReadOnlyDictionary<string, string>? redirects = null)
    {
        _pages = pages;
        _redirects = redirects ?? new Dictionary<string, string>(StringComparer.Ordinal);
        Port = FreePort();
        _listener.Prefixes.Add(Origin + "/");
        _listener.Start();
        _ = Task.Run(ServeAsync, CancellationToken.None);
    }

    internal int Port { get; }

    /// <summary>Every request path served, in arrival order.</summary>
    internal ConcurrentQueue<string> Requests { get; } = new();

    /// <summary>Every request as '&lt;method&gt; &lt;path and query&gt; &lt;body&gt;', in arrival order.</summary>
    internal ConcurrentQueue<string> Received { get; } = new();

    /// <summary>Each request's X-Probe header as 'X-Probe=&lt;value&gt;' (absent headers are skipped), in arrival order.</summary>
    internal ConcurrentQueue<string> Probes { get; } = new();

    internal string Origin => "http://127.0.0.1:" + Port.ToString(CultureInfo.InvariantCulture);

    public void Dispose() => _listener.Close();

    internal static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    /// <summary>
    /// Binds a loopback port without Listen(), so it stays ours and a connect is refused until the socket is disposed. A freed
    /// <see cref="FreePort"/> number can be taken by a parallel test in between (#2254).
    /// </summary>
    /// <param name="port">The port the socket holds.</param>
    /// <returns>The socket that holds the port; the caller disposes it.</returns>
    internal static Socket ClosedPort(out int port)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        port = ((IPEndPoint)socket.LocalEndPoint!).Port;
        return socket;
    }

    private async Task ServeAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            Respond(context);
        }
    }

    private void Respond(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath ?? "/";
        Requests.Enqueue(path);
        if (context.Request.Headers["X-Probe"] is { } probe)
        {
            Probes.Enqueue("X-Probe=" + probe);
        }

        using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
        {
            Received.Enqueue($"{context.Request.HttpMethod} {context.Request.RawUrl} {reader.ReadToEnd()}");
        }

        if (_redirects.TryGetValue(path, out var location))
        {
            context.Response.StatusCode = 302;
            context.Response.RedirectLocation = location;
            context.Response.Close();
            return;
        }

        var found = _pages.TryGetValue(path, out var html);
        var body = Encoding.UTF8.GetBytes(found ? html! : "not found");   // why: TryGetValue set it when found
        context.Response.StatusCode = found ? 200 : 404;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = body.Length;
        try
        {
            context.Response.OutputStream.Write(body);
            context.Response.Close();
        }
        catch (Exception ex) when (ex is HttpListenerException or IOException)
        {
            // The client went away (an aborted navigation); nothing to answer.
        }
    }
}
