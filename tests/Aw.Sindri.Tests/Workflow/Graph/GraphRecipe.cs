using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Graph;

/// <summary>
/// Long-form recipes for the graph rules, and a run of those rules alone. The invalid fixtures under
/// Workflow/Recipes/invalid mirror these recipes; a test gives the positions the reader records for the nodes it reports.
/// </summary>
internal static class GraphRecipe
{
    public static RecipeState Active(string? loop = null, string next = "Produce the Step output.", TimeoutAction? then = TimeoutAction.Park) => new(
        StateKind.Active,
        new StepUse(BlockId.From("step.produce"), new Dictionary<string, WithValue>(StringComparer.Ordinal), []),
        WaitFor: null, Timeout(then), loop, Budget: null, next, Outcome: null);

    public static RecipeState WaitForEvent(string kind, string next = "Waiting for the event.", TimeoutAction? then = TimeoutAction.Park) => new(
        StateKind.Wait, Step: null, new WaitFor(EventKind.From(kind), Answer: null, Resource: null, Child: null), Timeout(then),
        Loop: null, Budget: null, next, Outcome: null);

    public static RecipeState WaitForAnswer(string id = "why", string question = "What failed?") => new(
        StateKind.Wait, Step: null, new WaitFor(Event: null, new AnswerWait(AnswerId.From(id), question), Resource: null, Child: null),
        Timeout(TimeoutAction.Park), Loop: null, Budget: null, "Waiting for your answer.", Outcome: null);

    public static RecipeState WaitForChild(string recipe, TimeoutAction then = TimeoutAction.Park) => new(
        StateKind.Wait, Step: null,
        new WaitFor(Event: null, Answer: null, Resource: null,
            new ChildWait(RecipeId.From(recipe), new Dictionary<FactKey, WithValue>(), OnParked.Propagate)),
        Timeout(then), Loop: null, Budget: null, "Waiting for {child}.", Outcome: null);

    public static RecipeState Terminal(string outcome = "resolved") =>
        new(StateKind.Terminal, Step: null, WaitFor: null, Timeout: null, Loop: null, Budget: null, Next: null, OutcomeName.From(outcome));

    public static RecipeTransition Go(string from, string to, string on = "step.completed") => new([from], to, on, When: null, [], [], [], []);

    public static Recipe Of(
        string initial,
        IEnumerable<(string Name, RecipeState State)> states,
        IEnumerable<RecipeTransition> transitions,
        IEnumerable<(string Name, LoopSpec Loop)>? loops = null,
        string id = "workflow.graph") =>
        ARecipe.Minimal(RecipeId.From(id)) with
        {
            Initial = initial,
            States = states.ToDictionary(s => s.Name, s => s.State, StringComparer.Ordinal),
            Transitions = [.. transitions],
            Loops = (loops ?? []).ToDictionary(l => l.Name, l => l.Loop, StringComparer.Ordinal),
        };

    /// <summary>A parent that waits on <paramref name="child"/> in 'running', then ends in 'done'.</summary>
    public static Recipe Parent(string id, string child) => Of(
        "running",
        [("running", WaitForChild(child)), ("done", Terminal())],
        [Go("running", "done", "wait.satisfied")],
        id: id);

    /// <summary>The graph rules' finding lines for <paramref name="recipe"/>, located with <paramref name="positions"/>.</summary>
    public static IReadOnlyList<string> Lines(
        Recipe recipe,
        string file,
        IEnumerable<(string Path, int Line, int Column)> positions,
        IChildRecipeResolver? children = null)
    {
        var source = new RecipeSource(file, new SourcePositions(
            positions.ToDictionary(p => p.Path, p => new SourcePosition(p.Line, p.Column), StringComparer.Ordinal)));
        var document = new RecipeDocument(recipe, RecipeHash.Of("{}"u8), source);
        var context = new ValidationContext(BlockCatalog.Empty, RecipeProfileFacts.None, children ?? InMemoryChildRecipes.None, BaseRecipe: null);
        return [.. new GraphRules().Check(document, context).Select(f => f.ToLine())];
    }

    /// <summary>The lines for a recipe without positions: every finding is at the start of the file.</summary>
    public static IReadOnlyList<string> Lines(Recipe recipe, IChildRecipeResolver? children = null) => Lines(recipe, "r.yaml", [], children);

    /// <summary>A child recipe the resolver hands back.</summary>
    public static ChildRecipe Child(Recipe recipe) => new(recipe, RecipeHash.Of("{}"u8));

    private static TimeoutSpec? Timeout(TimeoutAction? then) => then is { } t ? new TimeoutSpec(RecipeDuration.From("4h"), t) : null;
}
