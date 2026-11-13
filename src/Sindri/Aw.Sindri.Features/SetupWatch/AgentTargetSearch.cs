using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Features.SetupWatch;

/// <summary>
/// Finds the first agent whose binary the login shell finds. The launcher's answer costs one login-shell call per agent binary, so a search that found none is not repeated on the next ticks:
/// it runs again after <see cref="RetryEvery"/> ticks, or at once when the PATH changed.
/// </summary>
/// <param name="launch">The launcher's catalog: which agents are installed.</param>
/// <param name="environment">Reads PATH, to tell a changed PATH from the same one.</param>
/// <param name="state">What is remembered between ticks.</param>
internal sealed class AgentTargetSearch(ILaunchSetup launch, IEnvironmentReader environment, AgentSearchState state)
{
    /// <summary>The number of ticks between two searches while no agent is found.</summary>
    internal const int RetryEvery = 5;

    /// <summary>Finds the first installed agent.</summary>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The agent id, or null when none was found (or the search was skipped as just done).</returns>
    internal async ValueTask<string?> First(CancellationToken cancellationToken)
    {
        var path = environment.Get("PATH");
        if (state.Missed && string.Equals(state.MissedPath, path, StringComparison.Ordinal) && ++state.TicksSince < RetryEvery)
        {
            return null;
        }

        var shown = await launch.Show(only: null, cancellationToken).ConfigureAwait(false);
        if (shown.IsError)
        {
            return null;   // asked again on the next tick: the launcher failing is not "none found"
        }

        var found = shown.Value.Entries.FirstOrDefault(static e => e.ResolvedBin is not null);
        state.Missed = found is null;
        state.MissedPath = path;
        state.TicksSince = 0;
        return found?.Id.Value;
    }
}
