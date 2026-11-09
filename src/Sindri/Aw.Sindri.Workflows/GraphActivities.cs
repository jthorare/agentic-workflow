namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The activity classes of <see cref="GraphWorkflow"/> (issue 2258, PR 6).</summary>
/// <param name="Runs">GraphLoad, GraphBegin, GraphRecordNode and GraphFinish.</param>
/// <param name="Blocks">GraphRunBlock.</param>
internal sealed record GraphActivities(GraphRunActivities Runs, GraphBlockActivities Blocks);
