using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>
/// The run canvas's inspector (F3 spec 3 §2 and §5, slice 4) for the selected step or card: its state, the step timeline from the served
/// Step runs, why it matters, what it needs and made (the stage's facts until the engine serves them per step), how it ended, what you
/// can do next and the technical details. Read-only: it navigates (Take over, See its context) and shows the protected approve as the
/// terminal copy; it sends nothing.
/// </summary>
internal sealed partial class RunCanvasInspectorViewModel
{
    private readonly RunInspectorPorts _ports;
    private readonly InstanceDetail _line;
    private readonly InstanceDetail? _owner;

    internal RunCanvasInspectorViewModel(RunCardViewModel card, RunStepViewModel? step, InstanceDetail line, InstanceDetail? owner, RunInspectorPorts ports, IClipboard clipboard, TimeProvider clock)
    {
        (_ports, _line, _owner) = (ports, line, owner);
        var shown = Describe(card, step);
        (State, Title, Place, Word, Icon, TextBrush, Kind) = (shown.State, shown.Title, shown.Place, shown.Word, shown.Icon, shown.TextBrush, shown.Kind);
        SoftBrush = RunStates.Brushes(State).Soft;
        var stepId = step?.Key[(card.Id.Length + 1)..];
        Timeline = TimelineOf(owner, stepId, clock);
        TimelineNote = NoteOf(Timeline, stepId, State);
        Needs = Facts(owner, FactDirection.Input);
        Produced = Facts(owner, FactDirection.Output);
        EndNote = EndNoteOf(Timeline, State);
        var current = IsCurrent(owner, stepId);
        Protected = current ? ProtectedOf(owner, clipboard, clock) : null;
        RawOutput = current ? owner?.Current.Status.Text ?? string.Empty : string.Empty;
        Technical = TechnicalOf(Timeline, owner);
    }

    /// <summary>Where the step or card is.</summary>
    public RunNodeState State { get; }

    /// <summary>The step's or card's name.</summary>
    public string Title { get; }

    /// <summary>"Plan stage, step 1 of 3", or the card's own line.</summary>
    public string Place { get; }

    /// <summary>The state's word.</summary>
    public string Word { get; }

    /// <summary>The state's mark (an Icons.axaml name).</summary>
    public string Icon { get; }

    /// <summary>The brush key of the glyph and the word.</summary>
    public string TextBrush { get; }

    /// <summary>The brush key of the badge's fill.</summary>
    public string SoftBrush { get; }

    /// <summary>Who does the step, as far as it is served; empty when it is not.</summary>
    public string Kind { get; }

    /// <summary>True while the step's mark spins.</summary>
    public bool IsRunning => State == RunNodeState.Running;

    /// <summary>The tries of the step, oldest first.</summary>
    public IReadOnlyList<RunTimelineRow> Timeline { get; }

    /// <summary>The line shown instead of rows: the step has not started, or it has no run record; empty when there are rows (or a card is selected).</summary>
    public string TimelineNote { get; }

    /// <summary>True when the timeline block is drawn (a step is selected).</summary>
    public bool HasTimeline => Timeline.Count > 0 || TimelineNote.Length > 0;

    /// <summary>What the step needs, from the stage's facts.</summary>
    public IReadOnlyList<RunFactItem> Needs { get; }

    /// <summary>What the step produced, from the stage's facts.</summary>
    public IReadOnlyList<RunFactItem> Produced { get; }

    /// <summary>The heading of the how-it-ended block.</summary>
    public string EndHeading => State is RunNodeState.Cancelled ? Labels.RunHowItEnded : Labels.RunWentWrong;

    /// <summary>The how-it-ended block's line: the check's sentence for a refused run, else that nothing more is recorded; empty when the step did not end badly.</summary>
    public string EndNote { get; }

    /// <summary>True when the how-it-ended block is drawn.</summary>
    public bool HasEnd => EndNote.Length > 0;

    /// <summary>The approve of a step that waits for you, as the lock button with the terminal copy; null for a step that is not the current one or has no protected answer.</summary>
    public ProtectedActionViewModel? Protected { get; }

