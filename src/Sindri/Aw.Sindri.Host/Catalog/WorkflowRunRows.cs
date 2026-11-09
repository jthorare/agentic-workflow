using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.AnswerQuestion;
using Aw.Sindri.Features.CancelWorkflowRun;
using Aw.Sindri.Features.RunWorkflow;

namespace Aw.Sindri.Host.Catalog;

/// <summary>The graph run commands (issue 2258, PR 6): starting a run of a workflow's active revision by hand and cancelling a run. Desk class, and an agent session may run both (Part 1: every verb for agents).</summary>
internal static class WorkflowRunRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        new(ActionId.From("RunWorkflow"), typeof(RunWorkflowRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/workflows/{slug}/runs", ShellRows.Form("sindri wf run <slug> [--payload <json>]"), "ready", SessionOk: true),
        new(ActionId.From("CancelWorkflowRun"), typeof(CancelWorkflowRunRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/workflow-runs/{run}/cancel", ShellRows.Form("sindri wf cancel <run>"), "ready", SessionOk: true),
        new(ActionId.From("AnswerQuestion"), typeof(AnswerQuestionRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/workflow-runs/{run}/nodes/{node}/answer", ShellRows.Form("sindri answer <question> [--text <text>] [--choice <choice>]"), "ready", SessionOk: true),
    ];
}
