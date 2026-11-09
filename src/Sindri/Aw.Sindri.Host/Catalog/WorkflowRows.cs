using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.ActivateWorkflow;
using Aw.Sindri.Features.ApplyWorkflowOps;
using Aw.Sindri.Features.CreateWorkflow;
using Aw.Sindri.Features.DeleteWorkflow;
using Aw.Sindri.Features.PauseWorkflow;
using Aw.Sindri.Features.ReplaceWorkflow;
using Aw.Sindri.Features.ValidateWorkflow;

namespace Aw.Sindri.Host.Catalog;

/// <summary>The workflow write commands (issue 2258, PR 8b): create, edit by ops, replace, validate, activate, pause and archive. Desk class, and an agent session may run every one (Part 1: every verb for agents); the run and cancel rows are <see cref="WorkflowRunRows"/>.</summary>
internal static class WorkflowRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        Row("CreateWorkflow", typeof(CreateWorkflowRequest), "POST", "/sindri/workflows", "sindri wf create <slug> (--template <name> | --file <spec.json>)", destructive: false),
        Row("ApplyWorkflowOps", typeof(ApplyWorkflowOpsRequest), "POST", "/sindri/workflows/{slug}/ops", "sindri wf edit <slug> --base <revision> (--ops <json> | --ops-file <file>)", destructive: false),
        Row("ReplaceWorkflow", typeof(ReplaceWorkflowRequest), "PUT", "/sindri/workflows/{slug}", "sindri wf edit <slug> --base <revision> --file <spec.json>", destructive: false),
        Row("ValidateWorkflow", typeof(ValidateWorkflowRequest), "POST", "/sindri/workflows/validate", "sindri wf validate --slug <slug> [--revision <n>]", destructive: false),
        Row("ActivateWorkflow", typeof(ActivateWorkflowRequest), "POST", "/sindri/workflows/{slug}/activate", "sindri wf activate <slug> [--revision <n>]", destructive: false),
        Row("PauseWorkflow", typeof(PauseWorkflowRequest), "POST", "/sindri/workflows/{slug}/pause", "sindri wf pause <slug>", destructive: false),
        Row("DeleteWorkflow", typeof(DeleteWorkflowRequest), "DELETE", "/sindri/workflows/{slug}", "sindri wf delete <slug>", destructive: true),
    ];

    private static CommandRow Row(string action, Type request, string method, string route, string cli, bool destructive) =>
        new(ActionId.From(action), request, Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: destructive, ReadsViews: false, Shortcut: null,
            method, route, ShellRows.Form(cli), "ready", SessionOk: true);
}
