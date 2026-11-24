using Aw.Sindri.Core.Home;

namespace Aw.Sindri.Testing;

/// <summary>The aw-meta team in memory (<see cref="IMetaTeamReader"/>): answers each read with the team a test set. Passes <see cref="MetaTeamReaderContract"/>.</summary>
/// <param name="team">The first answer.</param>
public sealed class FakeMetaTeamReader(MetaTeamRead team) : IMetaTeamReader
{
    /// <summary>Gets or sets what the next read answers.</summary>
    public MetaTeamRead Team { get; set; } = team;

    /// <summary>Gets how many reads were made.</summary>
    public int Reads { get; private set; }

    /// <inheritdoc />
    public ValueTask<MetaTeamRead> Read(CancellationToken cancellationToken)
    {
        Reads++;
        return ValueTask.FromResult(Team);
    }
}
