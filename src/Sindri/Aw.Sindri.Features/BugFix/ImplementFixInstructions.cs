using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.BugFix;

/// <summary>What 'step.implement.fix' is told (#1682): fix the instance and build the prevention the block decided, both proven.</summary>
internal sealed class ImplementFixInstructions : IAgentStepInstructions
{
    private static readonly BlockId _step = BlockId.From("step.implement.fix");

    private static readonly string[] _lines =
    [
        "Goal: fix the reported bug and build the prevention the root cause's block decided, tests first, in atomic commits.",
        "Read fact:issue and fact:rootCause from your inputs. fact:rootCause holds the handoff block: failure class, invariant, affected scope, prevention decision, mitigation, proof. Work inside the phase's allowed scope.",
        "The question behind the block's prevention decision, so you can check it still holds once you see the code: " + BugClassPrevention.Question,
        "Write the instance fix test first (it fails for the reported reason, then passes) and build the mechanism the Prevention decision names. When it says deferred, build the instance fix only and make sure the follow-up it names exists. If the code shows the decision was wrong, change it, and say so in the block.",
        "Prove both. The mechanism needs its own proof, e.g. an analyzer that rejects a second bad sample and accepts a good one, or a check that runs over every instance; a regression test for one fixture is not class-wide prevention. A quick mitigation is fine, called mitigation.",
        "Never change the reproduction check (fact:check) to make a broader change pass, and never claim the class is gone without the mechanism's own proof. The full rule: $HOME/.agentic-workflow/toolkit/skills/_shared/prevent-the-class.md.",
        "Report done with fact:output: what changed, and the handoff block as you read it, its Proof filled in (Original failure: red before, green after; Prevention: the mechanism's proof, or deferred with the follow-up). The description must end with that block.",
    ];

    /// <inheritdoc/>
    public bool IsFor(BlockId step) => step == _step;

    /// <inheritdoc/>
    public IReadOnlyList<string> Lines => _lines;
}
