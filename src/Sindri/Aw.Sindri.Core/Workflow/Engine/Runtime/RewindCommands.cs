using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>What a parked run can go back to (#1270): one 'ApplyCommand:rewind' entry per state 'sindri workflow rewind' would accept now.</summary>
internal static class RewindCommands
{
    /// <summary>The rewind entries of <paramref name="instance"/>, the withheld transition's state first; none unless it is parked.</summary>
    /// <param name="instance">The interpreter's state.</param>
    /// <param name="recipe">The recipe it runs.</param>
    /// <returns>The entries, each with its CLI line.</returns>
    public static IEnumerable<AllowedCommand> Of(WorkflowInstance instance, InterpretedRecipe recipe) =>
        instance.Suspension is { Kind: SuspensionKind.Park }
            ? TransitionDecision.RewindTargets(recipe.Recipe, recipe.Catalog, instance).Select(state => new AllowedCommand("ApplyCommand:rewind", $"sindri workflow rewind {instance.Address.Value} --to {state}"))
            : [];
}
