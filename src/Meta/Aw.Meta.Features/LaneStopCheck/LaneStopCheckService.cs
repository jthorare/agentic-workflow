using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.LaneStopCheck;

/// <summary>
/// The lane's stop hook question: does the lane owe the meta a reply? It blocks while one is owed, with the loop guard of the meta's
/// own stop-check (a check that isn't a continuation starts a new turn; the identical owed set blocks at most MaxBlocks times in a
/// row). A block shows the owed messages, so a message that arrived mid-turn reaches the lane through it. The call is the lane's
/// heartbeat as a stop when the stop hook makes it (AW_META_HOOK set); a hand-typed check only notes the CLI was used. With no team set up the lane may stop.
/// </summary>
/// <param name="config">The team config.</param>
/// <param name="mail">The lanes' inboxes.</param>
/// <param name="beats">The lane hooks' heartbeats.</param>
/// <param name="guard">The loop guard.</param>
/// <param name="clock">Dates the check.</param>
internal sealed class LaneStopCheckService(IMetaConfig config, ILaneMail mail, LaneBeats beats, LaneStopGuard guard, TimeProvider clock) : IService<LaneStopCheckRequest, LaneStopCheckResponse>
{
    public async ValueTask<Result<LaneStopCheckResponse>> Handle(LaneStopCheckRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        if (team.IsError)
        {
            return team.Error.Code == MetaErrors.NoTeamConfig.Code ? new LaneStopCheckResponse(Blocked: false, [], Warning: null) : team.Error;
        }

        var now = clock.GetUtcNow();
        var owed = await mail.Owed(request.Identity.Lane, cancellationToken).ConfigureAwait(false);
        if (owed.IsError)
        {
            return owed.Error;
        }

        var outcome = await guard.Decide(new LaneStop(request.Identity, owed.Value, request.HookActive, request.MaxBlocks, now), cancellationToken).ConfigureAwait(false);
        if (outcome.IsError)
        {
            return outcome.Error;
        }

        // The lane counts as stopped (idle, wakeable) only when this check lets it stop; a blocked agent keeps working.
        // Only the lane's own stop hook (AW_META_HOOK set) is a heartbeat; a check typed by hand just notes the CLI was used.
        var beat = request.HookEvent is { Length: > 0 }
            ? await beats.Record(request.Identity.Lane, stopped: !outcome.Value.Block, now, cancellationToken).ConfigureAwait(false)
            : await beats.RecordCli(request.Identity.Lane, now, cancellationToken).ConfigureAwait(false);
        if (beat.IsError)
        {
            return beat.Error;
        }

        var shown = outcome.Value.Block ? await Show(owed.Value, now, cancellationToken).ConfigureAwait(false) : Results.Ok();
        return shown.IsError ? shown.Error : new LaneStopCheckResponse(outcome.Value.Block, owed.Value, outcome.Value.Warning);
    }

    private async ValueTask<Result<Unit>> Show(IReadOnlyList<LaneMessage> owed, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var undelivered = owed.Where(m => !m.Delivered).Select(m => m.Seq).ToList();
        return undelivered.Count == 0 ? Results.Ok() : await mail.Deliver(undelivered, now, cancellationToken).ConfigureAwait(false);
    }
}
