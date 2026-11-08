using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Features.RunWorkflow;

/// <summary>
/// Starts a run of a workflow's active revision by hand: refuses an unknown workflow (SND-GRAPH-008), one with no active revision (SND-GRAPH-105)
/// and a payload that is not a JSON object (SND-GRAPH-106), then asks <see cref="IGraphRunStarter"/>, which starts, queues or skips by the workflow's concurrency.
/// </summary>
/// <param name="workflows">The workflows and their revisions.</param>
/// <param name="runs">The runs; the next manual occurrence comes from them.</param>
/// <param name="starter">Starts, queues or skips the run.</param>
internal sealed class RunWorkflowService(IWorkflowStore workflows, IWorkflowRunStore runs, IGraphRunStarter starter) : IService<RunWorkflowRequest, RunWorkflowResponse>
{
    private const string TriggerKind = "manual";

    public async ValueTask<Result<RunWorkflowResponse>> Handle(RunWorkflowRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var payload = Payload(request.Payload);
        if (payload.IsError)
        {
            return payload.Error;
        }

        var listed = await workflows.List(includeArchived: false, cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        var record = listed.Value.FirstOrDefault(r => r.Slug == request.Slug);
        if (record is null)
        {
            return SindriErrors.GraphNotFound.With($"no workflow '{request.Slug.Value}'; list the workflows to see what exists");
        }

        if (record.ActiveRevision is not { } active)
        {
            return SindriErrors.GraphNotActive.With($"'{request.Slug.Value}' has no active revision; activate one first");
        }

        var revision = await workflows.GetRevision(record.Id, active, cancellationToken).ConfigureAwait(false);
        if (revision.IsError)
        {
            return revision.Error;
        }

        var trigger = new JsonObject { ["kind"] = TriggerKind, ["payload"] = payload.Value, ["by"] = request.By }.ToJsonString();
        return await Start(new Trigger(record, revision.Value, trigger), cancellationToken).ConfigureAwait(false);
    }

    private static Result<JsonObject?> Payload(string? text)
    {
        if (text is null)
        {
            return (JsonObject?)null;
        }

        try
        {
            return JsonNode.Parse(text) is JsonObject payload ? payload : SindriErrors.GraphBadPayload.With("the payload is valid JSON but not an object; wrap it, for example {\"value\": ...}");
        }
        catch (JsonException e)
        {
            return SindriErrors.GraphBadPayload.With($"the payload is not valid JSON: {e.Message}");
        }
    }

    private async ValueTask<Result<RunWorkflowResponse>> Start(Trigger trigger, CancellationToken cancellationToken)
    {
        var last = await runs.LatestOccurrence(trigger.Record.Id, TriggerKind, cancellationToken).ConfigureAwait(false);
        if (last.IsError)
        {
            return last.Error;
        }

        var occurrence = (last.Value ?? 0) + 1;
        // why: two manual runs at once read the same latest number; the starter refuses the second with SND-GRAPH-100, which gets the next number once.
        for (var attempt = 0; ; attempt++)
        {
            var request = new GraphStartRequest(trigger.Record.Id, trigger.Record.Slug, trigger.Revision.Revision, occurrence, trigger.Json, Concurrency(trigger.Revision.Spec.Concurrency));
            var started = await starter.Start(request, cancellationToken).ConfigureAwait(false);
            if (started.IsSuccess)
            {
                return Answer(started.Value);
            }

            if (attempt > 0 || started.Error.Code != SindriErrors.GraphRunIdTaken.Code)
            {
                return started.Error;
            }

            occurrence++;
        }
    }

    private static RunWorkflowResponse Answer(GraphStart start) => start.Kind switch
    {
        GraphStartKind.Skipped => new RunWorkflowResponse(start.RunId, "skipped", "another run is in flight and the workflow runs one at a time"),
        GraphStartKind.Queued => new RunWorkflowResponse(start.RunId, "queued", "another run is in flight; this one waits its turn"),
        GraphStartKind.Started => new RunWorkflowResponse(start.RunId, "started", Reason: null),
        _ => throw new ArgumentOutOfRangeException(nameof(start)),
    };

    private static GraphConcurrency Concurrency(string? mode) => mode switch
    {
        "queue" => GraphConcurrency.Queue,
        "unlimited" => GraphConcurrency.Unlimited,
        _ => GraphConcurrency.OnePerWorkflow,
    };

    private sealed record Trigger(WorkflowRecord Record, WorkflowRevision Revision, string Json);
}
