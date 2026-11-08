using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Events;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aw.Sindri.Tests.BridgeEvents;

/// <summary>
/// What the bridge does with an event it can't deliver (engine spec §5.4): one whose item isn't an item key, or whose
/// delivery fails for good, is logged and passed, so it can't stall every event behind it; a failure that may pass
/// (Temporal unavailable, a timeout) keeps the cursor before the event, so the next run retries it.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class EventBridgeFailureTests : IDisposable
{
    private const string Router = "service:router-stub";
    private readonly WorkflowsHarness _harness = new();
    private int _events;
    private FakeInstances? _instances;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task An_event_whose_item_is_not_an_item_key_is_skipped_and_the_cursor_moves_past_it()
    {
        BridgeRecipes.WriteTo(_harness);
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, Token);
        var sequence = await Append(sindri, "T-1\\");   // why: a visibility query treats the backslash as an escape and refuses it

        await using var scope = sindri.CreateAsyncScope();
        var bridge = scope.ServiceProvider.GetServices<IScheduledService>().Single(s => s.Identity() == EventBridge.ServiceId);
        var ran = await bridge.Run(new ScheduledRun(sindri.GetRequiredService<HeldLease>().Current!, KnownInstants.Epoch), Token);

        Assert.False(ran.IsError, ran.IsError ? ran.Error.Message : null);
        Assert.Equal(sequence, (await sindri.GetRequiredService<EventBridgeCursor>().Read(Token)).Value);
    }

    [Fact]
    public async Task A_delivery_refused_for_good_is_passed_and_the_cursor_moves()
    {
        var (bridge, sequence, cursor) = await Prepared(SindriErrors.InvalidInput.With("refused"));

        var ran = await bridge.Run(Token);

        Assert.False(ran.IsError, ran.IsError ? ran.Error.Message : null);
        Assert.Equal(sequence, (await cursor.Read(Token)).Value);
    }

    [Theory]
    [InlineData(ErrorKind.Unavailable)]
    [InlineData(ErrorKind.Timeout)]
    public async Task A_delivery_that_may_pass_keeps_the_cursor_before_the_event(ErrorKind kind)
    {
        var (bridge, _, cursor) = await Prepared(new Error("SND-TEST-001", kind, "try again"));

        var ran = await bridge.Run(Token);

        Assert.True(ran.IsError);
        Assert.Equal(0, (await cursor.Read(Token)).Value);
    }

    [Fact]
    public async Task A_run_that_signals_nothing_is_quiet_so_the_scheduler_records_no_row_after_an_ok_one()
    {
        var (bridge, _, _) = await Prepared(SindriErrors.InvalidInput.With("refused"));

        var first = await bridge.Run(Token);
        var idle = await bridge.Run(Token);

        Assert.Equal((ScheduledResult.Quiet, ScheduledResult.Quiet), (first.Value, idle.Value));
    }

    [Fact]
    public async Task A_run_that_signals_an_instance_worked()
    {
        var (bridge, _, _) = await Prepared(Unit.Value);

        var ran = await bridge.Run(Token);

        Assert.Equal(ScheduledResult.Worked, ran.Value);
    }

    [Fact]
    public async Task An_item_event_goes_to_an_instance_that_started_before_it_and_not_to_one_that_started_after_it()
    {
        var (bridge, _, _, runtime) = await Prepared([TimeSpan.FromSeconds(-2), TimeSpan.FromSeconds(2)], Unit.Value);

        _instances!.Visible = false;   // why: visibility lags the starts, so the first delivery finds no instance
        _ = await bridge.Run(Token);
        _instances.Visible = true;
        _ = await bridge.Run(Token);

        Assert.Equal(["T-47/relay/1"], runtime.Addresses);
    }

    /// <summary>A bridge over the real ledger whose live instance of 'T-47' answers a signal with the given errors (the last repeats), and its event.</summary>
    private async Task<(Bridge Bridge, long Sequence, EventBridgeCursor Cursor)> Prepared(params Result<Unit>[] answers)
    {
        var (bridge, sequence, cursor, _) = await Prepared(TimeSpan.FromSeconds(-2), answers);
        return (bridge, sequence, cursor);
    }

    /// <summary>The same, with the instance's start <paramref name="startedAt"/> after the event happened, and the runtime that counts the signals.</summary>
    private Task<(Bridge Bridge, long Sequence, EventBridgeCursor Cursor, RefusingRuntime Runtime)> Prepared(TimeSpan startedAt, params Result<Unit>[] answers) =>
        Prepared([startedAt], answers);

    /// <summary>The same, with one live instance for each start offset (the first is generation 1, the next generation 2, and so on).</summary>
    private async Task<(Bridge Bridge, long Sequence, EventBridgeCursor Cursor, RefusingRuntime Runtime)> Prepared(TimeSpan[] startedAt, params Result<Unit>[] answers)
    {
        BridgeRecipes.WriteTo(_harness);
        var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, Token);
        var sequence = await Append(sindri, "T-47");
        var now = sindri.GetRequiredService<TimeProvider>().GetUtcNow();
        var rows = _instances = new FakeInstances([.. startedAt.Select((offset, i) =>
            new InstanceRow(InstanceAddress.Root(ItemKey.From("T-47"), RecipeId.From("workflow.relay"), i + 1).Value, "routed", NeedsYou: false, now + offset))]);
        var held = new HeldItemEvents(rows, sindri.GetRequiredService<TimeProvider>());
        var cursor = sindri.GetRequiredService<EventBridgeCursor>();
        var runtime = new RefusingRuntime(answers);
        var bridge = new EventBridge(
            sindri.GetRequiredService<ILedgerStore>(), sindri.GetRequiredService<BlockCatalog>(), held, sindri.GetRequiredService<ICommandOrigins>(),
            runtime, cursor, NullLogger<EventBridge>.Instance);
        return (new Bridge(bridge, sindri.GetRequiredService<HeldLease>().Current!), sequence, cursor, runtime);
    }

    private async Task<long> Append(IServiceProvider sindri, string item)
    {
        var n = Interlocked.Increment(ref _events);
        var id = Guid.Parse(string.Create(CultureInfo.InvariantCulture, $"00000000-0000-7000-8000-{n:D12}"));
        var envelope = new EventEnvelope<string>(id, "item.routed", 1, sindri.GetRequiredService<TimeProvider>().GetUtcNow(), Router, "local", 0, id, CausationId: null, TraceId: null, """{"outcome":"startable"}""");
        var grant = sindri.GetRequiredService<HeldLease>().Current ?? throw new InvalidOperationException("the test holds no lease");
        var appended = await sindri.GetRequiredService<ILedgerStore>().Append(new LedgerEntry(envelope, "test:" + id.ToString("N"), item), grant, Token);
        Assert.False(appended.IsError, appended.IsError ? appended.Error.Message : null);
        var all = await sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 1000, Token);
        return all.Value.Last(e => e.Envelope.Id == id).Sequence;
    }

    /// <summary>A bridge and the lease it runs under.</summary>
    private sealed record Bridge(EventBridge Service, LeaseGrant Grant)
    {
        public ValueTask<Result<ScheduledResult>> Run(CancellationToken ct) => Service.Run(new ScheduledRun(Grant, KnownInstants.Epoch), ct);
    }

    private sealed class FakeInstances(params InstanceRow[] rows) : IWorkflowInstances
    {
        public ValueTask<Result<IReadOnlyList<InstanceRow>>> List(InstanceFilter filter, bool ended, CancellationToken cancellationToken) =>
            List(InstanceFilters.ToQuery(filter), ended, cancellationToken);

        public ValueTask<Result<IReadOnlyList<InstanceRow>>> List(string? query, bool ended, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<IReadOnlyList<InstanceRow>>>(Visible ? rows.ToList() : []);

        public bool Visible { get; set; } = true;
    }

    /// <summary>Answers every signal with the next of its answers, the last one for good.</summary>
    private sealed class RefusingRuntime(Result<Unit>[] answers) : IInstanceRuntime
    {
        public int Signals { get; private set; }

        public List<string> Addresses { get; } = [];

        public ValueTask<Result<Unit>> Signal(InstanceAddress address, WaitSatisfied delivered, CancellationToken cancellationToken)
        {
            Addresses.Add(address.Value);
            return ValueTask.FromResult(answers[Math.Min(Signals++, answers.Length - 1)]);
        }

        public ValueTask<Result<InstanceView>> Start(InstanceStart start, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<CommandResult>> Send(InstanceAddress address, CommandNonce nonce, Trigger command, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<CommandResult>> OpenRequest(InstanceAddress address, LaunchId launch, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
