using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Features.SessionExit;

/// <summary>
/// The agent's exit (API spec §8.1): checks the launch secret, records the session id the call carries (before the end), appends 'session.ended' with
/// reason exit and the code (reason limit with the kind, the limit and the peaks when a sandbox limit ended the session, after one final usage sample), and deletes the launch's hash and per-launch files, so a late call from a dead launch can't validate.
/// </summary>
/// <param name="session">The session events and the secret check.</param>
/// <param name="usage">Takes the final usage sample before the session ends.</param>
internal sealed class SessionExitService(LaunchSessionEvents session, ISessionUsageSampler usage) : IService<SessionExitRequest, SessionExitResponse>
{
    public async ValueTask<Result<SessionExitResponse>> Handle(SessionExitRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var proven = session.Verify(request.LaunchId, request.Secret);
        if (proven.IsError)
        {
            return proven.Error;
        }

        var reported = await session.Report(request.LaunchId, request.SessionId, cancellationToken).ConfigureAwait(false);
        if (reported.IsError && !LaunchSessionEvents.IsPatternTimeout(reported.Error))
        {
            return reported.Error;
        }

        _ = await usage.Flush(request.LaunchId, cancellationToken).ConfigureAwait(false);   // the final sample first, so a kill doesn't lose the heaviest minute against the budget (sandbox spec E1-I3)
        var ended = await (request.Limit is { } limit
            ? session.EndByLimit(request.LaunchId, limit, cancellationToken)
            : session.End(request.LaunchId, SessionEndReason.Exit, request.ExitCode, cancellationToken)).ConfigureAwait(false);
        if (ended.IsError)
        {
            return ended.Error;
        }

        return reported.IsError ? reported.Error : new SessionExitResponse(request.LaunchId);   // a pattern timeout still ends the session, then says why no id was recorded
    }
}
