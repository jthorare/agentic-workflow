using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.IssueChallenge;

/// <summary>
/// Issues the app's challenge (setup-once spec §4 items 1 and 2). The actions the app confirmed are gone (the recipe ones with the engine,
/// 'approve profile &lt;hash6&gt;' with the profile approval, #2357), so every text is SND-AUTH-002: a human decision at a terminal.
/// The route stays until the profile concept is removed (#2373).
/// </summary>
internal sealed class IssueAppChallengeService : IService<IssueAppChallengeRequest, IssueChallengeResponse>
{
    public ValueTask<Result<IssueChallengeResponse>> Handle(IssueAppChallengeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult<Result<IssueChallengeResponse>>(SindriErrors.AuthRefused.With(
            $"The app confirms no action now; '{request.Action.Trim()}' is a human decision at a terminal"));
    }
}
