using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.IssueChallenge;

/// <summary>
/// Issues a challenge for one protected action (engine spec §4.2, S1-D5). The recipe engine's forms went with the recipes and
/// 'approve profile &lt;hash6&gt;' went with the profile approval (#2357), so no action takes a challenge now: every text is refused.
/// The route and its chain stay until the profile concept is removed (#2373).
/// </summary>
internal sealed class IssueChallengeService : IService<IssueChallengeRequest, IssueChallengeResponse>
{
    public ValueTask<Result<IssueChallengeResponse>> Handle(IssueChallengeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult<Result<IssueChallengeResponse>>(
            SindriErrors.InvalidInput.With($"'{request.Action.Trim()}' isn't an action a challenge is issued for; no action takes a challenge now"));
    }
}
