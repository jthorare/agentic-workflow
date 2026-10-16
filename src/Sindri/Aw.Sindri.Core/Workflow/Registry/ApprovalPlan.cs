using Aw.Sindri.Core.Challenges;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>What approving a registered recipe would do (engine spec §4.1, §4.2): the preview, and the challenge action and subject it is confirmed by.</summary>
/// <param name="Recipe">The registered recipe.</param>
/// <param name="Preview">The preview the human reads.</param>
/// <param name="Latest">The latest approval of the same hash, for a re-approval (drift or a newer child); else null.</param>
/// <param name="Action">'approve &lt;id&gt; &lt;version&gt;'.</param>
/// <param name="Subject">The recipe hash and version, plus the pins digest (every approval binds it).</param>
public sealed record ApprovalPlan(RegisteredRecipe Recipe, ApprovalPreview Preview, RecipeApproval? Latest, ChallengeAction Action, ChallengeSubject Subject)
{
    /// <summary>Whether this approval re-approves an approved hash with new pins (a drift approval, spec §4.1).</summary>
    public bool Reapproval => Latest is not null;

    /// <summary>
    /// Whether the hash already stands approved with exactly these pins: nothing is recorded, and the approval only sends its
    /// wake again (the remedy for an instance a first wake missed).
    /// </summary>
    public bool AlreadyApproved => Latest is { } latest && latest.Pins == Preview.Pins;
}
