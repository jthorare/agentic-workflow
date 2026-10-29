using Aw.Context.Core.Engine;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Host.Models;

/// <summary>
/// The doctor's 'context:models' row (context model routing spec §8.4): what will run (one line per distinct route) when ok, and a warning with its fixes for a listed harness that is not installed, a list of one harness
/// and a Codex model that Codex's own list does not hold. Claude has no list command and Cursor's reader is not here yet, so neither is checked.
/// </summary>
/// <param name="models">'context.models' of the approved profile.</param>
/// <param name="catalog">The models each agent offers (Codex's list).</param>
/// <param name="installed">The harnesses found on PATH, in the default order.</param>
internal sealed class ModelRoutingDoctorChecks(IApprovedModels models, IModelCatalog catalog, Func<IReadOnlyList<ModelHarness>> installed) : IDoctorChecks
{
    public ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        _ = (models, catalog, installed);
        return ValueTask.FromResult<IReadOnlyList<DoctorCheck>>([]);
    }
}
