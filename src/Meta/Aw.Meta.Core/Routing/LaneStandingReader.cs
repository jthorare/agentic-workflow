using Aw.Meta.Core.Config;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Usage;

namespace Aw.Meta.Core.Routing;

/// <summary>Adds each lane's provider, model and usage windows to what the usage look found.</summary>
/// <param name="files">The lane records.</param>
/// <param name="rollouts">Codex's rollout files.</param>
/// <param name="clock">The current time.</param>
public sealed class LaneStandingReader(ILaneFiles files, ICodexRollouts rollouts, TimeProvider clock)
{
    /// <summary>The most of a rollout's end that is read.</summary>
    public const int TailBytes = 256 * 1024;

    /// <summary>Builds one standing per subject, the meta included.</summary>
    /// <param name="team">The team.</param>
    /// <param name="readings">The usage look.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The standings, in the readings' order.</returns>
    public ValueTask<IReadOnlyList<LaneStanding>> Read(TeamConfig team, IReadOnlyList<UsageReading> readings, CancellationToken cancellationToken)
    {
        _ = (files, rollouts, clock, team, readings, cancellationToken);
        throw new NotSupportedException();
    }
}
