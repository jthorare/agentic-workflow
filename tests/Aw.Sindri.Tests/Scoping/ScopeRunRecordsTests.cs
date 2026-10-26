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
                ["tmux", "attach"], CatalogSource.Builtin, "sha256:abc", launch.Run, launch.State, launch.Number),
            $"launch.outcome:{launch.Launch.Value}");

    protected override async ValueTask Spent(IScopeRunRecords records, LaunchId launch, int sample, long tokens, string? model) =>
        await Append(EventTypes.SessionUsage, new SessionUsage(launch, sample, tokens / 2, tokens - (tokens / 2), _agent, model),
            string.Create(CultureInfo.InvariantCulture, $"{EventTypes.SessionUsage}:{launch.Value}:{sample}"));

    protected override async ValueTask CompletedAtTheDesk(IScopeRunRecords records, InstanceAddress run, string state, int number) =>
        await Append(EventTypes.WorkflowTransition,
            new WorkflowEventPayload(run.Value, state, "done", TriggerNames.StepCompleted, "drafted", string.Create(CultureInfo.InvariantCulture, $"1.{number}")),
            string.Create(CultureInfo.InvariantCulture, $"workflow:{run.Value}:run:{state}-{number}"));

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
