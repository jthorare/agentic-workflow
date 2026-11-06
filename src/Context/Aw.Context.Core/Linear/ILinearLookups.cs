using Aw.Kernel;

namespace Aw.Context.Core.Linear;

/// <summary>Reads the pickers' data from Linear: the states, cycles, members and labels of the profile's teams (spec 2026-11-04 §4.2). Owner-only data, read with the read key.</summary>
public interface ILinearLookups
{
    /// <summary>Reads every team the settings name (every team the key sees when they name none).</summary>
    /// <param name="settings">The checked 'context.sources.linear' settings.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One entry per team, or the source's error (down, refused key).</returns>
    public ValueTask<Result<IReadOnlyList<LinearTeamLookups>>> Fetch(LinearSettings settings, CancellationToken cancellationToken);
}
