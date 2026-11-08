using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Infrastructure.Ledger;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>'workflow_run' and 'workflow_node_run' in ledger.db (issue 2258, PR 6): the run id is the primary key, so a second insert of one id is refused. A read never creates or migrates the file.</summary>
/// <param name="database">The ledger file.</param>
/// <param name="clock">The clock the end times come from.</param>
internal sealed class SqliteWorkflowRunStore(LedgerDatabase database, TimeProvider clock) : IWorkflowRunStore
{
    public ValueTask<Result<WorkflowRun>> Insert(WorkflowRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        return RecipeSessions.Write(database, async ValueTask<Result<WorkflowRun>> (session, token) =>
        {
            // why: checked in the insert's transaction, so a used id is SND-GRAPH-100 and never a primary-key error.
            var existing = await FindRun(session, run.Id, token).ConfigureAwait(false);
            if (existing.IsError || existing.Value is not null)
            {
                return existing.IsError ? existing.Error : SindriErrors.GraphRunIdTaken.With($"the run '{run.Id.Value}' already exists; a trigger that fires twice is one run");
            }

            var inserted = await RecipeSessions.Execute(session, new SqlStatement(WorkflowRunSql.InsertRun,
                c => c.Bind("$id", run.Id.Value).Bind("$workflow", run.WorkflowId.Value).Bind("$revision", run.Revision).Bind("$trigger", run.Trigger)
                    .Bind("$status", RunStatusText.Of(run.Status)).Bind("$outputs", run.Outputs).Bind("$error", run.Error)
                    .Bind("$item", run.ItemId?.Value).Bind("$at", run.StartedAt), $"run {run.Id.Value}"), token).ConfigureAwait(false);
            if (inserted.IsError)
            {
                return inserted.Error;
            }

            if (run.EndedAt is { } ended)
            {
                var closed = await RecipeSessions.Execute(session, new SqlStatement(WorkflowRunSql.SetEnded, c => c.Bind("$id", run.Id.Value).Bind("$at", ended)), token).ConfigureAwait(false);
                if (closed.IsError)
                {
                    return closed.Error;
                }
            }

            return run;
        }, cancellationToken);
    }

    public ValueTask<Result<GraphStartKind>> Admit(WorkflowRun run, GraphConcurrency mode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        // why: the in-flight read and the insert share one write transaction, so two triggers cannot both see "nothing in flight".
        return RecipeSessions.Write(database, async ValueTask<Result<GraphStartKind>> (session, token) =>
        {
            var existing = await FindRun(session, run.Id, token).ConfigureAwait(false);
            if (existing.IsError || existing.Value is not null)
            {
                return existing.IsError ? existing.Error : SindriErrors.GraphRunIdTaken.With($"the run '{run.Id.Value}' already exists; a trigger that fires twice is one run");
            }

            return await Decide(session, run, mode, token).ConfigureAwait(false);
        }, cancellationToken);
    }

    public ValueTask<Result<WorkflowRun?>> PromoteOldestQueued(WorkflowId workflow, CancellationToken cancellationToken) => throw new NotSupportedException();

    public async ValueTask<Result<WorkflowRun>> Get(WorkflowRunId id, CancellationToken cancellationToken)
    {
        var found = await RecipeSessions.Read(database, LedgerDatabase.WorkflowRunsVersion, empty: null, (session, token) => FindRun(session, id, token), cancellationToken).ConfigureAwait(false);
        return found.IsError ? found.Error : found.Value ?? (Result<WorkflowRun>)NotFound(id);
    }

    public ValueTask<Result<IReadOnlyList<WorkflowRun>>> InFlight(WorkflowId workflow, CancellationToken cancellationToken) =>
        RecipeSessions.Read(database, LedgerDatabase.WorkflowRunsVersion, [],
            (session, token) => RecipeSessions.List(session, new SqlStatement(WorkflowRunSql.InFlight, c => c.Bind("$workflow", workflow.Value)), WorkflowRunMapper.Instance, token), cancellationToken);

    public async ValueTask<Result<int?>> LatestOccurrence(WorkflowId workflow, string triggerKind, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(triggerKind);
        var latest = await RecipeSessions.Read(database, LedgerDatabase.WorkflowRunsVersion, [],
            (session, token) => RecipeSessions.List(session, new SqlStatement(WorkflowRunSql.LatestOfKind, c => c.Bind("$workflow", workflow.Value).Bind("$kind", triggerKind)), WorkflowRunMapper.Instance, token), cancellationToken).ConfigureAwait(false);
        return latest.IsError ? latest.Error : latest.Value.Count == 0 ? (int?)null : latest.Value[0].Id.Occurrence;
    }

