using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Temporalio.Client;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The open run's input sources, read with the 'GetInjection' Query (plan "How the text reaches the Step").</summary>
/// <param name="frontend">Connects to serve's Temporal.</param>
internal sealed class TemporalInjectionSources(TemporalFrontend frontend) : IInjectionSources
{
    public async ValueTask<Result<InjectionSource>> Read(InstanceAddress address, CancellationToken cancellationToken)
    {
        var client = await frontend.Connect(flag: null, cancellationToken).ConfigureAwait(false);
        if (client.IsError)
        {
            return client.Error;
        }

        try
        {
            var snapshot = await TemporalReads.Run(
                _ => client.Value.GetWorkflowHandle<RecipeWorkflow>(address.Value)
                    .QueryAsync(static wf => wf.GetInjection(), new WorkflowQueryOptions { Rpc = TemporalReads.Rpc(cancellationToken, TemporalInstanceRuntime.QueryPatience) }),
                cancellationToken).ConfigureAwait(false);
            return new InjectionSource(snapshot.State, snapshot.Run, [.. snapshot.Fills.Select(static f => new InjectionInputSource(f.Recorded, f.Request.Context()))]);
        }
        catch (RpcException e) when (e.Code == RpcException.StatusCode.NotFound) // boundary: no such execution
        {
            return WorkflowErrors.InstanceNotFound.With($"{address.Value} isn't an instance (fix: sindri workflow list)");
        }
        catch (RpcException e) when (e.Code is RpcException.StatusCode.Unavailable or RpcException.StatusCode.DeadlineExceeded) // boundary: the dev server or its worker is not answering
        {
            return SindriErrors.ApiTimeout.With($"no worker answered the injection query for {address.Value}: {e.Message} (fix: start sindri serve, which runs the worker)");
        }
        catch (WorkflowQueryFailedException e) // boundary: the instance hasn't taken its input yet
        {
            return WorkflowErrors.InstanceNotFound.With($"{address.Value} can't be queried yet: {e.Message} (fix: try again in a moment)");
        }
    }
}
