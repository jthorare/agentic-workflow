using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>What a decision records (spec §5.6, §5.7): one ledger event per changed instance with its post-state delta, and the effects.</summary>
public static partial class TransitionDecision
{
    /// <summary>
    /// The transition's effects (§5.7), each with identity '{instance}:{entry}:{index}' under the entered frame's entry;
    /// the interpreter runs each as an activity after the transition's ledger event.
    /// </summary>
    private static List<EffectCommand> Effects(RecipeTransition transition, WorkflowInstanceId instance, int entry) =>
        [.. transition.Effects.Select((use, index) => new EffectCommand(use.Use, new EffectIdentity(instance, entry, index), CommandOrigin.Recipe, use.With))];

    private sealed partial record Deciding
    {
        /// <summary>The instance's one workflow.transition event, and nothing else.</summary>
        public Applied Changed(WorkflowInstance after) => Changed(after, Records.None);

        /// <summary>The instance's one workflow.transition event with the transition's gate and evidence records.</summary>
        public Applied Changed(WorkflowInstance after, Records records) =>
            new(Key, [Change(after, WorkflowEventType.Transition, records)], [], []);

        /// <summary>The change, or <see cref="Absorbed"/> when the rule left the instance as it was.</summary>
        public Decision ChangedOrAbsorbed(WorkflowInstance after, WorkflowInstance before, string reason) =>
            ReferenceEquals(after, before) ? new Absorbed(Key, reason) : Changed(after);

        /// <summary>The instance after the decision and its event: the trigger, the records and the delta (§5.6).</summary>
        public InstanceChange Change(WorkflowInstance after, WorkflowEventType type, Records records)
        {
            var before = Input.Instance;
            var trigger = Answered is { } answer
                ? new TriggerRecord(answer.Name, Key, answer.Actor, Outcome: null)
                : new TriggerRecord(Trigger.Name, Key, Trigger.Actor, Outcome(Trigger), (Trigger as StepFailed)?.Reason,
                    Via: Trigger is StepCompleted { Via: StepRuntime.Block } ? "block" : null, Decision: (Trigger as StepCompleted)?.Decision);
            return new InstanceChange(after, new WorkflowEvent(
                type, after.Id, trigger, Started: null, Rules.Delta(before, after),
                records.Gates, records.Evidence, Grant: null, Override: null, Refusal: null, Effects: []));
        }
    }
}