    /// <summary>The step's raw status text, while it is the current step; empty otherwise.</summary>
    public string RawOutput { get; }

    /// <summary>True when the raw output disclosure is drawn.</summary>
    public bool HasRawOutput => RawOutput.Length > 0;

    /// <summary>The raw facts of each try, then the owning instance's address.</summary>
    public IReadOnlyList<string> Technical { get; }

    /// <summary>True when the technical details disclosure is drawn.</summary>
    public bool HasTechnical => Technical.Count > 0;

    private static Shown Describe(RunCardViewModel card, RunStepViewModel? step) =>
        step is null
            ? new Shown(card.State, card.Title, card.Sub, card.Word, card.Icon, card.TextBrush, string.Empty)
            : new Shown(step.State, step.Name, Labels.StepOf(card.Title, step.Number, card.Steps.Count), step.Word, step.Icon, step.TextBrush, step.Kind);

    private static IReadOnlyList<RunTimelineRow> TimelineOf(InstanceDetail? owner, string? stepId, TimeProvider clock) =>
        stepId is null ? [] : RunTimeline.Of(owner?.StepRuns, stepId, clock.GetUtcNow());

    private static bool IsCurrent(InstanceDetail? owner, string? stepId) =>
        stepId is not null && owner is not null && string.Equals(owner.Current.State, stepId, StringComparison.Ordinal);

    // A step with no rows says why: it has not been reached, or its runs were not recorded.
    private static string NoteOf(IReadOnlyList<RunTimelineRow> timeline, string? stepId, RunNodeState state)
    {
        if (timeline.Count > 0 || stepId is null)
        {
            return string.Empty;
        }

        return state == RunNodeState.NotStarted ? Labels.RunNotStartedYet : Labels.RunNoTimes;
    }

    private static string EndNoteOf(IReadOnlyList<RunTimelineRow> timeline, RunNodeState state)
    {
        if (!RunStates.EndedBadly(state))
        {
            return string.Empty;
        }

        var note = timeline.Count > 0 ? timeline[^1].Note : string.Empty;
        return note.Length > 0 ? note : Labels.RunNoDetail;
    }

    // A protected answer is shown, never sent: the lock button holds the served CLI.
    private static ProtectedActionViewModel? ProtectedOf(InstanceDetail? owner, IClipboard clipboard, TimeProvider clock) =>
        owner?.Current.Status.Answer is { } answer && string.Equals(answer.Class, "protected", StringComparison.Ordinal)
            ? new ProtectedActionViewModel(new ProtectedAction(Labels.AnswerVerb(answer.Kind), answer.Cli, owner.Address), clipboard, clock)
            : null;

    private static List<string> TechnicalOf(IReadOnlyList<RunTimelineRow> timeline, InstanceDetail? owner)
    {
        var lines = timeline.Select(r => r.Raw).ToList();
        if (owner is not null)
        {
            lines.Add(owner.Address);
        }

        return lines;
    }

    private static List<RunFactItem> Facts(InstanceDetail? owner, FactDirection direction) =>
        [.. (owner?.Facts ?? []).Where(f => f.Direction == direction).Select(Item)];

    private static RunFactItem Item(FactRow fact) => fact.Status switch
    {
        FactStatus.Produced => new RunFactItem(fact.Label, fact.Summary, Labels.FactReady, "Brush.Success"),
        FactStatus.InProgress => new RunFactItem(fact.Label, fact.Summary, Labels.FactInProgress, "Brush.PrimaryHover"),
        FactStatus.Pending => new RunFactItem(fact.Label, fact.Summary, fact.Direction == FactDirection.Output ? Labels.FactNotProduced : Labels.FactNotYet, "Brush.InkSubtle"),
        _ => new RunFactItem(fact.Label, fact.Summary, Labels.FactNotYet, "Brush.InkSubtle"),
    };

    private sealed record Shown(RunNodeState State, string Title, string Place, string Word, string Icon, string TextBrush, string Kind);
}
