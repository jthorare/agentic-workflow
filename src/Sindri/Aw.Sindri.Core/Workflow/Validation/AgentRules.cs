using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// RCP-AGENT-001..003 (#1269): a state's named agent is approved for unattended Steps (skipped without profile facts, like RCP-EFFECT-001), a 'differentFrom' names another state that runs an
/// agent Step, and 'agent:' holds an agent or a state to differ from, never both.
/// </summary>
internal sealed class AgentRules : IRecipeRules
{
    /// <inheritdoc/>
    public IEnumerable<RecipeFinding> Check(RecipeDocument document, ValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);
        return document.Recipe.States.SelectMany(s => s.Value.Agent is { } agent ? Of(s.Key, agent, document, context) : []);
    }

    private static IEnumerable<RecipeFinding> Of(string name, StateAgent agent, RecipeDocument document, ValidationContext context)
    {
        var source = document.Source;
        if ((agent.Use is null) == (agent.DifferentFrom is null))
        {
            yield return source.Finding("RCP-AGENT-003", $"states.{name}.agent", $"state '{name}' must name an agent or a state to differ from, not both and not neither", "write 'agent: <id>' or 'agent: { differentFrom: <state> }'");
            yield break;
        }

        if (agent.Use is { } use && !Approved(use, context.Profile))
        {
            yield return context.Profile.StepAgents is null
                ? source.Finding("RCP-AGENT-001", $"states.{name}.agent", $"agent '{use}' wasn't checked against the agents approved for Steps, since there is no approved profile", "approve a profile; until then the launch refuses an agent that isn't approved", FindingSeverity.Skipped)
                : source.Finding("RCP-AGENT-001", $"states.{name}.agent", $"agent '{use}' isn't approved for Steps", $"run `sindri launch show {use}`, put its argvHash under agents.{use}.stepArgvHash in the profile and approve it, or name another agent (stepArgvHash)");
        }

        if (agent.DifferentFrom is { } other)
        {
            var finding = Differs(name, other, document, context);
            if (finding is not null)
            {
                yield return finding;
            }
        }
    }

    // Approved when the profile's Step agents hold it; without profile facts nothing is known, and the finding is a skip.
    private static bool Approved(string use, RecipeProfileFacts profile) => profile.StepAgents?.Contains(use) ?? false;

    private static RecipeFinding? Differs(string name, string other, RecipeDocument document, ValidationContext context)
    {
        var source = document.Source;
        var path = $"states.{name}.agent.differentFrom";
        if (string.Equals(name, other, StringComparison.Ordinal))
        {
            return source.Finding("RCP-AGENT-003", $"states.{name}.agent", $"state '{name}' can't differ from itself", "name an earlier state that runs an agent Step");
        }

        return document.Recipe.States.TryGetValue(other, out var target) && target.Step is { } step && context.Catalog.Find(step.Use) is not { Runtime: BlockRuntime.Block }   // why: a Step with no default runtime may be run by an agent
            ? null
            : source.Finding("RCP-AGENT-002", path, $"state '{name}' differs from '{other}', which is not a state that runs an agent Step", "name a state of this recipe that runs an agent Step");
    }
}
