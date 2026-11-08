using System.Globalization;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The §4.2 challenge of a protected answer on an instance (engine spec §4.2, §6.2): its action text is 'grant n
/// &lt;address&gt;', 'grant-tokens n &lt;address&gt;' or 'accept &lt;address&gt;', and it is bound to the instance's address
/// and version (spec §4.2 "address and version"): the version is the count of events it has mirrored, so a challenge
/// goes stale once anything else has happened to the instance. IssueChallenge issues it and ApplyCommand redeems it.
/// </summary>
public static class InstanceChallenge
{
    /// <summary>The action text the human types back for a protected answer.</summary>
    /// <param name="address">The instance.</param>
    /// <param name="answer">The variant.</param>
    /// <param name="amount">The grant's amount.</param>
    /// <returns>'grant 1 T-1/step/1', 'grant-tokens 100 T-1/step/1' or 'accept T-1/step/1'.</returns>
    public static ChallengeAction Action(InstanceAddress address, AnswerVariant answer, int? amount)
    {
        var n = amount?.ToString(CultureInfo.InvariantCulture) ?? "?";
        return new ChallengeAction(answer switch
        {
            AnswerVariant.Grant => $"grant {n} {address.Value}",
            AnswerVariant.GrantTokens => $"grant-tokens {n} {address.Value}",
            AnswerVariant.Accept => "accept " + address.Value,
            AnswerVariant.Text or AnswerVariant.Reject or AnswerVariant.Resume or _ =>
                throw new ArgumentOutOfRangeException(nameof(answer), answer, "only a grant, a token grant or an accept is protected"),
        });
    }

    /// <summary>What a protected answer's challenge is bound to: the instance's address and version as the preview showed them.</summary>
    /// <param name="view">The instance.</param>
    /// <returns>'instance:&lt;address&gt;@v&lt;version&gt;'.</returns>
    public static ChallengeSubject Subject(InstanceView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new ChallengeSubject(
            string.Create(CultureInfo.InvariantCulture, $"instance:{view.Address.Value}@v{view.Version}"),
            Digest: null);
    }
}
