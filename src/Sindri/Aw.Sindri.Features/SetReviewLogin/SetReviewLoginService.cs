using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.SetReviewLogin;

/// <summary>
/// Saves 'review.login' the way 'sindri profile set' does (comments and order kept, the edited text read and checked before the atomic write). Any caller with a context may do it; 'review.login-set' records the caller's actor.
/// </summary>
/// <param name="callers">The caller.</param>
/// <param name="writer">Plans and writes the edit.</param>
/// <param name="parser">Reads the edited text.</param>
/// <param name="live">The live profile after the write.</param>
/// <param name="ledger">Appends 'review.login-set'.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry; its actor is the caller's.</param>
internal sealed class SetReviewLoginService(ICallerContextAccessor callers, IProfileKeyWriter writer, IProfileTextParser parser, IProfileSource live, ILedgerStore ledger, HeldLease held, EventFactory events)
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

        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");   // before anything is written
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

        var appended = await ledger.Append(events.Entry(EventTypes.ReviewLoginSet, new ReviewLoginSet(caller.CommandId, request.Login), $"{EventTypes.ReviewLoginSet}:{caller.CommandId.Value}"), grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new SetReviewLoginResponse(request.Login);
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
