namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>200 of POST /sindri/linear/issues/{key}/change: the edit is in Linear.</summary>
/// <param name="Outcome">"applied".</param>
/// <param name="Warning">SND-LINEAR-013 when Linear took the edit but Sindri's store write failed ("Saved in Linear; Sindri shows it after the next sync"); else null.</param>
/// <param name="AlreadyApplied">True when a retry found every field already at its new value; else null.</param>
/// <param name="Issue">The issue as the re-read serves it.</param>
internal sealed record LinearChangeApplied(string Outcome, string? Warning, bool? AlreadyApplied, LinearIssueOne Issue) : LinearWriteOutcome;
