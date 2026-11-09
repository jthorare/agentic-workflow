namespace Aw.Sindri.Core.Connections.Polling;

/// <summary>What the backoff policy decided after a rate-limited or failed poll.</summary>
/// <param name="Failures">The new count of failures in a row.</param>
/// <param name="Until">No poll before this instant.</param>
public sealed record BackoffDecision(int Failures, DateTimeOffset Until);
