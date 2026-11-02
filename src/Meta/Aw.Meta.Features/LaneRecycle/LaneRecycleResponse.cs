using System.Text.Json.Serialization;

namespace Aw.Meta.Features.LaneRecycle;

/// <summary>What a recycle did, or with a dry run what it would do.</summary>
/// <param name="Lane">The lane.</param>
/// <param name="DryRun">Whether nothing was sent.</param>
/// <param name="OldSession">The session id before the recycle.</param>
/// <param name="NewSession">The session id the lane reported after /clear; null for a dry run.</param>
/// <param name="OldTokens">The context size before the recycle; null when it could not be told.</param>
/// <param name="Digest">The digest file.</param>
/// <param name="Steps">The steps, in order.</param>
/// <param name="Warning">What the recycle did not verify but went on past (the continue line's pane check misread); null when nothing is in doubt.</param>
public sealed record LaneRecycleResponse(string Lane, bool DryRun, string OldSession, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? NewSession, long? OldTokens, string Digest, IReadOnlyList<string> Steps, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Warning = null);
