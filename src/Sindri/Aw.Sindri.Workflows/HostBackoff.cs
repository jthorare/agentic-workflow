namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>One host after an unanswered poll: how many in a row, and when it is asked again.</summary>
/// <param name="Failures">The polls in a row it did not answer.</param>
/// <param name="NextAt">Not asked before this.</param>
#pragma warning disable MA0182 // kept for the graph workflow (PR 6) and the agent block (PR 7), which have no caller of it yet
internal sealed record HostBackoff(int Failures, DateTimeOffset NextAt);
#pragma warning restore MA0182
