using Aw.Sindri.Ui.Api.Setup;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>
/// One step of the checklist. It is kept by its id across reads and updated in place, so a control with focus keeps it. It maps served fields to words and decides nothing:
/// the ids and codes it holds are for the closed Details, never the words up front. A step opens by itself when it needs the person or failed; the person's own choice to open or
/// close a row is kept across reads.
/// </summary>
internal sealed class SetupStepViewModel : ObservableObject
{
    private static readonly string[] _all =
    [
        nameof(Title), nameof(NumberText), nameof(Chip), nameof(ChipWord), nameof(Sub), nameof(DetailText), nameof(ProgressText), nameof(ProgressFraction), nameof(ErrorText), nameof(Items), nameof(Action), nameof(Fields),
        nameof(IsMeterFailed), nameof(HintText), nameof(HasHint), nameof(Agents), nameof(HasAgents), nameof(TrustOnceText), nameof(AgentsNoteText),
        nameof(Name), nameof(Note), nameof(HasNote), nameof(HasDetail), nameof(HasProgress), nameof(HasError), nameof(HasItems), nameof(HasAction),
    ];

    private readonly Func<SetupStepView, SetupActionViewModel?, SetupActionViewModel?> _action;
    private string? _overrideNote;

    internal SetupStepViewModel(SetupStepView step, int number, Func<SetupStepView, SetupActionViewModel?, SetupActionViewModel?> action)
    {
        (Step, Number, _action) = (step, number, action);
        Derive();
        IsOpen = NeedsYou || IsFailed;
    }

    /// <summary>The step as last served.</summary>
    internal SetupStepView Step { get; private set; }

    /// <summary>The step's id (repo, profile, recipes, mcp, map, onyx).</summary>
    public string Id => Step.Id;

    /// <summary>The step's place in the checklist, from 1.</summary>
    public int Number { get; }

