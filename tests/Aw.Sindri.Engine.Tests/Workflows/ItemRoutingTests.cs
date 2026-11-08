using System.Globalization;
using System.Text.Json;
using Aw.Kernel.Events;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.CompleteStep;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Temporalio.Client;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// 'workflow.item' on the pinned dev server (engine P7 unit U3, plan D7 and D8): the router stub answers a started item with
/// 'item.routed' and its Step spec, the gates of 'triaged' and 'startable' run their runtimes ('gate.route.startable' reads
/// the approved profile), and the slot stub grants a slot the moment the instance waits for one.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ItemRoutingTests(DevServer server) : IClassFixture<DevServer>, IDisposable
{
    private const string Spec = "Add the export button to the settings page.";
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(30);
    private readonly WorkflowsHarness _harness = new();
    private int _events;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task Item_started_emits_item_routed_with_the_step_spec()
    {
        await Serve(replace: null, async sindri =>
        {
            var item = await Start(sindri, "T-51");
            await Visible(sindri, "T-51");

            await Run(sindri, RouterStub.ServiceId);
            await Run(sindri, RouterStub.ServiceId);   // the second run routes nothing more

            var routed = await Routed(sindri);
            var row = Assert.Single(routed);
            using var payload = JsonDocument.Parse(row.Envelope.Payload);
            var spec = payload.RootElement.GetProperty("facts").GetProperty("fact:stepSpec");
            var stored = await sindri.GetRequiredService<IFactContents>().Read(new FactValue(spec.GetProperty("ref").GetString()!, spec.GetProperty("sha256").GetString()!, HeadSha: null), Token);
            Assert.Equal(("service:router-stub", "T-51", "startable"), (row.Envelope.Actor, row.Item, payload.RootElement.GetProperty("outcome").GetString()));
            Assert.Equal(Spec, System.Text.Encoding.UTF8.GetString(stored.Value.Span));
            Assert.Equal("triaged", (await item.QueryAsync(static wf => wf.GetInstance())).State);
        }, TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 120_000)]
    public async Task A_route_of_none_routes_the_item_as_not_startable_and_its_gate_refuses_a_startable_one()
    {
        await Serve(NoRoute, async sindri =>
        {
            var item = await Start(sindri, "T-52");
            await Visible(sindri, "T-52");
            await Run(sindri, RouterStub.ServiceId);
            using (var payload = JsonDocument.Parse(Assert.Single(await Routed(sindri)).Envelope.Payload))
            {
                Assert.Equal("notStartable", payload.RootElement.GetProperty("outcome").GetString());
            }

            await Run(sindri, EventBridge.ServiceId);
            _ = await WorkflowsHarness.Until(item, static v => v.State == "dropped");   // the router's word 'notStartable' drops it

            var other = await Start(sindri, "T-54");   // started after the router ran: nothing has routed it
            await Visible(sindri, "T-54");
            _ = await Append(sindri, "T-54", """{"outcome":"startable"}""");   // another router's word: the gate still reads the profile
            await Run(sindri, EventBridge.ServiceId);

            await WaitFor(async () => (await Types(sindri)).Contains("workflow.refused", StringComparer.Ordinal));
            Assert.Equal("parked", (await other.QueryAsync(static wf => wf.GetInstance())).State);   // the refused gate parks it at triaged: no move to startable
        }, TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 120_000)]
    public async Task A_slot_wait_is_granted()
    {
        await Serve(replace: null, async sindri =>
        {
            var item = await Start(sindri, "T-53");
            await Visible(sindri, "T-53");
            await Run(sindri, RouterStub.ServiceId);
            await Run(sindri, EventBridge.ServiceId);

            var view = await WorkflowsHarness.Until(item, static v => v.State == "claimed");   // triaged, startable (the slot stub grants), claimed (gate.budget.available passes)
            Assert.Equal("claimed", view.State);
        }, TestContext.Current.CancellationToken);
    }

    private static void NoRoute(IServiceCollection services)
    {
        var profiles = new InMemoryProfiles();
        var none = new SindriProfile(1, "thor") { Workflow = new(Route: new("none")) };
        _ = profiles.Save(new LoadedProfile(none, ProfileHash.From(new string('c', 64)), "/home/profile.yaml"), Token).AsTask().GetAwaiter().GetResult();
        _ = services.Replace(ServiceDescriptor.Singleton<IApprovedProfile>(profiles));
    }

    private async Task Serve(Action<IServiceCollection>? replace, Func<ServiceProvider, Task> body, CancellationToken ct)
    {
        _harness.Recipe("workflow.item", WorkflowsHarness.Shipped("sindri/workflow.item.yaml"));
        await using var sindri = _harness.Sindri(replace ?? (static _ => { }));
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

    private async Task<WorkflowHandle<RecipeWorkflow, RecipeResult>> Start(IServiceProvider sindri, string item)
    {
        var approved = await sindri.GetRequiredService<IApprovedRecipes>().Latest(RecipeId.From("workflow.item"), Token);
        Assert.False(approved.IsError, approved.IsError ? approved.Error.Message : null);
        var fact = _harness.Fact(JsonSerializer.Serialize(new { id = item, repo = (string?)null, spec = Spec }));
        var inputs = new Dictionary<FactKey, InputFact> { [FactKey.From("fact:item")] = new InputFact("item.json", fact) };
        var start = new InstanceStart(approved.Value.Version, ItemKey.From(item), Attempt: 1, inputs, Tenant: "local", Ring: 0);
        return await server.Environment.Client.StartWorkflowAsync(
            (RecipeWorkflow wf) => wf.RunAsync(start),
            new WorkflowOptions(id: start.WorkflowId().Value, taskQueue: TemporalSettings.DefaultTaskQueue));
    }

    private static async Task Visible(IServiceProvider sindri, string item)
    {
        await WaitFor(async () =>
        {
            var listed = await sindri.GetRequiredService<IWorkflowInstances>().List($"Item = '{item}'", ended: false, Token);
            return !listed.IsError && listed.Value.Count == 1;
        });
    }

    /// <summary>Appends 'item.routed' as the router would, with a fresh id.</summary>
    private async Task<Guid> Append(IServiceProvider sindri, string item, string payload)
    {
        var n = Interlocked.Increment(ref _events);
        var id = Guid.Parse(string.Create(CultureInfo.InvariantCulture, $"00000000-0000-7000-8000-{n:D12}"));
        Guid? causation = null;
        var envelope = new EventEnvelope<string>(id, "item.routed", 1, sindri.GetRequiredService<TimeProvider>().GetUtcNow(), "service:router-stub", "local", 0, id, causation, TraceId: null, payload);
        var grant = sindri.GetRequiredService<HeldLease>().Current ?? throw new InvalidOperationException("the test holds no lease");
        var appended = await sindri.GetRequiredService<ILedgerStore>().Append(new LedgerEntry(envelope, "test:" + id.ToString("N"), item), grant, Token);
        Assert.False(appended.IsError, appended.IsError ? appended.Error.Message : null);
        return id;
    }

    /// <summary>Runs the scheduled service <paramref name="name"/> once under the test's lease, as the scheduler would.</summary>
    private static async Task Run(IServiceProvider sindri, ServiceName name)
    {
        await using var scope = sindri.CreateAsyncScope();
        var service = scope.ServiceProvider.GetServices<IScheduledService>().Single(s => s.Identity() == name);
        var grant = sindri.GetRequiredService<HeldLease>().Current ?? throw new InvalidOperationException("the test holds no lease");
        using var caller = sindri.GetRequiredService<ICallerContextAccessor>().EnterService(name.Value);   // the scheduler's own caller: it names the actor of what the service appends
        var ran = await service.Run(new ScheduledRun(grant, KnownInstants.Epoch), Token);
        Assert.False(ran.IsError, ran.IsError ? ran.Error.Message : null);
    }

    private static async Task<IReadOnlyList<LedgerEvent>> Routed(IServiceProvider sindri) =>
        [.. (await sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 1000, Token)).Value.Where(static e => e.Envelope.Type == "item.routed" && e.Envelope.Actor == "service:router-stub")];

    private static async Task<IReadOnlyList<string>> Types(IServiceProvider sindri) =>
        [.. (await sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 1000, Token)).Value.Select(static e => e.Envelope.Type)];

    private static async Task WaitFor(Func<Task<bool>> holds)
    {
        var deadline = RealTime.NowDateTime + _patience;
        while (RealTime.NowDateTime < deadline)
        {
            if (await holds())
            {
                return;
            }

            await Task.Delay(100, Token);
        }

        Assert.Fail("the condition never held");
    }
}
