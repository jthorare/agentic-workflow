namespace Aw.Sindri.Features.ContextCalls;

/// <summary>What a refresh did: the promoted version, the heads it mapped (what the service compares the next checkout heads with) and the cleanup's counts.</summary>
/// <param name="Repo">The repo the map's version is for.</param>
/// <param name="Version">The version now current.</param>
/// <param name="Heads">Each mapped repo's HEAD the build read, by repo name.</param>
/// <param name="Accepted">Review entries the cleanup accepted.</param>
/// <param name="Removed">Model entries the cleanup rejected (weak, or not judged).</param>
/// <param name="Dropped">Orphaned overrides the cleanup dropped.</param>
/// <param name="Cleanup">How the low-confidence entries were settled: none, agent, partial or unavailable.</param>
public sealed record RefreshContextMapResult(string Repo, int Version, IReadOnlyDictionary<string, string> Heads, int Accepted, int Removed, int Dropped, string Cleanup);