    /// <summary>What the number circle shows: the number, or a tick once the step is done or skipped.</summary>
    public string NumberText => IsFinished ? "✓" : Number.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>What the step is, in a few words (the app's own title).</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>The state chip: one of five words with its tone, or a state this build doesn't know as itself on the To do chip.</summary>
    public StateChip Chip { get; private set; } = new(string.Empty, ChipTone.Neutral);

    /// <summary>The state in words: To do, Working, Done, Failed or Needs you (Skipped for a step that was skipped).</summary>
    public string ChipWord { get; private set; } = string.Empty;

    /// <summary>The line under the title: why the step is waiting or what it does, or what it did once done.</summary>
    public string Sub { get; private set; } = string.Empty;

    /// <summary>What it did or is doing, in the API's words; empty when nothing.</summary>
    public string DetailText { get; private set; } = string.Empty;

    /// <summary>"40 of 100"; empty for an uncounted step.</summary>
    public string ProgressText { get; private set; } = string.Empty;

    /// <summary>How far a counted step is, 0 to 1.</summary>
    public double ProgressFraction { get; private set; }

    /// <summary>Why it failed, in the API's sentence; empty otherwise (and while a retry runs).</summary>
    public string ErrorText { get; private set; } = string.Empty;

    /// <summary>One line per repo.</summary>
    public IReadOnlyList<SetupItemViewModel> Items { get; private set; } = [];

    /// <summary>The action, or null.</summary>
    public SetupActionViewModel? Action { get; private set; }

    /// <summary>The repo form of the repo step; null for every other step.</summary>
    public SetupRepoFormViewModel? Repo { get; set; }

    /// <summary>The typed approval of the profile step; null for every other step.</summary>
    public SetupProfileConfirmViewModel? Profile { get; set; }

    /// <summary>The plan, hold and typed alternative of the workflows step (the approve-all dialog's own view model); null for every other step.</summary>
    public ViewModels.Approvals.ApproveAllViewModel? Workflows { get; set; }

    /// <summary>The Onyx controls of the Onyx step; null for every other step.</summary>
    public SetupOnyxViewModel? Onyx { get; set; }

    /// <summary>One line per coding agent for the agents step; empty for every other step.</summary>
    public IReadOnlyList<SetupAgentViewModel> Agents { get; private set; } = [];

    /// <summary>True when the meter of a failed step shows in the error colour.</summary>
    public bool IsMeterFailed => IsFailed && HasProgress;

    /// <summary>A line under the error: what Sindri does on its own; empty when nothing.</summary>
    public string HintText { get; private set; } = string.Empty;

    /// <summary>True when there is a hint.</summary>
    public bool HasHint => HintText.Length > 0;

    /// <summary>The fixed line under the agents list about trusting the folder.</summary>
    public string TrustOnceText => HasAgents ? Labels.SetupTrustOnce : string.Empty;

    /// <summary>The fixed line under the agents list about scope.</summary>
    public string AgentsNoteText => HasAgents ? Labels.SetupAgentsNote : string.Empty;

    /// <summary>True when the agents list shows.</summary>
    public bool HasAgents => Agents.Count > 0;

    /// <summary>The ids and codes, for the closed Details.</summary>
    public IReadOnlyList<SetupFieldViewModel> Fields { get; private set; } = [];

    /// <summary>The row's accessible name: title, state and the line under it.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>What the app says about the step's own button (a retry started, or refused); empty otherwise.</summary>
    public string Note { get; private set; } = string.Empty;

    /// <summary>True while the row is open. Opening a row never closes another.</summary>
    public bool IsOpen { get; set => Set(ref field, value); }

    /// <summary>True when it has a note.</summary>
    public bool HasNote => Note.Length > 0;

    /// <summary>True when it has detail text.</summary>
    public bool HasDetail => DetailText.Length > 0;

    /// <summary>True when it counts.</summary>
    public bool HasProgress => ProgressText.Length > 0;

    /// <summary>True when it failed with a sentence.</summary>
    public bool HasError => ErrorText.Length > 0;

    /// <summary>True when it carries repo lines.</summary>
    public bool HasItems => Items.Count > 0;

    /// <summary>True when it has an action.</summary>
    public bool HasAction => Action is not null;

    /// <summary>True when the step needs the person.</summary>
    internal bool NeedsYou => string.Equals(Step.State, "needs-you", StringComparison.Ordinal);

    /// <summary>True when the step is done or skipped: setup is finished when every step is.</summary>
    internal bool IsFinished => string.Equals(Step.State, "done", StringComparison.Ordinal) || string.Equals(Step.State, "skipped", StringComparison.Ordinal);

    /// <summary>True while Sindri works on it, as served.</summary>
    internal bool IsRunning => string.Equals(Step.State, "running", StringComparison.Ordinal);

    /// <summary>True when it stopped, as served.</summary>
    internal bool IsFailed => string.Equals(Step.State, "failed", StringComparison.Ordinal);

    /// <summary>True while the row shows Working for a retry the app started, until the read that follows it.</summary>
    internal bool IsWorking => _overrideNote is not null;

    /// <summary>True when the step is done, as served.</summary>
    internal bool IsDone => string.Equals(Step.State, "done", StringComparison.Ordinal);

    /// <summary>The progress strip's kind for this step: "done" (or skipped), "working", or "todo".</summary>
    internal string SegmentKind => IsFinished ? "done" : IsRunning || _overrideNote is not null ? "working" : "todo";

    /// <summary>Takes a newer serving of the same step and redraws only what changed. A step that moves into needs-you or failed opens; nothing else is closed.</summary>
    /// <param name="step">The step as served now.</param>
    /// <param name="keepWorking">True for a read that began before a retry was sent: the row keeps showing Working.</param>
    /// <returns>True when its state changed.</returns>
    internal bool Update(SetupStepView step, bool keepWorking = false)
    {
        var changed = !string.Equals(Step.State, step.State, StringComparison.Ordinal);
        Step = step;
        if (!keepWorking)
        {
            _overrideNote = null;
        }

        Derive();
        if (changed && (NeedsYou || IsFailed))
        {
            IsOpen = true;
        }

        return changed;
    }

    /// <summary>Shows the step as working with a note, until the next read says otherwise (a retry was started, or one was already running).</summary>
    /// <param name="note">The note.</param>
    internal void ShowWorking(string note)
    {
        _overrideNote = note;
        Derive();
    }

    /// <summary>Says something about the step's button without changing the state (a retry that could not start).</summary>
    /// <param name="note">The note.</param>
    internal void Say(string note)
    {
        Note = note;
        OnChanged(nameof(Note));
        OnChanged(nameof(HasNote));
    }

    /// <summary>Asks again whether the app can do its action now (the catalog or the Needs-you list changed).</summary>
    internal void Rederive()
    {
        Action = _action(Step, Action);
        foreach (var name in (string[])[nameof(Action), nameof(HasAction)])
        {
            OnChanged(name);
        }
    }

    private void Derive()
    {
        DeriveWords();
        DeriveParts();
        foreach (var name in _all)
        {
            OnChanged(name);
        }
    }

    private void DeriveWords()
    {
        var running = _overrideNote is not null;
        var state = running ? "running" : Step.State;
        var (word, said, tone) = Labels.SetupChip(state);
        (Title, Chip, ChipWord) = (Labels.SetupTitle(Step.Id, Step.Title), new StateChip(word, tone), said);
        Sub = Labels.SetupSubFor(Step.Id, state, Waiting, Step.Why, Step.Detail);
        Note = _overrideNote ?? string.Empty;
        DetailText = IsFinished || running ? string.Empty : Step.Detail ?? string.Empty;
        ErrorText = running ? string.Empty : Step.Error?.Sentence ?? string.Empty;
        Name = Labels.SetupStepName(Title, said, Sub);
        DeriveProgress();
    }

    // A todo step with no action waits on another step.
    private bool Waiting => string.Equals(Step.State, "todo", StringComparison.Ordinal) && string.IsNullOrEmpty(Step.Action);

    private void DeriveProgress() =>
        (ProgressText, ProgressFraction) = Step.Progress is { Total: > 0 } progress
            ? (Labels.SetupProgress(progress.Done, progress.Total, progress.Unit), Math.Clamp((double)progress.Done / progress.Total, 0, 1))
            : (string.Empty, 0);

    private void DeriveParts()
    {
        Items = [.. (Step.Items ?? []).Select(i => new SetupItemViewModel(i.Repo, i.State, i.Detail))];
        Action = _overrideNote is null ? _action(Step, Action) : null;
        Fields = Details();
        HintText = IsFailed && string.Equals(Step.Id, "map", StringComparison.Ordinal) && _overrideNote is null ? Labels.SetupMapRetries : string.Empty;
        Agents = string.Equals(Step.Id, "mcp", StringComparison.Ordinal) ? AgentLines() : [];
        Repo?.Update(Step);
        Onyx?.Update(Step, _overrideNote is not null);
    }

    // Claude Code, Codex and Cursor: connected when every repo line names the agent, connecting while the step runs, else not started.
    private List<SetupAgentViewModel> AgentLines()
    {
        var items = Step.Items ?? [];
        var running = _overrideNote is not null || IsRunning;
        return
        [
            .. Labels.SetupAgentNames.Select(agent =>
            {
                var connected = items.Count > 0 && items.All(i => Names(i.Detail, agent));
                var (word, chip) = connected ? (Labels.SetupAgentConnected, Labels.SetupChip("done"))
                    : running ? (Labels.SetupAgentConnecting, Labels.SetupChip("running"))
                    : (Labels.SetupAgentNotStarted, Labels.SetupChip("todo"));
                return new SetupAgentViewModel(agent, word, new StateChip(chip.Word, chip.Tone));
            }),
        ];
    }

    private static bool Names(string? detail, string agent) =>
        detail is not null && detail.Split(',', StringSplitOptions.TrimEntries).Contains(agent, StringComparer.OrdinalIgnoreCase);

    private List<SetupFieldViewModel> Details()
    {
        List<SetupFieldViewModel> fields = [new(Labels.SetupStepField, Step.Id), new(Labels.SetupStateField, Step.State)];
        if (Step.Error is { } error)
        {
            fields.Add(new SetupFieldViewModel(Labels.JobCodeField, error.Code));
        }

        if (Step.Action is { Length: > 0 } action)
        {
            fields.Add(new SetupFieldViewModel(Labels.SetupActionField, action));
        }

        return fields;
    }
}
