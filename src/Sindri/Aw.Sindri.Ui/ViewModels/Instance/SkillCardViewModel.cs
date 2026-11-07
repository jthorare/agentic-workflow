using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>
/// The card the expanded state shows for a `step.skill.*` state (skill flows spec §5.2, §5.4): the block's sentence, a state word,
/// the question or the stopped text, the outcome in words, the outputs as paths, the loop round and the cost. Slice 1 is red:
/// every member throws until the card is written.
/// </summary>
#pragma warning disable CA1822, MA0041, MA0182 // red stubs: the members become instance reads and the type is wired into the expanded state in the green commit
internal sealed class SkillCardViewModel(SkillCardInput input)
{
    /// <summary>What the card was built from.</summary>
    internal SkillCardInput Input { get; } = input;

    /// <summary>True when the step is a skill block's (`step.skill.*`).</summary>
    /// <param name="step">The state's step, or null.</param>
    /// <returns>True for a skill block.</returns>
    internal static bool IsSkillStep(StepDeclaration? step) => throw new NotSupportedException();

    /// <summary>The sentence of the block the cell names (`block`) in GET /sindri/skills; empty when none is listed.</summary>
    public string Sentence => throw new NotSupportedException();

    /// <summary>Which state the card is in.</summary>
    public SkillCardState State => throw new NotSupportedException();

    /// <summary>The state as a word, never colour alone ("waiting to start", "running", "waiting on you", "done", "failed").</summary>
    public string StateWord => throw new NotSupportedException();

    /// <summary>The line the owner reads: "Shape the idea is waiting on you: …", or the stopped and broken-file texts; empty when nothing waits.</summary>
    public string Headline => throw new NotSupportedException();

    /// <summary>The skill's question, whole; empty when the block asks none.</summary>
    public string Question => throw new NotSupportedException();

    /// <summary>What Details shows: the result path and, for a broken file, the parse error; empty when nothing waits.</summary>
    public string Details => throw new NotSupportedException();

    /// <summary>The state cell's outcome in words ("needs work"); empty until it has one.</summary>
    public string Outcome => throw new NotSupportedException();

    /// <summary>The block's outputs that hold a path.</summary>
    public IReadOnlyList<SkillOutput> Outputs => throw new NotSupportedException();

    /// <summary>"round 2 of 2" when the state is in a loop; empty otherwise.</summary>
    public string RoundLine => throw new NotSupportedException();

    /// <summary>True while Take over applies: the block runs or waits on the owner.</summary>
    public bool CanTakeOver => throw new NotSupportedException();

    /// <summary>The polite live region that says the waiting row once when it appears.</summary>
    public LiveAnnouncer Voice => throw new NotSupportedException();

    /// <summary>"cost: $0.42", or "cost: not reported".</summary>
    public string CostLine => throw new NotSupportedException();

    /// <summary>Opens an output in the file manager.</summary>
    /// <param name="output">The output.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>True when the system opened it.</returns>
    internal Task<bool> Open(SkillOutput output, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Puts an output's path on the clipboard.</summary>
    /// <param name="output">The output.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns>True when the clipboard holds it.</returns>
    internal Task<bool> CopyPath(SkillOutput output, CancellationToken cancellationToken) => throw new NotSupportedException();
}
#pragma warning restore CA1822, MA0041, MA0182
