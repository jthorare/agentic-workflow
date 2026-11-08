using Aw.Context.Core.Engine;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Features.GetStatus;

namespace Aw.Sindri.Host.Models;

/// <summary>
/// The doctor's 'context:labelpick' row (model routing spec §8.4), the way 'model:pick' works for the launch pick: whether Jev picks each cluster's label from the model's candidates. Off (the approved profile says so, or says nothing) is fine,
/// a choice; on without a Jev key is a warning, because every label then takes the model's first candidate.
/// </summary>
/// <param name="models">'context.models' of the approved profile.</param>
/// <param name="key">Says whether Jev has a key.</param>
internal sealed class LabelPickDoctorChecks(IApprovedModels models, IJevKeyProbe key) : IDoctorChecks
{
    private const string Name = "context:labelpick";

    public async ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        if ((await models.Current(cancellationToken).ConfigureAwait(false)).LabelPick != LabelPick.Jev)
        {
            return [new DoctorCheck(Name, CheckState.Ok, "label choice is off", Fix: null)];
        }

        return !await key.Present(cancellationToken).ConfigureAwait(false)
            ? [new DoctorCheck(Name, CheckState.Warn, "label choice is on, Jev unavailable: labels take the model's first candidate", $"set {JevKey.Variable} where sindri runs")]
            : [new DoctorCheck(Name, CheckState.Ok, "label choice is on", Fix: null)];
    }
}
