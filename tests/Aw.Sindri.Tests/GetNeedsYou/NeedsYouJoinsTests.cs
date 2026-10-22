using System.Text;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.GetNeedsYou;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Testing.Workflow.Registry;
using Aw.Sindri.Tests.GetInstance;
using Aw.Sindri.Tests.Workflow;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.GetNeedsYou;

/// <summary>The read-time joins of the desk and park rows: the stop from the instance's pinned recipe and the timeout from the status (API spec section 3.6).</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouJoinsTests
{
    private static readonly PassThroughScrubber _plain = new();
    private static readonly DateTimeOffset _since = new(2026, 10, 16, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryRecipeStore _recipes = new();

    [Fact]
    public async Task A_desk_row_names_its_stop_among_the_recipes_desk_states_in_recipe_order()
    {
        await Register();
        var checks = StatusCases.View("desk-task") with { State = "verify" };
        var row = Desk(checks);

        var joined = Assert.Single(await Join([row], new SomeInstances(checks)));

        var desks = EngineFixture.Step.States.Where(s => s.Value.Step is not null && EngineFixture.Catalog.Find(s.Value.Step.Use) is { DeskCapable: true }).Select(static s => s.Key).ToList();
        Expect.Equal(new NeedsYouStop(desks.IndexOf("verify") + 1, desks.Count), joined.Stop);
    }

    [Fact]
    public async Task A_desk_row_carries_the_timeout_the_status_serves_measured_at_the_response()
    {
        await Register();
        var view = StatusCases.View("desk-task");

        var joined = Assert.Single(await Join([Desk(view)], new SomeInstances(view)));

        Expect.Equal(new StatusTimeout(TimeoutKind.Parks, 168_480, "produce"), joined.Timeout);
    }

    [Fact]
    public async Task A_park_row_takes_the_timeout_and_no_stop()
    {
        await Register();
        var view = StatusCases.View("desk-task");
        var row = NeedsYouRows.ForPark(Item(view), view.Address, new SuspensionSummary(SuspensionKind.Park, ParkReason.Rounds, ParkKind.Loop, Escalation: null, Mirrors: null), _since, _plain);

        var joined = Assert.Single(await Join([row], new SomeInstances(view)));

        Assert.Null(joined.Stop);
        Expect.Equal(168_480L, joined.Timeout?.InSeconds);
    }

    [Fact]
    public async Task A_row_of_another_kind_is_returned_as_it_is()
    {
        var view = StatusCases.View("desk-task");
        var takeover = NeedsYouRows.ForIdleTakeover(Item(view), view.Address, _since, _plain);

        var joined = Assert.Single(await Join([takeover], new SomeInstances(view)));

        Expect.Equal(takeover, joined);
    }

    [Fact]
    public async Task An_instance_that_cannot_be_read_leaves_the_row_alone()
    {
        await Register();
        var view = StatusCases.View("desk-task");
        var row = Desk(view);

        var joined = Assert.Single(await Join([row], new SomeInstances()));

        Expect.Equal(row, joined);
    }

    [Fact]
    public async Task A_recipe_that_is_not_registered_drops_the_stop_and_keeps_the_timeout()
    {
        var view = StatusCases.View("desk-task");

        var joined = Assert.Single(await Join([Desk(view)], new SomeInstances(view)));

        Assert.Null(joined.Stop);
        Assert.NotNull(joined.Timeout);
    }

    [Fact]
    public async Task A_reader_that_throws_leaves_the_row_alone()
    {
        await Register();
        var view = StatusCases.View("desk-task");
        var row = Desk(view);
        var joins = Joins(new SomeInstances(view), new ThrowingReader());

        var joined = Assert.Single(await joins.Apply([row], StatusCases.Now, TestContext.Current.CancellationToken));

        Expect.Equal(row, joined);
    }

    [Fact]
    public async Task An_instance_and_a_recipe_are_read_once_per_response()
    {
        await Register();
        var view = StatusCases.View("desk-task");
        var reader = new CountingReader(view);
        var joins = Joins(reader, reader);

        var joined = await joins.Apply([Desk(view), NeedsYouRows.ForPark(Item(view), view.Address, new SuspensionSummary(SuspensionKind.Park, ParkReason.Rounds, ParkKind.Loop, Escalation: null, Mirrors: null), _since, _plain)], StatusCases.Now, TestContext.Current.CancellationToken);

        Expect.Equal(2, joined.Count);
        Expect.Equal(1, reader.Gets);
    }

    [Fact]
    public async Task The_desk_row_with_its_stop_matches_its_golden_file()
    {
        await Register();
        var view = StatusCases.View("desk-task") with { State = "checks" };

        Golden(await Join([Desk(view)], new SomeInstances(view)), "desk-with-stop");
    }

    [Fact]
    public async Task The_park_row_with_its_timeout_matches_its_golden_file()
    {
        await Register();
        var view = StatusCases.View("desk-task");
        var park = NeedsYouRows.ForPark(Item(view), view.Address, new SuspensionSummary(SuspensionKind.Park, ParkReason.Rounds, ParkKind.Loop, Escalation: null, Mirrors: null), _since, _plain);

        Golden(await Join([park], new SomeInstances(view)), "park-with-timeout");
    }

    private static void Golden(IReadOnlyList<NeedsYouRow> rows, string scenario) =>
        ApiGoldens.ApiGolden.Match(
            Encoding.UTF8.GetString(AwJson.Serialize(new GetNeedsYouResponse(rows, Empty: null, new AsOf(7, 7, _since, Rebuilding: false, Stalled: false)), SliceHarness.Json)),
            Path.GetTempPath(), "needs-you", scenario);

    private async Task Register()
    {
        var document = RecipeReaderTests.Reader.Read(ShippedRecipes.Read("sindri/workflow.step.yaml"), "workflow.step.yaml").Document!;   // why: the shipped recipes bind (ShippedRecipesTests)
        var text = Encoding.UTF8.GetString(AwJson.Serialize(document.Recipe with { Hash = null }, SliceHarness.Json));
        _ = await _recipes.Add(new RegisteredRecipe(document.Recipe.Id, document.Recipe.Version, document.Hash, text, Base: null, DateTimeOffset.UnixEpoch), TestContext.Current.CancellationToken);
    }

    private async Task<IReadOnlyList<NeedsYouRow>> Join(IReadOnlyList<NeedsYouRow> rows, SomeInstances instances) =>
        await Joins(instances, instances).Apply(rows, StatusCases.Now, TestContext.Current.CancellationToken);

    private NeedsYouJoins Joins(IInstanceDirectory directory, IInstanceReader reader) =>
        new(directory, reader, new RecipeRegistryReader(_recipes, new InMemoryRecipeApprovals(), EngineFixture.Catalog, SliceHarness.Json, new NoProfileFacts()), EngineFixture.Catalog);

    private static NeedsYouRow Desk(InstanceView view) => NeedsYouRows.ForDeskTask(Item(view), view.Address, _since, _plain);

    private static ItemRow Item(InstanceView view) => new(
        ItemId.From("ITM-11111111111111111111111111111111"), "the item", RepoName.From("agentic-workflow"), ItemState.Parked, Step: "produce", Version: 2, "human:thor", _since, _since, _since,
        Link: null, Alias: null, Body: null, WaitingOn: null, Instance: view.Address.Value, Owner: null, Sequence: 3);

    private sealed class NoProfileFacts : IRecipeProfileFacts
    {
        public ValueTask<Result<RecipeProfileFacts>> Current(CancellationToken cancellationToken) => throw new NotSupportedException("reading a recipe back needs no profile");
    }

    private sealed class ThrowingReader : IInstanceReader
    {
        public ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken) => throw new InvalidOperationException("Temporal is down");
    }

    private sealed class CountingReader(InstanceView view) : IInstanceDirectory, IInstanceReader
    {
        public int Gets { get; private set; }

        public ValueTask<Result<AttemptState?>> Latest(ItemKey item, Core.Workflow.Ids.RecipeId recipe, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<InstanceAddress>> Resolve(string reference, CancellationToken cancellationToken) => ValueTask.FromResult<Result<InstanceAddress>>(view.Address);

        public ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken)
        {
            Gets++;
            return ValueTask.FromResult<Result<InstanceView>>(view);
        }
    }
}
