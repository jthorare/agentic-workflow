using System.Globalization;
using System.Net;
using System.Text;

namespace Aw.Sindri.Tests.Sandbox.Launch;

/// <summary>A loopback stand-in for GitHub's installation-token route.</summary>
internal sealed class FakeGitHubApi : IDisposable
{
    private readonly HttpListener _listener;
    private readonly Lock _gate = new();
    private readonly List<ApiRequest> _requests = [];
    private readonly SemaphoreSlim _arrived = new(0);
    private (int Status, string Body) _reply = (201, "{}");

    internal FakeGitHubApi()
        : this(FreePort)
    {
    }

    /// <summary>Starts the fake on the port <paramref name="pickPort"/> names.</summary>
    /// <param name="pickPort">Picks the port to listen on.</param>
    internal FakeGitHubApi(Func<int> pickPort)
    {
        // the port is free when it is picked, not when the listener starts: a parallel test can take it in between, and a failed listener can't be started again
        const int Tries = 5;
        for (var attempt = 1; ; attempt++)
        {
            var port = pickPort();
            var url = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture);
            var listener = new HttpListener();
            listener.Prefixes.Add(url + "/");
            try
            {
                listener.Start();
            }
            catch (HttpListenerException) when (attempt < Tries)
            {
                listener.Close();
                continue;
            }

            (Url, _listener) = (url, listener);
            break;
        }

        _ = Serve();
    }

    /// <summary>Gets the base URL the fake listens on.</summary>
    internal string Url { get; }

    /// <summary>Gets the requests received so far.</summary>
    internal IReadOnlyList<ApiRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>Sets the answer to every later request.</summary>
    /// <param name="status">The HTTP status.</param>
    /// <param name="body">The JSON body.</param>
    internal void Reply(int status, string body)
    {
        lock (_gate)
        {
            _reply = (status, body);
        }
    }

    /// <summary>Waits until at least <paramref name="count"/> requests arrived.</summary>
    /// <param name="count">How many.</param>
    /// <returns>A task that ends when they did, or fails after 5 s.</returns>
    internal async Task WaitForRequests(int count)
    {
        while (Requests.Count < count)
        {
            if (!await _arrived.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken))
            {
                throw new TimeoutException(string.Create(CultureInfo.InvariantCulture, $"only {Requests.Count} of {count} requests arrived"));
            }
        }
    }

    /// <summary>Gets a URL on a port nothing listens on.</summary>
    /// <returns>The URL.</returns>
    internal static string ClosedUrl() => "http://127.0.0.1:" + FreePort().ToString(CultureInfo.InvariantCulture);

    public void Dispose()
    {
        _listener.Close();
        _arrived.Dispose();
    }

    private static int FreePort()
    {
        using var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    private async Task Serve()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(CancellationToken.None);
            }
            catch (Exception e) when (e is HttpListenerException or ObjectDisposedException)
            {
                return;
            }

            using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync(CancellationToken.None);
            var headers = context.Request.Headers.AllKeys.Where(k => k is not null).ToDictionary(k => k!, k => context.Request.Headers[k]!, StringComparer.OrdinalIgnoreCase);
            (int status, string reply) answer;
            lock (_gate)
            {
                _requests.Add(new ApiRequest(context.Request.HttpMethod + " " + context.Request.Url!.AbsolutePath, headers, body));
                answer = _reply;
            }

            context.Response.StatusCode = answer.status;
            context.Response.ContentType = "application/json";
            var bytes = Encoding.UTF8.GetBytes(answer.reply);
            await context.Response.OutputStream.WriteAsync(bytes, CancellationToken.None);
            context.Response.Close();
            _ = _arrived.Release();
        }
    }

    internal sealed record ApiRequest(string Line, IReadOnlyDictionary<string, string> Headers, string Body);
}
