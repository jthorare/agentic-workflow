using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetStatus;

/// <summary>The 'services' rows health judges: those of the approved profile's services and of the build's built-ins (<see cref="ApprovedServices"/>).</summary>
/// <param name="views">Reads the services view.</param>
/// <param name="builtIns">The build's built-in services.</param>
/// <param name="codeIndex">The code index's health rows; none when the build has no index.</param>
internal sealed class HealthServiceRows(IViewReader views, IEnumerable<BuiltInService> builtIns, ICodeIndexHealth? codeIndex = null)
{
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
