using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>The admission rule of <see cref="SqliteWorkflowRunStore.Admit"/>: how many live runs are in flight and whether a trigger's run starts, queues or is skipped, inside the caller's write.</summary>
internal sealed partial class SqliteWorkflowRunStore
{
    private static async ValueTask<Result<GraphStartKind>> Decide(ISqliteSession session, WorkflowRun run, GraphConcurrency mode, CancellationToken token)
    {
        // why: a test run neither waits for a live run nor makes one wait (spec §3.2), so it is never counted against and never queued.
        var inFlight = mode == GraphConcurrency.Unlimited || run.Mode == WorkflowRunMode.Test ? 0 : await CountInFlight(session, run.WorkflowId, token).ConfigureAwait(false);
        if (inFlight < 0)
        {
            return SindriErrors.LedgerUnavailable.With("could not read the runs in flight");
        }

        var busy = inFlight > 0;
        if (busy && mode == GraphConcurrency.OnePerWorkflow)
        {
            return GraphStartKind.Skipped;
        }

        var status = busy ? WorkflowRunStatus.Queued : WorkflowRunStatus.Running;
        var inserted = await RecipeSessions.Execute(session, new SqlStatement(WorkflowRunSql.InsertRun,
            c => c.Bind("$id", run.Id.Value).Bind("$workflow", run.WorkflowId.Value).Bind("$revision", run.Revision).Bind("$trigger", run.Trigger)
                .Bind("$status", RunStatusText.Of(status)).Bind("$outputs", run.Outputs).Bind("$error", run.Error)
                .Bind("$item", run.ItemId?.Value).Bind("$at", run.StartedAt).Bind("$mode", run.Mode).Bind("$options", run.TestOptions), $"run {run.Id.Value}"), token).ConfigureAwait(false);
        return inserted.IsError ? inserted.Error : busy ? GraphStartKind.Queued : GraphStartKind.Started;
    }

    private static async ValueTask<int> CountInFlight(ISqliteSession session, WorkflowId workflow, CancellationToken token)
    {
        var inFlight = await RecipeSessions.List(session, new SqlStatement(WorkflowRunSql.LiveInFlight, c => c.Bind("$workflow", workflow.Value)), WorkflowRunMapper.Instance, token).ConfigureAwait(false);
        return inFlight.IsError ? -1 : inFlight.Value.Count;
    }
}
