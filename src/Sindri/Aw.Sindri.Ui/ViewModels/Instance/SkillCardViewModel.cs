using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>
/// The card the expanded state shows for a skill block's state (skill flows spec §5.2, §5.4): the block's sentence, a state word (never colour alone),
/// the question or the stopped text, the outcome in words, the outputs as paths, the loop round and the cost. Every line is a served field in words.
/// </summary>
internal sealed class SkillCardViewModel : ObservableObject
{
    /// <param name="input">What the card is drawn from.</param>
    /// <param name="announced">The headline an earlier card of this state already said; this card keeps quiet about the same words.</param>
    internal SkillCardViewModel(SkillCardInput input, string announced = "")
    {
        Input = input;
        Voice = new LiveAnnouncer(Labels.SkillCardResting);
        Announced = Headline.Length > 0 && string.Equals(Headline, announced, StringComparison.Ordinal) ? announced : string.Empty;
    }

    /// <summary>The headline this card has said, or empty.</summary>
    internal string Announced { get; private set; }

    /// <summary>Says the waiting row, once per question. The view calls it after the card is in the tree, so the live region changes text while a reader is watching it; a second call with the same words says nothing.</summary>
    internal void Announce()
    {
        var headline = Headline;
        if (headline.Length == 0 || string.Equals(headline, Announced, StringComparison.Ordinal))
        {
            return;
        }

        Announced = headline;
        Voice.Say(headline);
    }

    /// <summary>What the card was drawn from.</summary>
    internal SkillCardInput Input { get; private set; }

    /// <summary>True when the step is a skill block's (`step.skill.*`).</summary>
    /// <param name="step">The state's step, or null.</param>
    /// <returns>True for a skill block.</returns>
    internal static bool IsSkillStep(StepDeclaration? step) => step is not null && step.Ref.StartsWith("step.skill.", StringComparison.Ordinal);

    /// <summary>True when the state runs a skill block (the cell names a `step.skill.*` block; an agent step names its own block too).</summary>
    /// <param name="cell">The state's cell.</param>
    /// <returns>True for a skill state.</returns>
    internal static bool IsSkillState(StateCell cell) => cell.Block is { } block && block.StartsWith("step.skill.", StringComparison.Ordinal);

    private SkillBlockView? Block => Input.Skills.Blocks.FirstOrDefault(b => string.Equals(b.Descriptor, Input.Cell.Block, StringComparison.Ordinal) || string.Equals(b.Id, Input.Cell.Block, StringComparison.Ordinal));

    // The latest skillWaiting row of this instance and state, for the run being drawn. A run that ended has no waiting row (the safety net: the live row goes with the run).
    private OwnerWaitingView? Waiting
    {
        get
        {
            if (Input.Run is { } ended && !string.Equals(ended.State, "running", StringComparison.Ordinal))
            {
                return null;
            }

            return Input.NeedsYou
                .Where(r => string.Equals(r.Kind, "skillWaiting", StringComparison.Ordinal))
                .Select(r => r.OwnerWaiting)
                .OfType<OwnerWaitingView>()
                .Where(IsOfThisRun)
                .Select((w, index) => (Row: w, Index: index))
                .OrderBy(x => x.Row.Attempt is { } attempt && attempt == Input.Run?.Attempt ? 1 : 0)   // several rows can match (a stale attempt next to the live one): the latest run's own first,
                .ThenBy(x => x.Row.Attempt ?? -1)   // else the highest attempt,
                .ThenBy(x => x.Index)   // else the latest in the feed
                .Select(x => x.Row)
                .LastOrDefault();
        }
    }

    // The row names this instance and state, and the run being drawn when both sides have an attempt.
    private bool IsOfThisRun(OwnerWaitingView w) =>
        string.Equals(w.Instance, Input.Address, StringComparison.Ordinal)
        && string.Equals(w.State, Input.Cell.State, StringComparison.Ordinal)
        && (w.Attempt is null || Input.Run is null || w.Attempt == Input.Run.Attempt);

    // A run's state in the card's words: running and passed are themselves, and refused, cancelled and abandoned read as failed (the run's state wins over the glyph).
    private static SkillCardState Ended(string run) =>
        string.Equals(run, "running", StringComparison.Ordinal) ? SkillCardState.Running : string.Equals(run, "passed", StringComparison.Ordinal) ? SkillCardState.Done : SkillCardState.Failed;

    /// <summary>The polite live region that says the waiting row once when it appears.</summary>
    public LiveAnnouncer Voice { get; }

    /// <summary>The block's sentence; empty when the server lists no such block.</summary>
    public string Sentence => Block?.Sentence ?? string.Empty;

    /// <summary>Which state the card is in.</summary>
    public SkillCardState State
    {
        get
        {
            if (Waiting is not null)
            {
                return SkillCardState.WaitingOnYou;
            }

            return Input.Run is { } run ? Ended(run.State) : Input.Cell.Glyph == Api.Workflow.StateGlyph.Done ? SkillCardState.Done : SkillCardState.WaitingToStart;
        }
    }

    /// <summary>The state as a word ("waiting to start", "running", "waiting on you", "done", "failed").</summary>
    public string StateWord => State switch
    {
        SkillCardState.WaitingToStart => Labels.SkillWaitingToStart,
        SkillCardState.Running => Labels.SkillRunning,
        SkillCardState.WaitingOnYou => Labels.SkillWaitingOnYou,
        SkillCardState.Done => Labels.SkillDone,
        SkillCardState.Failed => Labels.SkillFailed,
        _ => Labels.SkillFailed,
    };

