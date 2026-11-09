using System.IO.Pipes;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Toolkit.Testing;

/// <summary>A toolkit's MCP host running in this process, joined to a real MCP client over two in-memory pipes.</summary>
public sealed class ToolkitSession : IAsyncDisposable
{
    private readonly AnonymousPipeServerStream _toServer;
    private readonly AnonymousPipeClientStream _serverInput;
    private readonly AnonymousPipeServerStream _fromServer;
    private readonly AnonymousPipeClientStream _clientInput;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _serving;
    private McpClient? _client;

    private ToolkitSession(Toolkit toolkit, IReadOnlyDictionary<string, string> environment)
    {
        _toServer = new AnonymousPipeServerStream(PipeDirection.Out);
        _serverInput = new AnonymousPipeClientStream(PipeDirection.In, _toServer.ClientSafePipeHandle);
        _fromServer = new AnonymousPipeServerStream(PipeDirection.Out);
        _clientInput = new AnonymousPipeClientStream(PipeDirection.In, _fromServer.ClientSafePipeHandle);
        _serving = Task.Run(() => ToolkitHost.ServeAsync(toolkit, _serverInput, _fromServer, name => environment.GetValueOrDefault(name), _stop.Token), _stop.Token);
    }

    /// <summary>The connected client.</summary>
    public McpClient Client => _client ?? throw new InvalidOperationException("The session is not connected.");

    /// <summary>Starts the host and connects a client to it.</summary>
    /// <param name="toolkit">The toolkit to serve.</param>
    /// <param name="environment">The environment variables the toolkit sees.</param>
    /// <param name="cancellationToken">Stops the connect.</param>
    /// <returns>The running session; dispose it.</returns>
    public static async Task<ToolkitSession> StartAsync(Toolkit toolkit, IReadOnlyDictionary<string, string> environment, CancellationToken cancellationToken)
    {
        var session = new ToolkitSession(toolkit, environment);
        session._client = await McpClient.CreateAsync(new StreamClientTransport(session._toServer, session._clientInput), cancellationToken: cancellationToken).ConfigureAwait(false);
        return session;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync().ConfigureAwait(false);
        }

        await _stop.CancelAsync().ConfigureAwait(false);
        await _toServer.DisposeAsync().ConfigureAwait(false);
        try
        {
            await _serving.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The host stops by cancellation or when its input closes; both end the session.
        }

        await _serverInput.DisposeAsync().ConfigureAwait(false);
        await _fromServer.DisposeAsync().ConfigureAwait(false);
        await _clientInput.DisposeAsync().ConfigureAwait(false);
        _stop.Dispose();
    }
}
