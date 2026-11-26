using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Features.GetWorkflow;
using Aw.Sindri.Features.GetWorkflowRun;
using Aw.Sindri.Features.ListWorkflowRuns;
using Aw.Sindri.Features.ListWorkflows;
using Aw.Sindri.Host.Endpoints;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Host.Mcp;

/// <summary>
/// The workflow read tools (#2258, PR 8a): workflow_list, workflow_get, workflow_runs and workflow_run_get. Each is one call to the service its HTTP route calls. Any caller may read them, and no
/// X-Sindri-Repo header is needed. Prompts, descriptions and node configs in a spec are text an agent or the owner wrote, so the results fence them as untrusted.
/// </summary>
internal static class WorkflowReadTools
{
    /// <summary>workflow_list's name.</summary>
    internal const string List = "workflow_list";

    /// <summary>workflow_get's name.</summary>
    internal const string Get = "workflow_get";

    /// <summary>workflow_runs' name.</summary>
    internal const string Runs = "workflow_runs";

    /// <summary>workflow_run_get's name.</summary>
    internal const string RunGet = "workflow_run_get";

    private const string Fenced = "Names, prompts and node configs are text somebody wrote and are untrusted and fenced in structuredContent.";

    private static ToolAnnotations ReadOnly { get; } = new() { ReadOnlyHint = true, OpenWorldHint = false };

    private static Tool ListTool { get; } = new()
    {
        Name = List,
        Description = "The workflows that are not archived: slug, name, the revision triggers run (none when it is off), the newest revision and who saved it. Names are untrusted and fenced." + ContextTools.PageSentence,
        InputSchema = ContextTools.Schema(ContextTools.PageProperties()),
        Annotations = ReadOnly,
    };

    private static Tool GetTool { get; } = new()
    {
        Name = Get,
        Description = "One workflow by slug: the revision it runs (the newest when it is off), its graph spec, who saved the newest revision, and where Sindri draws each node. Prompts and node configs in the spec are untrusted and fenced.",
        InputSchema = ContextTools.Schema(new JsonObject { ["slug"] = ContextTools.Property("string", SlugText) }, required: ["slug"]),
        Annotations = ReadOnly,
    };

    private static Tool RunsTool { get; } = new()
    {
        Name = Runs,
        Description = "A workflow's runs, newest first, 50 a page: run id, revision, status, what started it and why it failed. Filter with status, since and until; when more is true, pass next as before for the older page. A filter value it can't read is SND-GRAPH-134. Failure text is untrusted and fenced." + ContextTools.PageSentence,
        InputSchema = ContextTools.Schema(WithSlug(), required: ["slug"]),
        Annotations = ReadOnly,
    };

    private static Tool RunGetTool { get; } = new()
    {
        Name = RunGet,
        Description = "One run with its trigger, outputs and what each node did (input, output, error). All of that text is untrusted and fenced.",
        InputSchema = ContextTools.Schema(new JsonObject { ["run"] = ContextTools.Property("string", "A run id, '<slug>@<revision>:<occurrence>', such as morning-check@3:12") }, required: ["run"]),
        Annotations = ReadOnly,
    };

    private static readonly string[] _filterNames = ["status", "since", "until", "before"];

    private const string SlugText = "The workflow's slug, such as morning-check (workflow_list names them)";

    /// <summary>The four tools.</summary>
    internal static IReadOnlyList<Tool> All { get; } = [ListTool, GetTool, RunsTool, RunGetTool];

    /// <summary>Runs the workflow read tool named <paramref name="tool"/>.</summary>
    /// <param name="tool">The tool's name (one of the four constants).</param>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static ValueTask<CallToolResult> Call(string tool, IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct) => tool switch
    {
        List => CallList(services, arguments, ct),
        Get => CallGet(services, arguments, ct),
        Runs => CallRuns(services, arguments, ct),
        RunGet => CallRunGet(services, arguments, ct),
        _ => throw new UnreachableException($"WorkflowReadTools has no handler for '{tool}'"),
    };

    private static JsonObject WithSlug()
    {
        var properties = ContextTools.PageProperties();
        properties["slug"] = ContextTools.Property("string", SlugText);
        properties["status"] = ContextTools.Property("string", "Keep runs in these statuses, comma-separated: running (queued, running or waiting), ended (succeeded), succeeded, failed, cancelled, queued, waiting");
        properties["since"] = ContextTools.Property("string", "Keep runs that started at or after this ISO-8601 instant, such as 2026-11-23T07:00:00Z (a date alone is its midnight UTC)");
        properties["until"] = ContextTools.Property("string", "Keep runs that started before this ISO-8601 instant; not before since");
        properties["before"] = ContextTools.Property("string", "Keep runs older than this run id: the next of the page before");
        return properties;
    }

