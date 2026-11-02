using System.Text.Json.Serialization;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// The 'model.picked' payload (model-pick spec §6): the model an engine launch was given and how it was chosen. Key 'model.pick:&lt;launchId&gt;', first write wins.
/// It never holds file text or brief text: only the brief's hash, the candidates, the file list with sizes and the omitted list.
/// </summary>
/// <param name="Launch">The launch.</param>
/// <param name="Item">The item.</param>
/// <param name="Instance">The workflow instance.</param>
/// <param name="State">The Step's state id.</param>
/// <param name="Attempt">The Step run's number.</param>
/// <param name="Target">The agent.</param>
/// <param name="Model">The model id launched; null when <paramref name="By"/> is 'default'.</param>
/// <param name="By">'jev', 'constraint' (one candidate), 'rules' (the fallback) or 'default' (no candidates); see <see cref="ModelPickedBy"/>.</param>
/// <param name="Reason">For 'rules': why Jev did not decide (the judge reason code); else null.</param>
/// <param name="Decision">The judge decision id, so 'judge why' explains the pick; null when Jev was not asked.</param>
/// <param name="Candidates">The keys and ids Jev was shown.</param>
/// <param name="BriefHash">SHA-256 of the brief Jev saw.</param>
/// <param name="Files">The files Jev saw.</param>
/// <param name="Omitted">The files left out.</param>
/// <param name="JevMs">The milliseconds Jev took.</param>
/// <param name="JevCalls">The calls made for this pick (0 when Jev was not asked).</param>
/// <param name="ContentSent">True when any file text or brief text went to Jev.</param>
/// <param name="Unusual">True when the picked model failed its check or was refused in more than half of its last 20 runs for this step (spec 4.5).</param>
/// <param name="EscalatedFrom">The model that failed, for an escalation (spec 7); else null.</param>
/// <param name="EscalatedFromKind"><see cref="AgentFailureKinds"/> of the run that failed (refused or check_failed), for an escalation; else null.</param>
public sealed record ModelPicked(
    [property: JsonPropertyName("launchId")] LaunchId Launch,
    ItemId Item,
    InstanceAddress Instance,
    string State,
    int Attempt,
    AgentId Target,
    string? Model,
    string By,
    string? Reason,
    string? Decision,
    IReadOnlyList<string> Candidates,
    string BriefHash,
    IReadOnlyList<PickedFile> Files,
    IReadOnlyList<OmittedFile> Omitted,
    int JevMs,
    int JevCalls,
    bool ContentSent,
    bool Unusual,
    string? EscalatedFrom,
    string? EscalatedFromKind = null);
