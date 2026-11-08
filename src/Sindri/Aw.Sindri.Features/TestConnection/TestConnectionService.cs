using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Features.TestConnection;

/// <summary>
/// Tests a connection: reads its secret and calls the probe URL (the connection's Config 'probeUrl', else its preset's). An unknown name is SND-CONN-001.
/// A rejected credential sets status 'error' (or 'needs-sign-in' for an oauth connection) and a reason; the answer is Ok false, not a failed call.
/// The reason is the failure's code and fixed title: a provider's refusal text is never shown, because it can repeat the token.
/// </summary>
/// <param name="store">The connections.</param>
/// <param name="secrets">Reads the tokens.</param>
/// <param name="presets">The shipped presets.</param>
/// <param name="probe">Calls the probe URL.</param>
/// <param name="time">The clock.</param>
/// <param name="logger">Where a refused test is reported (code only); null logs nothing.</param>
/// <param name="mcp">Lists an mcp server's tools (the test of an mcp connection); the host always supplies it.</param>
internal sealed partial class TestConnectionService(IConnectionStore store, IConnectionSecrets secrets, IConnectionPresetCatalog presets, IConnectionProbe probe, TimeProvider time, ILogger<TestConnectionService>? logger = null, IMcpConnectionClient? mcp = null)
    : IService<TestConnectionRequest, TestConnectionResponse>
{
    private readonly IConnectionStore _store = store;
    private readonly IConnectionSecrets _secrets = secrets;
    private readonly IConnectionPresetCatalog _presets = presets;
    private readonly IConnectionProbe _probe = probe;
    private readonly TimeProvider _time = time;
    private readonly ILogger? _logger = logger;
    private readonly IMcpConnectionClient? _mcp = mcp;

    public async ValueTask<Result<TestConnectionResponse>> Handle(TestConnectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = ConnectionName.Parse(request.Name);
        if (name.IsError)
        {
            return name.Error;
        }

        var found = await _store.Get(name.Value, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return ConnectionFailures.Safe(found.Error);
        }

        return found.Value.Kind == ConnectionKind.Mcp
            ? await TestMcp(found.Value, cancellationToken).ConfigureAwait(false)
            : await TestKeyed(found.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<TestConnectionResponse>> TestKeyed(Connection connection, CancellationToken cancellationToken)
    {
        if (connection.SecretPointer is null)
        {
            return await Record(connection, ConnectionStatus.NeedsSignIn, "no secret is stored yet (the sign-in did not finish)", cancellationToken).ConfigureAwait(false);
        }

        var tokens = _secrets.Read(connection.SecretPointer);
        if (tokens.IsError)
        {
            return await Failed(connection, tokens.Error, cancellationToken).ConfigureAwait(false);
        }

        var url = connection.Config.GetValueOrDefault("probeUrl") ?? (connection.Preset is { } key ? _presets.Find(key)?.ProbeUrl : null);
        if (url is null)
        {
            return await Record(connection, ConnectionStatus.Connected, "the stored secret was read; this connection has no probe URL to try it against", cancellationToken, ok: true).ConfigureAwait(false);
        }

        var probed = await _probe.Probe(url, tokens.Value, cancellationToken).ConfigureAwait(false);
        return probed.IsError
            ? await Failed(connection, probed.Error, cancellationToken).ConfigureAwait(false)
            : await Record(connection, ConnectionStatus.Connected, reason: null, cancellationToken, ok: true).ConfigureAwait(false);
    }

    // An mcp connection: read the secret, connect to its 'serverUrl' and list the tools (Ok true, Tools set), or Ok false with the SND code as the reason. Written in the green.
    private ValueTask<Result<TestConnectionResponse>> TestMcp(Connection connection, CancellationToken cancellationToken)
    {
        _ = (connection, cancellationToken, _mcp);
        throw new NotSupportedException();
    }

    private async ValueTask<Result<TestConnectionResponse>> Failed(Connection connection, Error error, CancellationToken cancellationToken)
    {
        if (_logger is not null)
        {
            LogRefused(_logger, connection.Name, error.Code);
        }

        var status = connection.Kind == ConnectionKind.OAuth && error.Code == SindriErrors.ConnectionNeedsSignIn.Code ? ConnectionStatus.NeedsSignIn : ConnectionStatus.Error;
        return await Record(connection, status, ConnectionFailures.Reason(error), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<TestConnectionResponse>> Record(Connection connection, ConnectionStatus status, string? reason, CancellationToken cancellationToken, bool ok = false)
    {
        var updated = connection with { Status = status, StatusReason = ok ? null : reason, UpdatedAt = _time.GetUtcNow() };
        var written = await _store.Update(updated, cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            return ConnectionFailures.Safe(written.Error);
        }

        return new TestConnectionResponse(ConnectionView.From(updated), ok, reason);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The test of connection {Name} was refused ({Code})")]
    private static partial void LogRefused(ILogger logger, ConnectionName name, string code);
}
