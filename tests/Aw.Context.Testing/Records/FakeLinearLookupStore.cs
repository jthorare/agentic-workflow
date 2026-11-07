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
    private string? _urlKey;

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
    public ValueTask<Result<LinearTeamLookups?>> Team(string teamKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(teamKey);
        lock (_gate)
        {
            var team = _teams.Values.FirstOrDefault(t => string.Equals(t.TeamKey, teamKey, StringComparison.Ordinal));
            return ValueTask.FromResult(Results.Success(team is null ? null : Sorted(team)));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> ReplaceWorkspaceUrlKey(string urlKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(urlKey);
        lock (_gate)
        {
            _urlKey = urlKey;
        }

        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }

    /// <inheritdoc />
    public ValueTask<Result<string?>> WorkspaceUrlKey(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult(Results.Success<string?>(_urlKey));
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

    // the order a real store reads a team in: states by position, cycles by start, members and labels by name
    private static LinearTeamLookups Sorted(LinearTeamLookups team) => team with
    {
        States = [.. team.States.OrderBy(static s => s.Position).ThenBy(static s => s.Name, StringComparer.Ordinal).ThenBy(static s => s.Id.Value, StringComparer.Ordinal)],
        Cycles = [.. team.Cycles.OrderBy(static c => c.StartsAt).ThenBy(static c => c.Number).ThenBy(static c => c.Id.Value, StringComparer.Ordinal)],
        Members = [.. team.Members.OrderBy(static m => m.Name, StringComparer.OrdinalIgnoreCase).ThenBy(static m => m.Name, StringComparer.Ordinal).ThenBy(static m => m.Id.Value, StringComparer.Ordinal)],
        Labels = [.. team.Labels.OrderBy(static l => l.Name, StringComparer.OrdinalIgnoreCase).ThenBy(static l => l.Name, StringComparer.Ordinal).ThenBy(static l => l.Id.Value, StringComparer.Ordinal)],
    };
}
