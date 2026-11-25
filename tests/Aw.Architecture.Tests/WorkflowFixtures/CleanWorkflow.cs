using Temporalio.Workflows;

namespace Aw.Architecture.Tests.WorkflowFixtures;

/// <summary>The Workflow.* forms, a parameterless token source with a sync Cancel and ConfigureAwait(true): nothing ARCH-WF-001 reports (a fixture read as IL, never run).</summary>
[Workflow]
internal static class CleanWorkflow
{
    internal static async Task Run(Task first, Task second)
    {
        await Workflow.DelayAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(true);
        _ = await Workflow.WhenAnyAsync(first, second).ConfigureAwait(true);
        await Workflow.WhenAllAsync(first, second).ConfigureAwait(true);
        await Workflow.ExecuteActivityAsync(() => PingActivities.Ping(), new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(1) }).ConfigureAwait(true);
        _ = Workflow.UtcNow;
        _ = Workflow.Patched("fixture");
        using var stop = new CancellationTokenSource();
#pragma warning disable MA0042 // why: CancelAsync runs its callbacks on the thread pool, outside the workflow scheduler; Cancel() runs them inline
        stop.Cancel();
#pragma warning restore MA0042
    }
}
