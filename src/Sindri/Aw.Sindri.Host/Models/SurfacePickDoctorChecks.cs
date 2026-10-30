using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Features.GetStatus;

namespace Aw.Sindri.Host.Models;

/// <summary>
/// The doctor's 'context:surfacepick' row (model routing spec §8.4), the same three states as 'context:labelpick' for 'context.models.productSurfaces.pick': off is fine, on with a Jev key is fine, on without one is a warning,
/// because no candidate group is then decided and the map keeps the candidates as the build found them.
/// </summary>
/// <param name="models">'context.models' of the approved profile.</param>
/// <param name="key">Says whether Jev has a key.</param>
internal sealed class SurfacePickDoctorChecks(IApprovedModels models, IJevKeyProbe key) : IDoctorChecks
{
    public ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken) => throw new NotSupportedException($"{models}{key}");
}
