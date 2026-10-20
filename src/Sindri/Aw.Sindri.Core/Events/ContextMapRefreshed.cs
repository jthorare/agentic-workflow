namespace Aw.Sindri.Core.Events;

/// <summary>An automatic refresh built, cleaned up and promoted the map (owner F1, 2026-10-19; 'context.map.refreshed').</summary>
/// <param name="Repo">The map's repo.</param>
/// <param name="Version">The version now current.</param>
/// <param name="Heads">Each mapped repo's HEAD the build read, by repo name: what the next check compares the checkouts with.</param>
/// <param name="Accepted">Review entries the cleanup accepted.</param>
/// <param name="Removed">Model entries the cleanup rejected.</param>
/// <param name="Dropped">Orphaned overrides the cleanup dropped.</param>
/// <param name="Cleanup">How the low-confidence entries were settled: none, agent, partial or unavailable.</param>
public sealed record ContextMapRefreshed(string Repo, int Version, IReadOnlyDictionary<string, string> Heads, int Accepted, int Removed, int Dropped, string Cleanup);
