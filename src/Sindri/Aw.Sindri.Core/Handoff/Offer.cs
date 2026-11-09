namespace Aw.Sindri.Core.Handoff;

/// <summary>
/// One piece of work an outside system offers to this owner. Every text field is untrusted (the outside system wrote it), so it is fenced
/// wherever it reaches a prompt. Wire names: id, title, repo, branch, prompt, links, labels (schemas/handoff/inbox.openapi.yaml).
/// </summary>
/// <param name="Key">The offer's id at the outside system (the wire field 'id'; named Key because a raw string id may not sit in a public signature, AW1001).</param>
/// <param name="Title">A one-line title.</param>
/// <param name="Repo">The repo the work is for ('owner/name' or a name the profile knows).</param>
/// <param name="Branch">The branch to work on, or null.</param>
/// <param name="Prompt">What to do, or null.</param>
/// <param name="Links">Related links.</param>
/// <param name="Labels">Labels an 'inbox' trigger filters on.</param>
public sealed record Offer(string Key, string Title, string Repo, string? Branch, string? Prompt, IReadOnlyList<HandoffLink> Links, IReadOnlyList<string> Labels);
