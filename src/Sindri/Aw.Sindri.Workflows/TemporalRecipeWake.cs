using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Temporalio.Client;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// <see cref="IRecipeWake"/> over the Temporal client (engine spec §4.1, W11; engine P6 integration): the running
/// instances of a hash are the open executions whose 'RecipeHash' search attribute is it, and a wake is the 'wake'
/// signal each takes, acting as 'service:engine' for the approver. With no Temporal serving this home nothing runs, so
/// there is nobody to wake and the registry change still stands.
/// </summary>
/// <param name="frontend">Finds the dev server.</param>
internal class TemporalRecipeWake(TemporalFrontend frontend) : IRecipeWake
{
    private const string Actor = "service:engine";

    public async ValueTask<Result<IReadOnlyList<WorkflowInstanceId>>> Running(RecipeHash hash, CancellationToken cancellationToken)
    {
        var open = await Open(hash, cancellationToken).ConfigureAwait(false);
        WorkflowInstanceId[] running = [.. open.Select(static a => WorkflowInstanceId.Of(a))];
        return running;
    }

    public async ValueTask<Result<Unit>> Wake(RecipeWakeNotice notice, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notice);
        var client = await frontend.Connect(flag: null, cancellationToken).ConfigureAwait(false);
        if (client.IsError)
        {
            return Unit.Value;   // nothing serves this home: no instance runs, so none needs the wake
        }

        var wake = new RecipeWake(Actor, notice.Kind, notice.Sequence);
        List<string> failed = [];
        foreach (var address in await Open(notice.Hash, cancellationToken).ConfigureAwait(false))
        {
            try
            {
                await Signal(client.Value, address, wake, cancellationToken).ConfigureAwait(false);
            }
            catch (RpcException e) when (e.Code == RpcException.StatusCode.NotFound)   // boundary: it ended between the list and the signal
            {
                continue;
            }
            catch (RpcException e)   // boundary: one instance's signal failing must not abandon the others; every one is tried, then the failures are reported
            {
                failed.Add($"{address.Value}: {e.Message}");
            }
        }

        return failed.Count == 0
            ? Unit.Value
            : SindriErrors.NotRunning.With($"Temporal didn't take the {notice.Kind} wake for {failed.Count} instance(s): {string.Join("; ", failed)} (fix: {Retry(notice)})");
    }

    /// <summary>
    /// What to do about a wake that missed an instance: an approval with the same pins sends it again; a revoke runs again
    /// to finish any cascade it didn't reach (refused only when nothing is left), and an instance still missed parks lazily at its next step.
    /// </summary>
    private static string Retry(RecipeWakeNotice notice) => notice.Kind == RecipeWakeKind.Revoked
        ? $"the revocation of {notice.Hash.Value} is recorded; run the revoke again to finish any cascade it didn't reach and send the wake again to what it reaches (refused SND-RECIPE-005 when nothing is left); an instance the wake missed is refused at its next command, answer, complete or effect (SND-RECIPE-005) and parks then"
        : "run `sindri recipe approve <recipe>` again; approving with the same pins records nothing and sends the wake again";

    /// <summary>Delivers <paramref name="wake"/> to the instance at <paramref name="address"/>.</summary>
    internal virtual async Task Signal(ITemporalClient client, InstanceAddress address, RecipeWake wake, CancellationToken cancellationToken) =>
        await client.GetWorkflowHandle<RecipeWorkflow>(address.Value).SignalAsync(
            wf => wf.WakeAsync(wake), new WorkflowSignalOptions { Rpc = new RpcOptions { CancellationToken = cancellationToken } }).ConfigureAwait(false);

    /// <summary>The open instances running <paramref name="hash"/>; none when nothing serves this home.</summary>
    internal virtual async ValueTask<IReadOnlyList<InstanceAddress>> Open(RecipeHash hash, CancellationToken cancellationToken)
    {
        var client = await frontend.Connect(flag: null, cancellationToken).ConfigureAwait(false);
        if (client.IsError)
        {
            return [];
        }

        var addresses = new List<InstanceAddress>();
        try
        {
            await foreach (var execution in client.Value.ListWorkflowsAsync($"WorkflowType = 'RecipeWorkflow' AND ExecutionStatus = 'Running' AND RecipeHash = '{hash.Value}'")
                .WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                addresses.Add(InstanceAddress.From(execution.Id));
            }
        }
        catch (RpcException e) when (e.Code is RpcException.StatusCode.NotFound or RpcException.StatusCode.InvalidArgument)   // boundary: not serve's Temporal, or its visibility has no RecipeHash yet
        {
            return [];
        }

        return addresses;
    }
}
