using System.Globalization;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// The block and event rules (spec §9.1 "Blocks registered", "Allowlisted effects"): RCP-BLOCK-001–003, RCP-EVID-001, RCP-EVENT-001, RCP-THRESH-001/002, RCP-EFFECT-001 (skipped without the profile's allowlist) and RCP-RES-001 (information).
/// Pure: they read the bound recipe, the catalog and the profile's facts. A block is checked where it is named: a
/// state's Step, a Step's inputs, a transition's gates and effects. The findings come back ordered by where their
/// node starts in the file, then by rule.
/// </summary>
internal sealed partial class BlockRules : IRecipeRules
{
    public IEnumerable<RecipeFinding> Check(RecipeDocument document, ValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);
        var recipe = document.Recipe;
        var source = document.Source;
        var catalog = context.Catalog;
        IEnumerable<RecipeFinding> findings =
        [
            .. References(recipe).SelectMany(r => Registered(r, catalog, source)),
            .. Thresholds(recipe, catalog, source),
            .. Evidence(recipe, catalog, source),
            .. Events(recipe, catalog, source),
            .. Effects(recipe, catalog, context.Profile, source),
            .. Resources(recipe, source),
        ];
        return
        [
            .. findings
                .OrderBy(f => f.Line)
                .ThenBy(f => f.Column)
                .ThenBy(f => f.Rule, StringComparer.Ordinal),
        ];
    }

    /// <summary>A block the recipe names, where it names it, and the kind that place takes.</summary>
    /// <param name="Path">The node of the use: 'states.produce.step', 'transitions[0].gates[1]'.</param>
    /// <param name="Id">The block.</param>
    /// <param name="Place">The kind of block the place takes.</param>
    /// <param name="With">Its arguments.</param>
    private sealed record BlockReference(string Path, BlockId Id, BlockKind Place, IReadOnlyDictionary<string, WithValue> With);

    private static IEnumerable<BlockReference> References(Recipe recipe)
    {
        foreach (var (name, state) in recipe.States)
        {
            if (state.Step is not { } step)
            {
                continue;
            }

            yield return new BlockReference($"states.{name}.step", step.Use, BlockKind.Step, step.With);
            for (var k = 0; k < step.Inputs.Count; k++)
            {
                yield return new BlockReference($"states.{name}.step.inputs[{Inv(k)}]", step.Inputs[k].Use, BlockKind.StepInput, step.Inputs[k].With);
            }
        }

        for (var i = 0; i < recipe.Transitions.Count; i++)
        {
            var transition = recipe.Transitions[i];
            for (var j = 0; j < transition.Gates.Count; j++)
            {
                yield return new BlockReference($"transitions[{Inv(i)}].gates[{Inv(j)}]", transition.Gates[j].Use, BlockKind.Gate, transition.Gates[j].With);
            }

            for (var j = 0; j < transition.Effects.Count; j++)
            {
                yield return new BlockReference($"transitions[{Inv(i)}].effects[{Inv(j)}]", transition.Effects[j].Use, BlockKind.Effect, transition.Effects[j].With);
            }
        }
    }

    /// <summary>RCP-BLOCK-002 (a block of another kind than its place takes), RCP-BLOCK-001 (not registered), then RCP-BLOCK-003 (its parameters).</summary>
    private static IEnumerable<RecipeFinding> Registered(BlockReference reference, BlockCatalog catalog, RecipeSource source)
    {
        var id = reference.Id.Value;
        var kind = BlockKinds.Of(reference.Id);
        if (kind != reference.Place)
        {
            return
            [
                source.Finding(
                    "RCP-BLOCK-002", $"{reference.Path}.use", $"'{id}' is {Article(kind)} block, but {Takes(reference.Place)}",
                    $"name {Article(reference.Place)} block here ({Word(reference.Place)}.<area>.<name>)"),
            ];
        }

        return catalog.Find(reference.Id) is { } descriptor
            ? Parameters(reference, descriptor, source).Concat(ProviderLists(reference, descriptor, source)).Concat(CallWidths(reference, descriptor, source)).Concat(Bundles(reference, descriptor, source))
            : [source.Finding("RCP-BLOCK-001", $"{reference.Path}.use", $"block '{id}' isn't registered", $"name a registered {Word(kind)} block, or fix the id")];
    }

    private static IEnumerable<RecipeFinding> Evidence(Recipe recipe, BlockCatalog catalog, RecipeSource source) =>
        recipe.Transitions.SelectMany((t, i) => t.Evidence.SelectMany((e, j) => EvidenceFindings(e, $"transitions[{Inv(i)}].evidence[{Inv(j)}]", catalog, source)));

    private static IEnumerable<RecipeFinding> EvidenceFindings(EvidenceCheck check, string path, BlockCatalog catalog, RecipeSource source)
    {
        var provider = $"evidence.{check.Kind}";
        if (!BlockId.TryFrom(provider, out var id) || catalog.Find(id) is not { Kind: BlockKind.EvidenceProvider })
        {
            yield return source.Finding(
                "RCP-EVID-001", $"{path}.kind", $"evidence kind '{check.Kind}' has no registered provider ({provider})",
                $"name a kind a registered evidence provider declares, or register {provider}");
        }

        if (!Enum.IsDefined(check.At))
        {
            yield return source.Finding(
                "RCP-EVID-001", $"{path}.at", $"evidence point '{((int)check.At).ToString(CultureInfo.InvariantCulture)}' isn't base or head",
                "use at: base or at: head");
        }

        if (!Enum.IsDefined(check.Expect))
        {
            yield return source.Finding(
                "RCP-EVID-001", $"{path}.expect", $"evidence expectation '{((int)check.Expect).ToString(CultureInfo.InvariantCulture)}' isn't pass or fail",
                "use expect: pass or expect: fail");
        }
    }

    private static IEnumerable<RecipeFinding> Events(Recipe recipe, BlockCatalog catalog, RecipeSource source)
    {
        var registered = string.Join(", ", catalog.Events.Select(e => e.Kind.Value).Order(StringComparer.Ordinal));
        return recipe.States
            .Where(s => s.Value.WaitFor?.Event is { } kind && catalog.FindEvent(kind) is null)
            .Select(s => source.Finding(
                "RCP-EVENT-001",
                $"states.{s.Key}.waitFor.event",
                $"event '{s.Value.WaitFor?.Event?.Value}' isn't registered",
                registered.Length == 0 ? "register it with AddEvent" : $"wait for a registered event: {registered}"));
    }

    /// <summary>RCP-EFFECT-001 over the registered effects: skipped without the profile's 'workflow.effects' (S1-D11), an error off it.</summary>
    private static IEnumerable<RecipeFinding> Effects(Recipe recipe, BlockCatalog catalog, RecipeProfileFacts profile, RecipeSource source) =>
        recipe.Transitions
            .SelectMany((t, i) => t.Effects.Select((e, j) => (Path: $"transitions[{Inv(i)}].effects[{Inv(j)}].use", e.Use)))
            .Where(e => catalog.Find(e.Use) is { Kind: BlockKind.Effect } && !(profile.EffectAllowlist?.Contains(e.Use) ?? false))
            .Select(e => profile.EffectAllowlist is null
                ? source.Finding(
                    "RCP-EFFECT-001", e.Path, $"effect '{e.Use.Value}' wasn't checked against an effect allowlist, since the profile has no workflow.effects",
                    "add workflow.effects to the profile; until then the effect is refused at run time", FindingSeverity.Skipped)
                : source.Finding(
                    "RCP-EFFECT-001", e.Path, $"effect '{e.Use.Value}' isn't on the profile's effect allowlist",
                    "add it to workflow.effects and approve the profile, or remove it"));

    /// <summary>RCP-RES-001: information, since the session-slot resource has only a stub allocator in this build (P7); the heavy resource is allocated over the heavy-job lock (LN-WF3).</summary>
    private static IEnumerable<RecipeFinding> Resources(Recipe recipe, RecipeSource source) =>
        recipe.States
            .Where(s => s.Value.WaitFor?.Resource is ResourceKind.Slot)
            .Select(s => Resource(source, s.Key, "slot", "it parks with SND-WF-010 until the session-slot stub lands (P7)"));

    private static RecipeFinding Resource(RecipeSource source, string state, string name, string fix) => source.Finding(
        "RCP-RES-001", $"states.{state}.waitFor.resource", $"resource '{name}' has no allocator in this build", fix, FindingSeverity.Information);

    private static string Word(BlockKind kind) => kind switch
    {
        BlockKind.Gate => "gate",
        BlockKind.Step => "step",
        BlockKind.Effect => "effect",
        BlockKind.EvidenceProvider => "evidence",
        BlockKind.StepInput => "input",
        _ => "unknown",
    };

    private static string Article(BlockKind kind) => kind is BlockKind.Gate or BlockKind.Step ? $"a {Word(kind)}" : $"an {Word(kind)}";

    private static string Inv(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Takes(BlockKind place) => place switch
    {
        BlockKind.Step => "a state's step takes step blocks",
        BlockKind.StepInput => "a Step's inputs take input blocks",
        BlockKind.Gate => "a transition's gates take gate blocks",
        BlockKind.Effect => "a transition's effects take effect blocks",
        BlockKind.EvidenceProvider => "a transition's evidence takes evidence kinds",
        _ => "this place takes another kind of block",
    };
}
