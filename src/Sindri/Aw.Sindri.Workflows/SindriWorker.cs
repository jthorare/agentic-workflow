using Temporalio.Worker;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The worker 'sindri serve' runs on the settings' task queue (engine spec rev 8 §0 "Operations"). It serves the graph workflow
/// (issue 2258 PR 6) and the agent runtime's activities; the recipe workflow is gone. <see cref="SindriWorkerHost"/> runs it inside 'sindri serve'.
/// </summary>
internal static class SindriWorker
{
    /// <summary>The worker options: every activity class of <paramref name="activities"/>.</summary>
    /// <param name="settings">The task queue.</param>
    /// <param name="activities">The activity classes, the agent runtime's (engine P8) among them.</param>
    /// <returns>The options, on <see cref="TemporalSettings.TaskQueue"/>.</returns>
    public static TemporalWorkerOptions Options(TemporalSettings settings, WorkerActivities activities) =>
        new TemporalWorkerOptions(settings.TaskQueue)
            .AddWorkflow<GraphWorkflow>()
            .AddWorkflow<GraphIterationWorkflow>()
            .AddAllActivities(activities.Agents)
            .AddAllActivities(activities.Nodes)
            .AddAllActivities(activities.Graph.Runs)
            .AddAllActivities(activities.Graph.Blocks);
}
