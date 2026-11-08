using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>Builds instances, triggers and contexts for the decision tests, over the shipped workflow.step and workflow.item.</summary>
internal static class EngineFixture
{
    private static readonly Lazy<ChildRecipe> _step = new(() => Read("sindri/workflow.step.yaml"));
    private static readonly Lazy<ChildRecipe> _item = new(() => Read("sindri/workflow.item.yaml"));

    public static DateTimeOffset Now { get; } = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    public static WorkflowInstanceId Id { get; } = WorkflowInstanceId.From("WFI-0123456789abcdef0123456789abcdef");

    public static WorkflowInstanceId ChildId { get; } = WorkflowInstanceId.From("WFI-fedcba9876543210fedcba9876543210");

    /// <summary>The shipped workflow.step.</summary>
    public static Recipe Step => _step.Value.Recipe;

    /// <summary>The shipped workflow.item.</summary>
    public static Recipe Item => _item.Value.Recipe;

    /// <summary>The shipped catalog plus a deterministic 'gate.test.check', a top-rung 'gate.test.model' and 'step.test.agent', which isn't desk-capable.</summary>
    public static BlockCatalog Catalog { get; } = WithTestGates();

    /// <summary>A context with the recipe approved and no runtimes but those given.</summary>
    public static DecisionContext Context(params string[] runtimes) => new(
        ApprovalCheck.Holds, Catalog, runtimes.ToDictionary(BlockId.From, static _ => StepRuntime.Agent), new InMemoryChildRecipes().With(_step.Value));

    /// <summary>An instance of <paramref name="recipe"/> in <paramref name="state"/> at entry <paramref name="entry"/>, its Step on run '{entry}.{run}'.</summary>
    public static WorkflowInstance At(Recipe recipe, string state, int entry = 1, int run = 1)
    {
        var target = recipe.States[state];
        var open = target.Step is null ? null : new OpenRun(RunToken.For(entry, run), StepRuntime.Desk, Launch: null, Task: "desk-1");
        var children = recipe.Id == Item.Id
            ? new Dictionary<RecipeId, RecipeHash> { [Step.Id] = _step.Value.Hash }
            : [];
        var loops = target.Loop is { } name
            ? new Dictionary<string, LoopState>(StringComparer.Ordinal) { [name] = new(1, recipe.Loops[name].Cap, Last: null) }
            : new Dictionary<string, LoopState>(StringComparer.Ordinal);
        return new WorkflowInstance(
            Id, InstanceAddress.Root(ItemKey.From("T-3"), recipe.Id, 1), SindriProfile.DefaultTenant, Ring: 0,
            new PinnedRecipe(recipe.Id, recipe.Version, recipe.Hash ?? RecipeHash.From("sha256:" + new string('0', 64)), children),
            ItemKey.From("T-3"), Attempt: 1, Parent: null,
            new Frame(state, entry, open, Child: null),
            Suspension: null, PendingPark: null, loops, TokenSpend.None, new Dictionary<FactKey, FactValue>(), Outcome: null);
    }

    /// <summary>A Step report for run '{entry}.{run}'.</summary>
    public static StepCompleted Completed(int entry, string outcome, params (string Fact, string Text)[] outputs) =>
        Completed(entry, 1, outcome, outputs);

    /// <summary>A Step report for run '{entry}.{run}'.</summary>
    public static StepCompleted Completed(int entry, int run, string outcome, params (string Fact, string Text)[] outputs) => new(
        "human:thor", RunToken.For(entry, run), OutcomeName.From(outcome),
        outputs.ToDictionary(o => FactKey.From(o.Fact), o => Fact(o.Text)), Tokens: 1200, StepRuntime.Desk, Sha(string.Create(CultureInfo.InvariantCulture, $"{outcome}{entry}.{run}")));

    /// <summary>An event wait's satisfaction from the router.</summary>
    public static WaitSatisfied Routed(string? outcome, long sequence = 41, params (string Fact, string Text)[] outputs) => new(
        "service:workflow.inbox", new EventWaitSource(EventKind.From("item.routed"), sequence),
        outcome is null ? null : OutcomeName.From(outcome), outputs.ToDictionary(o => FactKey.From(o.Fact), o => Fact(o.Text)));

    /// <summary>A stored fact for <paramref name="text"/>.</summary>
    public static FactValue Fact(string text) => new("blob:" + Sha(text), Sha(text), HeadSha: null);

    /// <summary>Gate answers.</summary>
    public static GateAnswers Gates(params (string Gate, GateVerdict Verdict, double? Score)[] answers) => new(
        answers.ToDictionary(a => BlockId.From(a.Gate), a => new GateResult(a.Verdict, a.Score, [$"{a.Gate} said {a.Verdict}"])),
        new Dictionary<EvidenceCheck, EvidenceResult>());

    /// <summary>Decides through <paramref name="rules"/> (a <see cref="FakeEngineRules"/> by default).</summary>
    public static Decision Decide(WorkflowInstance instance, Recipe recipe, Trigger trigger, GateAnswers? answers = null, DecisionContext? context = null, IEngineRules? rules = null) =>
        TransitionDecision.Decide(new DecisionInput(instance, recipe, trigger, answers ?? GateAnswers.None, context ?? Context()), rules ?? new FakeEngineRules());

    /// <summary>A recipe from <see cref="ARecipe.Minimal"/> with other transitions, states and loops.</summary>
    public static Recipe Minimal(IReadOnlyList<RecipeTransition> transitions, IReadOnlyDictionary<string, LoopSpec>? loops = null, string? loop = null)
    {
        var recipe = ARecipe.Minimal(RecipeId.From("workflow.test"));
        var states = new Dictionary<string, RecipeState>(recipe.States, StringComparer.Ordinal)
        {
            ["produce"] = recipe.States["produce"] with { Loop = loop },
            ["dropped"] = recipe.States["done"] with { Outcome = OutcomeName.From("unresolved") },
        };
        return recipe with { States = states, Transitions = transitions, Loops = loops ?? recipe.Loops };
    }

    /// <summary>A transition from produce on step.completed.</summary>
    public static RecipeTransition From(string to, WhenClause? when = null, params string[] gates) => new(
        ["produce"], to, "step.completed", when, [.. gates.Select(g => new GateUse(BlockId.From(g), Threshold: g == "gate.test.model" ? 0.8 : null, new Dictionary<string, WithValue>(StringComparer.Ordinal)))],
        [], [], []);

    public static string Sha(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static ChildRecipe Read(string path)
    {
        var document = RecipeComposition.ReadClean(ShippedRecipes.Read(path), Path.GetFileName(path));
        return new ChildRecipe(document.Recipe with { Hash = document.Hash }, document.Hash);
    }

    private static BlockCatalog WithTestGates()
    {
        var shipped = FixtureCatalog.Shipped();
        var model = TestBlocks.Gate("gate.test.model") with { Rung = Rung.BoundedModel };
        var agentOnly = TestBlocks.Step("step.test.agent") with { DeskCapable = false };
        return new BlockCatalog([.. shipped.Blocks, TestBlocks.Gate("gate.test.check"), model, agentOnly], shipped.Events);
    }
}
