namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>
/// An 'item.open-requested' (UI spec E-6, X18): an Update the engine sends itself wherever a takeover is allowed (§5.2),
/// keyed 'wf:{instance}:open:{launch}'. The contract: the actor is 'service:engine' (any other is SND-WF-003) and the launch
/// is a <see cref="Launch.LaunchId"/> (a malformed one is SND-WF-003 too); it is a trigger, not a command, so it has no nonce; it
/// is exempt from the approval check like a takeover; the request event is its causation; and it is absorbed (nothing
/// recorded) when the instance is already taken over.
/// </summary>
/// <param name="Actor">'service:engine'.</param>
/// <param name="Launch">The launch request's id ('LCH-…').</param>
public sealed record OpenRequested(string Actor, string Launch) : Trigger(Actor)
{
    /// <inheritdoc/>
    public override string Name => TriggerNames.OpenRequest;

    /// <inheritdoc/>
    public override string KeyPart => "open:" + Launch;

    /// <inheritdoc/>
    public override bool ExemptFromApproval => true;
}
