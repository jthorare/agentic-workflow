using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.SessionExit;

/// <summary>
/// The agent's exit (API spec §8.1): checks the launch secret, records the session id the call carries (before the end), appends 'session.ended' with
/// reason exit and the code (or reason limit with the limit, kind and peaks when wrap killed the agent for one), after one final usage sample (spec E1-I3), and deletes the launch's hash and per-launch files, so a late call from a dead launch can't validate.
/// </summary>
/// <param name="session">The session events and the secret check.</param>
/// <param name="sampler">Takes the final usage sample.</param>
internal sealed class SessionExitService(LaunchSessionEvents session, ISessionUsageSampler sampler) : IService<SessionExitRequest, SessionExitResponse>
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

        await FinalSample(request.LaunchId, cancellationToken).ConfigureAwait(false);
        var ended = request.Limit is { } limit
            ? await session.EndByLimit(request.LaunchId, limit, cancellationToken).ConfigureAwait(false)
            : await session.End(request.LaunchId, SessionEndReason.Exit, request.ExitCode, cancellationToken).ConfigureAwait(false);
        if (ended.IsError)
        {
            return ended.Error;
        }

        return reported.IsError ? reported.Error : new SessionExitResponse(request.LaunchId);   // a pattern timeout still ends the session, then says why no id was recorded
    }

    /// <summary>One last usage sample for every exit, a kill included, so the heaviest minute is not lost; a sample that fails never stops the end from being recorded.</summary>
    private async ValueTask FinalSample(LaunchId launch, CancellationToken cancellationToken)
    {
        try
        {
            _ = await sampler.Flush(launch, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // why: the usage is a measurement, the end is a fact; the next ledger read shows the last tick's sample instead
        }
    }
}
