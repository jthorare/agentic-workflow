using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The recipe verbs of 'allowedCommands'.</summary>
public static partial class InstanceViews
{
    /// <summary>
    /// The recipe's verbs out of the current state, one entry per distinct verb in recipe order, all the generic 'ApplyCommand:command' (one desk row serves every verb).
    /// A verb whose transition carries a human-trigger effect (the babysitter's 'merge') is listed too, marked destructive: the server refuses an agent session's call. 'rework' carries no effect and is not.
    /// </summary>
    private static IEnumerable<AllowedCommand> VerbCommands(WorkflowInstance instance, InterpretedRecipe recipe) =>
        recipe.Recipe.Transitions
            .Where(t => t.On.StartsWith(TriggerNames.CommandPrefix, StringComparison.Ordinal) && t.From.Contains(instance.State, StringComparer.Ordinal))
            .GroupBy(static t => t.On[TriggerNames.CommandPrefix.Length..], StringComparer.Ordinal)
            .Select(verb => new AllowedCommand("ApplyCommand:command", $"sindri workflow command {instance.Address.Value} {verb.Key}")
            {
                Verb = verb.Key,
                Destructive = verb.Any(t => t.Effects.Any(e => recipe.Catalog.Find(e.Use) is { Kind: BlockKind.Effect, HumanTrigger: true })) ? true : null,
            });
}
