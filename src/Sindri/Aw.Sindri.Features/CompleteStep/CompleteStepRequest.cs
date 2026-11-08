using System.Text.Json.Serialization;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.CompleteStep;

/// <summary>
/// 'workflow complete' from the desk (engine spec rev 8.1 §5.3, §6.3 'POST /workflow/instances/{id}/runs/{run}/complete'):
/// the report for an open desk run. Outputs travel as content, at most 1 MiB each; the server never opens a client path.
/// </summary>
/// <param name="Instance">The instance: its id ('WFI-…', what the route carries), its address, or a bare item.</param>
/// <param name="Run">The open run's token, as status shows it.</param>
/// <param name="Outcome">One of the Step's declared outcomes.</param>
/// <param name="Outputs">Each output fact's content.</param>
/// <param name="Nonce">The command's nonce; null to have one issued.</param>
/// <param name="LaunchSecret">The launch secret an agent session proves its run with (header X-Sindri-Launch-Secret, never part of the body or any log); null for a desk report.</param>
public sealed record CompleteStepRequest(
    string Instance,
    RunToken Run,
    OutcomeName Outcome,
    IReadOnlyDictionary<FactKey, ReadOnlyMemory<byte>> Outputs,
    CommandNonce? Nonce = null,
    [property: JsonIgnore] LaunchSecret? LaunchSecret = null) : ICommand<CompleteStepResponse>, IClassifiedCommand
{
    /// <summary>The catalog variant (API spec §2.1): a report carrying a launch secret is 'CompleteStep:report', a session's step-report; any other is the desk row 'CompleteStep'.</summary>
    [JsonIgnore]
    public string? Variant => LaunchSecret is null ? null : "report";

    /// <summary>Never: a launch secret is not a protected field.</summary>
    [JsonIgnore]
    public bool CarriesProtectedFields => false;
}
