namespace Aw.Sindri.Core.Launches;

/// <summary>The skill block runs whose session ended a turn and needs the owner (skill flows spec §7, F-2a), for Needs you.</summary>
public interface ISkillWaitingRuns
{
    /// <summary>The waiting runs now: a skill launch still open, in an instance nobody took over, whose Step is still the instance's state, with a 'launch.owner-waiting' newer than its last 'launch.turn-started'.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The runs, in no set order.</returns>
    public ValueTask<IReadOnlyList<SkillWaitingRun>> Find(CancellationToken cancellationToken);
}
