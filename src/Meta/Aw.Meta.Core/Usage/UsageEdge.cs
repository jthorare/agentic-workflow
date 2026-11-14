namespace Aw.Meta.Core.Usage;

/// <summary>One usage edge.</summary>
/// <param name="Limited">True for a new stop, false for the window coming back.</param>
/// <param name="Subject">The lane, or "meta".</param>
/// <param name="Until">When the stop says it resets; null when it gave no time.</param>
internal sealed record UsageEdge(bool Limited, string Subject, DateTimeOffset? Until);
