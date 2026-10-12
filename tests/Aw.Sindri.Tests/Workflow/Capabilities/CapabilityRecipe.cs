using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Capabilities;

/// <summary>
/// Long-form recipes for the capability rules, and a run of those rules alone. <see cref="Base"/> mirrors
/// Workflow/Recipes/base/workflow.cap-base.yaml and <see cref="Generated"/> mirrors
/// Workflow/Recipes/valid/workflow.cap-generated.yaml; each invalid fixture is one change to the generated recipe, and a
/// test gives the positions the reader records for the nodes it reports.
/// </summary>
internal static class CapabilityRecipe
{
    /// <summary>The hash of Workflow/Recipes/base/workflow.cap-base.yaml, which the fixtures name in their 'base' (the end-to-end fixture test checks it).</summary>
    public static readonly RecipeHash BaseHash = RecipeHash.From("sha256:5aa84cb58a5a6a2c70fa4c9b3a1758625a9e78b625734ec55e49f2e762125b03");

    /// <summary>
    /// The blocks the fixtures name: a desk Step and two gates without capabilities, the effects 'effect.claim.ref'
    /// (Effects, WriteScope) and 'effect.notify.desk' (Effects) as P1 unit (e) declares them, the test-only
    /// 'effect.repo.push' (Effects, Network), whose 'remote' is a capability parameter and whose 'message' isn't, and
    /// the evidence provider 'evidence.tests' the fixtures' 'evidence: [{ kind: tests }]' needs.
    /// </summary>
    public static BlockCatalog Catalog { get; } = new(
    [
        TestBlocks.Step("step.produce"),
        TestBlocks.Gate("gate.check"),
        TestBlocks.Gate("gate.review") with { Rung = Rung.JudgeQuestion },
        new BlockDescriptor(BlockId.From("evidence.tests"), BlockKind.EvidenceProvider, Version: 1, Inputs: [], Outputs: [], Outcomes: [],
            Capability.None, Parameters: [], Rung: null, DeskCapable: false),
        Effect("effect.claim.ref", Capability.Effects | Capability.WriteScope),
        Effect("effect.notify.desk", Capability.Effects),
        Effect("effect.repo.push", Capability.Effects | Capability.Network) with
        {
            Parameters =
            [
                new ParameterSpec("remote", ParameterType.Text, Required: true, IsCapabilityField: true),
                new ParameterSpec("message", ParameterType.Text, Required: false, IsCapabilityField: false),
            ],
        },
    ],
    []);

    /// <summary>The base: produce (loop revisions, cap 3; budget 200k) -> publish -> running (child workflow.step) -> done, budget 400k.</summary>
    public static Recipe Base { get; } = Of(
        "workflow.cap-base",
        @base: null,
        review: 0.8,
        extraGateOnProduce: false,
        effects: [Push("origin"), Use("effect.notify.desk")],
        cap: 3,
        stateTokens: 200_000,
        recipeTokens: 400_000);

    /// <summary>The generated recipe at or below <see cref="Base"/>: a stricter threshold, an extra gate, one effect fewer, a lower cap and budgets.</summary>
    public static Recipe Generated { get; } = Of(
        "workflow.cap-generated",
        new RecipeRef(RecipeId.From("workflow.cap-base"), BaseHash),
        review: 0.9,
        extraGateOnProduce: true,
        effects: [Push("origin")],
        cap: 2,
        stateTokens: 100_000,
        recipeTokens: 300_000);

    public static BlockUse Push(string remote) =>
        new(BlockId.From("effect.repo.push"), new Dictionary<string, WithValue>(StringComparer.Ordinal) { ["remote"] = Literal(remote) });

    public static BlockUse Use(string effect) => new(BlockId.From(effect), new Dictionary<string, WithValue>(StringComparer.Ordinal));

    public static WithValue Literal(string text)
    {
        // why: the test texts are plain words, so quoting them makes a JSON string
        using var json = JsonDocument.Parse("\"" + text + "\"");
        return new WithValue(json.RootElement.Clone(), Fact: null, Profile: null);
    }

    /// <summary>Replaces the transition at <paramref name="index"/>.</summary>
    public static Recipe WithTransition(this Recipe recipe, int index, Func<RecipeTransition, RecipeTransition> change)
    {
        var transitions = recipe.Transitions.ToArray();
        transitions[index] = change(transitions[index]);
        return recipe with { Transitions = transitions };
    }

