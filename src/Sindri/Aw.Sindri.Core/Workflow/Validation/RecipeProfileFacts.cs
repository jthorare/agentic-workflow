using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>What the validator reads from the approved profile, when there is one (spec §9.1, S1-D11).</summary>
/// <param name="EffectAllowlist">The profile's 'workflow.effects'; null when the key doesn't exist yet, and then RCP-EFFECT-001 reports 'skipped'.</param>
public sealed record RecipeProfileFacts(IReadOnlySet<BlockId>? EffectAllowlist)
{
    /// <summary>No profile facts: every profile-dependent rule is skipped.</summary>
    public static RecipeProfileFacts None { get; } = new(EffectAllowlist: null);
}
