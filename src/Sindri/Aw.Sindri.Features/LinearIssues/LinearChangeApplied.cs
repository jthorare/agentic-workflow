
namespace Aw.Sindri.Features.LinearIssues;

/// <summary>200 of the edit route: the edit is in Linear.</summary>
/// <param name="Outcome">"applied".</param>
/// <param name="Warning">SND-LINEAR-013 when Linear took the edit but Sindri's store write failed; else null.</param>
/// <param name="AlreadyApplied">True when every field was already at its new value, so nothing was sent; else null.</param>
/// <param name="Issue">The issue as the re-read serves it.</param>
internal sealed record LinearChangeApplied(string Outcome, string? Warning, bool? AlreadyApplied, GetLinearIssueResponse Issue);
