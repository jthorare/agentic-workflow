using System.Globalization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Challenges;

/// <summary>
/// The action text a challenge is for, which the human types back at a TTY (engine spec §4.2): 'approve workflow.step 1',
/// 'revoke workflow.step@9ab2d40c1e77', 'grant 1 T-1/step/1'. A request's verb and arguments must equal it.
/// </summary>
/// <param name="Text">The action text.</param>
public sealed record ChallengeAction(string Text)
{
    /// <summary>'approve &lt;id&gt; &lt;version&gt;'.</summary>
    /// <param name="id">The recipe id.</param>
    /// <param name="version">Its version.</param>
    /// <returns>The action.</returns>
    public static ChallengeAction Approve(RecipeId id, int version) =>
        new(string.Create(CultureInfo.InvariantCulture, $"approve {id.Value} {version}"));

    /// <summary>'revoke &lt;id&gt;@&lt;hash12&gt;'.</summary>
    /// <param name="id">The recipe id.</param>
    /// <param name="hash">The hash to revoke.</param>
    /// <returns>The action.</returns>
    public static ChallengeAction Revoke(RecipeId id, RecipeHash hash) => new($"revoke {id.Value}@{hash.Display}");

    /// <summary>'approve-all &lt;digest6&gt;' (setup-once spec §5.3): one hold approves the shipped recipes of a plan.</summary>
    /// <param name="digest">The plan's digest (six hex characters).</param>
    /// <returns>The action.</returns>
    public static ChallengeAction ApproveAll(string digest) => new("approve-all " + digest);
}
