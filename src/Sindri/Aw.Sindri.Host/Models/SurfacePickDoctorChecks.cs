using Aw.Context.Core.Engine;
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
    private const string Name = "context:surfacepick";

    public async ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        if ((await models.Current(cancellationToken).ConfigureAwait(false)).SurfacePick != LabelPick.Jev)
        {
            return [new DoctorCheck(Name, CheckState.Ok, "product surface choice is off", Fix: null)];
        }

        return !await key.Present(cancellationToken).ConfigureAwait(false)
            ? [new DoctorCheck(Name, CheckState.Warn, "product surface choice is on, Jev unavailable: product surfaces stay as the build found them", $"set {JevKey.Variable} where sindri runs")]
            : [new DoctorCheck(Name, CheckState.Ok, "product surface choice is on", Fix: null)];
    }
}
