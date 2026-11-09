using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Guards;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>What a 'run_workflow' node starts (agent-first PR 9): the callee's active revision, one run per caller run, node and index, and a wait that gives up.</summary>
public sealed class GraphWorkflowCallerTests
{
    private static readonly WorkflowRunId _caller = WorkflowRunId.From("nightly@2:5");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed record World(GraphWorkflowCaller Caller, InMemoryWorkflowStore Workflows, InMemoryWorkflowRunStore Runs, FakeGraphRunLauncher Launcher, InMemoryProfiles Profiles, FakeTimeProvider Clock);

    private static async Task<World> Build()
    {
        var harness = new SliceHarness();
        _ = await harness.HoldLease();
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var runs = new InMemoryWorkflowRunStore(clock);
        var workflows = new InMemoryWorkflowStore(clock);
        var launcher = new FakeGraphRunLauncher();
        var starter = new GraphRunStarter(runs, launcher, new GraphMirror(harness.Store, harness.Held, harness.Events), clock, NullLogger<GraphRunStarter>.Instance);
        var profiles = new InMemoryProfiles();
        return new World(new GraphWorkflowCaller(workflows, runs, starter, profiles, clock), workflows, runs, launcher, profiles, clock);
    }

    private static async Task Define(World w, string slug, string triggerType = "workflow_call", bool activate = true, string? concurrency = null)
    {
        var id = WorkflowId.From("WF-" + Math.Abs(slug.GetHashCode(StringComparison.Ordinal)).ToString("D32", System.Globalization.CultureInfo.InvariantCulture));
        var spec = new GraphSpec(1, slug, Description: null, concurrency, Resources: null, [new GraphNode(NodeId.From("start"), triggerType, Title: null, [])], []);
        _ = (await w.Workflows.Create(id, WorkflowSlug.From(slug), spec, "owner", Token)).Value;
        if (activate)
        {
            _ = (await w.Workflows.Activate(id, 1, Token)).Value;
        }
    }

    private static GraphCall Call(string target = "triage", int index = 0, JsonObject? payload = null, int depth = 1) =>
        new(_caller, NodeId.From("call"), index, WorkflowSlug.From(target), payload, depth);

