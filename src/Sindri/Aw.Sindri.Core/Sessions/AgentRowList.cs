namespace Aw.Sindri.Core.Sessions;

/// <summary>The Agents list as the wire carries it (Agents page spec §8): the rows in the order of spec §4.1, and what could not be read.</summary>
/// <param name="Rows">The rows, in page order.</param>
/// <param name="Degraded">The sources that could not be read in full; empty when all were.</param>
public sealed record AgentRowList(IReadOnlyList<AgentRow> Rows, IReadOnlyList<AgentDegraded> Degraded);
