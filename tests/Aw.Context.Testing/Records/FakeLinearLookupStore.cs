using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Testing.Records;

/// <summary>The in-memory <see cref="ILinearLookupStore"/>: the last <see cref="LinearTeamLookups"/> of each team behind a lock. It passes <see cref="LinearLookupStoreContract{TStore}"/>.</summary>
public sealed class FakeLinearLookupStore : ILinearLookupStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<ExternalId, LinearTeamLookups> _teams = [];
    private int _replaces;

    /// <summary>How many times a team was replaced.</summary>
    public int Replaces => Volatile.Read(ref _replaces);

    /// <summary>What the store holds for a team.</summary>
    /// <param name="teamId">The team's Linear id.</param>
    /// <returns>The team's lists, or null when none was ever stored.</returns>
    public LinearTeamLookups? Team(ExternalId teamId)
    {
        lock (_gate)
        {
            return _teams.GetValueOrDefault(teamId);
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> ReplaceTeam(LinearTeamLookups team, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(team);
        lock (_gate)
        {
            _teams[team.TeamId] = team;
        }

        _ = Interlocked.Increment(ref _replaces);
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }
}
