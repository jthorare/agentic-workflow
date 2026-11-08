using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The takeover Updates of <see cref="RecipeWorkflow"/> (engine spec §3.8, §5.2, §5.5, §5.8; UI spec E-6, E-19; engine
/// P5 unit (b)): 'takeover' (class pause; a session may take over) suspends the instance and pauses its state timer,
/// keeping what was left on the suspension; 'handback' (class desk) pops back to whatever the takeover covered and
/// re-arms that timer with what was left; 'open-request' is the takeover the engine sends itself (caller
/// 'service:engine') when a launch is requested for the item, keyed 'wf:{instance}:open:{launch}', a no-op when already
/// taken over. All three are exempt from the approval check. Each validator runs the pure decision on the instance as
/// it stands and rejects what it rejects (SND-WF-003), recording nothing; the accepted Update hands its trigger to the
/// loop through <see cref="Submit"/>.
/// </summary>
internal sealed partial class RecipeWorkflow
{
    /// <summary>'workflow takeover' (class pause, spec §5.5): suspends the instance and pauses its timer.</summary>
    /// <param name="takeover">The takeover.</param>
    /// <returns>The instance after it: taken over, with what was left of its timer.</returns>
    [WorkflowUpdate("takeover")]
    public Task<CommandResult> TakeoverAsync(TakeoverRequested takeover) => Applied(takeover);

    /// <summary>Rejects a takeover where none is allowed: an ended instance, or one already taken over (SND-WF-003).</summary>
    /// <param name="takeover">The takeover.</param>
    [WorkflowUpdateValidator(nameof(TakeoverAsync))]
    public void ValidateTakeover(TakeoverRequested takeover) => Validate(takeover);

    /// <summary>'workflow handback' (class desk, spec §5.5): ends the takeover and re-arms the timer with what remained.</summary>
    /// <param name="handback">The handback.</param>
    /// <returns>The instance after it: back where the takeover found it.</returns>
    [WorkflowUpdate("handback")]
    public Task<CommandResult> HandbackAsync(HandbackRequested handback) => Applied(handback);

    /// <summary>Rejects a handback of an instance that isn't taken over (SND-WF-003).</summary>
    /// <param name="handback">The handback.</param>
    [WorkflowUpdateValidator(nameof(HandbackAsync))]
    public void ValidateHandback(HandbackRequested handback) => Validate(handback);

    /// <summary>
    /// An 'open-request' (UI spec E-6, spec §5.2): the engine's own takeover before the launcher opens a session for the
    /// item. Update id 'wf:{instance}:open:{launch}' (<see cref="InstanceIds.OpenRequestUpdate"/>), no nonce, actor
    /// 'service:engine', exempt from the approval check; a no-op when already taken over. The launcher waits for this
    /// result.
    /// </summary>
    /// <param name="request">The open-request.</param>
    /// <returns>The instance after it: taken over, or as it was.</returns>
    [WorkflowUpdate("open-request")]
    public async Task<CommandResult> OpenRequestAsync(OpenRequested request)
    {
        await Workflow.WaitConditionAsync(() => _instance is not null).ConfigureAwait(true);   // E-6: a launch for an instance still starting takes it over once it has started
        return await Applied(request).ConfigureAwait(true);
    }

    /// <summary>
    /// Rejects an open-request where no takeover is allowed: an ended instance, any actor but 'service:engine', or a
    /// launch that isn't a launch id (SND-WF-003). One that arrives while the instance is starting is accepted; the Update waits for the start.
    /// </summary>
    /// <param name="request">The open-request.</param>
    [WorkflowUpdateValidator(nameof(OpenRequestAsync))]
    public void ValidateOpenRequest(OpenRequested request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!LaunchId.TryFrom(request.Launch, out _))
        {
            throw Refusal(WorkflowErrors.TriggerNotAllowed.With($"an open-request names launch '{request.Launch}', not a launch id: 'LCH-' and 32 lower-case hex digits (fix: the launcher sends the id it minted)"));
        }

        if (_instance is null)
        {
            if (!string.Equals(request.Actor, EngineActor.Name, StringComparison.Ordinal))
            {
                throw Refusal(WorkflowErrors.TriggerNotAllowed.With($"an open-request comes from {EngineActor.Name}, not {request.Actor} (fix: the launcher sends it)"));
            }

            return;   // starting: nothing to decide against yet, and an instance that hasn't started hasn't ended
        }

        Validate(request);
    }

    /// <summary>Hands <paramref name="trigger"/> to the loop and returns the instance after it and what happened.</summary>
    private async Task<CommandResult> Applied(Trigger trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        var before = _instance!; // why: the validator refused the Update while the instance was starting
        var happened = Outcome(before, await Submit(trigger).ConfigureAwait(true));
        return new CommandResult(happened.Instance, happened.Happened);
    }
}
