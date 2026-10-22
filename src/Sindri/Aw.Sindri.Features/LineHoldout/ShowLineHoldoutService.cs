using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Prove;

namespace Aw.Sindri.Features.LineHoldout;

/// <summary>
/// Reads a phase's hidden scenarios for the owner, behind the typed confirmation, and marks what it showed spent (line spec §9.2, LN-API5). The preview gives counts and the challenge
/// 'line holdout show &lt;line&gt; &lt;phase&gt;', bound to the set's hash so a replaced set makes it stale; an answer that redeems it shows the text. The scenarios are marked spent before
/// any text is returned: a store that can't record the mark shows nothing. The canary is neither shown nor counted. The chain has already refused a caller that isn't the human at a terminal.
/// </summary>
/// <param name="store">The holdout store.</param>
/// <param name="challenges">The process's open challenges.</param>
internal sealed class ShowLineHoldoutService(IHoldoutStore store, IChallenges challenges) : IService<ShowLineHoldoutRequest, ShowLineHoldoutResponse>
{
    public async ValueTask<Result<ShowLineHoldoutResponse>> Handle(ShowLineHoldoutRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var set = await store.Set(request.Line, request.Phase, cancellationToken).ConfigureAwait(false);
        if (set.IsError)
        {
            return set.Error;
        }

        var spent = await store.Spent(request.Line, request.Phase, cancellationToken).ConfigureAwait(false);
        if (spent.IsError)
        {
            return spent.Error;
        }

        var hidden = set.Value.Scenarios.Where(static s => !s.IsCanary).ToList();
        var action = new ChallengeAction($"line holdout show {request.Line} {request.Phase}");
        var subject = new ChallengeSubject($"holdout:{request.Line}/{request.Phase}:{set.Value.Hash}", Digest: null);
        var counts = new ShowLineHoldoutResponse(request.Line, request.Phase, set.Value.Hash, hidden.Count, hidden.Count(s => spent.Value.Contains(s.Key)), Scenarios: null, Challenge: null);
        if (request.Answer is null)
        {
            var issued = challenges.Issue(action, subject);
            return counts with { Challenge = new HoldoutChallenge(issued.Id, issued.Action.Text, issued.ExpiresAt) };
        }

        var redeemed = challenges.Redeem(request.Answer, action, subject, SindriErrors.AuthRefused);
        if (redeemed.IsError)
        {
            return redeemed.Error;
        }

        var marked = await store.MarkSpent(request.Line, request.Phase, [.. hidden.Select(static s => s.Key)], cancellationToken).ConfigureAwait(false);
        return marked.IsError
            ? marked.Error
            : counts with { Scenarios = [.. hidden.Select(s => new ShownScenario(s.Key, s.Item, s.Kind, s.Steps, s.Expected, spent.Value.Contains(s.Key)))] };
    }
}
