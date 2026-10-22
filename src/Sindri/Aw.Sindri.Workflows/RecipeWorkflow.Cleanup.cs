using Aw.Sindri.Core.Workflow.Engine.Instances;
using Microsoft.Extensions.Logging;
using Temporalio.Common;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Engine-owned cleanup (engine P7 plan D9, IDEA-033): after any ending, 'cancelled' included, the engine frees the item's
/// claim (the marker ref and the 'workflow_claims' row) if this instance still owns it. It runs in a detached cancellation
/// scope, so a cancel that cancels the workflow itself doesn't skip it. P8's per-run worktree removal joins it here. A history
/// from before engine P7 (no 'p7-children' marker) runs no cleanup, as recorded.
/// </summary>
internal sealed partial class RecipeWorkflow
{
    private static readonly ActivityOptions _cleanupOptions = new()
    {
        StartToCloseTimeout = TimeSpan.FromSeconds(60),
        RetryPolicy = new RetryPolicy { MaximumAttempts = 5 },
        CancellationToken = CancellationToken.None,   // detached: a cancelled workflow still runs its cleanup
    };

    private async Task Cleanup(WorkflowInstance ended)
    {
        await RemovePhaseWorkspace(ended).ConfigureAwait(true);   // LN-WF2: a phase's worktree goes with it, unless its pull request may be open
        if (!_children || _start is not { } start || _repo is not { } repo)
        {
            return;
        }

        var run = new ReleaseRun(start.WorkflowId().Value, start.Item.Value, repo);
        try
        {
            _ = await Workflow.ExecuteActivityAsync((CleanupActivities a) => a.ReleaseClaimAsync(run), _cleanupOptions).ConfigureAwait(true);
        }
        catch (Exception failed) when (IsActivityFailure(failed))
        {
            NotReleased(Workflow.Logger, run.Item, run.Address, failed.Message);   // boundary: a failed cleanup must not change how the instance ended
        }
    }

    private async Task RemovePhaseWorkspace(WorkflowInstance ended)
    {
        if (!Workflow.Patched("ln-wf2-phase-workspace-removal") || _start is not { } start || PhaseWorkspace.ToRemove(start.Recipe.Id, ended.Facts, ended.Outcome) is not { } workspace)
        {
            return;
        }

        try
        {
            _ = await Workflow.ExecuteActivityAsync((CleanupActivities a) => a.RemovePhaseWorkspaceAsync(workspace), _cleanupOptions).ConfigureAwait(true);
        }
        catch (Exception failed) when (IsActivityFailure(failed))
        {
            WorkspaceKept(Workflow.Logger, ended.Address.Value, failed.Message);   // boundary: a failed cleanup must not change how the instance ended
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "the phase worktree of {Address} was not removed: {Why}")]
    private static partial void WorkspaceKept(ILogger logger, string address, string why);

    [LoggerMessage(Level = LogLevel.Warning, Message = "the claim of {Item} was not released after {Address} ended: {Why}")]
    private static partial void NotReleased(ILogger logger, string item, string address, string why);
}
