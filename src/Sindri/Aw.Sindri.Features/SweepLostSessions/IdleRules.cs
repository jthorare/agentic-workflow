using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Features.SweepLostSessions;

/// <summary>The idle sweep's decision for one Step session. Pure: the time comes in through <see cref="IdleInputs.Now"/>.</summary>
public static class IdleRules
{
    /// <summary>
    /// Decides what to do with the session. A tool call in flight is never touched. A turn that ended with a report missing (Stop) ends the session after <see cref="LaunchRules.IdleAfter"/>. A turn an API error
    /// ended (StopFailure) is nudged once after <see cref="LaunchRules.IdleAfter"/>; a session with no hook marker that the collector's snapshot sees idle is nudged once after <paramref name="stalledAfter"/>. A nudged session
    /// that is still idle <paramref name="stalledAfter"/> after the nudge (or after a newer idle, if it worked and went idle again) ends. A session that works again has no idle-since, so its clock starts over.
    /// </summary>
    /// <param name="inputs">What is known about it.</param>
    /// <param name="stalledAfter">How long a session may sit idle with no hook marker, or after the nudge, before it is nudged or ended.</param>
    /// <returns>The action.</returns>
    public static IdleAction Decide(IdleInputs inputs, TimeSpan stalledAfter)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (inputs.ToolRunning)
        {
            return IdleAction.None;
        }

        if (inputs.TurnFailed is null && inputs.TurnEnded is { } ended)
        {
            return LaunchRules.IdleTurn(ended, inputs.Now) ? IdleAction.End : IdleAction.None;
        }

        return inputs.TurnFailed is { } failed
            ? Stalled(inputs, failed, LaunchRules.IdleAfter, stalledAfter)
            : inputs.SnapshotIdleSince is { } idle ? Stalled(inputs, idle, stalledAfter, stalledAfter) : IdleAction.None;
    }

    // A session idle since idle: nudged once when firstAfter has passed, then ended when stalledAfter has passed since the later of the nudge and its latest idle.
    private static IdleAction Stalled(IdleInputs inputs, DateTimeOffset idle, TimeSpan firstAfter, TimeSpan stalledAfter)
    {
        if (inputs.Nudged is not { } nudged)
        {
            return inputs.Now - idle >= firstAfter ? IdleAction.Nudge : IdleAction.None;
        }

        return inputs.Now - (idle > nudged ? idle : nudged) >= stalledAfter ? IdleAction.End : IdleAction.None;
    }
}
