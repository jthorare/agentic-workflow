using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Features.ActivateWorkflow;
using Aw.Sindri.Features.ApplyWorkflowOps;
using Aw.Sindri.Features.CancelWorkflowRun;
using Aw.Sindri.Features.RerunWorkflowRun;
using Aw.Sindri.Features.CreateWorkflow;
using Aw.Sindri.Features.DeleteWorkflow;
using Aw.Sindri.Features.ListWorkflowBlocks;
using Aw.Sindri.Features.PauseWorkflow;
using Aw.Sindri.Features.ReplaceWorkflow;
using Aw.Sindri.Features.RunWorkflow;
using Aw.Sindri.Features.SearchWorkflowDocs;
using Aw.Sindri.Features.ValidateWorkflow;
using Aw.Sindri.Host.Endpoints;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Host.Mcp;

internal static partial class WorkflowTools
{
    /// <summary>Runs the workflow write tool named <paramref name="tool"/>.</summary>
    /// <param name="tool">The tool's name (one of the nine constants).</param>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static ValueTask<CallToolResult> Call(string tool, IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct) => tool switch
    {
        Create => CallCreate(services, arguments, ct),
        ApplyOps => CallApplyOps(services, arguments, ct),
        Replace => CallReplace(services, arguments, ct),
        Validate => CallValidate(services, arguments, ct),
        Activate => CallActivate(services, arguments, ct),
        Pause => CallPause(services, arguments, ct),
        Delete => CallDelete(services, arguments, ct),
        Run => CallRun(services, arguments, ct),
        Rerun => CallRerun(services, arguments, ct),
        Cancel => CallCancel(services, arguments, ct),
        Docs => CallDocs(services, arguments, ct),
        Blocks => CallBlocks(services, arguments, ct),
        _ => throw new UnreachableException($"WorkflowTools has no handler for '{tool}'"),
    };

    private static string By(IServiceProvider services) => services.GetRequiredService<ICallerContextAccessor>().Current?.Actor ?? "owner";

    private static async ValueTask<CallToolResult> CallCreate(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var request = McpArguments.Only(Create, arguments, ["slug", "template", "spec"]) is { } refused ? refused : Bind(arguments, Create);
        if (request.IsError)
        {
            return McpResults.Failure(services, request.Error);
        }

        var answer = await services.GetRequiredService<IService<CreateWorkflowRequest, CreateWorkflowResponse>>().Handle(request.Value with { By = By(services) }, ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static made => string.Create(CultureInfo.InvariantCulture, $"{made.Slug.Value} created at revision {made.Revision} (off); activate it when it is ready"));

        static Result<CreateWorkflowRequest> Bind(IDictionary<string, JsonElement>? arguments, string tool)
        {
            var slug = Slug(tool, arguments);
            var template = McpArguments.OptionalString(tool, arguments, "template");
            var spec = Json(tool, arguments, "spec", JsonValueKind.Object);
            return slug.IsError ? slug.Error : template.IsError ? template.Error : spec.IsError ? spec.Error : new CreateWorkflowRequest(slug.Value, template.Value, spec.Value, By: string.Empty);
        }
    }

    private static async ValueTask<CallToolResult> CallApplyOps(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var slug = McpArguments.Only(ApplyOps, arguments, ["slug", "baseRevision", "ops"]) is { } refused ? refused : Slug(ApplyOps, arguments);
        var baseRevision = Base(ApplyOps, arguments);
        var ops = Json(ApplyOps, arguments, "ops", JsonValueKind.Array);
        if (slug.IsError || baseRevision.IsError || ops.IsError || ops.Value is null)
        {
            return McpResults.Failure(services, slug.IsError ? slug.Error : baseRevision.IsError ? baseRevision.Error : ops.IsError ? ops.Error : Missing(ApplyOps, "ops", "[...]"));
        }

        var answer = await services.GetRequiredService<IService<ApplyWorkflowOpsRequest, ApplyWorkflowOpsResponse>>().Handle(new ApplyWorkflowOpsRequest(slug.Value, baseRevision.Value, ops.Value, By(services)), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static saved => string.Create(CultureInfo.InvariantCulture, $"{saved.Slug.Value} is now at revision {saved.Revision}"));
    }

    private static async ValueTask<CallToolResult> CallReplace(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var slug = McpArguments.Only(Replace, arguments, ["slug", "baseRevision", "spec"]) is { } refused ? refused : Slug(Replace, arguments);
        var baseRevision = Base(Replace, arguments);
        var spec = Json(Replace, arguments, "spec", JsonValueKind.Object);
        if (slug.IsError || baseRevision.IsError || spec.IsError || spec.Value is null)
        {
            return McpResults.Failure(services, slug.IsError ? slug.Error : baseRevision.IsError ? baseRevision.Error : spec.IsError ? spec.Error : Missing(Replace, "spec", "{...}"));
        }

        var answer = await services.GetRequiredService<IService<ReplaceWorkflowRequest, ReplaceWorkflowResponse>>().Handle(new ReplaceWorkflowRequest(slug.Value, baseRevision.Value, spec.Value, By(services)), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static saved => string.Create(CultureInfo.InvariantCulture, $"{saved.Slug.Value} is now at revision {saved.Revision}"));
    }

