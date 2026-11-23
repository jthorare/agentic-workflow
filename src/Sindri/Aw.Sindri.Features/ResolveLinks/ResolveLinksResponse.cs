namespace Aw.Sindri.Features.ResolveLinks;

/// <summary>One answer per url, in the request's order (messages spec §9.3).</summary>
/// <param name="Links">The answers.</param>
public sealed record ResolveLinksResponse(IReadOnlyList<ResolvedLink> Links);
