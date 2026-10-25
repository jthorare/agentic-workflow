namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The action bar under the inbox's request card (F3 needs-you spec §3, §5). It follows the card's row and decides, per kind of
/// request, what the primary action and send back are: a desk request is reported once through <see cref="DeskReporter"/>, a decision
/// goes to the shell as its option, and one of the three approvals is one plain click through <see cref="NeedsYouConfirm"/>. A, D and Ctrl+Enter reach the same methods the buttons do. This file holds the state and what the bar
/// shows for a request; the words are in NeedsYouBarViewModel.Look.cs and the presses in NeedsYouBarViewModel.Actions.cs.
/// </summary>
internal sealed partial class NeedsYouBarViewModel : ObservableObject
{
    private const string InstanceKind = "instance";
    private const string CompleteStepAction = "CompleteStep";

    private static readonly string[] _all =
    [
        nameof(Mode), nameof(IsPending), nameof(IsSendingBack), nameof(IsConfirmingDrop), nameof(IsApproved), nameof(ShowsActions), nameof(ApproveWord), nameof(ApproveName), nameof(IsAsking), nameof(ShowsPrimary), nameof(PrimaryEnabled),
        nameof(ShowsSendBack), nameof(SendBackEnabled), nameof(SendBackWord), nameof(SendBackName), nameof(SendBackTooltip), nameof(ShowsEditChip),
        nameof(ShowsDrop), nameof(ShowsRowControls), nameof(Hint), nameof(HasHint), nameof(HintIsAttention), nameof(Refusal), nameof(HasRefusal), nameof(ReasonRequired), nameof(ReasonPlaceholder), nameof(CanSend),
        nameof(SendWord), nameof(IsSending), nameof(DoneTitle), nameof(DoneBody), nameof(HasBar), nameof(MoreName), nameof(BarName), nameof(ConfirmEnabled),
        nameof(MergeCard), nameof(HasMergeCard),
        nameof(DeskWhy), nameof(IfApprove), nameof(IfSendBack), nameof(HasIfApprove), nameof(HasIfSendBack),
        nameof(ApproveSuggested), nameof(SendBackSuggested), nameof(Questions), nameof(HasQuestions), nameof(AllAnswered), nameof(QuestionsHint),
        nameof(Scoping), nameof(HasScoping), nameof(ScopingReady), nameof(ScopingHint), nameof(ShowsDoneAsking), nameof(DoneAskingEnabled),
        nameof(HasVerbs), nameof(HasMergeVerb), nameof(VerbsEnabled), nameof(VerbNote),
    ];

    private readonly DeskReporter _desk;
    private readonly TimeProvider _clock;
    private readonly NeedsYouConfirm? _confirm;
    private NeedsYouRowViewModel? _row;
    private DeskTarget? _target;
    private bool _targetKnown;
    private NeedsYouBarOutcome _done;

    /// <param name="desk">Reports an open desk run.</param>
    /// <param name="clock">Stamps the scoping interview's answers.</param>
    /// <param name="confirm">The three approvals the app does with one click; null offers none.</param>
    internal NeedsYouBarViewModel(DeskReporter desk, TimeProvider clock, NeedsYouConfirm? confirm = null)
    {
        (_desk, _clock, _confirm) = (desk, clock, confirm);
        _confirm?.Changed += OnConfirmChanged;
    }

    /// <summary>Raised when the bar asks to answer a decision: the shell sends it as the decision sheet would.</summary>
    internal event EventHandler<NeedsYouDecisionEventArgs>? DecisionRequested;

    /// <summary>Raised when the reason box opened: focus goes into it.</summary>
    internal event EventHandler? ReasonRequested;

    /// <summary>What the bar shows.</summary>
    public NeedsYouBarMode Mode { get; private set; }

    /// <summary>Completes when the desk run of the shown row has been read.</summary>
    internal Task Loaded { get; private set; } = Task.CompletedTask;

    /// <summary>True once the card has been scrolled to its end (a card shorter than its viewport counts as read at once). Reading to the end ends the question "Approve?".</summary>
    internal bool ReadToEnd
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            if (value && Mode == NeedsYouBarMode.Confirming)
            {
                Mode = NeedsYouBarMode.Pending;
            }

