using System.Globalization;
using System.Text.Json;
using Aw.Kernel.Events;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.CompleteStep;
using Aw.Sindri.Tests.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;

namespace Aw.Sindri.Tests.BridgeEvents;

/// <summary>
/// The ledger-to-Temporal bridge (engine spec rev 8 §5.4; engine P4 unit (d)) against the pinned dev server: a registered
/// ledger event from its producer becomes an 'event' signal, routed by item to every live instance of the item or by
/// causation only to the instance whose command caused it; Temporal buffers it until the instance reaches the wait; the
/// cursor keeps a restarted bridge from sending anything twice, and the workflow drops a re-sent event.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class EventBridgeTests(DevServer server) : IClassFixture<DevServer>, IDisposable
{
    private const string Router = "service:router-stub";
    private const string Dispatcher = "system:dispatcher";
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(20);
    private readonly WorkflowsHarness _harness = new();
    private int _events;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task An_item_event_from_its_producer_reaches_the_waiting_instance_and_moves_it()
    {
        await Serve(async sindri =>
        {
            var relay = await Start(sindri, "workflow.relay", "T-41", attempt: 1);
            await Visible(sindri, "T-41", 1);
            _ = await Append(sindri, "item.routed", "human:desk", "T-41", """{"outcome":"notStartable"}""");   // not the producer
            _ = await Append(sindri, "item.routed", Router, "T-41", """{"outcome":"startable"}""");

            await Bridge(sindri);

            var view = await WorkflowsHarness.Until(relay, static v => v.State != "routed");
            Assert.Equal("claimed", view.State);
            Assert.Equal(1, await Signals(relay));
            Assert.Contains(("routed", "claimed", "wait.satisfied"), await Transitions(sindri, relay.Id));
        }, TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 120_000)]
    public async Task Early_event_applies_on_entering_the_wait()
    {
        await Serve(async sindri =>
        {
            var relay = await Start(sindri, "workflow.relay", "T-42", attempt: 1);
            await Visible(sindri, "T-42", 1);
            var started = await StartedEvent(sindri, relay.Id);
            _ = await Append(sindri, "claim.created", Dispatcher, "T-42", """{"outcome":"created"}""", causation: started);
            await Bridge(sindri);
            var early = await WorkflowsHarness.Until(relay, static v => v.State == "routed");

            _ = await Append(sindri, "item.routed", Router, "T-42", """{"outcome":"startable"}""");
            await Bridge(sindri);

            var result = await Ended(relay);
            Assert.Equal("routed", early.State);
            Assert.Equal(("done", "resolved"), (result.State, result.Outcome));
            Assert.Equal([("routed", "claimed", "wait.satisfied"), ("claimed", "done", "wait.satisfied")], await Transitions(sindri, relay.Id));
        }, TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 120_000)]
    public async Task A_causation_event_reaches_only_its_causer()
    {
        await Serve(async sindri =>
        {
            var causer = await Start(sindri, "workflow.relay", "T-43", attempt: 1);
            var other = await Start(sindri, "workflow.relay", "T-43", attempt: 2);
            await Visible(sindri, "T-43", 2);
            _ = await Append(sindri, "item.routed", Router, "T-43", """{"outcome":"startable"}""");   // route item: both
            await Bridge(sindri);
            _ = await WorkflowsHarness.Until(causer, static v => v.State == "claimed");
            _ = await WorkflowsHarness.Until(other, static v => v.State == "claimed");

            _ = await Append(sindri, "claim.created", Dispatcher, "T-43", """{"outcome":"created"}""", causation: await StartedEvent(sindri, causer.Id));
            await Bridge(sindri);

            var result = await Ended(causer);
            Assert.Equal(("done", "resolved"), (result.State, result.Outcome));
            Assert.Equal("claimed", (await other.QueryAsync(static wf => wf.GetInstance())).State);
            Assert.Equal((2, 1), (await Signals(causer), await Signals(other)));
        }, TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 120_000)]
    public async Task A_restarted_bridge_resends_nothing_and_a_resent_event_applies_once()
    {
        await Serve(async sindri =>
        {
            var twice = await Start(sindri, "workflow.twice", "T-44", attempt: 1);
            await Visible(sindri, "T-44", 1);
            _ = await Append(sindri, "item.routed", Router, "T-44", """{"outcome":"startable"}""");
            await Bridge(sindri);
            _ = await WorkflowsHarness.Until(twice, static v => v.State == "second");

            await using (var restarted = _harness.Sindri())
            {
                await Bridge(restarted, leaseHolder: sindri);   // a new composition over the same home: the cursor says everything was sent
            }

            var afterRestart = await Signals(twice);
            File.Delete(Path.Combine(_harness.Home, "workflow", "events.cursor"));   // the cursor is lost: the next run starts over
            _ = await Append(sindri, "item.routed", Router, "T-44", """{"outcome":"notStartable"}""");
            await Bridge(sindri);

            var result = await Ended(twice);
            Assert.Equal(1, afterRestart);
            Assert.Equal(3, await Signals(twice));                                   // the first event again, then the new one
            Assert.Equal(("dropped", "unresolved"), (result.State, result.Outcome)); // the re-sent 'startable' was dropped
            Assert.Equal([("first", "second", "wait.satisfied"), ("second", "dropped", "wait.satisfied")], await Transitions(sindri, twice.Id));
        }, TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 120_000)]
    public async Task An_item_event_does_not_reach_an_instance_started_after_it()
    {
        await Serve(async sindri =>
        {
            _ = await Append(sindri, "item.routed", Router, "T-45", """{"outcome":"startable"}""");
            await Bridge(sindri);   // no instance of T-45 is live yet, so the cursor moves past the event

            var later = await Start(sindri, "workflow.relay", "T-45", attempt: 2);   // a later attempt
            await Visible(sindri, "T-45", 1);
            await Bridge(sindri);
            await Bridge(sindri);

            Assert.Equal("routed", (await later.QueryAsync(static wf => wf.GetInstance())).State);
            Assert.Equal(0, await Signals(later));
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Composes Sindri over the test's home with both recipes, holds the lease, names the dev server in control.json and runs the worker around <paramref name="body"/>.</summary>
    private async Task Serve(Func<ServiceProvider, Task> body, CancellationToken ct)
    {
        BridgeRecipes.WriteTo(_harness);
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        var temporal = await WorkflowsHarness.AsServe(sindri, server.Environment, ct);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var worker = sindri.GetRequiredService<ITemporalWorker>().Run(temporal, stop.Token);
        try
        {
            await body(sindri);
        }
        finally
        {
            await stop.CancelAsync();
            await worker;
        }
    }

    /// <summary>Starts <paramref name="recipe"/> on <paramref name="item"/> as the given attempt, with a 'fact:spec' input.</summary>
    private async Task<WorkflowHandle<RecipeWorkflow, RecipeResult>> Start(IServiceProvider sindri, string recipe, string item, int attempt)
    {
        var approved = await sindri.GetRequiredService<IApprovedRecipes>().Latest(RecipeId.From(recipe), Token);
        Assert.False(approved.IsError, approved.IsError ? approved.Error.Message : null);
        var spec = new FactValue("fact:sha256:" + new string('a', 64), new string('a', 64), HeadSha: null);
        var inputs = new Dictionary<FactKey, InputFact> { [FactKey.From("fact:spec")] = new InputFact("spec.md", spec) };
        var start = new InstanceStart(approved.Value.Version, ItemKey.From(item), attempt, inputs, Tenant: "local", Ring: 0);
        return await server.Environment.Client.StartWorkflowAsync(
            (RecipeWorkflow wf) => wf.RunAsync(start),
            new WorkflowOptions(id: start.WorkflowId().Value, taskQueue: TemporalSettings.DefaultTaskQueue));
    }

    /// <summary>Waits until visibility lists <paramref name="count"/> live instances of <paramref name="item"/> (it is eventually consistent).</summary>
    private static async Task Visible(IServiceProvider sindri, string item, int count)
    {
        var deadline = RealTime.NowDateTime + _patience;
        while (RealTime.NowDateTime < deadline)
        {
            var listed = await sindri.GetRequiredService<IWorkflowInstances>().List($"Item = '{item}'", ended: false, Token);
            if (!listed.IsError && listed.Value.Count == count)
            {
                return;
            }

            await Task.Delay(100, Token);
        }

        Assert.Fail(string.Create(CultureInfo.InvariantCulture, $"visibility never listed {count} instances of {item}"));
    }

    /// <summary>Appends a registered event as its producer would, with a fresh id; returns the id.</summary>
    private async Task<Guid> Append(IServiceProvider sindri, string type, string actor, string item, string payload, Guid? causation = null)
    {
        var n = Interlocked.Increment(ref _events);
        var id = Guid.Parse(string.Create(CultureInfo.InvariantCulture, $"00000000-0000-7000-8000-{n:D12}"));
        // why: stamped with the host's clock, which follows the Temporal server's (WorkflowsHarness.Sindri), so the event is fresh for 'workflow.eventHold'
        var envelope = new EventEnvelope<string>(id, type, 1, sindri.GetRequiredService<TimeProvider>().GetUtcNow(), actor, "local", 0, id, causation, TraceId: null, payload);
        var grant = sindri.GetRequiredService<HeldLease>().Current ?? throw new InvalidOperationException("the test holds no lease");
        var appended = await sindri.GetRequiredService<ILedgerStore>().Append(new LedgerEntry(envelope, "test:" + id.ToString("N"), item), grant, Token);
        Assert.False(appended.IsError, appended.IsError ? appended.Error.Message : null);
        return id;
    }

    /// <summary>Runs the bridge service of <paramref name="sindri"/> once, as the scheduler would, under the test's lease.</summary>
    private static async Task Bridge(IServiceProvider sindri, IServiceProvider? leaseHolder = null)
    {
        await using var scope = sindri.CreateAsyncScope();
        var bridge = scope.ServiceProvider.GetServices<IScheduledService>().Single(s => s.Identity() == EventBridge.ServiceId);
        var grant = (leaseHolder ?? sindri).GetRequiredService<HeldLease>().Current ?? throw new InvalidOperationException("the test holds no lease");
        var ran = await bridge.Run(new ScheduledRun(grant, KnownInstants.Epoch), Token);
        Assert.False(ran.IsError, ran.IsError ? ran.Error.Message : null);
    }

    /// <summary>The 'workflow.started' event the interpreter mirrored for <paramref name="address"/>: the command a causation route names.</summary>
    private static async Task<Guid> StartedEvent(IServiceProvider sindri, string address)
    {
        var deadline = RealTime.NowDateTime + _patience;
        while (RealTime.NowDateTime < deadline)
        {
            var read = await sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 1000, Token);
            var started = read.Value.FirstOrDefault(e => e.Envelope.Type == "workflow.started" && InstanceOf(e) == address);
            if (started is not null)
            {
                return started.Envelope.Id;
            }

            await Task.Delay(100, Token);
        }

        Assert.Fail($"no workflow.started for {address}");
        return Guid.Empty;
    }

    /// <summary>The (from, to, trigger) of every 'workflow.transition' of <paramref name="address"/>, in ledger order.</summary>
    private static async Task<IReadOnlyList<(string From, string To, string Trigger)>> Transitions(IServiceProvider sindri, string address)
    {
        var read = await sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 1000, Token);
        return [.. read.Value.Where(e => e.Envelope.Type == "workflow.transition" && InstanceOf(e) == address).Select(static e =>
        {
            using var payload = JsonDocument.Parse(e.Envelope.Payload);
            var root = payload.RootElement;
            return (root.GetProperty("from").GetString()!, root.GetProperty("to").GetString()!, root.GetProperty("trigger").GetString()!);
        })];
    }

    private static string? InstanceOf(LedgerEvent e)
    {
        using var payload = JsonDocument.Parse(e.Envelope.Payload);
        return payload.RootElement.TryGetProperty("instance", out var instance) ? instance.GetString() : null;
    }

    /// <summary>How many signals the instance's history holds.</summary>
    private static async Task<int> Signals(WorkflowHandle handle) =>
        (await handle.FetchHistoryAsync()).Events.Count(static e => e.EventType == EventType.WorkflowExecutionSignaled);

    /// <summary>The instance's result once it reaches a terminal state.</summary>
    private static async Task<RecipeResult> Ended(WorkflowHandle<RecipeWorkflow, RecipeResult> handle)
    {
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(Token);
        patience.CancelAfter(_patience);
        return await handle.GetResultAsync(rpcOptions: new RpcOptions { CancellationToken = patience.Token });
    }
}
