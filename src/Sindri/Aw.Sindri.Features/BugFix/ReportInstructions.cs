using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.BugFix;

/// <summary>What 'step.report.write' is told (#1682): the resolution report's three separate lines.</summary>
internal sealed class ReportInstructions : IAgentStepInstructions
{
    private static readonly BlockId _step = BlockId.From("step.report.write");

    private static readonly string[] _lines =
    [
        "Goal: the resolution report for this bug, true to what was proven, for a fixed or an unfixed bug alike.",
        "Read fact:issue and fact:rootCause from your inputs. fact:rootCause holds the handoff block (failure class, invariant, affected scope, prevention decision, mitigation, proof). Copy that block into the report as it stands and add the proof you find; don't rewrite it.",
        "Write fact:resolution with three separate lines, never merged into one 'fixed'. 'Original bug:' the reproduction check's result before and after the fix, and the verdict. 'Class prevention:' what was built and its proof, or 'existing guard <which>', or 'none built'. 'Deferred:' the follow-ups recorded, with links, or 'none'.",
        "Never claim the class is gone without the mechanism's own proof; say what it covers and what it doesn't. A passing check for the instance is not prevention.",
        "The question the investigation asked, so the 'Class prevention' line answers it: " + BugClassPrevention.Question,
        "The full rule: $HOME/.agentic-workflow/toolkit/skills/_shared/prevent-the-class.md. Report done.",
    ];

    /// <inheritdoc/>
    public bool IsFor(BlockId step) => step == _step;

    /// <inheritdoc/>
    public IReadOnlyList<string> Lines => _lines;
}
