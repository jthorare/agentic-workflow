using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Host.Mcp;

/// <summary>
/// The workflow write tools (#2258, PR 8b), plus workflow_docs and workflow_blocks: workflow_create, workflow_apply_ops, workflow_replace, workflow_validate, workflow_activate, workflow_pause, workflow_delete, workflow_run and workflow_cancel.
/// Each is one call to the service its HTTP route calls, as the session that made the call. The read tools are <see cref="WorkflowReadTools"/>. workflow_run shares the loop guard (SND-GRAPH-109) with the HTTP route and the CLI: it lives in RunWorkflowService.
/// </summary>
internal static partial class WorkflowTools
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

    /// <summary>workflow_rerun's name.</summary>
    internal const string Rerun = "workflow_rerun";

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
        Description = "Start a run of a workflow's active revision by hand. It answers started, queued or skipped by the workflow's concurrency. A session that a run of this workflow launched can't start the workflow again (SND-GRAPH-109); that guard does nothing until graph runs launch agent sessions.",
        InputSchema = ContextTools.Schema(new JsonObject
        {
            ["slug"] = ContextTools.Property("string", SlugText),
            ["payload"] = ContextTools.Property("object", "What the manual trigger hands on, a JSON object"),
        }, required: ["slug"]),
        Annotations = Write(),
    };

    private static Tool RerunTool { get; } = new()
    {
        Name = Rerun,
        Description = "Re-run an ended run from one step: a new run of that run's revision. The steps before 'from' keep their stored output, 'from' and the steps after it run again. Refused while the run is in flight (SND-GRAPH-125), when an earlier step did not succeed (SND-GRAPH-126), or for a step inside a for_each body (re-run from the for_each step). Answers started, queued or skipped like workflow_run.",
        InputSchema = ContextTools.Schema(new JsonObject
        {
            ["run"] = ContextTools.Property("string", "The ended run to start from, '<slug>@<revision>:<occurrence>'"),
            ["from"] = ContextTools.Property("string", "The node id of the step to run first again"),
            ["input"] = ContextTools.Property("object", "A JSON object that replaces the step's stored input (the engine does not read it yet)"),
        }, required: ["run", "from"]),
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

    /// <summary>The twelve tools.</summary>
    internal static IReadOnlyList<Tool> All { get; } = [CreateTool, ApplyOpsTool, ReplaceTool, ValidateTool, ActivateTool, PauseTool, DeleteTool, RunTool, RerunTool, CancelTool, DocsTool, BlocksTool];
}
