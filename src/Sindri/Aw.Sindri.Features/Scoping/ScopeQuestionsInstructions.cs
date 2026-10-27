using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>What 'step.interview.questions' is told about its output (#1204): every question carries exactly one recommended option, checked by SCP-QST-001.</summary>
internal sealed class ScopeQuestionsInstructions : IAgentStepInstructions
{
    private static readonly BlockId _step = BlockId.From("step.interview.questions");

    private static readonly string[] _lines =
    [
        "Every question in fact:questions has two to four options and exactly one of them marked \"recommended\": true; the other options leave it out or set it false. Zero recommended and two recommended are both refused (SCP-QST-001).",
        "Recommend the option your cited sources favour, and say in its rationale which source. When a question can't be decided yet (it waits on a number or an answer you don't have), recommend the option that defers it and reports it, or put it in fact:openQuestions instead of asking it.",
    ];

    /// <inheritdoc/>
    public bool IsFor(BlockId step) => step == _step;

    /// <inheritdoc/>
    public IReadOnlyList<string> Lines => _lines;
}
