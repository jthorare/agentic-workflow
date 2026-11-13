using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Aw.UiDriver.Tests;

/// <summary>A stand-in driver on loopback: a driver directory with driver.json and the secret file, and a listener that records each call and answers what the test set.</summary>
internal sealed class StubDriver : IAsyncDisposable
{
    internal const string Secret = "s3cr3t-for-the-stub";

    private const int StartAttempts = 5;

    private readonly HttpListener _listener;
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("aw-uidriver-stub-");
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;

    // why: afterPortChosen is the test seam for #3415, called with each chosen port (every attempt) after the probe frees it and before the listener binds it, so a test can take that port on purpose.
    internal StubDriver(Action<int>? afterPortChosen = null, Action<string>? directoryMade = null)
    {
        directoryMade?.Invoke(_directory.FullName);
        // why: the probe frees the port before the bind, so another process can take it in between; retry on address-in-use with a fresh port and listener (same loop as Aw.Sindri.Testing's LoopbackListener, which this project doesn't reference).
        var port = 0;
        for (var attempt = 1; ; attempt++)
        {
            using var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            afterPortChosen?.Invoke(port);

            _listener = new HttpListener();
            _listener.Prefixes.Add(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/"));
            try
            {
                _listener.Start();
                break;
            }
            catch (HttpListenerException exception) when (attempt < StartAttempts && IsAddressInUse(exception))
            {
                _listener.Close();
            }
            catch
            {
                _listener.Close();
                throw;
            }
        }

        Port = port;
        File.WriteAllText(Path.Combine(_directory.FullName, "driver.json"), string.Create(CultureInfo.InvariantCulture, $$"""{"schema":"aw-ui-driver/1","url":"http://127.0.0.1:{{port}}","pid":1}"""));
        File.WriteAllText(Path.Combine(_directory.FullName, "driver.token"), Secret);
        _loop = Task.Run(ServeAsync, _stop.Token);
    }

    /// <summary>The port the listener serves on.</summary>
    internal int Port { get; }

    /// <summary>The driver directory.</summary>
    internal string Folder => _directory.FullName;

    /// <summary>Each call: the path, the secret header and the body.</summary>
    internal List<(string Path, string? Secret, string Body)> Calls { get; } = [];

    /// <summary>When true, a call other than ping is read and never answered.</summary>
    internal bool Silent { get; set; }

    /// <summary>What the next calls answer: status, media type, body.</summary>
    internal (int Status, string ContentType, byte[] Body) Answer { get; set; } = (200, "application/json", "{}"u8.ToArray());

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Close();
        try
        {
            await _loop;
        }
        catch (Exception e) when (e is HttpListenerException or ObjectDisposedException or OperationCanceledException)
        {
            // The listener closing ends the pending accept.
        }

        _stop.Dispose();
        _directory.Delete(recursive: true);
    }

    private static bool IsAddressInUse(HttpListenerException exception) =>
        exception.ErrorCode is 98 or 48 or 10048 or 32 or 183
        || exception.Message.Contains("in use", StringComparison.OrdinalIgnoreCase);

    internal void Fail(int status, string code, string message) =>
        Answer = (status, "application/json", Encoding.UTF8.GetBytes($$"""{"code":"{{code}}","message":"{{message}}"}"""));

    private async Task ServeAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception e) when (e is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync(_stop.Token);
            Calls.Add((context.Request.Url!.AbsolutePath, context.Request.Headers["X-Ui-Driver-Token"], body));
            if (Silent && context.Request.Url.AbsolutePath != "/v1/ping")
            {
                await Task.Delay(Timeout.Infinite, _stop.Token);
            }

            var (status, contentType, bytes) = context.Request.Url.AbsolutePath == "/v1/ping" ? (200, "application/json", "{\"ok\":true}"u8.ToArray()) : Answer;
            context.Response.StatusCode = status;
            context.Response.ContentType = contentType;
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, _stop.Token);
            context.Response.Close();
        }
    }
}