    private static async ValueTask<CallToolResult> CallValidate(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        if (McpArguments.Only(Validate, arguments, ["slug", "revision", "spec"]) is { } refused)
        {
            return McpResults.Failure(services, refused);
        }

        var slugText = McpArguments.OptionalString(Validate, arguments, "slug");
        var revision = McpArguments.OptionalLong(Validate, arguments, "revision", min: 1, max: int.MaxValue);
        var spec = Json(Validate, arguments, "spec", JsonValueKind.Object);
        if (slugText.IsError || revision.IsError || spec.IsError)
        {
            return McpResults.Failure(services, slugText.IsError ? slugText.Error : revision.IsError ? revision.Error : spec.Error);
        }

        WorkflowSlug? stored = null;
        if (slugText.Value is { } text)
        {
            var parsed = WorkflowReadEndpoints.Slug(text);
            if (parsed.IsError)
            {
                return McpResults.Failure(services, parsed.Error);
            }

            stored = parsed.Value;
        }

        var answer = await services.GetRequiredService<IService<ValidateWorkflowRequest, ValidateWorkflowResponse>>().Handle(new ValidateWorkflowRequest(stored, (int?)revision.Value, spec.Value), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static checkedIt => string.Create(CultureInfo.InvariantCulture,
            $"{(checkedIt.Passed ? "passed" : "has errors")}: {checkedIt.Findings.Count} findings\nFinding text is untrusted and fenced in structuredContent."), FencePlan.WorkflowValidation);
    }

    private static async ValueTask<CallToolResult> CallActivate(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var slug = McpArguments.Only(Activate, arguments, ["slug", "revision"]) is { } refused ? refused : Slug(Activate, arguments);
        var revision = McpArguments.OptionalLong(Activate, arguments, "revision", min: 1, max: int.MaxValue);
        if (slug.IsError || revision.IsError)
        {
            return McpResults.Failure(services, slug.IsError ? slug.Error : revision.Error);
        }

        var answer = await services.GetRequiredService<IService<ActivateWorkflowRequest, ActivateWorkflowResponse>>().Handle(new ActivateWorkflowRequest(slug.Value, (int?)revision.Value, By(services)), ct).ConfigureAwait(false);
        return McpResults.From(services, FenceFindings(answer), static on => string.Create(CultureInfo.InvariantCulture, $"{on.Slug.Value} is on at revision {on.Revision}"));
    }

    private static async ValueTask<CallToolResult> CallPause(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var slug = McpArguments.Only(Pause, arguments, ["slug"]) is { } refused ? refused : Slug(Pause, arguments);
        if (slug.IsError)
        {
            return McpResults.Failure(services, slug.Error);
        }

        var answer = await services.GetRequiredService<IService<PauseWorkflowRequest, PauseWorkflowResponse>>().Handle(new PauseWorkflowRequest(slug.Value, By(services)), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static off => off.Slug.Value + " is off");
    }

    private static async ValueTask<CallToolResult> CallDelete(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var slug = McpArguments.Only(Delete, arguments, ["slug"]) is { } refused ? refused : Slug(Delete, arguments);
        if (slug.IsError)
        {
            return McpResults.Failure(services, slug.Error);
        }

        var answer = await services.GetRequiredService<IService<DeleteWorkflowRequest, DeleteWorkflowResponse>>().Handle(new DeleteWorkflowRequest(slug.Value, By(services)), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static gone => gone.Slug.Value + " is archived");
    }

    private static async ValueTask<CallToolResult> CallRun(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var slug = McpArguments.Only(Run, arguments, ["slug", "payload"]) is { } refused ? refused : Slug(Run, arguments);
        var payload = Json(Run, arguments, "payload", JsonValueKind.Object);
        if (slug.IsError || payload.IsError)
        {
            return McpResults.Failure(services, slug.IsError ? slug.Error : payload.Error);
        }

        var caller = services.GetRequiredService<ICallerContextAccessor>().Current;
        var answer = await services.GetRequiredService<IService<RunWorkflowRequest, RunWorkflowResponse>>().Handle(new RunWorkflowRequest(slug.Value, payload.Value, caller?.Actor ?? "owner"), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static started => $"{started.Status} {started.Run.Value}{(started.Reason is null ? string.Empty : ": " + started.Reason)}");
    }