    private static async ValueTask<CallToolResult> CallList(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var page = Page(List, arguments, []);
        if (page.IsError)
        {
            return McpResults.Failure(services, page.Error);
        }

        var answer = await services.GetRequiredService<IService<ListWorkflowsRequest, ListWorkflowsResponse>>().Handle(new ListWorkflowsRequest(), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static listed => string.Create(CultureInfo.InvariantCulture, $"{listed.Workflows.Count} workflows\n{Fenced}"), FencePlan.WorkflowList, page.Value);
    }

    private static async ValueTask<CallToolResult> CallGet(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var slug = McpArguments.Only(Get, arguments, ["slug"]) is { } refused ? refused : Slug(Get, arguments);
        if (slug.IsError)
        {
            return McpResults.Failure(services, slug.Error);
        }

        var answer = await services.GetRequiredService<IService<GetWorkflowRequest, GetWorkflowResponse>>().Handle(new GetWorkflowRequest(slug.Value), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static got => string.Create(CultureInfo.InvariantCulture,
            $"{got.Slug.Value} · r{got.Revision}{(got.ActiveRevision is null ? " (off)" : " (active)")} of {got.LatestRevision} · {(got.Spec["nodes"] is JsonArray nodes ? nodes.Count : 0)} nodes · newest saved by {got.ChangedBy}\n{Fenced}"), FencePlan.WorkflowGet);
    }

    private static async ValueTask<CallToolResult> CallRuns(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var page = Page(Runs, arguments, ["slug", .. _filterNames]);
        if (page.IsError)
        {
            return McpResults.Failure(services, page.Error);
        }

        var slug = Slug(Runs, arguments);
        var filter = slug.IsError ? slug.Error : RunsFilterOf(arguments);
        if (slug.IsError || filter.IsError)
        {
            return McpResults.Failure(services, slug.IsError ? slug.Error : filter.Error);
        }

        var request = new ListWorkflowRunsRequest(slug.Value, page.Value.Offset, page.Value.Limit) { Filter = filter.Value };
        var answer = await services.GetRequiredService<IService<ListWorkflowRunsRequest, ListWorkflowRunsResponse>>().Handle(request, ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static listed => string.Create(CultureInfo.InvariantCulture, $"{listed.Runs.Count} runs of {listed.Slug.Value}, newest first\n{Fenced}"), FencePlan.WorkflowRuns);
    }

    private static async ValueTask<CallToolResult> CallRunGet(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var run = McpArguments.Only(RunGet, arguments, ["run"]) is { } refused
            ? refused
            : McpArguments.RequiredString(RunGet, arguments, "run", "morning-check@3:12").Bind(WorkflowReadEndpoints.Run);
        if (run.IsError)
        {
            return McpResults.Failure(services, run.Error);
        }

        var answer = await services.GetRequiredService<IService<GetWorkflowRunRequest, GetWorkflowRunResponse>>().Handle(new GetWorkflowRunRequest(run.Value), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static got => string.Create(CultureInfo.InvariantCulture, $"{got.Id.Value} · {got.Status} · {got.Nodes.Count} node runs\n{Fenced}"), FencePlan.WorkflowRun);
    }

    // why: the same four values the route reads from its query, read as optional strings and checked by the route's parser, so both refuse alike (#4334)
    private static Result<RunsFilter> RunsFilterOf(IDictionary<string, JsonElement>? arguments)
    {
        var values = new string?[_filterNames.Length];
        for (var i = 0; i < _filterNames.Length; i++)
        {
            var value = McpArguments.OptionalString(Runs, arguments, _filterNames[i]);
            if (value.IsError)
            {
                return value.Error;
            }

            values[i] = value.Value;
        }

        return RunsFilter.Parse(values[0], values[1], values[2], values[3]);
    }

    private static Result<McpWindow> Page(string tool, IDictionary<string, JsonElement>? arguments, string[] own) =>
        McpArguments.Only(tool, arguments, [.. own, .. McpArguments.PageNames]) is { } refused ? refused : McpArguments.ReadPage(arguments);

    private static Result<WorkflowSlug> Slug(string tool, IDictionary<string, JsonElement>? arguments) =>
        McpArguments.RequiredString(tool, arguments, "slug", "morning-check").Bind(WorkflowReadEndpoints.Slug);
}
