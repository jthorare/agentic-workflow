using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// <see cref="IInstanceRuntime"/> over the Temporal client (engine spec rev 8 §5.1–§5.4). Unit (b) builds Start
/// (WorkflowIdConflictPolicy.UseExisting, workflow id = the address) and Send's answer, command and cancel paths
/// (<see cref="TemporalCommands"/>: the Update named by the trigger, update id = the nonce, the validator's
/// ApplicationFailure error type mapped back to its code); unit (c) owns Send's 'complete' path,
/// <see cref="TemporalDeskReports"/>; unit (e) builds Get (the 'GetInstance' Query; unit (b) carries a minimal one that
/// gives way to (e)'s); unit (d) builds Signal ('event').
/// </summary>
/// <param name="deskReports">The 'complete' Update (unit (c)).</param>
/// <param name="frontend">Finds the dev server serve keeps (Start, unit (b); Get, unit (e); the 'event' signal, unit (d)).</param>
/// <param name="commands">The answer, command and cancel Updates (unit (b)).</param>
/// <param name="settings">The task queue instances start on.</param>
/// <param name="known">The ids this process has seen: every instance it starts or queries (a read cache for the id-to-address hop; it holds no instance state).</param>
internal sealed class TemporalInstanceRuntime(TemporalFrontend frontend, TemporalDeskReports deskReports, TemporalCommands commands, TemporalSettings settings, KnownInstances known) : IInstanceRuntime
{
    /// <summary>How long a Query waits for a worker to answer; past it, status says no worker answered.</summary>
    internal static readonly TimeSpan QueryPatience = TimeSpan.FromSeconds(60);

    /// <summary>How often, 50 ms apart, a start asks whether the new instance has left 'starting' (its recipe loaded, its first state entered): 120 s.</summary>
    private const int StartPolls = 2400;

