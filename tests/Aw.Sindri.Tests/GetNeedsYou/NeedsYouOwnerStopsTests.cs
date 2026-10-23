using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.OpenTargets;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Features.GetNeedsYou;
using Aw.Sindri.Testing.Workflow.Registry;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.GetInstance;
using Aw.Sindri.Tests.ListInstances;
using Aw.Sindri.Tests.Requests;
using Aw.Sindri.Tests.Workflow.Engine;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.GetNeedsYou;

/// <summary>The read-time 'desk:' row of an instance that waits on the owner at a human-verb wait (q65 A2b): a wait state with a 'command.&lt;verb&gt;' exit whose effect acts with a human's authority.</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouOwnerStopsTests
{
    private const string Pull = """{"pr":42,"url":"https://github.com/o/r/pull/42"}""";

    private static readonly InstanceAddress _second = InstanceAddress.From("T-9/pr-babysitter/1");

    private readonly RequestQueryHarness _q = new();
    private readonly InMemoryRecipeStore _recipes = new();
    private readonly ItemId _item = RequestQueryHarness.Item('1');

    [Fact]
    public async Task An_instance_in_a_human_verb_wait_gets_a_desk_row_that_opens_the_workflow_with_its_pull_request()
    {
        var view = await Waiting("ready", Pull);

        var row = Assert.Single(await Read([view], Human()));

        Expect.Equal(("desk:" + WorkflowInstanceId.Of(view.Address).Value, NeedsYouKind.Park, Urgency.Colour, "ready to merge"), (row.Id.Value, row.Kind, row.Urgency, row.Label));
        Expect.Equal("ApplyCommand:command", row.PrimaryAction!.Value.Value);
        Expect.Equal($"sindri workflow status {view.Address.Value}", row.Cli);
        Expect.Equal(new NeedsYouOpen(NeedsYouRoutes.Desk(view.Address), "Review and act", new NeedsYouOpenPr("https://github.com/o/r/pull/42", 42)), row.Open);
        Expect.Equal("An item", row.Title);
        Expect.Equal(view.Since, row.Since);
    }

    [Fact]
    public async Task A_wait_state_with_no_human_trigger_command_gets_no_row()
    {
        var view = await Waiting("merging", Pull);

        Assert.Empty(await Read([view], Human()));
    }

    [Fact]
    public async Task Without_a_pr_status_fact_the_row_opens_with_no_pull_request()
    {
        var view = await Waiting("ready", prStatus: null);

        var row = Assert.Single(await Read([view], Human()));

        Assert.Null(row.Open.Pr);
    }

    [Fact]
    public async Task An_instance_that_cannot_be_read_drops_only_its_own_row()
    {
        var readable = await Waiting("ready", Pull);
        var lost = readable with { Address = _second, Id = WorkflowInstanceId.Of(_second) };

        var row = Assert.Single(await Read([readable, lost], Human(), served: [readable]));

        Expect.Equal(WorkflowInstanceId.Of(readable.Address).Value, row.Subject.Id.Value);
    }

    [Fact]
    public async Task A_suspended_instance_gets_no_row()
    {
        var view = (await Waiting("ready", Pull)) with { Question = StatusCases.View("taken-over").Question };

        Assert.Empty(await Read([view], Human()));
    }

    [Fact]
    public async Task A_session_in_another_repo_does_not_see_the_row_and_one_in_its_repo_does()
    {
        var view = await Waiting("ready", Pull, repo: "agentic-workflow");

        Assert.Empty(await Read([view], A.Caller().Session().InRepo("other-repo").Build()));
        _ = Assert.Single(await Read([view], A.Caller().Session().InRepo("agentic-workflow").Build()));
    }

    [Fact]
    public async Task The_owner_stop_row_matches_its_golden_file()
    {
        var view = await Waiting("ready", Pull);
        var rows = await Read([view], Human());

        ApiGoldens.ApiGolden.Match(
            Encoding.UTF8.GetString(AwJson.Serialize(new GetNeedsYouResponse(rows, Empty: null, new AsOf(7, 7, view.Since, Rebuilding: false, Stalled: false)), SliceHarness.Json)),
            Path.GetTempPath(), "needs-you", "owner-stop");
    }

    private static CallerContext Human() => A.Caller().Human().Build();

    private async Task<InstanceView> Waiting(string state, string? prStatus, string? repo = null)
    {
        var document = RecipeReaderTests.Reader.Read(ShippedRecipes.Read("sindri/workflow.pr-babysitter.yaml"), "workflow.pr-babysitter.yaml").Document!;   // why: the shipped recipes bind (ShippedRecipesTests)
        var text = Encoding.UTF8.GetString(AwJson.Serialize(document.Recipe with { Hash = null }, SliceHarness.Json));
        _ = await _recipes.Add(new RegisteredRecipe(document.Recipe.Id, document.Recipe.Version, document.Hash, text, Base: null, DateTimeOffset.UnixEpoch), TestContext.Current.CancellationToken);
        await _q.CreateItem(_item, repo);
        return StatusCases.View("desk-task") with
        {
            Recipe = new RecipeVersion(document.Recipe.Id, document.Recipe.Version, document.Hash),
            State = state,
            Task = null,
            Run = null,
            AgentRun = null,
            Question = null,
            ItemId = ItemKey.From(_item.Value),
            FactShas = prStatus is null ? null : new Dictionary<string, string>(StringComparer.Ordinal) { ["fact:prStatus"] = new string('1', 64) },
        };
    }

    private async Task<IReadOnlyList<NeedsYouRow>> Read(InstanceView[] listed, CallerContext caller, InstanceView[]? served = null)
    {
        var instances = new SomeInstances(served ?? listed);
        var reader = new OwnerStopReader(
            new RecordingInstances([.. listed.Select(static v => new InstanceRow(v.Address.Value, v.State, NeedsYou: false))]), instances, instances,
            new RecipeRegistryReader(_recipes, new InMemoryRecipeApprovals(), EngineFixture.Catalog, SliceHarness.Json, new NoProfileFacts()), EngineFixture.Catalog);
        var stops = new NeedsYouOwnerStops(
            reader,
            _q.Views.Views, new Contents(), new PassThroughScrubber(), TimeProvider.System);
        return await stops.Read(caller, TestContext.Current.CancellationToken);
    }

    private sealed class Contents : IFactContents
    {
        public ValueTask<Result<ReadOnlyMemory<byte>>> Read(FactValue fact, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success((ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(Pull)));
    }

    private sealed class NoProfileFacts : IRecipeProfileFacts
    {
        public ValueTask<Result<RecipeProfileFacts>> Current(CancellationToken cancellationToken) => throw new NotSupportedException("reading a recipe back needs no profile");
    }
}
