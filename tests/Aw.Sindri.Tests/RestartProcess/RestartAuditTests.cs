using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.RestartProcess;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Commands.Chain;
using Microsoft.Extensions.DependencyInjection;

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

    // #4024: the early accepted row holds 'command.handled:<commandId>'; the chain's failed row for the same command must not be dropped as its replay
    [Fact, Trait("Lane", "unit")]
    public async Task A_restart_whose_stop_fails_with_the_grant_held_records_failed_after_accepted()
    {
        await using var c = new ChainHarness(serveStop: static sp => ActivatorUtilities.CreateInstance<HeldGrantFailingStop>(sp));
        _ = await c.H.HoldLease();

        var restarted = await c.Run<RestartProcessRequest, RestartProcessResponse>(new RestartProcessRequest(), Human);

        Assert.Equal(HeldGrantFailingStop.Failure.Code, restarted.Error.Code);
        var rows = c.Handled.Where(static h => string.Equals(h.Command.Value, "RestartProcess", StringComparison.Ordinal)).ToList();
        CommandOutcome[] expected = [CommandOutcome.Accepted, CommandOutcome.Failed];
        Assert.Equal(expected, rows.Select(static h => h.Outcome));
        Assert.Equal(rows[0].CommandId, rows[1].CommandId);   // one command id, the failed row last
        Assert.Equal(HeldGrantFailingStop.Failure.Code, rows[1].Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_replay_of_the_same_failed_restart_writes_no_second_failed_row()
    {
        await using var c = new ChainHarness(serveStop: static sp => ActivatorUtilities.CreateInstance<HeldGrantFailingStop>(sp));
        _ = await c.H.HoldLease();
        var caller = Human;   // the same command id both times: the second attempt is a replay

        _ = await c.Run<RestartProcessRequest, RestartProcessResponse>(new RestartProcessRequest(), caller);
        _ = await c.Run<RestartProcessRequest, RestartProcessResponse>(new RestartProcessRequest(), caller);

        var rows = c.Handled.Where(static h => string.Equals(h.Command.Value, "RestartProcess", StringComparison.Ordinal)).Select(static h => h.Outcome);
        CommandOutcome[] expected = [CommandOutcome.Accepted, CommandOutcome.Failed];
        Assert.Equal(expected, rows);
    }
}