    public ValueTask<Result<WorkflowRun>> SetStatus(WorkflowRunId id, WorkflowRunStatus status, string? outputs, string? error, CancellationToken cancellationToken) =>
        RecipeSessions.Write(database, async ValueTask<Result<WorkflowRun>> (session, token) =>
        {
            var existing = await FindRun(session, id, token).ConfigureAwait(false);
            if (existing.IsError || existing.Value is null)
            {
                return existing.IsError ? existing.Error : NotFound(id);
            }

            var ended = RunStatusText.IsFinal(status) ? clock.GetUtcNow().UtcDateTime.ToString("O", CultureInfo.InvariantCulture) : null;
            var moved = await RecipeSessions.Execute(session, new SqlStatement(WorkflowRunSql.SetStatus,
                c => c.Bind("$id", id.Value).Bind("$status", RunStatusText.Of(status)).Bind("$outputs", outputs).Bind("$error", error).Bind("$ended", ended)), token).ConfigureAwait(false);
            if (moved.IsError)
            {
                return moved.Error;
            }

            var after = await FindRun(session, id, token).ConfigureAwait(false);
            return after.IsError ? after.Error : after.Value ?? (Result<WorkflowRun>)NotFound(id);
        }, cancellationToken);

    public ValueTask<Result<NodeRun>> RecordNodeRun(NodeRun nodeRun, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nodeRun);
        var input = NodeRunLimits.Cap(nodeRun.Input, out var inputCut);
        var output = NodeRunLimits.Cap(nodeRun.Output, out var outputCut);
        var stored = nodeRun with { Input = input, Output = output, Truncated = nodeRun.Truncated || inputCut || outputCut };
        return RecipeSessions.Write(database, async ValueTask<Result<NodeRun>> (session, token) =>
        {
            var existing = await FindRun(session, stored.RunId, token).ConfigureAwait(false);
            if (existing.IsError || existing.Value is null)
            {
                return existing.IsError ? existing.Error : NotFound(stored.RunId);
            }

            var written = await RecipeSessions.Execute(session, new SqlStatement(WorkflowRunSql.UpsertNodeRun,
                c => c.Bind("$run", stored.RunId.Value).Bind("$node", stored.NodeId.Value).Bind("$item", stored.ItemIndex).Bind("$status", RunStatusText.Of(stored.Status))
                    .Bind("$input", stored.Input).Bind("$output", stored.Output).Bind("$truncated", stored.Truncated ? 1 : 0).Bind("$error", stored.Error).Bind("$usage", stored.Usage),
                $"node {stored.NodeId.Value}"), token).ConfigureAwait(false);
            return written.IsError ? written.Error : stored;
        }, cancellationToken);
    }

    public ValueTask<Result<IReadOnlyList<NodeRun>>> NodeRuns(WorkflowRunId id, CancellationToken cancellationToken) =>
        RecipeSessions.Read(database, LedgerDatabase.WorkflowRunsVersion, [],
            (session, token) => RecipeSessions.List(session, new SqlStatement(WorkflowRunSql.SelectNodeRuns, c => c.Bind("$run", id.Value)), NodeRunMapper.Instance, token), cancellationToken);

    private static async ValueTask<Result<GraphStartKind>> Decide(ISqliteSession session, WorkflowRun run, GraphConcurrency mode, CancellationToken token)
    {
        var inFlight = mode == GraphConcurrency.Unlimited ? 0 : await CountInFlight(session, run.WorkflowId, token).ConfigureAwait(false);
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
                .Bind("$item", run.ItemId?.Value).Bind("$at", run.StartedAt), $"run {run.Id.Value}"), token).ConfigureAwait(false);
        return inserted.IsError ? inserted.Error : busy ? GraphStartKind.Queued : GraphStartKind.Started;
    }

    private static async ValueTask<int> CountInFlight(ISqliteSession session, WorkflowId workflow, CancellationToken token)
    {
        var inFlight = await RecipeSessions.List(session, new SqlStatement(WorkflowRunSql.InFlight, c => c.Bind("$workflow", workflow.Value)), WorkflowRunMapper.Instance, token).ConfigureAwait(false);
        return inFlight.IsError ? -1 : inFlight.Value.Count;
    }

    private static ValueTask<Result<WorkflowRun?>> FindRun(ISqliteSession session, WorkflowRunId id, CancellationToken token) =>
        RecipeSessions.First(session, new SqlStatement(WorkflowRunSql.ById, c => c.Bind("$id", id.Value)), WorkflowRunMapper.Instance, token);

    private static Error NotFound(WorkflowRunId id) => SindriErrors.GraphRunNotFound.With($"no run '{id.Value}'; list the workflow's runs to see what exists");
}
