using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.ActivateWorkflow;
using Aw.Sindri.Features.ApplyWorkflowOps;
using Aw.Sindri.Features.CreateWorkflow;
using Aw.Sindri.Features.DeleteWorkflow;
using Aw.Sindri.Features.PauseWorkflow;
using Aw.Sindri.Features.PinWorkflowOutput;
using Aw.Sindri.Features.ReplaceWorkflow;
using Aw.Sindri.Features.TestWorkflow;
using Aw.Sindri.Features.ValidateWorkflow;

namespace Aw.Sindri.Host.Catalog;

/// <summary>The workflow write commands (issue 2258, PR 8b): create, edit by ops, replace, validate, activate, pause and archive. Desk class, and an agent session may run every one but activate and pause (workflows page spec §8: those two are the owner's, SND-AUTH-002); the run and cancel rows are <see cref="WorkflowRunRows"/>.</summary>
internal static class WorkflowRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        Row("CreateWorkflow", typeof(CreateWorkflowRequest), new RowRoute("POST", "/sindri/workflows"), "sindri wf create <slug> [--template <name>] [--file <specFile>]", destructive: false) with { ServiceGrants = ["setup"] },   // first-run spec E2: the setup service makes hello-sindri
        Row("ApplyWorkflowOps", typeof(ApplyWorkflowOpsRequest), new RowRoute("POST", "/sindri/workflows/{slug}/ops"), "sindri wf edit <slug> --base <revision> [--ops <json>] [--ops-file <file>]", destructive: false),
        Row("ReplaceWorkflow", typeof(ReplaceWorkflowRequest), new RowRoute("PUT", "/sindri/workflows/{slug}"), "sindri wf edit <slug> --base <revision> --file <specFile>", destructive: false),
        Row("ValidateWorkflow", typeof(ValidateWorkflowRequest), new RowRoute("POST", "/sindri/workflows/validate"), "sindri wf validate --slug <slug> [--revision <n>]", destructive: false),
        Row("ActivateWorkflow", typeof(ActivateWorkflowRequest), new RowRoute("POST", "/sindri/workflows/{slug}/activate"), "sindri wf activate <slug> [--revision <n>]", destructive: false) with { SessionOk = false, ServiceGrants = ["setup"] },
        Row("PauseWorkflow", typeof(PauseWorkflowRequest), new RowRoute("POST", "/sindri/workflows/{slug}/pause"), "sindri wf pause <slug>", destructive: false) with { SessionOk = false },
        Row("DeleteWorkflow", typeof(DeleteWorkflowRequest), new RowRoute("DELETE", "/sindri/workflows/{slug}"), "sindri wf delete <slug>", destructive: true),
        Row("TestWorkflow", typeof(TestWorkflowRequest), new RowRoute("POST", "/sindri/workflows/{slug}/test"), "sindri wf test <slug> [--revision <n>]", destructive: false) with { SessionOk = false },   // Workflows page P0 stub (#3191): the CLI verb lands with the test route (P4b)
        Row("PinWorkflowOutput", typeof(PinWorkflowOutputRequest), new RowRoute("POST", "/sindri/workflows/{slug}/pins"), "sindri wf pin <slug> --node <node> --run <run>", destructive: false) with { SessionOk = false },   // Workflows page P0 stub (#3191)
    ];

    private readonly record struct RowRoute(string Method, string Route);

    private static CommandRow Row(string action, Type request, RowRoute http, string cli, bool destructive) =>
        new(ActionId.From(action), request, Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: destructive, ReadsViews: false, Shortcut: null,
            http.Method, http.Route, ShellRows.Form(cli), "ready", SessionOk: true);
}
