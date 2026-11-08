using Temporalio.Worker;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The worker 'sindri serve' runs on the settings' task queue (engine spec rev 8 §0 "Operations"): the one workflow type and
/// its activities. Engine P4a: <see cref="SindriWorkerHost"/> runs it inside 'sindri serve'.
/// </summary>
internal static class SindriWorker
{
    /// <summary>The worker options: <see cref="RecipeWorkflow"/> and every activity class of <paramref name="activities"/>.</summary>
    /// <param name="settings">The task queue.</param>
    /// <param name="activities">The activity classes, the agent runtime's (engine P8) among them.</param>
    /// <returns>The options, on <see cref="TemporalSettings.TaskQueue"/>.</returns>
    public static TemporalWorkerOptions Options(TemporalSettings settings, WorkerActivities activities) =>
        new TemporalWorkerOptions(settings.TaskQueue)
            .AddWorkflow<RecipeWorkflow>()
            .AddAllActivities(activities.Steps)
            .AddAllActivities(activities.Events)
            .AddAllActivities(activities.Items)
            .AddAllActivities(activities.Effects)
            .AddAllActivities(activities.Engine.Gates)
            .AddAllActivities(activities.Engine.Cleanup)
            .AddAllActivities(activities.Engine.Children)
            .AddAllActivities(activities.Engine.Heavy)
            .AddAllActivities(activities.Engine.Literals)
            .AddAllActivities(activities.Engine.Linked)
            .AddAllActivities(activities.Inputs)
            .AddAllActivities(activities.Agents)
            .AddAllActivities(activities.AgentNodes);
}
