using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.Api;

/// <summary>The workflow reads (<see cref="SindriApiClient"/>, continued; #2258 PR 8a): the reads are GETs; Run now and Cancel run are POSTs, each sent once.</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<WorkflowList>> Workflows(Uri api, CancellationToken cancellationToken) =>
        Get<WorkflowList>(api, WorkflowRoutes.List(), cancellationToken);

    /// <inheritdoc />
    public Task<Result<WorkflowView>> Workflow(Uri api, string slug, CancellationToken cancellationToken) =>
        Get<WorkflowView>(api, WorkflowRoutes.One(slug), cancellationToken);

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
}
