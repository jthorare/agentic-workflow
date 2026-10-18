using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>
/// Something that asks an instance to change (spec §5.2, §5.4), identified as 'wf:{instance}:{<see cref="KeyPart"/>}'
/// in its ledger events. Commands are Updates through the API's chain with a nonce; system triggers carry 'service:&lt;name&gt;' and their
/// own ids. Every trigger kind is a sealed subtype.
/// </summary>
/// <param name="Actor">Who fired it: the caller of a command ('human:cli', 'session:…'), or 'service:&lt;name&gt;' for a system trigger.</param>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(StepCompleted), "step-completed")]
[JsonDerivedType(typeof(StepFailed), "step-failed")]
[JsonDerivedType(typeof(WaitSatisfied), "wait-satisfied")]
[JsonDerivedType(typeof(CommandFired), "command")]
[JsonDerivedType(typeof(AnswerGiven), "answer")]
[JsonDerivedType(typeof(TakeoverRequested), "takeover")]
[JsonDerivedType(typeof(HandbackRequested), "handback")]
[JsonDerivedType(typeof(CancelRequested), "cancel")]
[JsonDerivedType(typeof(DeadlineFired), "deadline")]
[JsonDerivedType(typeof(RecipeWake), "recipe-wake")]
[JsonDerivedType(typeof(OutboxRefused), "outbox-refused")]
[JsonDerivedType(typeof(ChildChanged), "child")]
[JsonDerivedType(typeof(OpenRequested), "open-request")]
[JsonDerivedType(typeof(RuntimeUnavailable), "runtime-unavailable")]
[JsonDerivedType(typeof(LaunchRefused), "launch-refused")]
public abstract record Trigger(string Actor)
{
    /// <summary>The trigger's name (<see cref="TriggerNames"/>): what the event records as its kind and an eval line names.</summary>
    [JsonIgnore]
    public abstract string Name { get; }

    /// <summary>The trigger's own id, after 'wf:{instance}:' in its key (spec §5.6).</summary>
    [JsonIgnore]
    public abstract string KeyPart { get; }

    /// <summary>Whether the approval check (RCP-CAP-003, §5.2) skips it: cancel, takeover, handback, takeoverIdle and open-request (W11, UI E-6).</summary>
    [JsonIgnore]
    public virtual bool ExemptFromApproval => false;

    /// <summary>The trigger's key on <paramref name="instance"/>.</summary>
    /// <param name="instance">The instance it is for.</param>
    /// <returns>'wf:{instance}:{KeyPart}'.</returns>
    public TriggerKey KeyFor(WorkflowInstanceId instance) => TriggerKey.For(instance, KeyPart);
}
