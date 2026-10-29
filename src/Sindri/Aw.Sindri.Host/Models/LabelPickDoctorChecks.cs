using Aw.Context.Core.Engine;
using Aw.Sindri.Core.Doctor;

namespace Aw.Sindri.Host.Models;

/// <summary>
/// The doctor's 'context:labelpick' row (model routing spec §8.4), the way 'model:pick' works for the launch pick: whether Jev picks each cluster's label from the model's candidates. Off (the approved profile says so, or says nothing) is fine,
/// a choice; on without a Jev key is a warning, because every label then takes the model's first candidate.
/// </summary>
/// <param name="models">'context.models' of the approved profile.</param>
/// <param name="environment">Reads an environment variable.</param>
internal sealed class LabelPickDoctorChecks(IApprovedModels models, Func<string, string?> environment) : IDoctorChecks
{
    private const string Name = "context:labelpick";
    private const string KeyVariable = "TYPESAFE_API_KEY";

    public async ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        if ((await models.Current(cancellationToken).ConfigureAwait(false)).LabelPick != LabelPick.Jev)
        {
            return [new DoctorCheck(Name, CheckState.Ok, "label choice is off", Fix: null)];
        }

        // macOS keeps the key in the keychain, which the judge reads when it is asked; only a box with no variable and no keychain can say the key is missing.
        return string.IsNullOrEmpty(environment(KeyVariable)) && !OperatingSystem.IsMacOS()
            ? [new DoctorCheck(Name, CheckState.Warn, "label choice is on, Jev unavailable: labels take the model's first candidate", $"set {KeyVariable} where sindri runs")]
            : [new DoctorCheck(Name, CheckState.Ok, "label choice is on", Fix: null)];
    }
}
