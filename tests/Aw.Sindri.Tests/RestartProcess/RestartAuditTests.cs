using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.RestartProcess;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Commands.Chain;

namespace Aw.Sindri.Tests.RestartProcess;

/// <summary>
/// A successful restart through its real chain (first-run spec decision 11, API spec §2.1): its stop clears the lease grant, so the restart writes its own
/// 'command.handled' row before the stop, and the chain's audit, finding no grant afterwards, neither loses a row nor logs SND-LEDGER-002. A row that
/// can't be written stops the restart before the stop: serve keeps running.
/// </summary>
public sealed class RestartAuditTests : IAsyncDisposable
{
    private readonly ChainHarness _c = new();

    private static CallerContext Human => A.Caller().AtTerminal().Client(ClientLabel.Cli).Build();

    public ValueTask DisposeAsync() => _c.DisposeAsync();

    [Fact, Trait("Lane", "unit")]
    public async Task A_successful_restart_writes_its_command_handled_row_and_logs_no_lost_row()
    {
        _ = await _c.H.HoldLease();

        var restarted = await _c.Run<RestartProcessRequest, RestartProcessResponse>(new RestartProcessRequest(), Human);

        Assert.True(restarted.IsSuccess);
        Assert.Contains(_c.Events, static e => string.Equals(e.Envelope.Type, EventTypes.ProcessStopped, StringComparison.Ordinal));   // serve really stopped
        var handled = Assert.Single(_c.Handled, static h => string.Equals(h.Command.Value, "RestartProcess", StringComparison.Ordinal));
        Assert.Equal(CommandOutcome.Accepted, handled.Outcome);
        Assert.DoesNotContain(_c.Log.Lines, static l => l.StartsWith(SindriErrors.LedgerUnavailable.Code, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_restart_whose_audit_row_cant_be_written_returns_the_error_and_leaves_serve_running()
    {
        await using var c = new ChainHarness(static inner => new HandledFailingLedger(inner));
        _ = await c.H.HoldLease();

        var restarted = await c.Run<RestartProcessRequest, RestartProcessResponse>(new RestartProcessRequest(), Human);

        Assert.True(restarted.IsError);
        Assert.Equal(SindriErrors.LedgerUnavailable.Code, restarted.Error.Code);
        Assert.DoesNotContain(c.Events, static e => string.Equals(e.Envelope.Type, EventTypes.ProcessStopped, StringComparison.Ordinal));
        Assert.False(c.H.Shutdown.Requested);   // serve was not stopped
        Assert.NotNull(c.H.Held.Current);       // and still holds its lease
    }
}
