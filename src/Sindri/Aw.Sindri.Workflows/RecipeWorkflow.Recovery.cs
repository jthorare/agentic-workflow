using Temporalio.Common;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Recovering an agent run whose session ended with no report (issue #1000): the result it left in its output folder.</summary>
internal sealed partial class RecipeWorkflow
{
    private static readonly AgentCall _recoverCall = new("agent-recover", TimeSpan.FromMinutes(2), Heartbeat: null, new RetryPolicy { InitialInterval = TimeSpan.FromSeconds(1), MaximumAttempts = 3 });
    private (int Entry, DateTimeOffset At)? _launchedAt;   // when the state entry's first session was asked for: an output older than this is an earlier Step's.
    private bool? _recoverStranded;
    private bool RecoverStranded => _recoverStranded ??= Workflow.Patched("recover-stranded-agent-result");   // issue #1000: a history without it fails an ended session's Step without reading its output folder

    // Issue #1000: a session that ended with no report may have left its result in the output folder (its 'workflow complete' got no answer while the control plane restarted).
    // The run completes from it when the folder proves one outcome; null (the Step fails with the session's own end) when the build predates this, the launch had no run directory,
    // the folder is empty, or the read failed.
    private async Task<AgentRecovered?> Recover(AgentRunning running)
    {
        if (!RecoverStranded || running.Launched?.Worktree is not { } worktree)
        {
            return null;
        }

        var request = new AgentRecoverRun(worktree, running.Run.Step.Block, running.Open.Token.Value, _launchedAt?.At);   // why: the activity lambda is an expression tree, which takes no '?.'
        try
        {
            return await Workflow.ExecuteActivityAsync((AgentStepActivities a) => a.RecoverAsync(request), AgentOptions(_recoverCall, running.Open, running.State, CancellationToken.None)).ConfigureAwait(true);
        }
        catch (ActivityFailureException)
        {
            return null;   // boundary: a folder that can't be read must not wedge the instance; the Step fails with the session's own end
        }
    }
}
