using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.BugFix;

/// <summary>What 'step.rootcause.investigate' is told (#1682): confirm the cause, ask the prevention question, and hand the block on in fact:rootCause.</summary>
internal sealed class InvestigateInstructions : IAgentStepInstructions
{
    private static readonly BlockId _step = BlockId.From("step.rootcause.investigate");

    private static readonly string[] _lines =
    [
        "Goal: the confirmed root cause of the reported bug, and the decision on how to stop its class from coming back.",
        "Read fact:issue, fact:expected and fact:actual from your inputs. Reproduce or otherwise confirm the cause from evidence before you conclude anything: a cause you haven't seen is a guess.",
        .. BugClassPrevention.SharedLines,
        "Report diagnosed with fact:rootCause. It MUST be the handoff block, with these six headings in this order: '## Failure class' (one sentence), '## Invariant' (one sentence), '## Affected scope' (other instances with file:line, or how you searched and found none), '## Prevention decision' (existing guard <which>, new mechanism <which>, or deferred: <follow-up>, and why it covers the class), '## Mitigation' (the instance fix and any stopgap, or none), '## Proof' (Original failure: the check, to be run red then green; Prevention: pending). Every later Step carries the block on unchanged.",
        "Report unfixed when you can't confirm a cause, with fact:findings saying what you ruled out and how.",
    ];

    /// <inheritdoc/>
    public bool IsFor(BlockId step) => step == _step;

    /// <inheritdoc/>
    public IReadOnlyList<string> Lines => _lines;
}