            Refresh();
        }
    }

    /// <summary>False while the request has left the list under the card: the buttons go quiet.</summary>
    internal bool Enabled
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                Refresh();
            }
        }
    } = true;

    /// <summary>The reason typed for a send back or a decline.</summary>
    public string ReasonText
    {
        get;
        set
        {
            Set(ref field, value);
            OnChanged(nameof(CanSend));
        }
    } = "";

    /// <summary>True while a report is in flight.</summary>
    public bool IsSending { get; private set; }

    /// <summary>The served refusal of the last report, as served; empty for none.</summary>
    public string Refusal { get; private set; } = "";

    private enum BarKind
    {
        None,
        Desk,
        Decision,
        Settings,
        Run,
    }

    private string Subject => _row?.Title ?? "";

    private BarKind Kind => _row is null ? BarKind.None : NeedsYouGrouping.KindOf(_row.Entry) switch
    {
        NeedsYouGroupKind.Approvals => BarKind.Desk,
        NeedsYouGroupKind.Decisions => BarKind.Decision,
        NeedsYouGroupKind.Settings => BarKind.Settings,
        NeedsYouGroupKind.Runs => BarKind.Run,
        NeedsYouGroupKind.Failures or NeedsYouGroupKind.Other => NeedsYouConfirm.IsApproval(_row.Entry) ? BarKind.Settings : BarKind.None,
        _ => throw new InvalidOperationException("a group the app doesn't know"),
    };

    // A desk request is reported by the engine's CompleteStep; the row must serve it ready for the bar to offer it.
    private NeedsYouActionControl? DeskControl => _row?.Controls.FirstOrDefault(c => string.Equals(c.Action.CatalogId, CompleteStepAction, StringComparison.Ordinal) && c.Action.IsReady && c.IsEnabled);

    // A desk step whose complete command is protected (and that has no merge verb): a send back is the step's own "rework" outcome.
    private bool TerminalOnly => Kind == BarKind.Desk && _target?.IsTerminalOnly == true && !HasMergeVerb;

    private NeedsYouActionControl? AcceptControl => _row?.Controls.FirstOrDefault(c => c is { IsPrimary: true, Action.OptionId.Length: > 0 });

    private NeedsYouActionControl? RejectControl => _row?.Controls.FirstOrDefault(c => c.Action is { RejectLike: true, OptionId.Length: > 0 });


    /// <summary>True while the bar asks something Esc withdraws: the reason box, the drop question or "Approve?".</summary>
    internal bool HasOpenQuestion => Mode is NeedsYouBarMode.SendingBack or NeedsYouBarMode.ConfirmingDrop or NeedsYouBarMode.Confirming;

    /// <summary>Shows a request; another one starts fresh (the box, the question and the refusal are dropped), the same one is updated in place.</summary>
    /// <param name="row">The request the card shows; null for none.</param>
    internal void Show(NeedsYouRowViewModel? row)
    {
        if (row is not null && _row is not null && string.Equals(row.Id, _row.Id, StringComparison.Ordinal))
        {
            _row = row;
            Refresh();
            return;
        }

        _row = row;
        (_target, _targetKnown, _done, Questions, Scoping) = (null, false, NeedsYouBarOutcome.None, [], []);
        DrawVerbs();
        DrawReviews();
        IsSending = false;
        (Mode, ReasonText, Refusal, ReadToEnd) = (NeedsYouBarMode.Pending, "", "", false);
        var read = row is not null && Kind == BarKind.Desk && InstanceOf(row) is { } instance ? Read(instance, row.Id) : Task.CompletedTask;
        Loaded = Task.WhenAll(read, _confirm?.Show(row) ?? Task.CompletedTask);
        Refresh();
    }

    private static string? InstanceOf(NeedsYouRowViewModel row) =>
        row.Entry.Subject is { } subject && string.Equals(subject.Kind, InstanceKind, StringComparison.Ordinal) ? subject.Id : null;

    private async Task Read(string instance, string rowId)
    {
        var target = await _desk.Load(instance).ConfigureAwait(true);
        if (_row is not null && string.Equals(_row.Id, rowId, StringComparison.Ordinal))
        {
            (_target, _targetKnown) = (target, true);
            Questions = [.. (target?.Questions ?? []).Select(q => new DeskQuestionViewModel(q, Refresh))];
            Scoping = [.. (target?.Scoping ?? []).Select(q => new ScopingQuestionViewModel(q, Refresh))];
            DrawVerbs();
            DrawReviews();
            Refresh();
        }
    }

    private void Refresh()
    {
        foreach (var name in _all)
        {
            OnChanged(name);
        }

        foreach (var verb in Verbs)
        {
            verb.Changed();
        }
    }
}