    /// <summary>Replaces the state <paramref name="name"/>.</summary>
    public static Recipe WithState(this Recipe recipe, string name, Func<RecipeState, RecipeState> change)
    {
        var states = new Dictionary<string, RecipeState>(recipe.States, StringComparer.Ordinal);
        states[name] = change(states[name]);
        return recipe with { States = states };
    }

    /// <summary>The capability rules' finding lines for <paramref name="recipe"/> against <paramref name="baseRecipe"/>, located with <paramref name="positions"/>.</summary>
    public static IReadOnlyList<string> Lines(
        Recipe recipe,
        Recipe? baseRecipe,
        string file,
        IEnumerable<(string Path, int Line, int Column)> positions)
    {
        var source = new RecipeSource(file, new SourcePositions(
            positions.ToDictionary(p => p.Path, p => new SourcePosition(p.Line, p.Column), StringComparer.Ordinal)));
        var document = new RecipeDocument(recipe, RecipeHash.Of("{}"u8), source);
        var context = new ValidationContext(Catalog, RecipeProfileFacts.None, InMemoryChildRecipes.None, baseRecipe);
        return [.. new CapabilityRules().Check(document, context).Select(f => f.ToLine())];
    }

    /// <summary>The lines for a recipe without positions: every finding is at the start of the file.</summary>
    public static IReadOnlyList<string> Lines(Recipe recipe, Recipe? baseRecipe) => Lines(recipe, baseRecipe, "r.yaml", []);

    private static BlockDescriptor Effect(string id, Capability capabilities) => new(
        BlockId.From(id), BlockKind.Effect, Version: 1, Inputs: [], Outputs: [], Outcomes: [], capabilities, Parameters: [],
        Rung: null, DeskCapable: false);

    private static Recipe Of(
        string id,
        RecipeRef? @base,
        double review,
        bool extraGateOnProduce,
        IReadOnlyList<BlockUse> effects,
        int cap,
        int stateTokens,
        int recipeTokens) => ARecipe.Minimal(RecipeId.From(id)) with
        {
            Base = @base,
            States = new Dictionary<string, RecipeState>(StringComparer.Ordinal)
            {
                ["produce"] = Active("revisions", new BudgetSpec(stateTokens)),
                ["publish"] = Active(loop: null, budget: null),
                ["running"] = new(StateKind.Wait, Step: null,
                    new WaitFor(Event: null, Answer: null, Resource: null,
                        new ChildWait(RecipeId.From("workflow.step"), new Dictionary<FactKey, WithValue>(), OnParked.Propagate)),
                    Timeout(), Loop: null, Budget: null, "Waiting for {child}.", Outcome: null),
                ["done"] = new(StateKind.Terminal, Step: null, WaitFor: null, Timeout: null, Loop: null, Budget: null, Next: null, OutcomeName.From("resolved")),
            },
            Transitions =
            [
                Go("produce", "publish") with
                {
                    Gates = extraGateOnProduce ? [Gate("gate.review", review), Gate("gate.check")] : [Gate("gate.review", review)],
                    Evidence = [new EvidenceCheck("tests", EvidencePoint.Head, EvidenceExpectation.Pass)],
                },
                Go("produce", "produce", "step.failed"),
                Go("publish", "running") with { Gates = [Gate("gate.check")], Effects = effects },
                Go("running", "done", "wait.satisfied"),
            ],
            Loops = new Dictionary<string, LoopSpec>(StringComparer.Ordinal) { ["revisions"] = new(cap, LoopSpec.DefaultNoProgress) },
            Budget = new BudgetSpec(recipeTokens),
        };

    public static GateUse Gate(string id, double? threshold = null) =>
        new(BlockId.From(id), threshold, new Dictionary<string, WithValue>(StringComparer.Ordinal));

    public static RecipeTransition Go(string from, string to, string on = "step.completed") => new([from], to, on, When: null, [], [], [], []);

    private static RecipeState Active(string? loop, BudgetSpec? budget) => new(
        StateKind.Active,
        new StepUse(BlockId.From("step.produce"), new Dictionary<string, WithValue>(StringComparer.Ordinal), []),
        WaitFor: null, Timeout(), loop, budget, "Produce the Step output.", Outcome: null);

    private static TimeoutSpec Timeout() => new(RecipeDuration.From("4h"), TimeoutAction.Park);
}
