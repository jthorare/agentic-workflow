using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.AgentNodeControl;
using Aw.Sindri.Features.AnswerQuestion;
using Aw.Sindri.Features.CancelWorkflowRun;
using Aw.Sindri.Features.RunWorkflow;

namespace Aw.Sindri.Host.Catalog;

/// <summary>The graph run commands (issue 2258, PR 6): starting a run of a workflow's active revision by hand and cancelling a run (an agent session may run both: Part 1, every verb for agents), and taking over and handing back the 'run_agent' node a run waits on (#2472). Desk class, open to agent sessions too.</summary>
internal static class WorkflowRunRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        new(ActionId.From("RunWorkflow"), typeof(RunWorkflowRequest), Variant: null, ActionClass.Desk, ServiceGrants: ["setup"],   // first-run spec decision 19: the setup service runs hello-sindri once
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/workflows/{slug}/runs", ShellRows.Form("sindri wf run <slug> [--payload <json>]"), "ready", SessionOk: true),
        new(ActionId.From("CancelWorkflowRun"), typeof(CancelWorkflowRunRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/workflow-runs/{run}/cancel", ShellRows.Form("sindri wf cancel <run>"), "ready", SessionOk: true),
        new(ActionId.From("AnswerQuestion"), typeof(AnswerQuestionRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/workflow-runs/{run}/nodes/{node}/answer", ShellRows.Form("sindri answer <question> [--text <text>] [--choice <choice>] [--item <index>]"), "ready", SessionOk: true),
        new(ActionId.From("TakeOverAgentNode"), typeof(TakeOverAgentNodeRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/workflow-runs/{run}/nodes/{node}/takeover", ShellRows.Form("sindri wf takeover <run> <node> [--item <index>]"), "ready"),
        new(ActionId.From("HandBackAgentNode"), typeof(HandBackAgentNodeRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/workflow-runs/{run}/nodes/{node}/handback", ShellRows.Form("sindri wf handback <run> <node> [--item <index>]"), "ready"),
    ];
}
