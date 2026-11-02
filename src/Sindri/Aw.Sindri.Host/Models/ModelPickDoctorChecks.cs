using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Host.Models;

/// <summary>
/// The doctor's 'model:pick' row (model-pick spec 8): whether Jev picks a model for each agent step. Off (the profile says so, or says nothing, or the owner has not approved the notice) is fine, a choice;
/// on without a Jev key is a warning, because every pick then falls back to the cheapest model allowed.
/// </summary>
/// <param name="settings">The profile's pick settings.</param>
/// <param name="environment">Reads an environment variable.</param>
internal sealed class ModelPickDoctorChecks(IModelPickSettings settings, Func<string, string?> environment) : IDoctorChecks
{
    private const string Name = "model:pick";
    private const string KeyVariable = "TYPESAFE_API_KEY";

    public async ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        if (!await settings.PickEnabled(cancellationToken).ConfigureAwait(false))
        {
            return [new DoctorCheck(Name, CheckState.Ok, "model choice is off", Fix: null)];
        }

        // macOS keeps the key in the keychain, which the judge reads when it is asked; only a box with no variable and no keychain can say the key is missing.
        return string.IsNullOrEmpty(environment(KeyVariable)) && !OperatingSystem.IsMacOS()
            ? [new DoctorCheck(Name, CheckState.Warn, "model choice is on, Jev unavailable: picks fall back to rules", $"set {KeyVariable} where sindri runs")]
            : [new DoctorCheck(Name, CheckState.Ok, "model choice is on", Fix: null)];
    }
}
