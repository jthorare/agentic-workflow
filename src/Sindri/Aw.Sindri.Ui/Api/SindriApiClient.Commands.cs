using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Api;

/// <summary>The commands on a workflow instance (<see cref="SindriApiClient"/>, continued): a desk report and the commands.</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<CompleteStepAccepted>> CompleteStep(Uri api, string instanceId, CompleteStepBody body, CancellationToken cancellationToken) =>
        Call<CompleteStepAccepted>(api, HttpMethod.Post, WorkflowRoutes.Complete(instanceId, body.Run), cancellationToken, new CallOptions(Content: AwJson.Serialize(body, Json)));

    /// <inheritdoc />
    public Task<Result<ApplyCommandAccepted>> ApplyCommand(Uri api, string instanceId, ApplyCommandBody body, CancellationToken cancellationToken) =>
        Call<ApplyCommandAccepted>(api, HttpMethod.Post, WorkflowRoutes.Commands(instanceId), cancellationToken, new CallOptions(Content: AwJson.Serialize(body, Json)));
}
