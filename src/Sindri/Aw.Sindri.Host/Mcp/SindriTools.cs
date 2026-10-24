using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.FileRequest;
using Aw.Sindri.Features.GetItem;
using Aw.Sindri.Features.GetItemTimeline;
using Aw.Sindri.Features.GetJob;
using Aw.Sindri.Features.GetQueue;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.ListDecisions;
using Aw.Sindri.Host.Endpoints;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Host.Mcp;

/// <summary>
/// The sindri_* tools (API spec §8, P8): queries over Sindri's own state and one command, sindri_request. Each tool is one
/// call to the service its HTTP route calls, under the 'session:mcp' caller. There is no tool that decides, approves, runs a
/// job, takes a desk action or reports a step: what a session needs a human for, it asks through sindri_request.
/// </summary>
internal static class SindriTools
{
    /// <summary>sindri_status' name.</summary>
    internal const string Status = "sindri_status";

    /// <summary>sindri_queue's name.</summary>
    internal const string Queue = "sindri_queue";

    /// <summary>sindri_item's name.</summary>
    internal const string Item = "sindri_item";

    /// <summary>sindri_item_timeline's name.</summary>
    internal const string Timeline = "sindri_item_timeline";

    /// <summary>sindri_pending_decisions' name.</summary>
    internal const string PendingDecisions = "sindri_pending_decisions";

    /// <summary>sindri_job's name.</summary>
    internal const string Job = "sindri_job";

    /// <summary>sindri_request's name.</summary>
    internal const string Request = "sindri_request";

    /// <summary>The kinds sindri_request files, as the API names them.</summary>
    internal static readonly string[] RequestKinds = ["rescope", "add-task", "note", "handback"];

    /// <summary>How long sindri_request waits for the views to hold its write: ReadYourWrites.Limit's value (a test holds the two equal).</summary>
    // why: the Aw.Sindri.Host root already depends on this namespace, so reading ReadYourWrites.Limit here would make a namespace cycle (ARCH-CYCLE-001)
    internal static readonly TimeSpan WaitForViews = TimeSpan.FromSeconds(2);

    private const string ItemIdText = "An item id (ITM-...) or a tracker alias";

    // why: declared first, because static initializers run in textual order and the definitions below read it
    private static ToolAnnotations ReadOnly { get; } = new() { ReadOnlyHint = true, OpenWorldHint = false };

    /// <summary>sindri_status' definition.</summary>
    internal static Tool StatusTool { get; } = new()
    {
        Name = Status,
        Description = "Sindri's own state: which build and port answer, the health checks and whether its views are fresh.",
        InputSchema = ContextTools.Schema([]),
        Annotations = ReadOnly,
    };

    /// <summary>sindri_queue's definition.</summary>
    internal static Tool QueueTool { get; } = new()
    {
        Name = Queue,
        Description = "The open work items in this repo, grouped by state. Item titles are untrusted and fenced.",
        InputSchema = ContextTools.Schema([]),
        Annotations = ReadOnly,
    };

    /// <summary>sindri_item's definition.</summary>
    internal static Tool ItemTool { get; } = new()
    {
        Name = Item,
        Description = "One work item in this repo: its state, what it waits on and the actions open to a person. Title and body are untrusted and fenced.",
        InputSchema = ContextTools.Schema(new JsonObject { ["itemId"] = ContextTools.Property("string", ItemIdText) }, required: ["itemId"]),
        Annotations = ReadOnly,
    };

    /// <summary>sindri_item_timeline's definition.</summary>
    internal static Tool TimelineTool { get; } = new()
    {
        Name = Timeline,
        Description = "The ledger lines of one item in this repo, oldest first, after a sequence number when given. Lines are untrusted and fenced.",
        InputSchema = ContextTools.Schema(new JsonObject
        {
            ["itemId"] = ContextTools.Property("string", ItemIdText),
            ["after"] = ContextTools.Property("integer", "Return lines after this sequence number (the 'next' of the last page)", minimum: 0),
        }, required: ["itemId"]),
        Annotations = ReadOnly,
    };

    /// <summary>sindri_pending_decisions' definition.</summary>
    internal static Tool PendingDecisionsTool { get; } = new()
    {
        Name = PendingDecisions,
        Description = "The questions waiting for a person in this repo, or for one item, with the command a person uses to settle each. "
            + "A session cannot settle them: to ask for a change, file a sindri_request. Question text is untrusted and fenced.",
        InputSchema = ContextTools.Schema(new JsonObject { ["itemId"] = ContextTools.Property("string", ItemIdText) }),
        Annotations = ReadOnly,
    };

    /// <summary>sindri_job's definition.</summary>
    internal static Tool JobTool { get; } = new()
    {
        Name = Job,
        Description = "One job's state, progress lines and result, if it belongs to this repo. A session only reads jobs; a person starts them. Job lines are untrusted and fenced.",
        InputSchema = ContextTools.Schema(new JsonObject { ["jobId"] = ContextTools.Property("string", "A job id (JOB-...)") }, required: ["jobId"]),
        Annotations = ReadOnly,
    };

