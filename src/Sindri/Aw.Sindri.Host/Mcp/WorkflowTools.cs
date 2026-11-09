using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Context.Core;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Features.ActivateWorkflow;
using Aw.Sindri.Features.ApplyWorkflowOps;
using Aw.Sindri.Features.CancelWorkflowRun;
using Aw.Sindri.Features.CreateWorkflow;
using Aw.Sindri.Features.DeleteWorkflow;
using Aw.Sindri.Features.PauseWorkflow;
using Aw.Sindri.Features.ListWorkflowBlocks;
using Aw.Sindri.Features.ReplaceWorkflow;
using Aw.Sindri.Features.RunWorkflow;
using Aw.Sindri.Features.SearchWorkflowDocs;
using Aw.Sindri.Features.ValidateWorkflow;
using Aw.Sindri.Host.Endpoints;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Host.Mcp;

/// <summary>
/// The workflow write tools (#2258, PR 8b), plus workflow_docs and workflow_blocks: workflow_create, workflow_apply_ops, workflow_replace, workflow_validate, workflow_activate, workflow_pause, workflow_delete, workflow_run and workflow_cancel.
/// Each is one call to the service its HTTP route calls, as the session that made the call. The read tools are <see cref="WorkflowReadTools"/>. workflow_run refuses a loop (<see cref="WorkflowLoopGuard"/>).
/// </summary>
internal static class WorkflowTools
{
    /// <summary>workflow_create's name.</summary>
    internal const string Create = "workflow_create";

    /// <summary>workflow_apply_ops' name.</summary>
    internal const string ApplyOps = "workflow_apply_ops";

    /// <summary>workflow_replace's name.</summary>
    internal const string Replace = "workflow_replace";

    /// <summary>workflow_validate's name.</summary>
    internal const string Validate = "workflow_validate";

    /// <summary>workflow_activate's name.</summary>
    internal const string Activate = "workflow_activate";

    /// <summary>workflow_pause's name.</summary>
    internal const string Pause = "workflow_pause";

    /// <summary>workflow_delete's name.</summary>
    internal const string Delete = "workflow_delete";

    /// <summary>workflow_run's name.</summary>
    internal const string Run = "workflow_run";

    /// <summary>workflow_cancel's name.</summary>
    internal const string Cancel = "workflow_cancel";

    /// <summary>workflow_docs' name.</summary>
    internal const string Docs = "workflow_docs";

    /// <summary>workflow_blocks' name.</summary>
    internal const string Blocks = "workflow_blocks";

    private const string SlugText = "The workflow's slug, such as morning-check (workflow_list names them)";
    private const string BaseText = "The newest revision you read (workflow_get shows it). A newer revision is refused with the current one, so read again and retry.";

    private static ToolAnnotations Write(bool destructive = false, bool idempotent = false) => new() { ReadOnlyHint = false, DestructiveHint = destructive, IdempotentHint = idempotent, OpenWorldHint = false };

    private static Tool CreateTool { get; } = new()
    {
        Name = Create,
        Description = "Make a workflow, off, at revision 1, from a spec object or a starter template (weekday-check, daily-digest, manual-notify). Give exactly one of spec and template. The graph must pass the draft checks; activate it afterwards.",
        InputSchema = ContextTools.Schema(new JsonObject
        {
            ["slug"] = ContextTools.Property("string", "The new workflow's slug: lowercase letters, digits and '-'"),
            ["template"] = ContextTools.Property("string", "A starter template's name"),
            ["spec"] = ContextTools.Property("object", "The whole graph: {version, name, nodes, edges}"),
        }, required: ["slug"]),
        Annotations = Write(),
    };

    private static Tool ApplyOpsTool { get; } = new()
    {
        Name = ApplyOps,
        Description = "Edit a workflow with small ops against the revision you read: add_node, set_config (a path set), remove_node, connect, disconnect, rename, set_meta. All ops apply or none do; the result is the next revision. "
            + "A stale baseRevision is a conflict that names the current revision.",
        InputSchema = ContextTools.Schema(new JsonObject
        {
            ["slug"] = ContextTools.Property("string", SlugText),
            ["baseRevision"] = ContextTools.Property("integer", BaseText, minimum: 1),
            ["ops"] = ContextTools.Property("array", "The ops, objects with an 'op' member"),
        }, required: ["slug", "baseRevision", "ops"]),
        Annotations = Write(),
    };

    private static Tool ReplaceTool { get; } = new()
    {
        Name = Replace,
        Description = "Replace a workflow's whole graph with a new revision. Same baseRevision rule as workflow_apply_ops. Prefer apply_ops for small changes.",
        InputSchema = ContextTools.Schema(new JsonObject
        {
            ["slug"] = ContextTools.Property("string", SlugText),
            ["baseRevision"] = ContextTools.Property("integer", BaseText, minimum: 1),
            ["spec"] = ContextTools.Property("object", "The whole graph: {version, name, nodes, edges}"),
        }, required: ["slug", "baseRevision", "spec"]),
        Annotations = Write(),
    };

    private static Tool ValidateTool { get; } = new()
    {
        Name = Validate,
        Description = "Check a workflow as if it were being activated and list the findings, each with how to fix it. Give a stored workflow (slug, and a revision or the newest) or a spec. Writes nothing. Finding text is untrusted and fenced.",
        InputSchema = ContextTools.Schema(new JsonObject
        {
            ["slug"] = ContextTools.Property("string", SlugText),
            ["revision"] = ContextTools.Property("integer", "A revision of that workflow; the newest when left out", minimum: 1),
            ["spec"] = ContextTools.Property("object", "A graph to check instead of a stored workflow"),
        }),
        Annotations = new ToolAnnotations { ReadOnlyHint = true, OpenWorldHint = false },
    };

