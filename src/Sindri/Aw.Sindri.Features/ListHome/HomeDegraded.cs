namespace Aw.Sindri.Features.ListHome;

/// <summary>One thing that makes the page less than whole (home spec §10): data, not an error. Agents branch on <paramref name="Code"/>.</summary>
/// <param name="Code">'source-stale', 'source-disconnected', 'brief-model-limited', 'brief-no-writer', 'brief-failed', 'lanes-unavailable' or 'behind'.</param>
/// <param name="Detail">What the page prints beside its label.</param>
public sealed record HomeDegraded(string Code, string Detail);
