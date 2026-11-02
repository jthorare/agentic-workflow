using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The learn-mode window (sandbox spec §3.6, owner decision Q13): a repo learns for its first ten runs or seven days, whichever ends first, or until the owner ends it.</summary>
public static class LearnWindow
{
    /// <summary>The most runs that start in learn mode.</summary>
    public const int MaxRuns = 10;

    /// <summary>How long learn mode lasts from the repo's first learn run.</summary>
    public static readonly TimeSpan Length = TimeSpan.FromDays(7);

    /// <summary>Decides the mode a launch plans with.</summary>
    /// <param name="profileMode">The profile's 'sandbox.network.mode', or null for the default.</param>
    /// <param name="history">What the ledger says about the repo's learn runs.</param>
    /// <param name="now">The launch time.</param>
    /// <returns>Learn with the instant it stops, or Enforce with no expiry.</returns>
    public static (NetworkMode Mode, DateTimeOffset? LearnUntil) Decide(NetworkMode? profileMode, LearnHistory history, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(history);
        if ((profileMode ?? SandboxDefaults.Network) != NetworkMode.Learn || history.EndedAt is not null || history.LearnRuns >= MaxRuns)
        {
            return (NetworkMode.Enforce, null);
        }

        var until = (history.FirstLearnRun ?? now) + Length;
        return now < until ? (NetworkMode.Learn, until) : (NetworkMode.Enforce, null);
    }
}
