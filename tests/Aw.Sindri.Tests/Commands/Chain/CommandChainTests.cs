using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.StopProcess;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands.Chain;

/// <summary>The command chain (API spec §2.1): every attempt that reaches it is audited once, refusals write nothing else, and commands run one at a time.</summary>
public sealed class CommandChainTests : IAsyncDisposable
{
    private readonly ChainHarness _c = new();

    public ValueTask DisposeAsync() => _c.DisposeAsync();

    private static CallerContext Human => A.Caller().AtTerminal().Client(ClientLabel.Cli).Build();

    /// <summary>A caller the table still refuses on a desk row: a service the row does not grant (a session is let through since owner decision q93).</summary>
    private static CallerContext Refused => A.Caller().Service("heartbeat").Build();

    [Fact, Trait("Lane", "unit")]
    public async Task Refused_command_writes_only_command_handled_refused()
    {
        _ = await _c.H.HoldLease();

        var refused = await _c.Run<FixtureCommand, CommandAccepted>(new FixtureCommand("accept"), Refused);

        Assert.Equal(SindriErrors.AuthRefused.Code, refused.Error.Code);
        Assert.Equal(0, _c.Latch.Started);
        var only = Assert.Single(_c.Events);
        Assert.Equal(EventTypes.CommandHandled, only.Envelope.Type);
        var handled = Assert.Single(_c.Handled);
        Assert.Equal((CommandOutcome.Refused, "SND-AUTH-002", "service:heartbeat", ActionClass.Desk), (handled.Outcome, handled.Code, handled.Caller, handled.Class));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_command_with_no_caller_is_refused_and_audited_as_none()
    {
        _ = await _c.H.HoldLease();

        var refused = await _c.Run<FixtureCommand, CommandAccepted>(new FixtureCommand("accept"), caller: null);

        Assert.Equal(SindriErrors.AuthRefused.Code, refused.Error.Code);
        Assert.Equal("none", Assert.Single(_c.Handled).Caller);
        Assert.Equal("host:sindri", Assert.Single(_c.Events).Envelope.Actor);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Invalid_command_writes_only_command_handled_invalid()
    {
        _ = await _c.H.HoldLease();

        var invalid = await _c.Run<FixtureCommand, CommandAccepted>(new FixtureCommand("invalid"), Human);

        Assert.Equal(SindriErrors.InvalidInput.Code, invalid.Error.Code);
        Assert.Equal(0, _c.Latch.Started);
        var handled = Assert.Single(_c.Handled);
        _ = Assert.Single(_c.Events);
        Assert.Equal((CommandOutcome.Invalid, "SND-CLI-003"), (handled.Outcome, handled.Code));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Accepted_command_appends_its_event_then_command_handled_accepted()
    {
        _ = await _c.H.HoldLease();
        var human = Human;

        var accepted = await _c.Run<FixtureCommand, CommandAccepted>(new FixtureCommand("accept"), human);

        Assert.True(accepted.IsSuccess);
        Assert.Equal([FixtureService.Done, EventTypes.CommandHandled], _c.Events.Select(e => e.Envelope.Type), StringComparer.Ordinal);
        Assert.All(_c.Events, e => Assert.Equal((human.Actor, human.CorrelationId.Value), (e.Envelope.Actor, e.Envelope.CorrelationId)));
        var handled = Assert.Single(_c.Handled);
        Assert.Equal(new CommandHandled(human.CommandId, Core.Ids.ActionId.From("FixtureCommand"), ActionClass.Desk, "human:cli", CommandOutcome.Accepted, Code: null), handled);
        Assert.Equal("command.handled:" + human.CommandId.Value, _c.Events[1].IdempotencyKey);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Two_commands_under_one_service_caller_write_two_audit_rows()
    {
        _ = await _c.H.HoldLease();
        using var service = _c.H.Callers.EnterService("jobs");   // one service context, as a scheduler run holds it
        var run = _c.H.Callers.Current!;
        var correlation = run.CorrelationId;

        _ = await _c.Run<FixtureCommand, CommandAccepted>(new FixtureCommand("accept"), caller: null);
        _ = await _c.Run<FixtureCommand, CommandAccepted>(new FixtureCommand("accept"), caller: null);

        Assert.Equal(2, _c.Handled.Count);   // each attempt has its own command id, so the second isn't a replay
        Assert.NotEqual(_c.Handled[0].CommandId, _c.Handled[1].CommandId);
        Assert.All(_c.Events, e => Assert.Equal(correlation.Value, e.Envelope.CorrelationId));
        Assert.Same(run, _c.H.Callers.Current);   // the run's context is back after each attempt
        Assert.DoesNotContain(_c.Handled, h => h.CommandId == run.CommandId);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Failed_command_is_audited_with_its_code()
    {
        _ = await _c.H.HoldLease();

        var failed = await _c.Run<FixtureCommand, CommandAccepted>(new FixtureCommand("fail"), Human);

        Assert.Equal(SindriErrors.ProfileHashMismatch.Code, failed.Error.Code);
        var handled = Assert.Single(_c.Handled);
        Assert.Equal((CommandOutcome.Failed, "SND-PROFILE-003"), (handled.Outcome, handled.Code));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Accepted_stop_is_self_audited_and_writes_no_command_handled()
    {
        _ = await _c.H.HoldLease();

        var stopped = await _c.Run<StopProcessRequest, StopProcessResponse>(new StopProcessRequest(StopReason.Stop), Human);

        Assert.True(stopped.IsSuccess);
        Assert.Equal(EventTypes.ProcessStopped, Assert.Single(_c.Events).Envelope.Type);
        Assert.Null(_c.H.Held.Current);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Refused_stop_is_audited()
    {
        _ = await _c.H.HoldLease();

        var refused = await _c.Run<StopProcessRequest, StopProcessResponse>(new StopProcessRequest(StopReason.Stop), Refused);

        Assert.Equal(SindriErrors.AuthRefused.Code, refused.Error.Code);
        Assert.Equal((CommandOutcome.Refused, "StopProcess"), (Assert.Single(_c.Handled).Outcome, _c.Handled[0].Command.Value));
        Assert.NotNull(_c.H.Held.Current);
        Assert.False(_c.H.Shutdown.Requested);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Service_host_stops_through_the_chain()
    {
        _ = await _c.H.HoldLease();

        var stopped = await _c.Run<StopProcessRequest, StopProcessResponse>(new StopProcessRequest(StopReason.Signal), A.Caller().Service("host").Build());

        Assert.True(stopped.IsSuccess);
        Assert.Equal("service:host", Assert.Single(_c.Events).Envelope.Actor);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Gate_times_out_after_5_seconds_with_SND_API_009()
    {
        _ = await _c.H.HoldLease();
        var first = _c.Start(new FixtureCommand("block"), Human);
        var second = _c.Start(new FixtureCommand("accept"), Human);

        _c.H.Clock.Advance(TimeSpan.FromSeconds(4.9));
        Assert.False(second.IsCompleted);
        _c.H.Clock.Advance(TimeSpan.FromSeconds(0.1));
        var busy = await second.WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, TestContext.Current.CancellationToken);
        _c.Latch.Release.SetResult();
        _ = await first.WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, TestContext.Current.CancellationToken);

        Assert.Equal(SindriErrors.CommandBusy.Code, busy.Error.Code);
        Assert.Equal(1, _c.Latch.Started);
    }

    // A stop must get through whatever holds the gate (the owner's stop met SND-API-009 behind a long apply): it skips the gate and cancels the holder.
    [Fact, Trait("Lane", "unit")]
    public async Task Stop_gets_past_a_command_holding_the_gate_and_cancels_it()
    {
        _ = await _c.H.HoldLease();
        var held = _c.Start(new FixtureCommand("block"), Human);
        await _c.Latch.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, TestContext.Current.CancellationToken);   // the holder is inside the handler

        var stopped = await _c.Run<StopProcessRequest, StopProcessResponse>(new StopProcessRequest(StopReason.Stop), Human)
            .WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, TestContext.Current.CancellationToken);
        var ended = await Task.WhenAny(held, Task.Delay(TimeSpan.FromSeconds(10), TimeProvider.System, TestContext.Current.CancellationToken));

        Assert.True(stopped.IsSuccess);
        Assert.Same(held, ended);
        Assert.Equal(SindriErrors.CommandBusy.Code, (await held).Error.Code);   // a refusal, not a handler failure (a 500)
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_refused_stop_cancels_nothing()
    {
        _ = await _c.H.HoldLease();
        var held = _c.Start(new FixtureCommand("block"), Human);
        await _c.Latch.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, TestContext.Current.CancellationToken);

        var refused = await _c.Run<StopProcessRequest, StopProcessResponse>(new StopProcessRequest(StopReason.Stop), Refused)
            .WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, TestContext.Current.CancellationToken);
        Assert.False(held.IsCompleted);
        _c.Latch.Release.SetResult();

        Assert.Equal(SindriErrors.AuthRefused.Code, refused.Error.Code);
        Assert.True((await held.WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, TestContext.Current.CancellationToken)).IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Commands_run_one_at_a_time()
    {
        _ = await _c.H.HoldLease();
        var human = Human;
        var secondId = Core.Ids.CommandId.From(new Guid(0xaa, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        var first = _c.Start(new FixtureCommand("block"), human);
        await _c.Latch.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, TestContext.Current.CancellationToken);   // the first is held inside the handler

        var second = _c.Start(new FixtureCommand("accept"), human with { CommandId = secondId });
        // why: the chain awaits before the handler (AwaitingViews), so give an ungated second the time to run past it
        _ = await Task.WhenAny(second, Task.Delay(TimeSpan.FromMilliseconds(100), TimeProvider.System, TestContext.Current.CancellationToken));
        Assert.False(second.IsCompleted);
        Assert.Equal(1, _c.Latch.Started);
        _c.Latch.Release.SetResult();
        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, TestContext.Current.CancellationToken);

        Assert.All(results, r => Assert.True(r.IsSuccess));
        Assert.Equal([human.CommandId, secondId], _c.Handled.Select(h => h.CommandId));   // the order they finished in: the second waited for the first
        Assert.Equal([FixtureService.Done, EventTypes.CommandHandled, FixtureService.Done, EventTypes.CommandHandled], _c.Events.Select(e => e.Envelope.Type),
            StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Audit_failure_after_accept_returns_the_result_and_logs_SND_LEDGER_002()
    {
        await using var c = new ChainHarness(inner => new HandledFailingLedger(inner));
        _ = await c.H.HoldLease();

        var accepted = await c.Run<FixtureCommand, CommandAccepted>(new FixtureCommand("accept"), Human);

        Assert.True(accepted.IsSuccess);
        Assert.Equal(FixtureService.Done, Assert.Single(c.Events).Envelope.Type);
        _ = Assert.Single(c.Log.Lines, l => l.StartsWith("SND-LEDGER-002 ", StringComparison.Ordinal) && l.Contains("FixtureCommand", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Audit_failure_on_a_refusal_is_SND_LEDGER_002()
    {
        var refused = await _c.Run<FixtureCommand, CommandAccepted>(new FixtureCommand("accept"), Refused);   // no lease held: no grant to write the row with

        Assert.Equal(SindriErrors.LedgerUnavailable.Code, refused.Error.Code);
        Assert.StartsWith(SindriErrors.AuthRefused.Code + " ", refused.Error.Message, StringComparison.Ordinal);   // why: the refusal's own reason comes first
        Assert.Empty(_c.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Command_span_and_counter_carry_class_caller_and_outcome()
    {
        _ = await _c.H.HoldLease();
        using var probe = new TelemetryProbe();

        _ = await _c.Run<FixtureCommand, CommandAccepted>(new FixtureCommand("fail"), Human);

        var expected = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["sindri.command.class"] = "desk",
            ["sindri.command.caller"] = "human:cli",
            ["sindri.command.outcome"] = "failed",
        };
        var span = Assert.Single(probe.Spans, s => s.DisplayName == "sindri.command FixtureCommand" && Equals(s.GetTagItem("sindri.command.caller"), "human:cli"));
        Assert.All(expected, kv => Assert.Equal(kv.Value, span.GetTagItem(kv.Key)));
        Assert.Contains(probe.Counted, m => m.Value == 1 && expected.All(kv => m.Tags.TryGetValue(kv.Key, out var v) && Equals(v, kv.Value)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Refusal_names_the_command_and_its_cli()
    {
        _ = await _c.H.HoldLease();

        var refused = await _c.Run<StopProcessRequest, StopProcessResponse>(new StopProcessRequest(StopReason.Stop), Refused);

        Assert.Equal("StopProcess is not available to this caller: sindri stop", refused.Error.Message);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_stop_keeps_its_own_reason_when_its_audit_row_can_not_be_written()
    {
        await using var c = new ChainHarness(endpoints: new UnremovableEndpointFile());
        _ = await c.H.HoldLease();

        var stopped = await c.Run<StopProcessRequest, StopProcessResponse>(new StopProcessRequest(StopReason.Stop), Human);

        Assert.Equal(SindriErrors.LedgerUnavailable.Code, stopped.Error.Code);   // API §2.1: a row that can't be written is SND-LEDGER-002
        Assert.Contains(UnremovableEndpointFile.Reason, stopped.Error.Message, StringComparison.Ordinal);   // why: the stop cleared the grant itself; its own reason is the one to show
    }
}
