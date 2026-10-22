using Aw.Sindri.Core.Forge;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>One 'pr.changed' to append.</summary>
/// <param name="Pull">The watched pull request.</param>
/// <param name="Outcome">'merged', 'closed', 'updated', 'review', 'ci' or 'idle'.</param>
/// <param name="Changes">Every change seen since the last poll, in the outcome table's order; empty for 'idle'.</param>
/// <param name="HeadSha">The head now.</param>
/// <param name="Key">The ledger idempotency key, so a replayed poll appends once.</param>
internal sealed record PrChange(WatchedPull Pull, string Outcome, IReadOnlyList<string> Changes, string HeadSha, string Key);
