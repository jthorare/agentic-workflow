using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The instances on Temporal, as the slices see them (port; engine spec rev 8 §5.1–§5.4). The adapter is the Temporal
/// client in Aw.Sindri.Workflows; every caller check (the API chain, the nonce, the approval check) runs before it.
/// </summary>
public interface IInstanceRuntime : IInstanceReader
{
    /// <summary>Starts a 'RecipeWorkflow' with id <see cref="InstanceStart.WorkflowId"/>, or finds the running one (UseExisting).</summary>
    /// <param name="start">The workflow input.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>The instance as 'GetInstance' shows it, or SND-API-001 (Temporal unreachable).</returns>
    public ValueTask<Result<InstanceView>> Start(InstanceStart start, CancellationToken cancellationToken);

    /// <summary>
    /// Sends <paramref name="command"/> as the Update its kind names ('complete', 'answer', 'command', 'cancel',
    /// 'takeover', 'handback'), with <paramref name="nonce"/> as the update id: a repeated nonce returns the first outcome.
    /// </summary>
    /// <param name="address">The instance.</param>
    /// <param name="nonce">The nonce, the Update's id.</param>
    /// <param name="command">The trigger: StepCompleted, AnswerGiven, CommandFired, CancelRequested, TakeoverRequested or HandbackRequested.</param>
    /// <param name="cancellationToken">Cancels the wait for the outcome.</param>
    /// <returns>The outcome, or the validator's refusal (SND-WF-003, -005, -008) with nothing recorded, or SND-WF-001.</returns>
    public ValueTask<Result<CommandResult>> Send(InstanceAddress address, CommandNonce nonce, Trigger command, CancellationToken cancellationToken);

    /// <summary>
    /// Sends an 'open-request' (UI spec E-6, engine spec §5.2) as the 'open-request' Update, with update id
    /// 'wf:{instance}:open:{launch}': a trigger, not a command, so it takes no nonce, and the engine sends it itself
    /// (caller 'service:engine'), past the API chain and so past the approval check it is exempt from. Temporal
    /// deduplicates the id, so a repeat returns the first outcome; one sent to an instance already taken over is a no-op.
    /// The launcher waits for this result (E-1) and fails the launch with the refusal's code. Engine P5 unit (b).
    /// </summary>
    /// <param name="address">The item's active instance.</param>
    /// <param name="launch">The launch the open-request answers; the runtime sends it as actor 'service:engine'.</param>
    /// <param name="cancellationToken">Cancels the wait for the outcome.</param>
    /// <returns>The instance after it (taken over, or as it was), or the validator's refusal (SND-WF-003), or SND-WF-001.</returns>
    public ValueTask<Result<CommandResult>> OpenRequest(InstanceAddress address, LaunchId launch, CancellationToken cancellationToken);

    /// <summary>Signals a registered event to the instance (the bridge, §5.4); Temporal buffers it until the instance reaches its wait.</summary>
    /// <param name="address">The instance the event routes to.</param>
    /// <param name="delivered">The event as the wait's trigger.</param>
    /// <param name="cancellationToken">Cancels the signal.</param>
    /// <returns>Done, or SND-WF-001.</returns>
    public ValueTask<Result<Unit>> Signal(InstanceAddress address, WaitSatisfied delivered, CancellationToken cancellationToken);
}
