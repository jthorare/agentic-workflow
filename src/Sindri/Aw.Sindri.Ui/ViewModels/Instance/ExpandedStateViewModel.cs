using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Components;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>
/// The state the instance is in, expanded: the engine's status layout (WF §6.4) as controls (screens §4.2). Every line is a
/// served field mapped to words, and the next sentence is shown whole. Nothing is sent from here yet (U8b): each command
/// shows its CLI with a Copy, a protected one is the lock button, and Cancel asks first with Keep focused.
/// </summary>
internal sealed partial class ExpandedStateViewModel : ObservableObject
{
    private CurrentState _current;
    private readonly string _address;
    private readonly ExpandedCommandViewModel? _cancel;

    internal ExpandedStateViewModel(CurrentState current, string address, IClipboard clipboard, TimeProvider clock)
    {
        (_current, _address) = (current, address);
        var status = current.Status;
        Commands = [.. status.Commands.Select(c => new ExpandedCommandViewModel(c, status.Answer?.Kind, address, clipboard, clock))];
        _cancel = Commands.FirstOrDefault(c => c.Verb == StatusVerb.Cancel);
        Gates = [.. current.Gates.Select(Labels.GateText)];
        StepLine = Line(current.Step, Labels.StepLine);
        (BudgetLine, TimeoutLine, PausedLine, DriftLine, Next) = Moving(current);
        if (status.Child is { } child)
        {
            Child = new ChildPointerViewModel(child);
            Child.Opened += (_, e) => ChildRequested?.Invoke(this, e);
        }

        if (InstanceRules.IsChildPointer(status.Answer))
        {
            AnswerChild = status.Answer;
            AnswerInChildName = ServedChild(status) is { } childAddress ? Labels.AnswerInChildName(childAddress) : Labels.AnswerInChildPanelName;
        }

        AttachCommandCopy = new CopyAction(clipboard, clock, Labels.CopyAttachName(address));
        CopyAttachName = Labels.CopyAttachName(address);
        InitRun(current, address);
        Name = Labels.ExpandedName(current.State, address);
        CopyStatusName = Labels.CopyStatusName(address);
        StatusCopy = new CopyAction(clipboard, clock, Labels.CopyStatus, Labels.CopyStatus);
    }

    // The lines that move with time or with the engine's counters: a re-read changes their words in place and keeps the controls.
    private static (string Budget, string Timeout, string Paused, string Drift, string Next) Moving(CurrentState current)
    {
        var status = current.Status;
        return (Line(status.Budget, Labels.BudgetLine), Line(status.Timeout, Labels.TimeoutLine), Line(status.Paused, p => Labels.PausedLine(p.TimeoutLeftSeconds)), Line(status.Drift, Labels.DriftLine), status.Next ?? string.Empty);
    }

    private static string Line<T>(T? served, Func<T, string> words)
        where T : class => served is null ? string.Empty : words(served);

    /// <summary>Raised when the child pointer is followed.</summary>
    internal event EventHandler<FrameOpenEventArgs>? ChildRequested;

    /// <summary>The state.</summary>
    public string State => _current.State;

    /// <summary>The step line ("kind · ref · declaration"), or empty.</summary>
    public string StepLine { get; }

    /// <summary>True when the state has a step.</summary>
    public bool HasStep => StepLine.Length > 0;

#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    /// <summary>The heading of the stage's closed details.</summary>
    public string DetailsHeading => Labels.StageDetailsHeading;
#pragma warning restore CA1822, MA0041

    /// <summary>True when the stage has anything for its details: a step, gates, budget, timeout or an agent session.</summary>
    public bool HasStageDetails => HasStep || HasGates || HasBudget || HasTimeout || HasRun;

    /// <summary>The exit gates, in served order.</summary>
    public IReadOnlyList<string> Gates { get; }

    /// <summary>True when there are gates.</summary>
    public bool HasGates => Gates.Count > 0;

    /// <summary>The served next sentence, whole; empty when none.</summary>
    public string Next { get; private set => Set(ref field, value); }

    /// <summary>True when a next sentence is served.</summary>
    public bool HasNext => Next.Length > 0;

    /// <summary>The budget line, or empty.</summary>
    public string BudgetLine { get; private set => Set(ref field, value); }

    /// <summary>True when a budget is served.</summary>
    public bool HasBudget => BudgetLine.Length > 0;

    /// <summary>The timeout line, or empty.</summary>
    public string TimeoutLine { get; private set => Set(ref field, value); }

    /// <summary>True when a timeout is served.</summary>
    public bool HasTimeout => TimeoutLine.Length > 0;

    /// <summary>The paused line, or empty.</summary>
    public string PausedLine { get; private set => Set(ref field, value); }

    /// <summary>True while the instance is taken over.</summary>
    public bool IsPaused => PausedLine.Length > 0;

    /// <summary>The drift line, or empty.</summary>
    public string DriftLine { get; private set => Set(ref field, value); }

    /// <summary>True when the recipe drifted.</summary>
    public bool HasDrift => DriftLine.Length > 0;

    /// <summary>The pointer to the child the instance waits on, or null.</summary>
    public ChildPointerViewModel? Child { get; }

    /// <summary>True when the answer is a pointer to a child that decides it: the state shows "Answer in the child" and no answer form.</summary>
    public bool AnswersInChild => AnswerChild is not null;

