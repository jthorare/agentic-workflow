using System.Globalization;
using System.Text.Json;
using Aw.Kernel.Events;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.CompleteStep;
using Aw.Sindri.Tests.Workflows.Effects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Temporalio.Client;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The engine releases an item's claim when its run ends (engine P7 unit U5, plan D9, IDEA-033, owner answer Q1): the marker
/// ref 'refs/sindri/{user}/{item}' goes and the 'workflow_claims' row is freed, on every ending, 'cancelled' included, and a
/// claim another instance has since taken over is left alone. Real git, the real claims table, the pinned dev server.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class CleanupTests(DevServer server) : IClassFixture<DevServer>, IDisposable
{
    private static readonly string _claimThenDone = string.Join(
        "\n",
        "schema: 1",
        "id: workflow.claim-done",
        "version: 1",
        "inputs:",
        "  - { key: fact:item, label: \"Item\", type: item, kind: item }",
        "initial: waiting",
        "states:",
        "  waiting: { kind: wait, waitFor: { event: item.routed }, timeout: { after: 1d, then: escalate }, next: \"Waiting for {item}.\" }",
        "  claimed: { kind: wait, waitFor: { event: claim.created }, timeout: { after: 10m, then: retry }, next: \"Waiting for the claim ref.\" }",
        "  done:    { kind: terminal, outcome: resolved }",
        "transitions:",
        "  - { from: waiting, to: claimed, on: wait.satisfied, effects: [effect.claim.ref] }",
        "  - { from: claimed, to: done,    on: wait.satisfied, when: { outcome: created } }",
        "  - { from: claimed, to: done,    on: wait.satisfied, when: { outcome: claimExists } }",
        "evals: []");

    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(30);
    private readonly WorkflowsHarness _harness = new();
    private readonly List<TempGitRepo> _repos = [];
    private int _events;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _harness.Dispose();
        foreach (var repo in _repos)
        {
            repo.Dispose();
        }
    }

    [Fact(Timeout = 120_000)]
    public async Task A_done_item_releases_its_claim_ref()
    {
        await Serve(_claimThenDone, "workflow.claim-done", async (sindri, repo) =>
        {
            var item = await Start(sindri, "workflow.claim-done", "T-61");
            _ = await Append(sindri, "T-61");
            await Finish(sindri, item);   // claim.created reaches the instance, which ends 'done'

            await Gone(sindri, repo, "T-61");
        }, TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 120_000)]
    public async Task A_cancelled_item_releases_its_claim_ref()
    {
        await Serve(_claimThenDone, "workflow.claim-done", async (sindri, repo) =>
        {
            var item = await Start(sindri, "workflow.claim-done", "T-62");
            _ = await Append(sindri, "T-62");
            await Run(sindri, EventBridge.ServiceId);
            await Held(sindri, repo, "T-62");

            _ = await item.ExecuteUpdateAsync(wf => wf.CancelAsync(Cancel()));

            await Gone(sindri, repo, "T-62");
        }, TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 120_000)]
    public async Task A_claim_taken_over_by_another_instance_is_left_alone()
    {
        await Serve(_claimThenDone, "workflow.claim-done", async (sindri, repo) =>
        {
            var item = await Start(sindri, "workflow.claim-done", "T-63");
            _ = await Append(sindri, "T-63");
            await Run(sindri, EventBridge.ServiceId);
            await Held(sindri, repo, "T-63");
            var claims = sindri.GetRequiredService<IWorkflowClaims>();
            var mine = (await claims.Find(ItemKey.From("T-63"), Token)).Value!;
            var other = new WorkflowClaim(ItemKey.From("T-63"), InstanceAddress.From("T-63/other/2"), Attempt: 2);
            _ = await claims.Take(other, ended: mine.Owner, Token);   // another instance took over this one's claim

            await Finish(sindri, item);   // this instance ends (done)

            Assert.Equal(other.Owner, (await claims.Find(ItemKey.From("T-63"), Token)).Value!.Owner);
            Assert.NotEmpty(await repo.Git("for-each-ref", "refs/sindri/thor/T-63"));
        }, TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 120_000)]
    public async Task Cleanup_runs_after_a_cancel_that_cancels_the_workflow()
    {
        await Serve(_claimThenDone, "workflow.claim-done", async (sindri, repo) =>
        {
            var item = await Start(sindri, "workflow.claim-done", "T-64");
            _ = await Append(sindri, "T-64");
            await Run(sindri, EventBridge.ServiceId);
            await Held(sindri, repo, "T-64");

            await item.CancelAsync();   // Temporal's own cancellation of the run, not the 'cancel' Update

            await Gone(sindri, repo, "T-64");
        }, TestContext.Current.CancellationToken);
    }

    private static CancelRequested Cancel() =>
        new("human:cli", CancelOrigin.Command, new CommandStamp(CommandNonce.From(new string('e', 32)), new string('e', 64)), Sequence: null, Parent: null);

    private async Task Serve(string recipe, string id, Func<ServiceProvider, TempGitRepo, Task> body, CancellationToken ct)
    {
        var repo = await TempGitRepo.Create();
        _repos.Add(repo);
        _harness.Recipe(id, recipe);
        var profiles = new InMemoryProfiles();
        var profile = new SindriProfile(1, "thor", Repos: [new RepoEntry("app", repo.Path)], Workflow: new WorkflowSettings(Effects: ["effect.claim.ref"], DispatchDelay: "5s"));   // why: the shortest delay the profile allows; the dev server runs in real time, and the 10 s default would wait 5 s more in every test
        _ = await profiles.Save(new LoadedProfile(profile, ProfileHash.From(new string('b', 64)), "/home/profile.yaml"), Token);
        await using var sindri = _harness.Sindri(services => _ = services.Replace(ServiceDescriptor.Singleton<IApprovedProfile>(profiles)));
        await WorkflowsHarness.HoldLease(sindri, ct);
        var temporal = await WorkflowsHarness.AsServe(sindri, server.Environment, ct);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var worker = sindri.GetRequiredService<ITemporalWorker>().Run(temporal, stop.Token);
        try
        {
            await body(sindri, repo);
        }
        finally
        {
            await stop.CancelAsync();
            await worker;
        }
    }

    private async Task<WorkflowHandle<RecipeWorkflow, RecipeResult>> Start(IServiceProvider sindri, string recipe, string item)
    {
        var approved = await sindri.GetRequiredService<IApprovedRecipes>().Latest(RecipeId.From(recipe), Token);
        Assert.False(approved.IsError, approved.IsError ? approved.Error.Message : null);
        var fact = _harness.Fact(JsonSerializer.Serialize(new { id = item, repo = "app", spec = "do it" }));
        var inputs = new Dictionary<FactKey, InputFact> { [FactKey.From("fact:item")] = new InputFact("item.json", fact) };
        var start = new InstanceStart(approved.Value.Version, ItemKey.From(item), Attempt: 1, inputs, Tenant: "local", Ring: 0);
        return await server.Environment.Client.StartWorkflowAsync(
            (RecipeWorkflow wf) => wf.RunAsync(start),
            new WorkflowOptions(id: start.WorkflowId().Value, taskQueue: TemporalSettings.DefaultTaskQueue));
    }

    /// <summary>The marker ref exists and the claim row names the item's holder.</summary>
    private static async Task Held(IServiceProvider sindri, TempGitRepo repo, string item) =>
        await WaitFor(async () => (await repo.Git("for-each-ref", $"refs/sindri/thor/{item}")).Length > 0
            && (await sindri.GetRequiredService<IWorkflowClaims>().Find(ItemKey.From(item), Token)).Value is not null);

    /// <summary>Delivers events until the instance has ended.</summary>
    private static async Task Finish(IServiceProvider sindri, WorkflowHandle<RecipeWorkflow, RecipeResult> item)
    {
        var result = item.GetResultAsync();
        await WaitFor(async () =>
        {
            await Run(sindri, EventBridge.ServiceId);
            return result.IsCompleted;
        });
        _ = await result;
    }

    /// <summary>The marker ref is gone and the claim row is freed.</summary>
    private static async Task Gone(IServiceProvider sindri, TempGitRepo repo, string item)
    {
        // why: cleanup deletes the ref, then frees the row; wait for both, or a loaded box sees the row between the two steps
        await WaitFor(async () => (await repo.Git("for-each-ref", $"refs/sindri/thor/{item}")).Length == 0
            && (await sindri.GetRequiredService<IWorkflowClaims>().Find(ItemKey.From(item), Token)).Value is null);
    }

    /// <summary>Appends 'item.routed' as the router would, with a fresh id.</summary>
    private async Task<Guid> Append(IServiceProvider sindri, string item)
    {
        await WaitFor(async () =>
        {
            var listed = await sindri.GetRequiredService<IWorkflowInstances>().List($"Item = '{item}'", ended: false, Token);
            return !listed.IsError && listed.Value.Count == 1;
        });
        var n = Interlocked.Increment(ref _events);
        var id = Guid.Parse(string.Create(CultureInfo.InvariantCulture, $"00000000-0000-7000-8000-{n:D12}"));
        Guid? causation = null;
        var envelope = new EventEnvelope<string>(id, "item.routed", 1, sindri.GetRequiredService<TimeProvider>().GetUtcNow(), "service:router-stub", "local", 0, id, causation, TraceId: null, """{"outcome":"startable"}""");
        var grant = sindri.GetRequiredService<HeldLease>().Current ?? throw new InvalidOperationException("the test holds no lease");
        var appended = await sindri.GetRequiredService<ILedgerStore>().Append(new LedgerEntry(envelope, "test:" + id.ToString("N"), item), grant, Token);
        Assert.False(appended.IsError, appended.IsError ? appended.Error.Message : null);
        return id;
    }

    private static async Task Run(IServiceProvider sindri, ServiceName name)
    {
        await using var scope = sindri.CreateAsyncScope();
        var service = scope.ServiceProvider.GetServices<IScheduledService>().Single(s => s.Identity() == name);
        var grant = sindri.GetRequiredService<HeldLease>().Current ?? throw new InvalidOperationException("the test holds no lease");
        var ran = await service.Run(new ScheduledRun(grant, KnownInstants.Epoch), Token);
        Assert.False(ran.IsError, ran.IsError ? ran.Error.Message : null);
    }

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
