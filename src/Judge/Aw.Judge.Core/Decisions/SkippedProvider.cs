namespace Aw.Judge.Core.Decisions;

/// <summary>A provider the chain passed over, and why.</summary>
/// <param name="Provider">The provider name, e.g. 'claude-cli'.</param>
/// <param name="Reason">Why it didn't decide.</param>
public sealed record SkippedProvider(string Provider, SkipReason Reason);
