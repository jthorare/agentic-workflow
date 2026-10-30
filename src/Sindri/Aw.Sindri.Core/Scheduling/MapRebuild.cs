namespace Aw.Sindri.Core.Scheduling;

/// <summary>Do not publish the held draft: it was built before the code index of its commit existed (its symbol citations lack the index), and the index is in now, so the map is built again (#1704).</summary>
/// <param name="Repos">The repos whose draft heads name no code index at their commit, sorted by name.</param>
public sealed record MapRebuild(IReadOnlyList<string> Repos) : MapPublishAction;
