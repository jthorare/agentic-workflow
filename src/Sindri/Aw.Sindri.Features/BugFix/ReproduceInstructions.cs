using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.BugFix;

/// <summary>What 'step.reproduce.test-check' and 'step.reproduce.ui-check' are told (#1682): reproduce the original failure only, and keep the handoff block as read.</summary>
internal sealed class ReproduceInstructions : IAgentStepInstructions
{
    private static readonly BlockId[] _steps = [BlockId.From("step.reproduce.test-check"), BlockId.From("step.reproduce.ui-check")];

    private static readonly string[] _lines =
    [
        "Goal: one check that fails for the reported reason, so the fix can be proven against it.",
        "Read fact:issue and fact:rootCause from your inputs. fact:rootCause holds the handoff block (failure class, invariant, affected scope, prevention decision, mitigation, proof); keep it as you read it and don't edit it.",
        "Make the check reproduce the original failure, for the reported reason only, and don't broaden it to the class: the class's proof belongs to the fix, and a broader check would no longer be the one that failed before.",
        "The question the investigation already asked, so you can see why the check stays narrow: " + BugClassPrevention.Question,
        "If you can't make it fail for that reason, report notReproduced with fact:findings saying what you tried. Never weaken a check to make it fail or pass.",
        "The full prevention rule: $HOME/.agentic-workflow/toolkit/skills/_shared/prevent-the-class.md.",
    ];

    /// <inheritdoc/>
    public bool IsFor(BlockId step) => Array.IndexOf(_steps, step) >= 0;

    /// <inheritdoc/>
    public IReadOnlyList<string> Lines => _lines;
}
