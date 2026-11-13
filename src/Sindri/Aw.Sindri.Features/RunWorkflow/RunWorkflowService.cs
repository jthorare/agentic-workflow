using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Features.RunWorkflow;

/// <summary>
/// Starts a run of a workflow's active revision by hand: refuses an unknown workflow (SND-GRAPH-008), one with no active revision (SND-GRAPH-105)
/// and a payload that is not a JSON object (SND-GRAPH-106), and a loop (SND-GRAPH-109: the caller's session was launched by a run of this same workflow), then asks <see cref="IGraphRunStarter"/>, which starts, queues or skips by the workflow's concurrency.
/// </summary>
/// <param name="workflows">The workflows and their revisions.</param>
/// <param name="runs">The runs; the next manual occurrence comes from them.</param>
/// <param name="starter">Starts, queues or skips the run.</param>
/// <param name="launches">Which run launched a session; the loop guard (SND-GRAPH-109) reads it.</param>
/// <param name="callers">The caller; its launch is the session the guard looks up.</param>
internal sealed class RunWorkflowService(IWorkflowStore workflows, IWorkflowRunStore runs, IGraphRunStarter starter, IGraphLaunchRuns launches, ICallerContextAccessor callers) : IService<RunWorkflowRequest, RunWorkflowResponse>
{
    private const string TriggerKind = "manual";
    private const int Page = 200;
    private const string SetupActor = "service:setup";
    private static readonly WorkflowSlug _helloSindri = WorkflowSlug.From("hello-sindri");

    public async ValueTask<Result<RunWorkflowResponse>> Handle(RunWorkflowRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var payload = Payload(request.Payload);
        if (payload.IsError)
        {
            return payload.Error;
        }

        if (await LoopRefusal(request.Slug, cancellationToken).ConfigureAwait(false) is { } loop)
        {
            return loop;
        }

        var found = await Find(request.Slug, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        var record = found.Value;
        if (record.ActiveRevision is not { } active)
        {
            return SindriErrors.GraphNotActive.With($"'{request.Slug.Value}' has no active revision; activate one first");
        }

        if (await SetupRefusal(record, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        var revision = await workflows.GetRevision(record.Id, active, cancellationToken).ConfigureAwait(false);
        if (revision.IsError)
        {
            return revision.Error;
        }

        var trigger = new JsonObject { ["kind"] = TriggerKind, ["payload"] = payload.Value, ["by"] = request.By }.ToJsonString();
        return await Start(new Trigger(record, revision.Value, trigger), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<WorkflowRecord>> Find(WorkflowSlug slug, CancellationToken cancellationToken)
    {
        var listed = await workflows.List(includeArchived: false, cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        return listed.Value.FirstOrDefault(r => r.Slug == slug) is { } record
            ? record
            : SindriErrors.GraphNotFound.With($"no workflow '{slug.Value}'; list the workflows to see what exists");
    }

    // The 'setup' grant on RunWorkflow is narrow (first-run spec decision 19): 'service:setup' may run only the hello-sindri workflow it made itself, and once. Every other caller is unchanged.
    private async ValueTask<Error?> SetupRefusal(WorkflowRecord record, CancellationToken cancellationToken)
    {
        if (callers.Current is not { Class: CallerClass.Service, Actor: SetupActor })
        {
            return null;
        }

        if (record.Slug != _helloSindri || !string.Equals(record.CreatedBy, SetupActor, StringComparison.Ordinal))
        {
            return SindriErrors.GraphSetupRunRefused.With($"'{record.Slug.Value}' is not the hello-sindri workflow the setup service made");
        }

        // why: the run store has no query by trigger actor, so page through every run of this workflow (a workflow made for one greeting has a handful)
        for (var offset = 0; ; offset += Page)
        {
            var recent = await runs.Recent(record.Id, offset, Page, cancellationToken).ConfigureAwait(false);
            if (recent.IsError)
            {
                return recent.Error;
            }

            if (recent.Value.Any(static r => StartedBySetup(r.Trigger)))
            {
                return SindriErrors.GraphSetupRunRefused.With("the setup service already ran hello-sindri once (fix: sindri wf run hello-sindri)");
            }

            if (recent.Value.Count < Page)
            {
                return null;
            }
        }
    }

    private static bool StartedBySetup(string trigger)
    {
        try
        {
            return string.Equals(JsonNode.Parse(trigger)?["by"]?.GetValue<string>(), SetupActor, StringComparison.Ordinal);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)   // boundary: a stored trigger that is not the shape we write
        {
            return false;
        }
    }

    private async ValueTask<Error?> LoopRefusal(WorkflowSlug slug, CancellationToken cancellationToken)
    {
        if (callers.Current?.Launch is not { } launch)
        {
            return null;
        }

        var parent = await launches.RunOf(launch, cancellationToken).ConfigureAwait(false);
        return parent.IsError ? parent.Error : WorkflowLoopRule.Decide(parent.Value, slug);
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
