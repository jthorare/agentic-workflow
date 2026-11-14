using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.Api;

/// <summary>The workflow reads (<see cref="SindriApiClient"/>, continued; #2258 PR 8a): the reads are GETs; Run now, Cancel run, ops, Activate and Pause are POSTs, each sent once.</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<WorkflowList>> Workflows(Uri api, CancellationToken cancellationToken) =>
        Get<WorkflowList>(api, WorkflowRoutes.List(), cancellationToken);

    /// <inheritdoc />
    public Task<Result<WorkflowList>> WorkflowsIncludingArchived(Uri api, CancellationToken cancellationToken) =>
        Get<WorkflowList>(api, WorkflowRoutes.ListAll(), cancellationToken);

    /// <inheritdoc />
    public Task<Result<WorkflowCreated>> CreateWorkflow(Uri api, string slug, string template, CancellationToken cancellationToken) =>
        Call<WorkflowCreated>(api, HttpMethod.Post, WorkflowRoutes.Create(), cancellationToken, new CallOptions(Content: AwJson.Serialize(new CreateWorkflowBody(slug, template), Json)));

    /// <inheritdoc />
    public Task<Result<WorkflowView>> Workflow(Uri api, string slug, CancellationToken cancellationToken) =>
        Get<WorkflowView>(api, WorkflowRoutes.One(slug), cancellationToken);

    /// <inheritdoc />
    public Task<Result<WorkflowView>> WorkflowNewest(Uri api, string slug, CancellationToken cancellationToken) =>
        Get<WorkflowView>(api, WorkflowRoutes.Newest(slug), cancellationToken);

    /// <inheritdoc />
    public Task<Result<WorkflowRunList>> WorkflowRuns(Uri api, string slug, CancellationToken cancellationToken) =>
        Get<WorkflowRunList>(api, WorkflowRoutes.Runs(slug), cancellationToken);

    /// <inheritdoc />
    public Task<Result<WorkflowRunView>> WorkflowRun(Uri api, string runId, CancellationToken cancellationToken) =>
        Get<WorkflowRunView>(api, WorkflowRoutes.Run(runId), cancellationToken);

    /// <inheritdoc />
    public Task<Result<WorkflowRunStarted>> StartWorkflowRun(Uri api, string slug, CancellationToken cancellationToken) =>
        Call<WorkflowRunStarted>(api, HttpMethod.Post, WorkflowRoutes.Runs(slug), cancellationToken, new CallOptions(Content: AwJson.Serialize(new RunWorkflowBody(Payload: null), Json)));

    /// <inheritdoc />
    public Task<Result<WorkflowRunCancelled>> CancelWorkflowRun(Uri api, string runId, CancellationToken cancellationToken) =>
        Call<WorkflowRunCancelled>(api, HttpMethod.Post, WorkflowRoutes.Cancel(runId), cancellationToken);

    /// <inheritdoc />
    public Task<Result<AgentNodeControlled>> TakeOverAgentNode(Uri api, string runId, string node, int itemIndex, CancellationToken cancellationToken) =>
        Call<AgentNodeControlled>(api, HttpMethod.Post, WorkflowRoutes.TakeOver(runId, node), cancellationToken, new CallOptions(Content: AwJson.Serialize(new AgentNodeBody(itemIndex), Json)));

    /// <inheritdoc />
    public Task<Result<AgentNodeControlled>> HandBackAgentNode(Uri api, string runId, string node, int itemIndex, CancellationToken cancellationToken) =>
        Call<AgentNodeControlled>(api, HttpMethod.Post, WorkflowRoutes.HandBack(runId, node), cancellationToken, new CallOptions(Content: AwJson.Serialize(new AgentNodeBody(itemIndex), Json)));

    public Task<Result<WorkflowBlockList>> WorkflowBlocks(Uri api, CancellationToken cancellationToken) =>
        Get<WorkflowBlockList>(api, WorkflowRoutes.Blocks(), cancellationToken);

    /// <inheritdoc />
    public async Task<Result<WorkflowOpsApplied>> ApplyWorkflowOps(Uri api, string slug, int baseRevision, string opsJson, CancellationToken cancellationToken)
    {
        JsonElement ops;
        try
        {
            using var document = JsonDocument.Parse(opsJson);
            ops = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return SindriUiErrors.BadAnswer.With("the edits are not JSON");
        }

        var content = AwJson.Serialize(new ApplyWorkflowOpsBody(baseRevision, ops), Json);
        return await Call<WorkflowOpsApplied>(api, HttpMethod.Post, WorkflowRoutes.Ops(slug), cancellationToken, new CallOptions(Content: content)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<Result<WorkflowActivated>> ActivateWorkflow(Uri api, string slug, CancellationToken cancellationToken) =>
        Call<WorkflowActivated>(api, HttpMethod.Post, WorkflowRoutes.Activate(slug), cancellationToken, new CallOptions(Content: AwJson.Serialize(new ActivateWorkflowBody(Revision: null), Json)));

    /// <inheritdoc />
    public Task<Result<WorkflowPaused>> PauseWorkflow(Uri api, string slug, CancellationToken cancellationToken) =>
        Call<WorkflowPaused>(api, HttpMethod.Post, WorkflowRoutes.Pause(slug), cancellationToken);
}