    [Fact, Trait("Lane", "unit")]
    public async Task A_call_starts_the_active_revision_with_a_workflow_call_trigger_carrying_payload_depth_and_caller()
    {
        var w = await Build();
        await Define(w, "triage", concurrency: "unlimited");

        var called = await w.Caller.Start(Call(payload: new JsonObject { ["pr"] = 12 }, depth: 2), Token);

        Assert.True(called.IsSuccess, called.IsError ? called.Error.ToString() : null);
        Assert.Equal("started", called.Value.Status);
        var run = (await w.Runs.Get(called.Value.Run, Token)).Value;
        var trigger = JsonNode.Parse(run.Trigger)!.AsObject();
        Assert.Equal("workflow_call", trigger["kind"]!.GetValue<string>());
        Assert.Equal(12, trigger["payload"]!["pr"]!.GetValue<int>());
        Assert.Equal(2, trigger["callDepth"]!.GetValue<int>());
        Assert.Equal("nightly@2:5", trigger["parentRun"]!.GetValue<string>());
        Assert.Equal("workflow:nightly", trigger["by"]!.GetValue<string>());
        Assert.Equal(CallOccurrence.Of("nightly@2:5", "call", 0), called.Value.Run.Occurrence);
        _ = Assert.Single(w.Launcher.Launched);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_same_call_twice_is_one_run_and_another_index_is_another_run()
    {
        var w = await Build();
        await Define(w, "triage", concurrency: "unlimited");

        var first = (await w.Caller.Start(Call(), Token)).Value;
        var again = await w.Caller.Start(Call(), Token);
        var second = (await w.Caller.Start(Call(index: 1), Token)).Value;

        Assert.True(again.IsSuccess, again.IsError ? again.Error.ToString() : null);
        Assert.Equal(first.Run, again.Value.Run);
        Assert.NotEqual(first.Run, second.Run);
        Assert.Equal(2, w.Launcher.Launched.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_call_whose_id_is_held_by_another_callers_run_starts_its_own_run_and_never_returns_the_foreign_one()
    {
        var w = await Build();
        await Define(w, "triage", concurrency: "unlimited");
        var record = (await w.Workflows.List(includeArchived: false, Token)).Value.Single(r => r.Slug == WorkflowSlug.From("triage"));
        var taken = WorkflowRunId.Of(record.Slug, 1, CallOccurrence.Of(_caller.Value, "call", 0));
        var foreign = new WorkflowRun(taken, record.Id, 1, "{\"kind\":\"workflow_call\",\"parentRun\":\"other@1:9\",\"node\":\"call\",\"index\":0}", WorkflowRunStatus.Running, Outputs: null, Error: null, ItemId: null, KnownInstants.Epoch, EndedAt: null);
        _ = (await w.Runs.Insert(foreign, Token)).Value;

        var called = await w.Caller.Start(Call(), Token);

        Assert.True(called.IsSuccess, called.IsError ? called.Error.ToString() : null);
        Assert.NotEqual(taken, called.Value.Run);
        var run = (await w.Runs.Get(called.Value.Run, Token)).Value;
        Assert.Equal("nightly@2:5", JsonNode.Parse(run.Trigger)!["parentRun"]!.GetValue<string>());
        _ = Assert.Single(w.Launcher.Launched);
        // why: a retried node must land on the same salted id it took before
        var again = (await w.Caller.Start(Call(), Token)).Value;
        Assert.Equal(called.Value.Run, again.Run);
        _ = Assert.Single(w.Launcher.Launched);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unknown_workflow_one_with_no_active_revision_and_one_without_a_workflow_call_trigger_are_refused()
    {
        var w = await Build();
        await Define(w, "draft", activate: false);
        await Define(w, "manual-only", triggerType: "manual");

        var unknown = await w.Caller.Start(Call("nothing"), Token);
        var inactive = await w.Caller.Start(Call("draft"), Token);
        var noTrigger = await w.Caller.Start(Call("manual-only"), Token);

        Assert.Equal(SindriErrors.GraphNotFound.Code, unknown.Error.Code);
        Assert.Equal(SindriErrors.GraphNotActive.Code, inactive.Error.Code);
        Assert.Equal(SindriErrors.GraphBlockFailed.Code, noTrigger.Error.Code);
        Assert.Contains("workflow_call", noTrigger.Error.Message, StringComparison.Ordinal);
        Assert.Empty(w.Launcher.Launched);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_callee_that_runs_one_at_a_time_and_is_busy_is_skipped_and_said_so()
    {
        var w = await Build();
        await Define(w, "triage");   // the default concurrency is one-per-workflow

        _ = await w.Caller.Start(Call(), Token);
        var busy = await w.Caller.Start(Call(index: 1), Token);

        Assert.Equal("skipped", busy.Value.Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_maximum_depth_is_the_profiles_call_depth_else_three()
    {
        var w = await Build();

        Assert.Equal(CallDepth.DefaultMax, await w.Caller.MaxDepth(Token));   // no profile at all

        var loaded = SliceHarness.Profile();
        _ = await w.Profiles.Save(loaded with { Profile = loaded.Profile with { Workflow = new WorkflowSettings { CallDepth = 5 } } }, Token);
        Assert.Equal(5, await w.Caller.MaxDepth(Token));

        _ = await w.Profiles.Save(loaded, Token);
        Assert.Equal(CallDepth.DefaultMax, await w.Caller.MaxDepth(Token));   // a profile that doesn't set it
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_wait_on_an_ended_run_returns_how_it_ended_with_its_outputs()
    {
        var w = await Build();
        await Define(w, "triage", concurrency: "unlimited");
        var run = (await w.Caller.Start(Call(), Token)).Value.Run;
        _ = await w.Runs.SetStatus(run, WorkflowRunStatus.Succeeded, """{"answer":42}""", error: null, Token);

        var end = await w.Caller.Wait(run, TimeSpan.FromSeconds(30), Token);

        Assert.Equal("succeeded", end.Value.Status);
        Assert.Equal(42, end.Value.Outputs!["answer"]!.GetValue<int>());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_wait_on_a_run_that_never_ends_gives_up_at_the_timeout_with_a_reason()
    {
        var w = await Build();
        await Define(w, "triage", concurrency: "unlimited");
        var run = (await w.Caller.Start(Call(), Token)).Value.Run;

        var waiting = w.Caller.Wait(run, TimeSpan.FromSeconds(5), Token).AsTask();
        for (var i = 0; i < 100 && !waiting.IsCompleted; i++)
        {
            w.Clock.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(10, Token);
        }

        var end = await waiting;
        Assert.Equal(SindriErrors.GraphBlockFailed.Code, end.Error.Code);
        Assert.Contains("did not end", end.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_run_is_reported_with_its_error()
    {
        var w = await Build();
        await Define(w, "triage", concurrency: "unlimited");
        var run = (await w.Caller.Start(Call(), Token)).Value.Run;
        _ = await w.Runs.SetStatus(run, WorkflowRunStatus.Failed, outputs: null, "boom", Token);

        var end = await w.Caller.Wait(run, TimeSpan.FromSeconds(30), Token);

        Assert.Equal(("failed", "boom"), (end.Value.Status, end.Value.Error));
    }
}
