using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Loops;
using Aw.Sindri.Core.Graph.Rerun;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Features.RerunWorkflowRun;

/// <summary>
/// Re-runs an ended run from one step (Workflows page spec §7.3, P7): a new run of the source run's revision that takes the stored node runs of every step the chosen step does not feed,
/// runs the chosen step and everything after it again, and carries the source run's id in its trigger. Refuses an unknown run (SND-GRAPH-101), a deleted workflow (SND-GRAPH-008),
/// a step the revision lacks or a loop body step (SND-GRAPH-001), a run that has not ended (SND-GRAPH-125), a step before the chosen one that did not succeed (SND-GRAPH-126),
/// an output the store cut at 64 KB (SND-GRAPH-108) and an input that is not a JSON object (SND-GRAPH-106).
/// </summary>
/// <param name="workflows">The workflows and their revisions.</param>
/// <param name="runs">The source run and its node runs; the next occurrence comes from them.</param>
/// <param name="starter">Starts or queues the new run and stores the restored node runs before its walk.</param>
internal sealed class RerunWorkflowRunService(IWorkflowStore workflows, IWorkflowRunStore runs, IGraphRunStarter starter) : IService<RerunWorkflowRunRequest, RerunWorkflowRunResponse>
{
    private const string TriggerKind = WorkflowRunNumbers.Rerun;
    private const int Attempts = 50;