    /// <summary>
    /// Starts 'RecipeWorkflow' with the address as its workflow id (spec §5.1): an execution already running under that id
    /// is returned as it is (UseExisting), and an ended one is never started again under the same id (RejectDuplicate;
    /// the slice asks for '--attempt next'). Returns the instance once it has entered its first state.
    /// </summary>
    public async ValueTask<Result<InstanceView>> Start(InstanceStart start, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(start);
        var client = await frontend.Connect(flag: null, cancellationToken).ConfigureAwait(false);
        if (client.IsError)
        {
            return client.Error;
        }

        var address = start.WorkflowId();
        try
        {
            _ = await client.Value.StartWorkflowAsync(
                (RecipeWorkflow wf) => wf.RunAsync(start),
                new WorkflowOptions(address.Value, settings.TaskQueue)
                {
                    IdConflictPolicy = WorkflowIdConflictPolicy.UseExisting,
                    IdReusePolicy = WorkflowIdReusePolicy.RejectDuplicate,
                    Rpc = new RpcOptions { CancellationToken = cancellationToken },
                }).ConfigureAwait(false);
        }
        catch (WorkflowAlreadyStartedException) // boundary: the attempt ended; RejectDuplicate keeps its id, and Get shows it as it ended
        {
            return Remembered(await Get(address, cancellationToken).ConfigureAwait(false));
        }
        catch (RpcException e) when (TemporalUpdates.NoNamespace(e)) // boundary: not serve's Temporal
        {
            return TemporalUpdates.NotServes(client.Value, e);
        }
        catch (RpcException e) when (e.Code == RpcException.StatusCode.Unavailable) // boundary: the dev server died under a live serve
        {
            return TemporalUpdates.Down(client.Value, e);
        }

        return await WaitStarted(address, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Asks every 50 ms whether the new instance has left 'starting', up to <see cref="StartPolls"/> times.</summary>
    private async ValueTask<Result<InstanceView>> WaitStarted(InstanceAddress address, CancellationToken cancellationToken)
    {
        for (var poll = 1; ; poll++)
        {
            var view = await Get(address, cancellationToken).ConfigureAwait(false);
            if (view.IsError || view.Value.State != "starting" || poll >= StartPolls)
            {
                return Remembered(view);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>A start remembers the instance it shows (the directory finds its id).</summary>
    private Result<InstanceView> Remembered(Result<InstanceView> view)
    {
        if (view.IsSuccess)
        {
            known.Remember(view.Value.Address);
        }

        return view;
    }

    /// <summary>
    /// The 'GetInstance' Query on the workflow whose id is <paramref name="address"/> (unit (e)). An ended instance answers
    /// too (the worker replays it). No execution with that id is SND-WF-001; a Query no worker answers in
    /// <see cref="QueryPatience"/> is SND-API-002.
    /// </summary>
    public async ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken)
    {
        var client = await frontend.Connect(flag: null, cancellationToken).ConfigureAwait(false);
        if (client.IsError)
        {
            return client.Error;
        }

        try
        {
            var view = await Query(client.Value, address, cancellationToken).ConfigureAwait(false);
            known.Remember(address);   // a read cache for the id-to-address hop, not instance state
            return await LiveChildren.Of(view, (child, token) => Query(client.Value, child, token), cancellationToken).ConfigureAwait(false);   // engine P7: each child as it answers now (owner answer Q2)
        }
        catch (RpcException e) when (TemporalUpdates.NoNamespace(e)) // boundary: not serve's Temporal; says nothing about the instance
        {
            return TemporalUpdates.NotServes(client.Value, e);
        }
        catch (RpcException e) when (e.Code == RpcException.StatusCode.NotFound) // boundary: no such execution is the caller's input
        {
            return WorkflowErrors.InstanceNotFound.With($"{address.Value} isn't an instance (fix: sindri workflow list, or sindri workflow start)");
        }
        catch (RpcException e) when (e.Code == RpcException.StatusCode.Unavailable) // boundary: the dev server died under a live serve
        {
            return TemporalUpdates.Down(client.Value, e);
        }
        catch (RpcException e) when (e.Code == RpcException.StatusCode.DeadlineExceeded) // boundary: no worker answered
        {
            return SindriErrors.ApiTimeout.With($"no worker answered the status query for {address.Value} within {StatusText.Duration(QueryPatience)}: {e.Message} (fix: start sindri serve, which runs the worker)");
        }
        catch (WorkflowQueryFailedException e) // boundary: the instance hasn't taken its input yet
        {
            return WorkflowErrors.InstanceNotFound.With($"{address.Value} can't be queried yet: {e.Message} (fix: try again in a moment)");
        }
    }

    /// <summary>The 'GetInstance' Query on <paramref name="address"/>, bounded by <see cref="QueryPatience"/>.</summary>
    private static Task<InstanceView> Query(ITemporalClient client, InstanceAddress address, CancellationToken cancellationToken) =>
        TemporalReads.Run(
            _ => client.GetWorkflowHandle<RecipeWorkflow>(address.Value)
                .QueryAsync(static wf => wf.GetInstance(), new WorkflowQueryOptions { Rpc = TemporalReads.Rpc(cancellationToken, QueryPatience) }),
            cancellationToken);

    public ValueTask<Result<CommandResult>> Send(InstanceAddress address, CommandNonce nonce, Trigger command, CancellationToken cancellationToken) => command switch
    {
        StepCompleted report => deskReports.Complete(address, nonce, report, cancellationToken),   // unit (c)
        AnswerGiven or CommandFired or CancelRequested => commands.Send(address, nonce, command, cancellationToken),   // unit (b)
        TakeoverRequested or HandbackRequested => commands.Send(address, nonce, command, cancellationToken),   // engine P5
        _ => throw new NotSupportedException($"the '{command.Name}' trigger is no command Update"),
    };

    /// <summary>
    /// The 'open-request' Update (UI spec E-6, spec §5.2) with update id <see cref="InstanceIds.OpenRequestUpdate"/>: no
    /// nonce, actor 'service:engine'.
    /// </summary>
    public ValueTask<Result<CommandResult>> OpenRequest(InstanceAddress address, LaunchId launch, CancellationToken cancellationToken) =>
        commands.OpenRequest(address, new OpenRequested(EngineActor.Name, launch.Value), cancellationToken);

    /// <summary>
    /// The bridge's 'event' signal (spec §5.4; unit (d)): Temporal buffers it until the instance reaches the wait. The
    /// event's ledger sequence is its idempotency key: the workflow drops a sequence it has already taken.
    /// </summary>
    public async ValueTask<Result<Unit>> Signal(InstanceAddress address, WaitSatisfied delivered, CancellationToken cancellationToken)
    {
        var client = await frontend.Connect(flag: null, cancellationToken).ConfigureAwait(false);
        if (client.IsError)
        {
            return client.Error;
        }

        try
        {
            await client.Value.GetWorkflowHandle<RecipeWorkflow>(address.Value).SignalAsync(
                wf => wf.EventAsync(delivered),
                new WorkflowSignalOptions { Rpc = new RpcOptions { CancellationToken = cancellationToken } }).ConfigureAwait(false);
            return Unit.Value;
        }
        catch (RpcException e) when (TemporalUpdates.NoNamespace(e))   // boundary: not serve's Temporal
        {
            return TemporalUpdates.NotServes(client.Value, e);
        }
        catch (RpcException e) when (e.Code == RpcException.StatusCode.NotFound)   // boundary: no such instance, or it has ended
        {
            return WorkflowErrors.InstanceNotFound.With($"no running instance {address.Value} (fix: sindri workflow list)");
        }
        catch (RpcException e) when (e.Code == RpcException.StatusCode.Unavailable) // boundary: the dev server died under a live serve
        {
            return TemporalUpdates.Down(client.Value, e);
        }
    }
}
