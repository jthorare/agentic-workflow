using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.SetReviewLogin;

/// <summary>
/// Saves 'review.login' the way 'sindri profile set' does (comments and order kept, the edited text read and checked before the atomic write), then settles the approval
/// (<see cref="LoginApproval"/>). The owner's own action: a launched session is refused (SND-AUTH-002).
/// </summary>
/// <param name="callers">The caller.</param>
/// <param name="writer">Plans and writes the edit.</param>
/// <param name="parser">Reads the edited text.</param>
/// <param name="live">The live profile after the write.</param>
/// <param name="approval">Approves when the login is the only waiting change.</param>
internal sealed class SetReviewLoginService(ICallerContextAccessor callers, IProfileKeyWriter writer, IProfileTextParser parser, IProfileSource live, LoginApproval approval)
    : IService<SetReviewLoginRequest, SetReviewLoginResponse>
{
    private const string Key = "review.login";

    public async ValueTask<Result<SetReviewLoginResponse>> Handle(SetReviewLoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("saving review.login needs a caller");
        }

        if (caller.Class == CallerClass.Session)
        {
            return SindriErrors.AuthRefused.With("review.login is the owner's to set; a session can't save it (fix: set it on the Reviews tab)");
        }

        if (PrReviewRules.LoginProblem(request.Login) is { } bad)
        {
            return bad;
        }

        var written = Write(request.Login);
        if (written is not null)
        {
            return written;
        }

        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error;
        }

        var approved = await approval.Settle(loaded.Value, request.Login, cancellationToken).ConfigureAwait(false);
        return approved.IsError ? approved.Error : new SetReviewLoginResponse(request.Login, approved.Value);
    }

    // Plans the edit, has the edited text read and checked (nothing is written on a refusal), then saves it atomically.
    private Error? Write(string login)
    {
        var plan = writer.PlanText(Key, login);
        if (plan.IsError)
        {
            return plan.Error;
        }

        if (plan.Value.Changed && parser.Parse(plan.Value.Text, plan.Value.Path) is { IsError: true } parsed)
        {
            return parsed.Error;   // SND-PROFILE-002 / SND-REVIEW-001 with the key path
        }

        var saved = writer.Save(plan.Value);
        return saved.IsError ? saved.Error : null;
    }
}
