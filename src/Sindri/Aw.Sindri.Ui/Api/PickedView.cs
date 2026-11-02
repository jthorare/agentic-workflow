namespace Aw.Sindri.Ui.Api;

/// <summary>How a run's model was chosen (the 'model.picked' record without its identifiers). It holds no file text.</summary>
/// <param name="Target">The agent.</param>
/// <param name="Model">The model id launched; null when <paramref name="By"/> is 'default'.</param>
/// <param name="By">'jev', 'constraint', 'rules' or 'default'.</param>
/// <param name="Reason">For 'rules': why Jev did not decide; else null.</param>
/// <param name="Decision">The judge decision id ('judge why' explains it); null when Jev was not asked.</param>
/// <param name="Candidates">The keys and ids Jev was shown.</param>
/// <param name="Files">The files Jev saw, with sizes.</param>
/// <param name="Omitted">The files left out, with why.</param>
/// <param name="JevMs">The milliseconds Jev took.</param>
/// <param name="JevCalls">The calls made for this pick.</param>
/// <param name="ContentSent">True when any file or brief text went to Jev.</param>
/// <param name="Unusual">True when the model failed most of its recent runs on this step.</param>
/// <param name="EscalatedFrom">The model that failed, for a retry; else null.</param>
/// <param name="EscalatedFromKind">'refused' or 'check_failed', for a retry; else null.</param>
internal sealed record PickedView(
    string Target, string? Model, string By, string? Reason, string? Decision, IReadOnlyList<string> Candidates, IReadOnlyList<PickedFileView> Files,
    IReadOnlyList<OmittedFileView> Omitted, int JevMs, int JevCalls, bool ContentSent, bool Unusual, string? EscalatedFrom, string? EscalatedFromKind);
