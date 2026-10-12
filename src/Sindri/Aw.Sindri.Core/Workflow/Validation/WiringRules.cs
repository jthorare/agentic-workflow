using System.Globalization;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// The wiring rules (spec §9.1 "Inputs upstream", W2: checked on every path): RCP-WIRE-001/002, RCP-WHEN-001/002, RCP-OUTCOME-001 and RCP-CHILD-001.
/// Pure: they read the bound recipe, the catalog and the child resolver. A block, event or child that isn't found is
/// the block rules' (or RCP-CHILD-001's) to report, so these rules skip what they can't resolve; a state name the
/// recipe doesn't declare is the schema rules' to report. The findings come back ordered by where their node starts
/// in the file, then by rule.
/// </summary>
internal sealed partial class WiringRules : IRecipeRules
{
    private static readonly OutcomeName _answered = OutcomeName.From("answered");
    private static readonly OutcomeName _cancelled = OutcomeName.From("cancelled");

    public IEnumerable<RecipeFinding> Check(RecipeDocument document, ValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);
        var recipe = document.Recipe;
        var source = document.Source;
        IEnumerable<RecipeFinding> findings =
        [
            .. Upstream(recipe, context.Catalog, source),
            .. Produced(recipe, context, source),
            .. Conditions(recipe, context, source),
            .. Unhandled(recipe, context, source),
            .. Terminals(recipe, source),
            .. Children(recipe, context.Children, source),
        ];
        return
        [
            .. findings
                .OrderBy(f => f.Line)
                .ThenBy(f => f.Column)
                .ThenBy(f => f.Rule, StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// What a transition's trigger comes from (spec §3.5, §3.6): a Step's descriptor, an event descriptor, an answer
    /// ('answered', recording 'fact:answer'), a resource, a child (its terminal outcomes plus 'cancelled'), or a trigger
    /// that carries nothing ('step.failed', 'command.&lt;verb&gt;').
    /// </summary>
    /// <param name="Name">How findings name it: "step 'step.produce'", "event 'item.routed'".</param>
    /// <param name="Outcomes">The outcomes it reports; empty when it reports none.</param>
    /// <param name="Facts">The facts it records, which a transition may list in 'produces'.</param>
    private sealed record TriggerSource(string Name, IReadOnlyList<OutcomeName> Outcomes, IReadOnlyList<FactKey> Facts);

    /// <summary>The source of a transition from <paramref name="from"/> on <paramref name="on"/>, or null when it can't be resolved (an unknown state, block, event or child, or a trigger that doesn't fit the state, which other rules report).</summary>
    private static TriggerSource? SourceOf(Recipe recipe, string from, string on, ValidationContext context)
    {
        if (!recipe.States.TryGetValue(from, out var state) || state.Kind == StateKind.Terminal)
        {
            return null;
        }

        if (on.StartsWith("command.", StringComparison.Ordinal) || (state.Kind == StateKind.Active && on == "step.failed"))
        {
            return new TriggerSource($"trigger '{on}'", [], []);
        }

        return (state.Kind, on) switch
        {
            (StateKind.Active, "step.completed") => StepSource(state.Step, context.Catalog),
            (StateKind.Wait, "wait.satisfied") => WaitSource(state.WaitFor, context),
            _ => null,
        };
    }

    private static TriggerSource? StepSource(StepUse? step, BlockCatalog catalog) =>
        step is not null && catalog.Find(step.Use) is { Kind: BlockKind.Step } descriptor
            ? new TriggerSource($"step '{step.Use.Value}'", descriptor.Outcomes, [.. descriptor.Outputs.Select(o => o.Fact)])
            : null;

    private static TriggerSource? WaitSource(WaitFor? waitFor, ValidationContext context) => waitFor switch
    {
        { Event: { } kind } => context.Catalog.FindEvent(kind) is { } e ? new TriggerSource($"event '{kind.Value}'", e.Outcomes, e.Facts) : null,
        { Answer: { } answer } => new TriggerSource($"answer '{answer.Id.Value}'", [_answered], [FactKey.Answer]),
        { Resource: { } resource } => new TriggerSource($"resource '{ResourceName(resource)}'", [], []),
        { Child: { } child } => ChildSource(child, context.Children),
        _ => null,
    };

    private static TriggerSource? ChildSource(ChildWait child, IChildRecipeResolver children)
    {
        if (children.Resolve(child.Recipe) is not { } found)
        {
            return null;
        }

        var terminals = found.Recipe.States.Values
            .Where(s => s.Kind == StateKind.Terminal && s.Outcome is not null)
            .Select(s => s.Outcome.GetValueOrDefault());
        return new TriggerSource($"child '{child.Recipe.Value}'", [.. terminals.Append(_cancelled).Distinct()], []);
    }

    /// <summary>Each state a transition leaves from, with its trigger's source where that resolves.</summary>
    private static IEnumerable<(string From, TriggerSource Source)> Sources(Recipe recipe, RecipeTransition transition, ValidationContext context) =>
        transition.From.Distinct(StringComparer.Ordinal)
            .Select(from => (From: from, Source: SourceOf(recipe, from, transition.On, context)))
            .Where(s => s.Source is not null)
            .Select(s => (s.From, s.Source!)); // why: the Where above keeps only resolved sources

    private static string Inv(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string ResourceName(ResourceKind resource) => resource == ResourceKind.Heavy ? "heavy" : "slot";

    private static string List(IEnumerable<FactKey> facts) => string.Join(", ", facts.Select(f => f.Value));

    private static string List(IEnumerable<OutcomeName> outcomes) => string.Join(", ", outcomes.Select(o => o.Value));
}