    public async ValueTask<Result<RerunWorkflowRunResponse>> Handle(RerunWorkflowRunRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var input = Input(request.Input);
        var source = input.IsError ? input.Error : await runs.Get(request.Run, cancellationToken).ConfigureAwait(false);
        if (source.IsError)
        {
            return source.Error;
        }

        if (source.Value.Status is WorkflowRunStatus.Queued or WorkflowRunStatus.Running or WorkflowRunStatus.Waiting)
        {
            return SindriErrors.GraphRerunNotEnded.With($"'{request.Run.Value}' is {source.Value.Status}; wait for it to end or cancel it first");
        }

        var plan = await Prepare(source.Value, request.FromNode, cancellationToken).ConfigureAwait(false);
        return plan.IsError ? plan.Error : await Start(request, source.Value, plan.Value, input.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Plan>> Prepare(WorkflowRun source, string fromNode, CancellationToken cancellationToken)
    {
        var listed = await workflows.List(includeArchived: false, cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        if (listed.Value.FirstOrDefault(r => r.Id == source.WorkflowId) is not { } record)
        {
            return SindriErrors.GraphNotFound.With("the run's workflow was deleted; its runs open read-only");
        }

        var revision = await workflows.GetRevision(source.WorkflowId, source.Revision, cancellationToken).ConfigureAwait(false);
        if (revision.IsError)
        {
            return revision.Error;
        }

        var rows = await runs.NodeRuns(source.Id, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        var spec = revision.Value.Spec;
        if (!NodeId.TryFrom(fromNode, out var from) || spec.Nodes.All(n => n.Id != from))
        {
            return SindriErrors.GraphInvalid.With($"revision {source.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture)} has no step '{fromNode}'");
        }

        var restored = RerunPlan.Restored(spec, from, rows.Value);
        var refused = LoopRefusal(spec, from) ?? UpstreamRefusal(spec, from, restored);
        return refused is { } error ? error : Split(record, spec, restored, rows.Value);
    }

    // why: a step inside a for_each body runs once per item; restoring the loop's own row and then running one body step alone would skip the loop. The owner re-runs from the for_each step.
    private static Error? LoopRefusal(GraphSpec spec, NodeId from)
    {
        foreach (var node in spec.Nodes.Where(static n => string.Equals(n.Type, LoopBody.ForEachType, StringComparison.Ordinal)))
        {
            if (LoopBody.Find(spec, node.Id) is { IsSuccess: true } loop && loop.Value.PerItem.Any(n => n.Id == from))
            {
                return SindriErrors.GraphInvalid.With($"'{from.Value}' runs once per item of '{node.Id.Value}'; re-run from '{node.Id.Value}' instead");
            }
        }

        return null;
    }

    // why: a step before the chosen one is taken from the source run; one that failed, never ran, or was skipped only because the walk halted is not, so the chosen step would read nothing
    private static Error? UpstreamRefusal(GraphSpec spec, NodeId from, IReadOnlyList<NodeId> restored)
    {
        var missing = RerunPlan.Before(spec, from).Where(n => !restored.Contains(n)).Take(1).ToList();
        return missing.Count == 0
            ? null
            : SindriErrors.GraphRerunUpstreamNotDone.With($"'{missing[0].Value}' comes before '{from.Value}' and did not succeed in that run; re-run from '{missing[0].Value}' instead");
    }

    private static Result<Plan> Split(WorkflowRecord record, GraphSpec spec, IReadOnlyList<NodeId> restored, IReadOnlyList<NodeRun> rows)
    {
        var cut = rows.FirstOrDefault(r => restored.Contains(r.NodeId) && r.Truncated);
        if (cut is not null)
        {
            return SindriErrors.GraphOutputTruncated.With($"the output of '{cut.NodeId.Value}' was cut at 64 KB in that run, so it can't be restored; re-run from '{cut.NodeId.Value}' instead");
        }

        return new Plan(record, spec, restored, [.. spec.Nodes.Select(static n => n.Id).Where(id => !restored.Contains(id))], [.. rows.Where(r => restored.Contains(r.NodeId))]);
    }

    private async ValueTask<Result<RerunWorkflowRunResponse>> Start(RerunWorkflowRunRequest request, WorkflowRun source, Plan plan, JsonObject? input, CancellationToken cancellationToken)
    {
        var next = await WorkflowRunNumbers.Next(runs, source.WorkflowId, cancellationToken).ConfigureAwait(false);
        if (next.IsError)
        {
            return next.Error;
        }

        var trigger = Trigger(request, source, input).ToJsonString();
        var concurrency = Concurrency(plan.Spec.Concurrency);
        var occurrence = next.Value;
        // why: manual runs and re-runs share one count (WorkflowRunNumbers), but two starts at once read the same number, and a hashed connection or call run can hold one; the starter refuses a used id with SND-GRAPH-100 and the next number is tried.
        for (var attempt = 0; attempt < Attempts; attempt++, occurrence++)
        {
            var id = WorkflowRunId.Of(plan.Record.Slug, source.Revision, occurrence);
            var restored = plan.Rows.Select(r => r with { RunId = id }).ToList();
            var started = await starter.Start(new GraphStartRequest(source.WorkflowId, plan.Record.Slug, source.Revision, occurrence, trigger, concurrency, Restored: restored), cancellationToken).ConfigureAwait(false);
            if (started.IsSuccess)
            {
                return Answer(started.Value, request.Run, plan);
            }

            if (started.Error.Code != SindriErrors.GraphRunIdTaken.Code)
            {
                return started.Error;
            }
        }

        return SindriErrors.GraphRunIdTaken.With("no free run number after 50 tries");
    }

    private static JsonObject Trigger(RerunWorkflowRunRequest request, WorkflowRun source, JsonObject? input)
    {
        var original = Parse(source.Trigger);
        var trigger = new JsonObject();
        // why: the source trigger's fields stay at the top, so '{{trigger.payload.repo}}' in a step that runs again reads what it read the first time.
        foreach (var (key, value) in original ?? [])
        {
            trigger[key] = value?.DeepClone();
        }

        trigger["kind"] = TriggerKind;
        trigger["of"] = request.Run.Value;
        trigger["fromNode"] = request.FromNode;
        trigger["by"] = request.By;
        trigger["original"] = original?.DeepClone();
        trigger["input"] = input?.DeepClone();
        return trigger;
    }

    private static JsonObject? Parse(string text)
    {
        try
        {
            return JsonNode.Parse(text) as JsonObject;
        }
        catch (JsonException)   // boundary: a stored trigger that is not JSON is carried as no original
        {
            return null;
        }
    }

    private static Result<JsonObject?> Input(string? text)
    {
        if (text is null)
        {
            return (JsonObject?)null;
        }

        try
        {
            return JsonNode.Parse(text) is JsonObject input ? input : SindriErrors.GraphBadPayload.With("the input is valid JSON but not an object");
        }
        catch (JsonException e)
        {
            return SindriErrors.GraphBadPayload.With($"the input is not valid JSON: {e.Message}");
        }
    }

    private static Result<RerunWorkflowRunResponse> Answer(GraphStart start, WorkflowRunId source, Plan plan) => start.Kind switch
    {
        GraphStartKind.Started => new RerunWorkflowRunResponse(start.RunId, "started", source, Names(plan.Restored), Names(plan.Again)),
        GraphStartKind.Queued => new RerunWorkflowRunResponse(start.RunId, "queued", source, Names(plan.Restored), Names(plan.Again)),
        GraphStartKind.Skipped => new RerunWorkflowRunResponse(start.RunId, "skipped", source, [], []),
        _ => throw new ArgumentOutOfRangeException(nameof(start)),
    };

    private static List<string> Names(IReadOnlyList<NodeId> ids) => [.. ids.Select(static i => i.Value)];

    private static GraphConcurrency Concurrency(string? mode) => mode switch
    {
        "queue" => GraphConcurrency.Queue,
        "unlimited" => GraphConcurrency.Unlimited,
        _ => GraphConcurrency.OnePerWorkflow,
    };

    private sealed record Plan(WorkflowRecord Record, GraphSpec Spec, IReadOnlyList<NodeId> Restored, IReadOnlyList<NodeId> Again, IReadOnlyList<NodeRun> Rows);
}
