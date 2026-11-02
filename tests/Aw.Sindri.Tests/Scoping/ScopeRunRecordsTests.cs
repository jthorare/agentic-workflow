using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.GetInstance;
using static Aw.Sindri.Tests.Workflow.Wiring.WiringRecipe;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>The ledger adapter of <see cref="IScopeRunRecords"/>, over the in-memory ledger: the contract the fake in the tests (task C) honours too.</summary>
[Lane(TestLane.Integration)]
public sealed class ScopeRunRecordsTests : ScopeRunRecordsContract
{
    private static readonly AgentId _agent = AgentId.From("claude-code");

    private readonly SliceHarness _slice = new();
    private LeaseGrant? _grant;

    protected override async ValueTask<IScopeRunRecords> Create()
    {
        _grant = await _slice.HoldLease();
        return new ScopeRunRecords(_slice.Store, _slice.Events, new Instances(), new StepRunRecipes(ScopingRecipe()), Catalog());
    }

    protected override async ValueTask Launched(IScopeRunRecords records, ScopeLaunch launch) =>
        await Append(EventTypes.LaunchStarted,
            new LaunchStarted(launch.Launch, A.FirstItemId, LaunchMode.Open, _agent, SessionId: null, "sindri " + A.FirstItemId.Value, new LaunchViewer("none", Label: null, ViewerOutcomeKind.Unconfirmed, Code: null, Detail: null),
                ["tmux", "attach"], CatalogSource.Builtin, "sha256:abc", launch.Run, launch.State, launch.Number, Model: launch.Picked),
            $"launch.outcome:{launch.Launch.Value}");

    protected override async ValueTask Spent(IScopeRunRecords records, LaunchId launch, int sample, long tokens, string? model) =>
        await Append(EventTypes.SessionUsage, new SessionUsage(launch, sample, tokens / 2, tokens - (tokens / 2), _agent, model),
            string.Create(CultureInfo.InvariantCulture, $"{EventTypes.SessionUsage}:{launch.Value}:{sample}"));

    protected override async ValueTask CompletedAtTheDesk(IScopeRunRecords records, InstanceAddress run, string state, int number) =>
        await Append(EventTypes.WorkflowTransition,
            new WorkflowEventPayload(run.Value, state, "done", TriggerNames.StepCompleted, "drafted", string.Create(CultureInfo.InvariantCulture, $"1.{number}")),
            string.Create(CultureInfo.InvariantCulture, $"workflow:{run.Value}:run:{state}-{number}"));

    /// <summary>The retry of a run on a stronger model has the run's token and its own launch id (model-pick spec 5): its record still names the run.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_escalated_launch_carries_the_run_id_it_was_retried_under()
    {
        var records = await Create();
        var scoping = InstanceAddress.From("T-1/scoping/1");
        var id = AgentLaunches.LaunchIdFor(WorkflowInstanceId.Of(scoping), RunToken.For(entry: 1, number: 1), escalation: 1);

        await Launched(records, new ScopeLaunch(scoping, "draft", "step.scope.draft", "claude-sonnet-5-5", 1, id));

        var read = await records.Of(scoping, TestContext.Current.CancellationToken);

        Assert.Equal("1.1", Assert.Single(read.Value, static r => r.Via == "agent").RunToken);
    }

    private static ScopeLaunch Challenge(string? picked, int number = 1) =>
        new(InstanceAddress.From("T-1/scoping/1"), "challenge", "step.scope.challenge", Requested: null, number, LaunchId.From("LCH-000000000000000000000000000000c" + number.ToString(CultureInfo.InvariantCulture)), picked);

    /// <summary>Model-pick M-7: a launch the pick gave its model, with no usage row yet, records that model as the one it ran on and asked for.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_picked_launch_with_no_usage_records_the_pick_as_its_model()
    {
        var records = await Create();
        await Launched(records, Challenge("haiku"));

        var record = Assert.Single((await records.Of(InstanceAddress.From("T-1/scoping/1"), TestContext.Current.CancellationToken)).Value);

        Assert.Equal(("haiku", "haiku"), (record.Model, record.Requested));
    }

    [Fact]
    public async Task The_model_the_session_reports_wins_over_the_pick_and_the_pick_is_still_what_was_asked_for()
    {
        var records = await Create();
        var launch = Challenge("haiku");
        await Launched(records, launch);
        await Spent(records, launch.Launch, 1, 100, "claude-haiku-5-5");

        var record = Assert.Single((await records.Of(InstanceAddress.From("T-1/scoping/1"), TestContext.Current.CancellationToken)).Value);

        Assert.Equal(("claude-haiku-5-5", "haiku"), (record.Model, record.Requested));
    }

    [Fact]
    public async Task A_model_the_recipe_names_is_what_was_asked_for_whatever_the_pick_says()
    {
        var records = await Create();
        var draft = new ScopeLaunch(InstanceAddress.From("T-1/scoping/1"), "draft", "step.scope.draft", "claude-sonnet-5-5", 1, LaunchId.From("LCH-0000000000000000000000000000000d"), Picked: "claude-sonnet-5-5");
        await Launched(records, draft);

        var record = Assert.Single((await records.Of(InstanceAddress.From("T-1/scoping/1"), TestContext.Current.CancellationToken)).Value);

        Assert.Equal(("claude-sonnet-5-5", "claude-sonnet-5-5"), (record.Model, record.Requested));
    }

    private async ValueTask Append<T>(string type, T payload, string key)
    {
        var appended = await _slice.Store.Append(_slice.Events.Entry(type, payload, key, A.FirstItemId.Value), _grant!, TestContext.Current.CancellationToken);   // why: Create ran first
        Assert.True(appended.IsSuccess);
    }

    private static BlockCatalog Catalog() => new([ScopingSteps.Draft, ScopingSteps.Challenge, GapSearchSteps.GapSearch], []);

    private static Recipe ScopingRecipe() => Of(
        "draft",
        [
            ("draft", Active("step.scope.draft", with: Model("claude-sonnet-5-5"))),
            ("challenge", Active("step.scope.challenge")),
            ("gap-search", Active("step.scope.gap-search", with: Model("claude-opus-5-5"))),
            ("done", Terminal()),
        ],
        [],
        id: "workflow.scoping");

    private static Dictionary<string, WithValue> Model(string id) =>
        new(StringComparer.Ordinal) { ["model"] = new WithValue(JsonSerializer.SerializeToElement(id), Fact: null, Profile: null) };

    /// <summary>Every instance runs the scoping recipe.</summary>
    private sealed class Instances : IInstanceReader
    {
        public ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<InstanceView>>(new InstanceView(address, new RecipeVersion(RecipeId.From("workflow.scoping"), 1, RecipeHash.From("sha256:" + new string('a', 64))), "draft", Detail: null,
                DateTimeOffset.UnixEpoch, string.Empty, [], Question: null, Task: null, Run: null, Children: [], Outcome: null)
            {
                Id = WorkflowInstanceId.Of(address),
            });
    }
}
