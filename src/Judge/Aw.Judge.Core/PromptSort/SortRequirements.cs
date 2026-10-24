namespace Aw.Judge.Core.PromptSort;

/// <summary>What the sorter decided the session's work needs before it can be called done (done-gate.sh reads 'requirements.uiEvidence').</summary>
/// <param name="UiEvidence">The work touches the UI, so the done claim must show UI evidence.</param>
public sealed record SortRequirements(bool UiEvidence = false);
