using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Temporalio.Client;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The 'complete' path of <see cref="TemporalInstanceRuntime.Send"/> (engine spec rev 8.1 §5.2, §5.3; engine P4 unit
/// (c)): the desk report as the 'complete' Update on the instance's workflow, with the nonce as its update id, so
/// Temporal returns the first outcome for a repeated nonce. The validator's refusal comes back as the
/// ApplicationFailure its error type names (SND-WF-003, -005, -008), mapped back to its catalogue code.
/// </summary>
/// <param name="frontend">Finds the dev server serve keeps.</param>
internal sealed class TemporalDeskReports(TemporalFrontend frontend)
{
    /// <summary>Sends <paramref name="report"/> as the 'complete' Update keyed by <paramref name="nonce"/>.</summary>
    /// <param name="address">The instance (its workflow id).</param>
    /// <param name="nonce">The update id.</param>
    /// <param name="report">The desk report.</param>
    /// <param name="cancellationToken">Cancels the wait for the outcome.</param>
    /// <returns>
    /// The outcome, the validator's refusal, SND-WF-001 for no such instance, SND-API-001 for a Temporal without the
    /// namespace, or SND-API-002 when no worker took the report within <see cref="TemporalUpdates.Patience"/>.
    /// </returns>
    public async ValueTask<Result<CommandResult>> Complete(InstanceAddress address, CommandNonce nonce, StepCompleted report, CancellationToken cancellationToken)
    {
        var client = await frontend.Connect(flag: null, cancellationToken).ConfigureAwait(false);
        if (client.IsError)
        {
            return client.Error;
        }

        var handle = client.Value.GetWorkflowHandle<RecipeWorkflow>(address.Value);
        return await TemporalUpdates.Execute(
            TemporalUpdates.UpdateFrontend.Of(client.Value),
            address,
            nonce.Value,
            rpc => handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(report), new WorkflowUpdateOptions(nonce.Value) { Rpc = rpc }),
            cancellationToken).ConfigureAwait(false);
    }
}