    private StatusAnswer? AnswerChild { get; }

#pragma warning disable CA1822, MA0041 // a fixed sentence is a property because the view binds it
    /// <summary>The "Answer in the child" words.</summary>
    public string AnswerInChildText => Labels.AnswerInChild;
#pragma warning restore CA1822, MA0041

    /// <summary>The control's accessible name: the drawn words, then the instance.</summary>
    public string AnswerInChildName { get; } = string.Empty;

    /// <summary>Follows "Answer in the child": the child's frame when the served pointer is the answer's child, else focus moves to the Children panel of this frame.</summary>
    public void AnswerInTheChild()
    {
        if (AnswerChild is null)
        {
            return;
        }

        if (Child is not null && ServedChild(_current.Status) is not null)
        {
            Child.Open();
        }
        else
        {
            ChildrenRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    // The served child pointer's address when it is the answer's child: "Answer in the child" opens that child, so its name says so; null when the Children panel is the target.
    private static string? ServedChild(StatusFields status) =>
        status.Child is { } child && string.Equals(child.Address, status.Answer?.Child, StringComparison.Ordinal) ? child.Address : null;

    /// <summary>Raised when "Answer in the child" has no served child to open: this frame's Children panel is where the answer goes.</summary>
    internal event EventHandler? ChildrenRequested;

    /// <summary>The commands in served order.</summary>
    public IReadOnlyList<ExpandedCommandViewModel> Commands { get; }

    /// <summary>True when there are commands.</summary>
    public bool HasCommands => Commands.Count > 0;

    /// <summary>True when one of the commands is an answer.</summary>
    public bool HasAnswer => Commands.Any(c => c.Verb == StatusVerb.Answer);

    /// <summary>True while the Cancel confirm shows.</summary>
    public bool IsConfirmingCancel { get; private set => Set(ref field, value); }

    /// <summary>The confirm line: the served sentence, else "Cancel &lt;address&gt;?".</summary>
    public string ConfirmLine => _cancel?.Confirm is { Length: > 0 } served ? served : Labels.CancelAsk(_address);

    /// <summary>Where focus lands in the confirm: Keep, the safe answer.</summary>
#pragma warning disable CA1822, MA0041 // a fixed answer is a property because the view reads it
    public ConfirmChoice ConfirmFocus => ConfirmChoice.Keep;

    /// <summary>The Keep button's words.</summary>
    public string KeepLabel => Labels.KeepStep;

    /// <summary>The go-ahead button's words.</summary>
    public string ConfirmCancelLabel => Labels.ConfirmCancel;
#pragma warning restore CA1822, MA0041

    /// <summary>The confirm's accessible name for going ahead.</summary>
    public string ConfirmCancelName => Labels.ConfirmCancelName(_address);

    /// <summary>The confirm's accessible name for keeping.</summary>
    public string KeepName => Labels.KeepName(_address);

    /// <summary>What the frame says after a confirmed cancel, which the app does not send yet; empty otherwise.</summary>
    public string PendingNote { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The live region that says the pending note: flag first, text second, never empty.</summary>
    public LiveAnnouncer PendingVoice { get; } = new(Labels.PendingNotesResting);

    /// <summary>The Copy status control: its words and the live region that says "Copied".</summary>
    public CopyAction StatusCopy { get; }

    /// <summary>The Copy status button's accessible name.</summary>
    public string CopyStatusName { get; }

    /// <summary>The expanded state's accessible name.</summary>
    public string Name { get; }

    /// <summary>
    /// What the state draws as controls and structure, so a re-read of the same keeps them (and the focus, the open confirm and a Copied label).
    /// The words that move with time (the status text, the timeout, the budget, the next sentence, the child pointer's state) are not: <see cref="Update"/> changes them in place.
    /// </summary>
    internal string Signature => string.Join('|', _current.State, StepLine, string.Join(',', Gates), string.Join(',', Commands.Select(c => c.Signature)), _current.Status.Child?.Id, AnswerChild?.Child, AnswersInChild, HasNext, HasBudget, HasTimeout, IsPaused, HasDrift, HasAttach, _current.Status.Run?.Launch);

    /// <summary>Takes a re-read of the same state: the moving words change, the controls stay.</summary>
    /// <param name="current">The state as served now; its <see cref="Signature"/> equals this one's.</param>
    internal void Update(CurrentState current)
    {
        _current = current;
        (BudgetLine, TimeoutLine, PausedLine, DriftLine, Next) = Moving(current);
        if (current.Status.Child is { } child)
        {
            Child?.Update(child);
        }
    }

    /// <summary>Asks to cancel: shows the confirm. Without a Cancel command there is nothing to ask.</summary>
    public void AskCancel()
    {
        if (_cancel is not null)
        {
            PendingVoice.Quiet();
            PendingNote = string.Empty;
            IsConfirmingCancel = true;
        }
    }

    /// <summary>Keeps the instance: closes the confirm.</summary>
    public void Keep() => IsConfirmingCancel = false;

    /// <summary>Goes ahead with the cancel the confirm asked about: sends it when the frame can (see <see cref="ConfirmCancelAsync"/>), else says the send arrives later.</summary>
    public void ConfirmCancel() => _ = ConfirmCancelAsync();

    /// <summary>Copies the engine's status text, whole.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the clipboard answered.</returns>
    public Task CopyStatus(CancellationToken cancellationToken) => StatusCopy.Run(_current.Status.Text, cancellationToken);
}
