namespace Aw.Sindri.Features.LinearIssues;

/// <summary>Whether the issue sheet may edit the issue, and why not when it may not.</summary>
/// <param name="Allowed">True when Sindri can write to this issue.</param>
/// <param name="Code">SND-LINEAR-002 (no usable write key), SND-LINEAR-003 (the issue's team isn't one the profile picked) or SND-AUTH-002 (a session read) when not allowed; null when allowed, or when the issue is removed.</param>
internal sealed record LinearWriteGateView(bool Allowed, string? Code);
