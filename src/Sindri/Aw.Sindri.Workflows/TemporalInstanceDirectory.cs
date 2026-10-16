using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Temporalio.Api.Enums.V1;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// <see cref="IInstanceDirectory"/> over the Temporal client (engine spec rev 8 §5.1, §6.1; engine P4 unit (b)). The
/// attempts of a '(recipe, item)' pair are read by describing their workflow ids, '{item}/{recipe}/1', '/2' … up to the
/// first that doesn't exist, so a start never waits on eventually consistent visibility. A bare item is resolved through
/// the 'Item' search attribute. An opaque id is found among the instances this process has seen, else by minting the id
/// of every instance visibility lists. An instance is live while its execution runs (<see cref="IInstanceLiveness"/>,
/// engine P6 unit (d)).
/// </summary>
/// <param name="frontend">Finds the dev server serve keeps.</param>
/// <param name="known">The ids this process has seen.</param>
internal sealed class TemporalInstanceDirectory(TemporalFrontend frontend, KnownInstances known) : IInstanceDirectory, IInstanceLiveness
{
    public async ValueTask<Result<AttemptState?>> Latest(ItemKey item, RecipeId recipe, CancellationToken cancellationToken)
    {
        var client = await frontend.Connect(flag: null, cancellationToken).ConfigureAwait(false);
        if (client.IsError)
        {
            return client.Error;
        }

        AttemptState? latest = null;
        for (var attempt = 1; ; attempt++)
        {
            var address = InstanceAddress.Root(item, recipe, attempt);
            try
            {
                var described = await client.Value.GetWorkflowHandle(address.Value).DescribeAsync(new() { Rpc = new() { CancellationToken = cancellationToken } }).ConfigureAwait(false);
                latest = new AttemptState(address, attempt, described.Status == WorkflowExecutionStatus.Running);
            }
            catch (RpcException e) when (TemporalUpdates.NoNamespace(e)) // boundary: not serve's Temporal, not a missing attempt
            {
                return TemporalUpdates.NotServes(client.Value, e);
            }
            catch (RpcException e) when (e.Code == RpcException.StatusCode.NotFound) // boundary: the first attempt that was never started ends the scan
            {
                return latest;
            }
            catch (RpcException e) when (e.Code == RpcException.StatusCode.Unavailable) // boundary: the dev server died under a live serve
            {
                return TemporalUpdates.Down(client.Value, e);
            }
        }
    }

    public async ValueTask<Result<bool>> IsLive(InstanceAddress address, CancellationToken cancellationToken)
    {
        var client = await frontend.Connect(flag: null, cancellationToken).ConfigureAwait(false);
        if (client.IsError)
        {
            return client.Error;
        }

        try
        {
            var described = await client.Value.GetWorkflowHandle(address.Value).DescribeAsync(new() { Rpc = new() { CancellationToken = cancellationToken } }).ConfigureAwait(false);
            return described.Status == WorkflowExecutionStatus.Running;
        }
        catch (RpcException e) when (TemporalUpdates.NoNamespace(e)) // boundary: not serve's Temporal, not a missing instance
        {
            return TemporalUpdates.NotServes(client.Value, e);
        }
        catch (RpcException e) when (e.Code == RpcException.StatusCode.NotFound) // boundary: never started (or no longer kept) is not live
        {
            return false;
        }
    }

    public async ValueTask<Result<InstanceAddress>> Resolve(string reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (WorkflowInstanceId.TryFrom(reference, out var id))   // first: 'WFI-…' has an item key's shape too
        {
            return await Find(id, cancellationToken).ConfigureAwait(false);
        }

        if (InstanceAddress.TryFrom(reference, out var address))
        {
            return address;
        }

        if (!ItemKey.TryFrom(reference, out var item))
        {
            return WorkflowErrors.InstanceNotFound.With($"'{reference}' is neither an address ('T-3/step/1') nor an item (fix: sindri workflow list)");
        }

        var client = await frontend.Connect(flag: null, cancellationToken).ConfigureAwait(false);
        if (client.IsError)
        {
            return client.Error;
        }

        return await Roots(client.Value, item, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The instance whose id is <paramref name="id"/>: one this process has seen, else the visible instance whose address mints it.</summary>
    private async ValueTask<Result<InstanceAddress>> Find(WorkflowInstanceId id, CancellationToken cancellationToken)
    {
        if (known.Find(id) is { } seen)
        {
            return seen;
        }

        var client = await frontend.Connect(flag: null, cancellationToken).ConfigureAwait(false);
        if (client.IsError)
        {
            return client.Error;
        }

        try
        {
            await foreach (var execution in client.Value.ListWorkflowsAsync("WorkflowType = 'RecipeWorkflow'").WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (InstanceAddress.TryFrom(execution.Id, out var address) && WorkflowInstanceId.Of(address) == id)
                {
                    known.Remember(address);
                    return address;
                }
            }
        }
        catch (RpcException e) when (TemporalUpdates.NoNamespace(e)) // boundary: not serve's Temporal
        {
            return TemporalUpdates.NotServes(client.Value, e);
        }
        catch (RpcException e) when (e.Code == RpcException.StatusCode.Unavailable) // boundary: the dev server died under a live serve
        {
            return TemporalUpdates.Down(client.Value, e);
        }

        return WorkflowErrors.InstanceNotFound.With($"no instance has the id {id.Value} (fix: sindri workflow list)");
    }

    private static async ValueTask<Result<InstanceAddress>> Roots(Temporalio.Client.ITemporalClient client, ItemKey item, CancellationToken cancellationToken)
    {
        var roots = new Dictionary<string, (int Attempt, InstanceAddress Address)>(StringComparer.Ordinal);
        try
        {
            await foreach (var execution in client.ListWorkflowsAsync($"WorkflowType = 'RecipeWorkflow' AND Item = '{item.Value}'").WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (execution.Id.Split('/') is [_, var recipe, var number] && int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var attempt)
                    && (!roots.TryGetValue(recipe, out var seen) || seen.Attempt < attempt))
                {
                    roots[recipe] = (attempt, InstanceAddress.From(execution.Id));
                }
            }
        }
        catch (RpcException e) when (TemporalUpdates.NoNamespace(e)) // boundary: not serve's Temporal
        {
            return TemporalUpdates.NotServes(client, e);
        }
        catch (RpcException e) when (e.Code == RpcException.StatusCode.Unavailable) // boundary: the dev server died under a live serve
        {
            return TemporalUpdates.Down(client, e);
        }

        return roots.Count switch
        {
            0 => WorkflowErrors.InstanceNotFound.With($"{item.Value} has no instance (fix: sindri workflow start <recipe> --item {item.Value})"),
            1 => roots.Values.Single().Address,
            _ => WorkflowErrors.AmbiguousAddress.With(
                $"{item.Value} has instances of more than one recipe: {string.Join(", ", roots.Values.Select(static r => r.Address.Value).Order(StringComparer.Ordinal))} (fix: use one of these addresses)"),
        };
    }
}
