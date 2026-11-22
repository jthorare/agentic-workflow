using Aw.Kernel;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.Api;

/// <summary>The workflow reads (<see cref="ISindriApi"/>, continued; #2258 PR 8a): the list, one workflow, its runs and one run; the block list, and the writes: Run now, Cancel run (PR 17 part 2a), ops, Activate and Pause (part 2b), each sent once.</summary>
internal partial interface ISindriApi
{
    /// <summary>GET /sindri/workflows: every workflow by slug.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The list, or SND-UI-002/003.</returns>
    public Task<Result<WorkflowList>> Workflows(Uri api, CancellationToken cancellationToken);

    /// <summary>GET /sindri/workflows?includeArchived=true: every workflow, archived ones too. An archived slug stays reserved, so the create form checks a new short id against these; the page's own list stays live workflows only.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The list with the archived rows, or SND-UI-002/003.</returns>
    public Task<Result<WorkflowList>> WorkflowsIncludingArchived(Uri api, CancellationToken cancellationToken);

    /// <summary>POST /sindri/workflows: makes a workflow, off, from a starter template. A human action, sent once, never retried.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="slug">The new workflow's slug.</param>
    /// <param name="template">The starter template's name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new workflow, SND-GRAPH-009 for a slug that is taken, SND-CLI-003 for a bad slug or template, or SND-UI-002/003.</returns>
    public Task<Result<WorkflowCreated>> CreateWorkflow(Uri api, string slug, string template, CancellationToken cancellationToken);

    /// <summary>GET /sindri/workflows/{slug}: one workflow with its shown revision.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="slug">The workflow.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The workflow, SND-GRAPH-008 for an unknown one, SND-CLI-003 for a bad slug, or SND-UI-002/003.</returns>
    public Task<Result<WorkflowView>> Workflow(Uri api, string slug, CancellationToken cancellationToken);

    /// <summary>GET /sindri/workflows/{slug}?revision=latest: one workflow with its newest revision shown, saved but maybe not live (#3303).</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="slug">The workflow.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The workflow, SND-GRAPH-008 for an unknown one, SND-CLI-003 for a bad slug, or SND-UI-002/003.</returns>
    public Task<Result<WorkflowView>> WorkflowNewest(Uri api, string slug, CancellationToken cancellationToken);

    /// <summary>GET /sindri/workflows/{slug}/runs: the newest runs, newest first.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="slug">The workflow.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The runs, SND-GRAPH-008 for an unknown workflow, SND-GRAPH-105 for one that is off, or SND-UI-002/003.</returns>
    public Task<Result<WorkflowRunList>> WorkflowRuns(Uri api, string slug, CancellationToken cancellationToken);

    /// <summary>GET /sindri/workflow-runs/{run}: one run with every node's input, output and error.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="runId">The run ("&lt;slug&gt;@&lt;revision&gt;:&lt;occurrence&gt;").</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The run, SND-GRAPH-101 for an unknown one, or SND-UI-002/003.</returns>
    public Task<Result<WorkflowRunView>> WorkflowRun(Uri api, string runId, CancellationToken cancellationToken);

    /// <summary>POST /sindri/workflows/{slug}/runs: starts a run of the active revision by hand. A human action, sent once, never retried.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="slug">The workflow.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>What the trigger became (started, queued or skipped), SND-GRAPH-105 for a workflow that is off, SND-GRAPH-008 for an unknown one, or SND-UI-002/003.</returns>
    public Task<Result<WorkflowRunStarted>> StartWorkflowRun(Uri api, string slug, CancellationToken cancellationToken);

    /// <summary>POST /sindri/workflow-runs/{run}/cancel: asks a run in flight to stop. Sent once.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="runId">The run.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The run, SND-GRAPH-107 for one that already ended, SND-GRAPH-101 for an unknown one, or SND-UI-002/003.</returns>
    public Task<Result<WorkflowRunCancelled>> CancelWorkflowRun(Uri api, string runId, CancellationToken cancellationToken);

    /// <summary>POST /sindri/workflow-runs/{run}/nodes/{node}/takeover: the human takes a waiting agent node's agent over. Sent once, never retried.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="runId">The run.</param>
    /// <param name="node">The node.</param>
    /// <param name="itemIndex">The item, 0 for a node that runs once.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The node's new agent state, a refusal as served, or SND-UI-002/003.</returns>
    public Task<Result<AgentNodeControlled>> TakeOverAgentNode(Uri api, string runId, string node, int itemIndex, CancellationToken cancellationToken);

    /// <summary>POST /sindri/workflow-runs/{run}/nodes/{node}/handback: the human hands a taken-over agent node back. Sent once, never retried.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="runId">The run.</param>
    /// <param name="node">The node.</param>
    /// <param name="itemIndex">The item, 0 for a node that runs once.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The node's new agent state, a refusal as served, or SND-UI-002/003.</returns>
    public Task<Result<AgentNodeControlled>> HandBackAgentNode(Uri api, string runId, string node, int itemIndex, CancellationToken cancellationToken);

    /// <summary>GET /sindri/workflows/blocks: the block types by group, with the config fields the editor's form is made from.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The groups, or SND-UI-002/003.</returns>
    public Task<Result<WorkflowBlockList>> WorkflowBlocks(Uri api, CancellationToken cancellationToken);

    /// <summary>POST /sindri/workflows/{slug}/ops: applies edits made on a revision, all together or not at all. A human action, sent once, never retried.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="slug">The workflow.</param>
    /// <param name="baseRevision">The newest revision the editor read.</param>
    /// <param name="opsJson">The ops, a JSON array of objects with an "op" member.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The revision the ops made, SND-GRAPH-007 when the workflow changed since (the current revision is in the message), SND-GRAPH-008 for an unknown one, or SND-UI-002/003.</returns>
    public Task<Result<WorkflowOpsApplied>> ApplyWorkflowOps(Uri api, string slug, int baseRevision, string opsJson, CancellationToken cancellationToken);

    /// <summary>POST /sindri/workflows/{slug}/activate: turns the newest revision on. Sent once.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="slug">The workflow.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The revision that runs, SND-GRAPH-008 for an unknown workflow, or SND-UI-002/003.</returns>
    public Task<Result<WorkflowActivated>> ActivateWorkflow(Uri api, string slug, CancellationToken cancellationToken);

    /// <summary>POST /sindri/workflows/{slug}/pause: turns the workflow off; its revisions stay saved. Sent once.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="slug">The workflow.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The newest revision, SND-GRAPH-008 for an unknown workflow, or SND-UI-002/003.</returns>
    public Task<Result<WorkflowPaused>> PauseWorkflow(Uri api, string slug, CancellationToken cancellationToken);
}
