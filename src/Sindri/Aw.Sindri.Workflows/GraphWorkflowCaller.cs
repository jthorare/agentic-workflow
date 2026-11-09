using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Guards;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Starts the workflow a 'run_workflow' node names (agent-first PR 9) and, with 'wait', polls its run until it ends. The callee's run id is made from the caller's run, node and index
/// (<see cref="CallOccurrence"/>), so a node that runs twice for one run starts one callee run. The callee must declare a 'workflow_call' trigger.
/// </summary>
/// <param name="workflows">The workflows and their revisions.</param>
/// <param name="runs">The runs; a waiting node reads its called run here.</param>
/// <param name="starter">Starts, queues or skips the run.</param>
/// <param name="profile">The approved profile, for 'workflow.callDepth'.</param>
/// <param name="clock">The clock a wait sleeps on.</param>
internal sealed class GraphWorkflowCaller(IWorkflowStore workflows, IWorkflowRunStore runs, IGraphRunStarter starter, IApprovedProfile profile, TimeProvider clock) : IGraphCallPort
{
    private const string TriggerKind = "workflow_call";

    private static readonly TimeSpan _poll = TimeSpan.FromSeconds(1);

    public async ValueTask<int> MaxDepth(CancellationToken cancellationToken)
    {
        var latest = await profile.Latest(cancellationToken).ConfigureAwait(false);
        return latest.IsSuccess && latest.Value?.Profile.Workflow?.CallDepth is { } set ? set : CallDepth.DefaultMax;
    }

    public async ValueTask<Result<GraphCalled>> Start(GraphCall call, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(call);
        var callee = await Callee(call.Target, cancellationToken).ConfigureAwait(false);
        if (callee.IsError)
        {
            return callee.Error;
        }

        var (record, revision) = callee.Value;
        var occurrence = CallOccurrence.Of(call.Caller.Value, call.Node.Value, call.Index);
        var request = new GraphStartRequest(record.Id, record.Slug, revision.Revision, occurrence, Trigger(call), Concurrency(revision.Spec.Concurrency));
        var started = await starter.Start(request, cancellationToken).ConfigureAwait(false);
        if (started.IsSuccess)
        {
            return new GraphCalled(started.Value.RunId, Status(started.Value.Kind));
        }

        // why: the same call twice is one run: the id is taken, so the run the first call started is the answer
        return string.Equals(started.Error.Code, SindriErrors.GraphRunIdTaken.Code, StringComparison.Ordinal)
            ? await Existing(WorkflowRunId.Of(record.Slug, revision.Revision, occurrence), cancellationToken).ConfigureAwait(false)
            : started.Error;
    }

    public async ValueTask<Result<GraphCallEnd>> Wait(WorkflowRunId run, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = clock.GetUtcNow() + timeout;
        while (true)
        {
            var row = await runs.Get(run, cancellationToken).ConfigureAwait(false);
            if (row.IsError)
            {
                return row.Error;
            }

            if (Ended(row.Value.Status) is { } name)
            {
                return new GraphCallEnd(name, Outputs(row.Value.Outputs), row.Value.Error);
            }

            if (clock.GetUtcNow() >= deadline)
            {
                return SindriErrors.GraphBlockFailed.With(string.Create(CultureInfo.InvariantCulture, $"the called run {run.Value} did not end within {(int)timeout.TotalSeconds} seconds (it keeps running; fix: raise 'waitSeconds', or leave 'wait' off)"));
            }

            await Task.Delay(_poll, clock, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<Result<(WorkflowRecord Record, WorkflowRevision Revision)>> Callee(WorkflowSlug slug, CancellationToken cancellationToken)
    {
        var listed = await workflows.List(includeArchived: false, cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        if (listed.Value.FirstOrDefault(r => r.Slug == slug) is not { } record)
        {
            return SindriErrors.GraphNotFound.With($"no workflow '{slug.Value}'; list the workflows to see what exists");
        }

        if (record.ActiveRevision is not { } active)
        {
            return SindriErrors.GraphNotActive.With($"'{slug.Value}' has no active revision; activate one first");
        }

        var revision = await workflows.GetRevision(record.Id, active, cancellationToken).ConfigureAwait(false);
        if (revision.IsError)
        {
            return revision.Error;
        }

        if (!revision.Value.Spec.Nodes.Any(static n => string.Equals(n.Type, TriggerKind, StringComparison.Ordinal)))
        {
            return SindriErrors.GraphBlockFailed.With($"'{slug.Value}' has no workflow_call trigger, so no other workflow can start it (fix: add a workflow_call node to it)");
        }

        var callee = (Record: record, Revision: revision.Value);
        return callee;
    }

    private async ValueTask<Result<GraphCalled>> Existing(WorkflowRunId id, CancellationToken cancellationToken)
    {
        var row = await runs.Get(id, cancellationToken).ConfigureAwait(false);
        return row.IsError ? row.Error : new GraphCalled(id, row.Value.Status == WorkflowRunStatus.Queued ? "queued" : "started");
    }

    private static string Trigger(GraphCall call) => new JsonObject
    {
        ["kind"] = TriggerKind,
        ["payload"] = call.Payload?.DeepClone(),
        ["callDepth"] = call.Depth,
        ["parentRun"] = call.Caller.Value,
        ["by"] = "workflow:" + CallDepth.SlugOf(call.Caller.Value),
    }.ToJsonString();

    private static string Status(GraphStartKind kind) => kind switch
    {
        GraphStartKind.Started => "started",
        GraphStartKind.Queued => "queued",
        GraphStartKind.Skipped => "skipped",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string? Ended(WorkflowRunStatus status) => status switch
    {
        WorkflowRunStatus.Succeeded => "succeeded",
        WorkflowRunStatus.Failed => "failed",
        WorkflowRunStatus.Cancelled => "cancelled",
        WorkflowRunStatus.Queued or WorkflowRunStatus.Running or WorkflowRunStatus.Waiting => null,
        _ => null,
    };

    private static JsonNode? Outputs(string? text)
    {
        try
        {
            return text is null ? null : JsonNode.Parse(text);
        }
        catch (JsonException)   // boundary: a stored output that is not JSON is no output
        {
            return null;
        }
    }

    private static GraphConcurrency Concurrency(string? mode) => mode switch
    {
        "queue" => GraphConcurrency.Queue,
        "unlimited" => GraphConcurrency.Unlimited,
        _ => GraphConcurrency.OnePerWorkflow,
    };
}
