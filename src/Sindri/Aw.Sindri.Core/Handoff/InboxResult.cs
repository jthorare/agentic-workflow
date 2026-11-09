namespace Aw.Sindri.Core.Handoff;

/// <summary>What a finished run hands back for a claimed offer.</summary>
/// <param name="ClaimKey">The claim this answers (the wire field 'claimId').</param>
/// <param name="Succeeded">True for a finished run; false when the run failed (the markdown then holds the failure text).</param>
/// <param name="Markdown">The result text.</param>
/// <param name="Links">Links to what the run made (a PR, a page).</param>
public sealed record InboxResult(string ClaimKey, bool Succeeded, string Markdown, IReadOnlyList<HandoffLink> Links);
