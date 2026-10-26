using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetStatus;

/// <summary>The 'services' rows health judges: those of the approved profile's services and of the build's built-ins (<see cref="ApprovedServices"/>).</summary>
/// <param name="views">Reads the services view.</param>
/// <param name="builtIns">The build's built-in services.</param>
/// <param name="codeIndex">The code index's health rows; none when the build has no index.</param>
/// <param name="stuck">The engine's stuck instances (API P13); none when this build has no engine read side.</param>
/// <param name="stalled">The agent runs idle past their launch's idle time (issue #1000); none when this build has no launcher.</param>
internal sealed class HealthServiceRows(IViewReader views, IEnumerable<BuiltInService> builtIns, ICodeIndexHealth? codeIndex = null, StuckInstanceCount? stuck = null, IStalledAgentRuns? stalled = null)
{
    /// <summary>How many workflow instances are stuck on a signal or update (API spec §3.8, E-30).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The count; 0 when the engine doesn't answer.</returns>
    public ValueTask<int> Stuck(CancellationToken cancellationToken) => stuck is null ? ValueTask.FromResult(0) : stuck.Current(cancellationToken);

    /// <summary>How many agent runs ended their turn without reporting and are still not ended (issue #1000).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The count; 0 when this build has no launcher.</returns>
    public async ValueTask<int> Stalled(CancellationToken cancellationToken) => stalled is null ? 0 : (await stalled.Find(cancellationToken).ConfigureAwait(false)).Count;

    /// <summary>How many live workflow instances are parked as unavailable: a dependency of their Step stayed down through the retries, so they wait for a rerun, not for an answer.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The count; 0 when the items view can't be read (status is where that is reported).</returns>
    public async ValueTask<int> Unavailable(CancellationToken cancellationToken)
    {
        var items = await views.List<ItemRow>(ViewNames.Items, cancellationToken).ConfigureAwait(false);
        return items.IsSuccess ? items.Value.Sum(static i => EngineItemStates.Unavailable(i.Engine)) : 0;
    }

    /// <summary>The rows that count for health.</summary>
    /// <param name="approved">The approved profile, or null before the first approval.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The rows, or null when the view can't be read.</returns>
    public async ValueTask<IReadOnlyList<ServiceRow>?> Read(LoadedProfile? approved, CancellationToken cancellationToken)
    {
        var rows = await views.List<ServiceRow>(ViewNames.Services, cancellationToken).ConfigureAwait(false);
        return rows.IsSuccess ? ApprovedServices.Of(rows.Value, approved, builtIns) : null;
    }

    /// <summary>The code index's problems for the approved profile's repos.</summary>
    /// <param name="approved">The approved profile, or null before the first approval.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The problems; none when there is no index port or nothing to read.</returns>
    public async ValueTask<IReadOnlyList<HealthProblem>> CodeIndexProblems(LoadedProfile? approved, CancellationToken cancellationToken) =>
        codeIndex is null ? [] : await codeIndex.Problems(approved, cancellationToken).ConfigureAwait(false);
}
