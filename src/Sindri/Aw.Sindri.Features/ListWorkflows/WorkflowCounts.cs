namespace Aw.Sindri.Features.ListWorkflows;

/// <summary>The list header: "14 workflows · 9 active · 1 paused" (issue 3683). Archived workflows are not counted.</summary>
/// <param name="Total">All listed workflows.</param>
/// <param name="Active">Workflows whose triggers run.</param>
/// <param name="Paused">Workflows turned off after they were on.</param>
/// <param name="Off">Workflows never turned on.</param>
internal sealed record WorkflowCounts(int Total, int Active, int Paused, int Off);