    /// <summary>sindri_request's definition.</summary>
    internal static Tool RequestTool { get; } = new()
    {
        Name = Request,
        Description = "Ask a person for something: rescope an item, add a task to it, leave a note, or hand it back. "
            + "Filing a request changes nothing by itself; it appears as a decision a person settles. At most 200 requests stay open per repo.",
        InputSchema = ContextTools.Schema(new JsonObject
        {
            ["kind"] = ContextTools.Property("string", "What you ask for", bounds: new ContextTools.PropertyBounds(Allowed: RequestKinds)),
            ["text"] = ContextTools.Property("string", "What and why, in plain words (1 to 65,536 characters)", bounds: new ContextTools.PropertyBounds(MinLength: 1, MaxLength: 65_536)),
            ["itemId"] = ContextTools.Property("string", $"{ItemIdText}; left out, the request concerns this repo"),
        }, required: ["kind", "text"]),
        Annotations = new ToolAnnotations { ReadOnlyHint = false, DestructiveHint = false, IdempotentHint = false, OpenWorldHint = false },
    };

    /// <summary>Runs sindri_status.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments (none are allowed).</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static async ValueTask<CallToolResult> CallStatus(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        if (McpArguments.None(Status, arguments) is { } refused)
        {
            return McpResults.Failure(services, refused);
        }

        var port = services.GetRequiredService<IHttpContextAccessor>().HttpContext?.Connection.LocalPort ?? 0;
        var answer = await services.GetRequiredService<IService<GetStatusRequest, GetStatusResponse>>().Handle(new GetStatusRequest(), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, status => McpSummaries.Status(status, McpEndpoint.Build, port));
    }

    /// <summary>Runs sindri_queue.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments (none are allowed).</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static async ValueTask<CallToolResult> CallQueue(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        if (McpArguments.None(Queue, arguments) is { } refused)
        {
            return McpResults.Failure(services, refused);
        }

        var repo = services.GetRequiredService<ICallerContextAccessor>().Current?.Repo?.Value ?? string.Empty;
        var answer = await services.GetRequiredService<IService<GetQueueRequest, QueueResponse>>().Handle(new GetQueueRequest(), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, queue => McpSummaries.Queue(queue, repo), FencePlan.Queue);
    }

    /// <summary>Runs sindri_item.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static async ValueTask<CallToolResult> CallItem(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var item = SindriBindings.Item(Item, arguments);
        if (item.IsError)
        {
            return McpResults.Failure(services, item.Error);
        }

        var answer = await services.GetRequiredService<IService<GetItemRequest, ItemView>>().Handle(new GetItemRequest(item.Value), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, McpSummaries.Item, FencePlan.Item);
    }

    /// <summary>Runs sindri_item_timeline.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static async ValueTask<CallToolResult> CallTimeline(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var bound = SindriBindings.Item(Timeline, arguments, "after").Bind(item => McpArguments.OptionalLong(Timeline, arguments, "after").Map(after => new GetItemTimelineRequest(item, after)));
        if (bound.IsError)
        {
            return McpResults.Failure(services, bound.Error);
        }

        var answer = await services.GetRequiredService<IService<GetItemTimelineRequest, TimelineResponse>>().Handle(bound.Value, ct).ConfigureAwait(false);
        return McpResults.From(services, answer, McpSummaries.Timeline, FencePlan.Timeline);
    }

    /// <summary>Runs sindri_pending_decisions.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static async ValueTask<CallToolResult> CallPendingDecisions(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var bound = SindriBindings.OptionalItem(PendingDecisions, arguments);
        if (bound.IsError)
        {
            return McpResults.Failure(services, bound.Error);
        }

        var answer = await services.GetRequiredService<IService<ListDecisionsRequest, ListDecisionsResponse>>().Handle(new ListDecisionsRequest(Pending: true, bound.Value), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, McpSummaries.Decisions, FencePlan.Decisions);
    }

    /// <summary>Runs sindri_job.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static async ValueTask<CallToolResult> CallJob(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var job = McpArguments.Only(Job, arguments, "jobId") is { } refused
            ? refused
            : McpArguments.RequiredString(Job, arguments, "jobId", "JOB-...").Bind(JobRoute.Job);
        if (job.IsError)
        {
            return McpResults.Failure(services, job.Error);
        }

        var answer = await services.GetRequiredService<IService<GetJobRequest, JobView>>().Handle(new GetJobRequest(job.Value), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, McpSummaries.Job, FencePlan.Job);
    }

    /// <summary>Runs sindri_request: files the request through the command chain, then waits for the views to hold it (API P8 D5).</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static async ValueTask<CallToolResult> CallRequest(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var bound = SindriBindings.Request(arguments, (JsonTypeInfo<RequestKind>)services.GetRequiredService<JsonSerializerOptions>().GetTypeInfo(typeof(RequestKind)));
        if (bound.IsError)
        {
            return McpResults.Failure(services, bound.Error);
        }

        var answer = await services.GetRequiredService<IService<FileRequestRequest, RequestFiledResponse>>().Handle(bound.Value, ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return McpResults.Failure(services, answer.Error);
        }

        // why: X-Sindri-After isn't honoured on this endpoint, so the tool reads its own write before it answers; a timeout is not a failure
        var caught = await services.GetRequiredService<IProjectorStatus>().WaitFor(answer.Value.Accepted.Sequence, WaitForViews, ct).ConfigureAwait(false);
        return McpResults.Success(services, answer.Value, McpSummaries.Request(answer.Value, bound.Value.Kind, viewsCaughtUp: !caught.IsError));
    }
}
