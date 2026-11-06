using System.Globalization;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Testing.Views;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Views;

/// <summary>
/// What the engine will serve, asserted over EVERY instance fixture (every list row, instance view and status) from the recipe
/// declaration (<see cref="FixtureRecipes"/>), which carries what the recipe file carries: so a golden cannot show a state, child,
/// run token, timeout, subject or answer the engine would not produce. One check per rule, and each names every offending fixture.
/// </summary>
public sealed partial class WorkflowEngineInvariantsTests
{
    private sealed record View(string Address, InstanceRecipe Recipe, string State, DateTimeOffset Since, EngineOwnedBadge? EngineOwned, SubjectRef Subject,
        IReadOnlyList<StateCell> Cells, StateChip? Chip, InstanceDetail? Detail, IReadOnlyList<StatusFields> Statuses);

    // The shipped catalog says which Steps are deskCapable; the engine runs those at the desk until the agent runtime (WF 12, W20).
    private static readonly BlockCatalog _catalog = FixtureCatalog.Shipped();

    private static string M(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    private static List<View> Views()
    {
        var details = WorkflowViewFixtures.AllDetails();
        var statuses = WorkflowViewFixtures.AllStatuses();
        List<StatusFields> StatusesOf(string address) => [.. statuses.Where(s => s.Header.Address.Value == address)];
        var views = WorkflowViewFixtures.AllSummaries().Select(r =>
            new View(r.Address.Value, r.Recipe, r.State, r.Since, r.EngineOwned, r.Subject, r.States, r.Chip,
                details.FirstOrDefault(d => d.Address.Value == r.Address.Value), StatusesOf(r.Address.Value))).ToList();
        views.AddRange(details.Where(d => !views.Exists(v => v.Address == d.Address.Value))
            .Select(d => new View(d.Address.Value, d.Recipe, d.State, d.Since, d.EngineOwned, d.Subject, d.States, Chip: null, d, StatusesOf(d.Address.Value))));
        return views;
    }

    private static RecipeShape? ShapeOf(InstanceRecipe recipe) =>
        FixtureRecipes.All.SingleOrDefault(s => s.Id == recipe.Id.Value && s.Stamp.Hash.Value == recipe.Hash.Value);

    private static void Walk(Func<View, RecipeShape, IEnumerable<string>> check) => Walk(check, Views());

    private static void Walk(Func<View, RecipeShape, IEnumerable<string>> check, IEnumerable<View> views)
    {
        var failures = new List<string>();
        foreach (var view in views)
        {
            if (ShapeOf(view.Recipe) is not { } shape)
            {
                failures.Add(M($"{view.Address}: no declaration for {view.Recipe.Id.Value} at {view.Recipe.Hash.Value}"));
                continue;
            }

            failures.AddRange(check(view, shape).Select(f => M($"{view.Address} ({view.Recipe.Id.Value} @ {view.State}): {f}")));
        }

        Assert.True(failures.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    private static string Word(FactType type) => type switch
    {
        FactType.ScopeMap => "scope-map",
        FactType.Text or FactType.File or FactType.Item or FactType.Link or FactType.Packet or FactType.Evidence or FactType.Decision or FactType.Payload or FactType.Findings =>
            type.ToString().ToLowerInvariant(),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "no such fact type"),
    };

    private static ShapeState StateOf(View view, RecipeShape shape) => shape.State(view.State);

    private static StateCell CellOf(View view) => view.Cells.Single(c => c.State == view.State);

    [Fact, Trait("Lane", "unit")]
    public void Every_declared_state_carries_what_the_engine_requires_of_it()
    {
        var failures = FixtureRecipes.All.SelectMany(shape => shape.States.SelectMany(state => Requirements(M($"{shape.Id}/{state.Id}"), state))).ToList();
        Assert.True(failures.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    private static List<string> Requirements(string at, ShapeState state)
    {
        var failures = new List<string>();
        if (state.Kind != StateKind.Terminal && state.Timeout is null)
        {
            failures.Add(M($"{at}: an active or wait state has a timeout (WF 3.4)"));
        }

        if (state.RunsAtDesk && !state.Desk)
        {
            failures.Add(M($"{at}: a state of a recipe under recipes/ has no agent runtime until P8, so it is served as a desk task (WF 12, W5, W20)"));
        }

        failures.AddRange(AgentRuntime(at, state));
        if (state.Block is { } block && _catalog.Find(BlockId.From(block)) is not { DeskCapable: true })
        {
            failures.Add(M($"{at}: {block} is not deskCapable, so it parks with SND-WF-010 instead of running"));
        }

        if (state.Child != (state.ChildRecipe is not null) || state.Child != state.OnParked.HasValue)
        {
            failures.Add(M($"{at}: a child wait names its child recipe and onParked, and no other state does"));
        }

        return failures;
    }

    private static List<string> AgentRuntime(string at, ShapeState state) =>
        state.AgentBlock is not null && (state.Desk || state.Kind != StateKind.Active || state.Block is not null)
            ? [M($"{at}: an agent-runtime Block belongs to an active, non-desk state of a fixture-only recipe")]
            : [];

    [Fact, Trait("Lane", "unit")]
    public void Exactly_one_state_is_current_only_it_is_active_parked_or_escalated_earlier_states_are_done_and_later_ones_pending() => Walk(OneCurrent);

    private static List<string> OneCurrent(View view, RecipeShape shape)
    {
        var failures = new List<string>();
        var now = CellOf(view);
        var active = view.Cells.Where(c => c.Glyph == StateGlyph.Active).Select(c => c.State).ToList();
        if (now.Terminal ? active.Count != 0 || now.Glyph != StateGlyph.Done : !active.SequenceEqual([now.State], StringComparer.Ordinal))
        {
            failures.Add(M($"the active cells are [{string.Join(", ", active)}]"));
        }

        foreach (var other in view.Cells.Where(c => c.State != view.State))
        {
            failures.AddRange(OtherCell(now, other));
        }

        return failures;
    }

    private static List<string> OtherCell(StateCell now, StateCell other)
    {
        var failures = new List<string>();
        if (other.Badge is not null || other.Round is not null || other.Children is not null)
        {
            failures.Add(M($"{other.State} carries the badge, round or child progress of the current state"));
        }

        if (now.Order is { } at && other.Order is { } order && other.BranchOf is null && now.BranchOf is null && other.Glyph != (order < at ? StateGlyph.Done : StateGlyph.Pending))
        {
            failures.Add(M($"{other.State} (order {order}) is {other.Glyph} but the current state is order {at}"));
        }

        return failures;
    }

    [Fact, Trait("Lane", "unit")]
    public void The_subject_kind_is_the_effective_kind_of_the_recipes_first_input() => Walk(Subjects);

    private static List<string> Subjects(View view, RecipeShape shape)
    {
        var first = shape.Inputs[0];
        var expected = SubjectKinds.Effective(new RecipeInput(FactKey.From(first.Key), first.Label, Word(first.Type), first.Kind, Generate: null));
        return view.Subject.Kind == expected ? [] : [M($"subject kind '{view.Subject.Kind}', the first input {first.Key} gives '{expected}'")];
    }
}
