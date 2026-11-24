namespace Aw.Sindri.Core.Home;

/// <summary>Reads the box's aw-meta team, read-only (home spec §3.3, §8): 'lanes-context', 'status', 'usage' and 'team-list', each with --json. It never runs merge-run, gate-run or lane-recycle.</summary>
public interface IMetaTeamReader
{
    /// <summary>One read of every command. Exit 1 (attention) still parses; a missing program, another exit code or JSON that does not parse makes the read unavailable.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The read; <see cref="MetaTeamRead.Unavailable"/> says why when there is no team to read.</returns>
    public ValueTask<MetaTeamRead> Read(CancellationToken cancellationToken);
}
