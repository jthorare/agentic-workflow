namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>One step a re-run took from its source run (<see cref="GraphRunActivities.RestoredAsync"/>).</summary>
/// <param name="Node">The node id.</param>
/// <param name="Taken">The branches it took; empty for a step the source run skipped.</param>
internal sealed record GraphRestoredNode(string Node, IReadOnlyList<string> Taken);
