using System.Collections.Frozen;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Questions;

/// <summary>
/// brief-scope: is a dispatch brief concrete and checkable ('ready'), present but too vague ('missing'), or does it need a
/// design decision first ('needs_design')? Read-only agent types and a skill's own internal dispatches are exempt
/// (ready); a blank goal or blank acceptance criteria is 'missing' without asking a model.
/// </summary>
public static class BriefScopeQuestion
{
    /// <summary>The question's name.</summary>
    public const string Name = "brief-scope";

    /// <summary>Concrete and checkable.</summary>
    public const string Ready = "ready";

    /// <summary>A required field is blank or too vague to act on.</summary>
    public const string Missing = "missing";

    /// <summary>Needs a design decision before it can be scoped.</summary>
    public const string NeedsDesign = "needs_design";

    /// <summary>Agent types that only read, so their briefs aren't scope-gated.</summary>
    public static FrozenSet<string> ExemptAgentTypes { get; } =
        FrozenSet.Create(StringComparer.Ordinal, "Explore", "claude-code-guide", "lean-researcher");

    /// <summary>The definition.</summary>
    public static QuestionDefinition<BriefScopeInput> Definition { get; } = new()
    {
        Name = Name,
        Outputs = [Ready, Missing, NeedsDesign],
        Criteria = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Ready] = "the brief is concrete and checkable",
            [Missing] = "a required field is present but too vague to act on",
            [NeedsDesign] = "a design decision is needed before this can be scoped at all",
        },
        ContentClass = ContentClass.Brief,
        Threshold = 0.6,
        TimeBudget = TimeSpan.FromSeconds(10),
        Prompt = Prompt,
        PreRules = PreRule,
    };

    internal static string? PreRule(BriefScopeInput input)
    {
        if (ExemptAgentTypes.Contains(input.AgentType) || input.SkillInternal == true)
        {
            return Ready;
        }

        return string.IsNullOrWhiteSpace(input.Goal) || string.IsNullOrWhiteSpace(input.AcceptanceCriteria) ? Missing : null;
    }

    private static string Prompt(BriefScopeInput input) => string.Join('\n',
    [
        $"A dispatch brief for agent type \"{input.AgentType}\":",
        $"Goal: {input.Goal}",
        $"Acceptance criteria: {input.AcceptanceCriteria}",
        $"Proof command: {input.ProofCommand}",
        "Reply {\"decision\":\"ready\"} if this is concrete and checkable, {\"decision\":\"missing\"} if a required field is present but too vague to act on, or {\"decision\":\"needs_design\"} if it needs a design decision before it can be scoped at all.",
    ]);
}