    /// <summary>The line the owner reads: the question, or the stopped, broken-file and skill-updated texts; empty when nothing waits.</summary>
    public string Headline => Waiting is { } waiting ? HeadlineOf(waiting) : string.Empty;

    private string HeadlineOf(OwnerWaitingView waiting) => waiting.Reason switch
    {
        _ when Is(waiting, "noResult") => Labels.SkillNoResult(Input.Name),
        _ when Is(waiting, "unreadable") => Labels.SkillUnreadable(Input.Name),
        _ when Is(waiting, "skillUpdated") => Labels.SkillUpdated(SkillWords.Outcome(Block?.Skill ?? Input.Name)),
        _ => Labels.SkillWaitingQuestion(Input.Name, waiting.Text ?? string.Empty),
    };

    private static bool Is(OwnerWaitingView waiting, string reason) => string.Equals(waiting.Reason, reason, StringComparison.Ordinal);

    /// <summary>The skill's question, whole; empty when the block asks none.</summary>
    public string Question => Waiting is { } w && Is(w, "question") ? w.Text ?? string.Empty : string.Empty;

    /// <summary>What Details shows: the result path and, for a broken file, the parse error; empty when nothing waits.</summary>
    public string Details => Waiting is { } w && !Is(w, "noResult") ? (w.Detail is { Length: > 0 } detail ? $"{w.ResultPath}\n{detail}" : w.ResultPath) : string.Empty;

    /// <summary>The state cell's outcome in words ("needs work"); empty until it has one.</summary>
    public string Outcome => Input.Cell.Outcome is { Length: > 0 } outcome ? SkillWords.Outcome(outcome) : string.Empty;

    /// <summary>The block's outputs that hold a path.</summary>
    public IReadOnlyList<SkillOutput> Outputs => [.. (Block?.Outputs ?? []).Select(key => Input.Facts.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.Ordinal)))
        .OfType<FactRow>()
        .Select(f => new SkillOutput(f.Label, PathOf(f)))
        .Where(o => o.Path.Length > 0)];

    // The path is the ref's; a produced fact with none but a summary that is an absolute path on one line falls back to the summary (a finished skill's path output is always absolute, spec §3.3).
    private static string PathOf(FactRow fact) =>
        fact.Ref?.Path is { } path && Path.IsPathRooted(path) ? path : (fact.Status == FactStatus.Produced && Path.IsPathRooted(fact.Summary) && !fact.Summary.Contains('\n', StringComparison.Ordinal) ? fact.Summary : string.Empty);

    /// <summary>"round 2 of 2" when the state is in a loop; empty otherwise.</summary>
    public string RoundLine => Input.Cell.Round is { } round ? Labels.SkillRound(round.N, round.Max) : string.Empty;

    /// <summary>"cost: $0.42", or "cost: not reported".</summary>
    public string CostLine => Input.Cost is { } cost ? Labels.SkillCost(cost) : Labels.SkillCostNotReported;

#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>The heading of the closed details.</summary>
    public string DetailsHeading => Labels.SkillDetails;

    /// <summary>The heading of the outputs.</summary>
    public string OutputsHeading => Labels.SkillOutputsHeading;

    /// <summary>An output's Open.</summary>
    public string OpenText => Labels.SkillOpen;

    /// <summary>An output's Copy path.</summary>
    public string CopyPathText => Labels.SkillCopyPath;
#pragma warning restore CA1822, MA0041

    /// <summary>True when there is a line to read (the question or a stopped text).</summary>
    public bool HasHeadline => Headline.Length > 0;

    /// <summary>True when Details has anything.</summary>
    public bool HasDetails => Details.Length > 0;

    /// <summary>True when the Step reported an outcome.</summary>
    public bool HasOutcome => Outcome.Length > 0;

    /// <summary>True when the block has outputs to list.</summary>
    public bool HasOutputs => Outputs.Count > 0;

    /// <summary>True when the state is in a loop.</summary>
    public bool HasRound => RoundLine.Length > 0;

    /// <summary>Takes a re-read with the same headline: the lines change in place and the card stays.</summary>
    /// <param name="input">What the card is drawn from now.</param>
    internal void Update(SkillCardInput input)
    {
        Input = input;
        if (Headline.Length == 0 && Announced.Length > 0)
        {
            Announced = string.Empty;
            Voice.Quiet();   // the question cleared: the next one is announced again
        }

        OnChanged(string.Empty);
    }

    /// <summary>Opens an output with the system's app for it (a folder opens in the file manager).</summary>
    /// <param name="output">The output.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>True when the system opened it.</returns>
    internal Task<bool> Open(SkillOutput output, CancellationToken cancellationToken) => new LinkLaunch(Input.Launcher).OpenPath(output.Path, cancellationToken);

    /// <summary>Puts an output's path on the clipboard.</summary>
    /// <param name="output">The output.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns>True when the clipboard holds it.</returns>
    internal Task<bool> CopyPath(SkillOutput output, CancellationToken cancellationToken) => Input.Clipboard.SetText(output.Path, cancellationToken);
}
