using System.Net;
using Aw.Evidence.Core.Desktop;

namespace Aw.Evidence.Infrastructure.Desktop;

/// <summary>The running site: answers requests until disposed.</summary>
internal sealed class ServedSite : IServedSite
{
    private readonly HttpListener _listener;
    private readonly string _root;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;

    internal ServedSite(HttpListener listener, string origin, string root)
    {
        _listener = listener;
        _root = root.TrimEnd('/');
        Origin = origin;
        _loop = Task.Run(ServeAsync, _stop.Token);
    }

    /// <inheritdoc/>
    public string Origin { get; }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        _listener.Close();
        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or OperationCanceledException)
        {
            // The listener closing ends the pending accept.
        }

        _stop.Dispose();
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
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            await AnswerAsync(context).ConfigureAwait(false);
        }
    }

    private static bool IsRead(string method) => string.Equals(method, "GET", StringComparison.Ordinal) || string.Equals(method, "HEAD", StringComparison.Ordinal);

    private async Task AnswerAsync(HttpListenerContext context)
    {
        try
        {
            var method = context.Request.HttpMethod;
            var file = IsRead(method) ? FileOf(context.Request.Url?.AbsolutePath) : null;
            if (!IsRead(method))
            {
                context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            }
            else if (file is null || !File.Exists(file))
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            }
            else
            {
                var bytes = await File.ReadAllBytesAsync(file, _stop.Token).ConfigureAwait(false);
                context.Response.ContentType = ContentTypes.Of(file);
                context.Response.ContentLength64 = bytes.Length;
                if (method == "GET")
                {
                    await context.Response.OutputStream.WriteAsync(bytes, _stop.Token).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or HttpListenerException or OperationCanceledException or ObjectDisposedException)
        {
            // The browser went away mid-answer.
        }
        finally
        {
            context.Response.Close();
        }
    }

    // The file a request names, only when it stays below the folder.
    private string? FileOf(string? absolutePath)
    {
        var path = Uri.UnescapeDataString(absolutePath ?? "/").TrimStart('/');
        if (path.Contains('\\', StringComparison.Ordinal) || path.Split('/').Contains(".."))
        {
            return null;
        }

        var full = Path.GetFullPath(Path.Combine(_root, path));
        return full.StartsWith(_root + "/", StringComparison.Ordinal) ? full : null;
    }
}