    private static Tool ActivateTool { get; } = new()
    {
        Name = Activate,
        Description = "Turn a workflow's triggers on at one revision (the newest when left out) after it passes the activation checks.",
        InputSchema = ContextTools.Schema(new JsonObject
        {
            ["slug"] = ContextTools.Property("string", SlugText),
            ["revision"] = ContextTools.Property("integer", "The revision to run; the newest when left out", minimum: 1),
        }, required: ["slug"]),
        Annotations = Write(idempotent: true),
    };

    private static Tool PauseTool { get; } = new()
    {
        Name = Pause,
        Description = "Turn a workflow's triggers off. Every revision stays saved and runs in flight keep going (workflow_cancel stops one).",
        InputSchema = ContextTools.Schema(new JsonObject { ["slug"] = ContextTools.Property("string", SlugText) }, required: ["slug"]),
        Annotations = Write(idempotent: true),
    };

    private static Tool DeleteTool { get; } = new()
    {
        Name = Delete,
        Description = "Archive a workflow: it leaves the list and turns off for good. Its revisions and runs stay readable.",
        InputSchema = ContextTools.Schema(new JsonObject { ["slug"] = ContextTools.Property("string", SlugText) }, required: ["slug"]),
        Annotations = Write(destructive: true),
    };

    private static Tool RunTool { get; } = new()
    {
        Name = Run,
        Description = "Start a run of a workflow's active revision by hand. It answers started, queued or skipped by the workflow's concurrency. A session that a run of this workflow launched can't start the workflow again.",
        InputSchema = ContextTools.Schema(new JsonObject
        {
            ["slug"] = ContextTools.Property("string", SlugText),
            ["payload"] = ContextTools.Property("object", "What the manual trigger hands on, a JSON object"),
        }, required: ["slug"]),
        Annotations = Write(),
    };

    private static Tool CancelTool { get; } = new()
    {
        Name = Cancel,
        Description = "Ask a run in flight to stop. A run that already ended is refused.",
        InputSchema = ContextTools.Schema(new JsonObject { ["run"] = ContextTools.Property("string", "A run id, '<slug>@<revision>:<occurrence>'") }, required: ["run"]),
        Annotations = Write(idempotent: true),
    };

    private static Tool DocsTool { get; } = new()
    {
        Name = Docs,
        Description = "The workflow docs shipped with Sindri: the spec, references, conditions, concurrency, runs, and one page per block with an example spec. Give question to rank the pages, page (a slug such as block-schedule) to read one in full, or neither to list every page.",
        InputSchema = ContextTools.Schema(new JsonObject
        {
            ["question"] = ContextTools.Property("string", "What you want to know, in words, such as 'how do I poll a connection'"),
            ["page"] = ContextTools.Property("string", "A page slug to read in full, such as block-schedule"),
        }),
        Annotations = new ToolAnnotations { ReadOnlyHint = true, OpenWorldHint = false },
    };

    private static Tool BlocksTool { get; } = new()
    {
        Name = Blocks,
        Description = "The block types a workflow can use, grouped as the picker shows them (Triggers, Data, AI, Logic, Outputs), each with its title, config fields, outputs, branches and an example config.",
        InputSchema = ContextTools.Schema([]),
        Annotations = new ToolAnnotations { ReadOnlyHint = true, OpenWorldHint = false },
    };

    /// <summary>The eleven tools.</summary>
    internal static IReadOnlyList<Tool> All { get; } = [CreateTool, ApplyOpsTool, ReplaceTool, ValidateTool, ActivateTool, PauseTool, DeleteTool, RunTool, CancelTool, DocsTool, BlocksTool];

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
        var revision = McpArguments.OptionalLong(Validate, arguments, "revision", min: 1);
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
        var revision = McpArguments.OptionalLong(Activate, arguments, "revision", min: 1);
        if (slug.IsError || revision.IsError)
        {
            return McpResults.Failure(services, slug.IsError ? slug.Error : revision.Error);
        }

        var answer = await services.GetRequiredService<IService<ActivateWorkflowRequest, ActivateWorkflowResponse>>().Handle(new ActivateWorkflowRequest(slug.Value, (int?)revision.Value, By(services)), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static on => string.Create(CultureInfo.InvariantCulture, $"{on.Slug.Value} is on at revision {on.Revision}"));
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
        if (await WorkflowLoopGuard.Check(services.GetRequiredService<IGraphLaunchRuns>(), caller?.Launch, slug.Value, ct).ConfigureAwait(false) is { } loop)
        {
            return McpResults.Failure(services, loop);
        }

        var answer = await services.GetRequiredService<IService<RunWorkflowRequest, RunWorkflowResponse>>().Handle(new RunWorkflowRequest(slug.Value, payload.Value, caller?.Actor ?? "owner"), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static started => $"{started.Status} {started.Run.Value}{(started.Reason is null ? string.Empty : ": " + started.Reason)}");
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

    private static Result<WorkflowSlug> Slug(string tool, IDictionary<string, JsonElement>? arguments) =>
        McpArguments.RequiredString(tool, arguments, "slug", "morning-check").Bind(WorkflowReadEndpoints.Slug);

    private static Result<int> Base(string tool, IDictionary<string, JsonElement>? arguments)
    {
        if (arguments is null || !arguments.TryGetValue("baseRevision", out _))
        {
            return Missing(tool, "baseRevision", "3");
        }

        var value = McpArguments.OptionalLong(tool, arguments, "baseRevision", min: 1);
        return value.IsError ? value.Error : value.Value is { } n ? checked((int)n) : Missing(tool, "baseRevision", "3");
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
