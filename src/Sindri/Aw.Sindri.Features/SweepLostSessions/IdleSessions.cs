using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Features.SweepLostSessions;

/// <summary>
/// The idle sweep over the Step sessions this process tracks, one <see cref="IdleRules.Decide"/> each. A session whose Stop hook says the turn ended <see cref="LaunchRules.IdleAfter"/> ago ends as idle: the agent has
/// nothing more to do and never reported, and its wrap still beats, so the silence sweep never sees it. A session an API error stopped (StopFailure) is typed one re-prompt first and ends as idle only if it stays idle;
/// so does a session with no hook marker that the collector's snapshot has seen idle for <see cref="LaunchRules.StalledAfter"/> (a state read from Claude's own record, never the transcript guess). A tool call in flight
/// is never touched. A prompt or a tool call deletes the hook's files, so a session that works again starts the clock over. A Step a person took over (the takeover pauses its timer), or one whose instance can't be read
/// right now, is left alone.
/// </summary>
/// <param name="liveness">The launches this process tracks.</param>
/// <param name="session">The session events.</param>
/// <param name="clock">The time.</param>
/// <param name="hosted">The sessions a host keeps without a wrap.</param>
/// <param name="nudges">Types the one re-prompt into a session.</param>
/// <param name="reads">What is known about a session: the hooks' markers and the collector's snapshot.</param>
/// <param name="takeovers">Whether a person holds the Step.</param>
internal sealed class IdleSessions(LaunchLiveness liveness, LaunchSessionEvents session, TimeProvider clock, IHostedSessions hosted, ISessionNudges nudges, IdleReads reads, StepTakeovers takeovers) : IIdleSessions
{
    private const string Reprompt = "Your last response failed with an error. Redo the work for this Step and report it with `sindri workflow complete`.";

    public async ValueTask<int> Sweep(CancellationToken cancellationToken)
    {
        var ended = 0;
        var now = clock.GetUtcNow();
        foreach (var launch in liveness.Active())
        {
            var action = IdleRules.Decide(reads.Read(launch, now), LaunchRules.StalledAfter);
            if (action == IdleAction.None || await takeovers.Held(launch, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            if (action == IdleAction.Nudge)
            {
                _ = await nudges.Send(launch, Reprompt, cancellationToken).ConfigureAwait(false);   // a host that can't type still counts as nudged: the session then ends StalledAfter later
                reads.Nudged(launch, now);
            }
            else if ((await session.End(launch, SessionEndReason.Idle, exitCode: null, cancellationToken).ConfigureAwait(false)).IsSuccess)
            {
                hosted.Forget(launch);
                ended++;
            }
        }

        return ended;
    }
}