    private static async ValueTask<CallToolResult> CallRerun(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var run = McpArguments.Only(Rerun, arguments, ["run", "from", "input"]) is { } refused
            ? refused
            : McpArguments.RequiredString(Rerun, arguments, "run", "morning-check@3:12").Bind(WorkflowReadEndpoints.Run);
        var from = McpArguments.RequiredString(Rerun, arguments, "from", "send-digest");
        var input = Json(Rerun, arguments, "input", JsonValueKind.Object);
        if (run.IsError || from.IsError || input.IsError)
        {
            return McpResults.Failure(services, run.IsError ? run.Error : from.IsError ? from.Error : input.Error);
        }

        var answer = await services.GetRequiredService<IService<RerunWorkflowRunRequest, RerunWorkflowRunResponse>>().Handle(new RerunWorkflowRunRequest(run.Value, from.Value, input.Value, By(services)), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static started => $"{started.Status} {started.Run.Value} from {started.Source.Value}");
    }

    private static async ValueTask<CallToolResult> CallCancel(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var run = McpArguments.Only(Cancel, arguments, ["run"]) is { } refused
            ? refused
            : McpArguments.RequiredString(Cancel, arguments, "run", "morning-check@3:12").Bind(WorkflowReadEndpoints.Run);
        if (run.IsError)
        {
            return McpResults.Failure(services, run.Error);
        }

        var answer = await services.GetRequiredService<IService<CancelWorkflowRunRequest, CancelWorkflowRunResponse>>().Handle(new CancelWorkflowRunRequest(run.Value, By(services)), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static cancelled => cancelled.Run.Value + " cancel requested");
    }

    private static async ValueTask<CallToolResult> CallDocs(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var question = McpArguments.Only(Docs, arguments, ["question", "page"]) is { } refused ? refused : McpArguments.OptionalString(Docs, arguments, "question");
        var page = McpArguments.OptionalString(Docs, arguments, "page");
        if (question.IsError || page.IsError)
        {
            return McpResults.Failure(services, question.IsError ? question.Error : page.Error);
        }

        var answer = await services.GetRequiredService<IService<SearchWorkflowDocsRequest, SearchWorkflowDocsResponse>>().Handle(new SearchWorkflowDocsRequest(question.Value, page.Value), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static found => found.Page is { } read ? read.Title : string.Create(CultureInfo.InvariantCulture, $"{found.Hits.Count} pages"));
    }

    private static async ValueTask<CallToolResult> CallBlocks(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        if (McpArguments.None(Blocks, arguments) is { } refused)
        {
            return McpResults.Failure(services, refused);
        }

        var answer = await services.GetRequiredService<IService<ListWorkflowBlocksRequest, ListWorkflowBlocksResponse>>().Handle(new ListWorkflowBlocksRequest(), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static listed => string.Create(CultureInfo.InvariantCulture, $"{listed.Groups.Sum(static g => g.Blocks.Count)} block types in {listed.Groups.Count} groups"));
    }

    /// <summary>A findings refusal carries text from the stored spec, which another agent may have written: fence it like workflow_validate's findings.</summary>
    private static Result<T> FenceFindings<T>(Result<T> answer) =>
        answer.IsError && string.Equals(answer.Error.Code, SindriErrors.GraphFindings.Code, StringComparison.Ordinal)
            ? SindriErrors.GraphFindings.With(UntrustedFence.Wrap("sindri:workflow", author: null, answer.Error.Message))
            : answer;

    private static Result<WorkflowSlug> Slug(string tool, IDictionary<string, JsonElement>? arguments) =>
        McpArguments.RequiredString(tool, arguments, "slug", "morning-check").Bind(WorkflowReadEndpoints.Slug);

    private static Result<int> Base(string tool, IDictionary<string, JsonElement>? arguments)
    {
        if (arguments is null || !arguments.ContainsKey("baseRevision"))
        {
            return Missing(tool, "baseRevision", "3");
        }

        var value = McpArguments.OptionalLong(tool, arguments, "baseRevision", min: 1, max: int.MaxValue);
        return value.IsError ? value.Error : value.Value is { } n ? (int)n : Missing(tool, "baseRevision", "3");
    }

    /// <summary>An optional JSON object or array argument as JSON text.</summary>
    private static Result<string?> Json(string tool, IDictionary<string, JsonElement>? arguments, string name, JsonValueKind kind)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var raw) || raw.ValueKind == JsonValueKind.Null)
        {
            return (string?)null;
        }

        return raw.ValueKind == kind
            ? raw.GetRawText()
            : ContextErrors.McpArgumentsInvalid.With($"'{name}' must be {(kind == JsonValueKind.Array ? "an array" : "an object")} (fix: see {tool}'s input schema)");
    }

    private static Error Missing(string tool, string name, string example) =>
        ContextErrors.McpArgumentsInvalid.With($"{tool} needs '{name}' (fix: call it with {{\"{name}\": {example}}})");
}
