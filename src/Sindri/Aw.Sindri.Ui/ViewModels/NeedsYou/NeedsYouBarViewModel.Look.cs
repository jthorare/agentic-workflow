namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>The action bar (<see cref="NeedsYouBarViewModel"/>, continued): what it shows, as the view reads it.</summary>
internal sealed partial class NeedsYouBarViewModel
{
    /// <summary>The More menu's words.</summary>
    public string MoreWord { get; } = Labels.NeedsYouMore;

    /// <summary>The More menu's accessible name.</summary>
    public string MoreName => Labels.NeedsYouMoreName(Subject);

    /// <summary>The menu item that drops the request.</summary>
    public string DropWord { get; } = Labels.NeedsYouDrop;

    /// <summary>The question before a drop is sent.</summary>
    public string DropAsk { get; } = Labels.NeedsYouDropAsk;

    /// <summary>The button that sends the drop.</summary>
    public string DropConfirmWord { get; } = Labels.NeedsYouDropConfirm;

    /// <summary>The words of the feature that is not built yet.</summary>
    public string EditWord { get; } = Labels.NeedsYouEditLater;

    /// <summary>The chip on it.</summary>
    public string NotAvailableWord { get; } = Labels.NeedsYouNotAvailableYet;

    /// <summary>The reason box's accessible name.</summary>
    public string ReasonName { get; } = Labels.NeedsYouReasonName;

    /// <summary>Closes the reason box or the drop question.</summary>
    public string CancelWord { get; } = Labels.NeedsYouCancel;

    /// <summary>What hovering the terminal button says.</summary>
    public string TerminalTip { get; } = Labels.NeedsYouTerminalTip;

    /// <summary>The reason box's caption.</summary>
    public string ReasonLabel { get; } = Labels.NeedsYouReasonLabel;

    /// <summary>The bar's accessible name.</summary>
    public string BarName => Labels.NeedsYouBarGroupName(Subject);

    /// <summary>True when the request has a bar of its own.</summary>
    public bool HasBar => Kind != BarKind.None && (ShowsPrimary || ShowsTerminal || ShowsSendBack || ShowsConfirm || HasVerbs || IsApproved);

    /// <summary>True in the resting modes (Approve and Send back show).</summary>
    public bool IsPending => Mode is NeedsYouBarMode.Pending or NeedsYouBarMode.Confirming;

    /// <summary>True while the reason box shows.</summary>
    public bool IsSendingBack => Mode == NeedsYouBarMode.SendingBack;

    /// <summary>True while the drop question shows.</summary>
    public bool IsConfirmingDrop => Mode == NeedsYouBarMode.ConfirmingDrop;

    /// <summary>True once the request was answered.</summary>
    public bool IsApproved => Mode == NeedsYouBarMode.Approved;

    /// <summary>True when the buttons show (the resting modes).</summary>
    public bool ShowsActions => IsPending;

    /// <summary>True while the primary asks first.</summary>
    public bool IsAsking => Mode == NeedsYouBarMode.Confirming;

    /// <summary>The ask step's questions, each with its answer field; empty for every other request.</summary>
    public IReadOnlyList<DeskQuestionViewModel> Questions { get; private set; } = [];

    /// <summary>True while the card asks questions: the answers are what the step needs.</summary>
    public bool HasQuestions => Kind == BarKind.Desk && Questions.Count > 0;

#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    /// <summary>The heading over the questions.</summary>
    public string QuestionsHeading => Labels.NeedsYouQuestionsHeading;
#pragma warning restore CA1822, MA0041

    /// <summary>True when every question has an answer.</summary>
    public bool AllAnswered => Questions.All(q => q.IsAnswered);

    /// <summary>The line that says every question needs an answer, while one is open; empty otherwise.</summary>
    public string QuestionsHint => HasQuestions && !AllAnswered ? Labels.NeedsYouAnswerAll : string.Empty;

    /// <summary>The primary button's words: the decision's own, "Send answers" for the ask step, else "Approve", or "Approve?" while it asks.</summary>
    public string ApproveWord => HasQuestions ? Labels.NeedsYouSendAnswers : Kind == BarKind.Decision ? AcceptControl?.Action.Label ?? Labels.NeedsYouApprove : IsAsking ? Labels.NeedsYouApproveAsk : Labels.NeedsYouApprove;

    /// <summary>The primary button's accessible name: the words and the request.</summary>
    public string ApproveName => Labels.NeedsYouBarName(ApproveWord, Subject);

    /// <summary>True when the primary button shows: a desk request whose step the person completes here, or a decision with a recommended option.</summary>
    public bool ShowsPrimary => Kind switch
    {
        BarKind.Desk => DeskControl is not null && !TerminalOnly,
        BarKind.Decision => AcceptControl is not null,
        BarKind.None or BarKind.Settings or BarKind.Run => false,
        _ => throw new InvalidOperationException("a kind the bar doesn't know"),
    };

    /// <summary>True when the primary can be pressed now.</summary>
    public bool PrimaryEnabled => ShowsPrimary && Enabled && !IsSending && (Kind != BarKind.Desk || _target is not null) && (!HasQuestions || AllAnswered) && (Kind != BarKind.Decision || AcceptControl is { IsEnabled: true });

    /// <summary>True when the terminal button shows: a merge, or a protected action the app does not confirm itself (yet); it copies the served command and sends nothing.</summary>
    public bool ShowsTerminal => !ShowsConfirm && (TerminalOnly || ConfirmKind == NeedsYouConfirmKind.Terminal || (Kind is BarKind.Settings or BarKind.Run && PrimaryControl is { IsProtected: true }));

    /// <summary>The terminal button's action words ("Allow more tries").</summary>
    public string TerminalWord => Kind == BarKind.Desk ? Labels.NeedsYouApproveMerge : PrimaryControl is { } primary ? WordsOf(primary.Action) : "";

    /// <summary>The terminal button's visible words.</summary>
    public string TerminalButton => Labels.NeedsYouTerminalButton(TerminalWord);

    /// <summary>The terminal button's accessible name.</summary>
    public string TerminalName => Labels.NeedsYouTerminalName(TerminalWord, Subject);

    /// <summary>True when the send back button shows (disabled with its reason on a settings request).</summary>
    public bool ShowsSendBack => Kind switch
    {
        BarKind.Desk => (DeskControl is not null || TerminalOnly) && !HasQuestions,
        BarKind.Decision => RejectControl is not null,
        BarKind.Settings => true,
        BarKind.None or BarKind.Run => false,
        _ => throw new InvalidOperationException("a kind the bar doesn't know"),
    };

    /// <summary>True when send back can be pressed.</summary>
    public bool SendBackEnabled => Kind switch
    {
        BarKind.Desk => Enabled && _target is not null && !IsSending,
        BarKind.Decision => Enabled && RejectControl is { IsEnabled: true },
        BarKind.None or BarKind.Settings or BarKind.Run => false,
        _ => throw new InvalidOperationException("a kind the bar doesn't know"),
    };

    /// <summary>The send back button's words: the decision's own, else "Send back".</summary>
    public string SendBackWord => Kind == BarKind.Decision ? RejectControl?.Action.Label ?? Labels.NeedsYouDecline : Labels.NeedsYouSendBack;

    /// <summary>The send back button's accessible name.</summary>
    public string SendBackName => Labels.NeedsYouBarName(SendBackWord, Subject);

    /// <summary>Why send back is off; empty when it is on.</summary>
    public string SendBackTooltip => Kind == BarKind.Settings ? Labels.NeedsYouSendBackLocked : "";

    /// <summary>True for the dashed chip of the feature that is not built yet (a desk request).</summary>
    public bool ShowsEditChip => Kind == BarKind.Desk;

    /// <summary>True when the card's More menu offers dropping the request (a desk request whose run is open).</summary>
    public bool ShowsDrop => Kind == BarKind.Desk && _target is not null && !TerminalOnly;

    /// <summary>True when the card keeps the row's own controls: no bar takes the request's primary.</summary>
    public bool ShowsRowControls => Kind == BarKind.None || (Kind is BarKind.Settings or BarKind.Run && !ShowsTerminal && !ShowsConfirm);

    /// <summary>The line under the buttons.</summary>
    public string Hint => Mode switch
    {
        NeedsYouBarMode.Approved => ApprovedHint,
        NeedsYouBarMode.Confirming => Labels.NeedsYouConfirmHint,
        NeedsYouBarMode.Pending or NeedsYouBarMode.SendingBack or NeedsYouBarMode.ConfirmingDrop => RestingHint,
        _ => throw new InvalidOperationException("a mode the bar doesn't know"),
    };

    private string ApprovedHint => _done switch
    {
        NeedsYouBarOutcome.Revise or NeedsYouBarOutcome.Rework => Labels.NeedsYouSentBackHint,
        NeedsYouBarOutcome.Drop => Labels.NeedsYouDroppedHint,
        NeedsYouBarOutcome.None or NeedsYouBarOutcome.Approve or NeedsYouBarOutcome.Answer => Labels.NeedsYouApprovedHint,
        _ => throw new InvalidOperationException("an outcome the bar doesn't know"),
    };

    private string RestingHint =>
        HintIsSuccess ? Labels.NeedsYouCopied
        : Kind == BarKind.Desk && _targetKnown && _target is null ? Labels.NeedsYouNotOpen
        : TerminalOnly ? Labels.NeedsYouTerminalOnly
        : ShowsTerminal ? Labels.NeedsYouProtectedHint
        : Kind == BarKind.Settings ? Labels.NeedsYouKeepSettings
        : SuggestedHint;

    /// <summary>True when there is a line to show.</summary>
    public bool HasHint => Hint.Length > 0;

    /// <summary>True for the line in the attention tone (the question before approving).</summary>
    public bool HintIsAttention => Mode == NeedsYouBarMode.Confirming;

    /// <summary>True when a refusal shows.</summary>
    public bool HasRefusal => Refusal.Length > 0;

    /// <summary>What the box says about the text.</summary>
    public string ReasonRequired => Kind == BarKind.Decision ? Labels.NeedsYouReasonRecorded : Labels.NeedsYouReasonSentBack;

    /// <summary>The box's placeholder.</summary>
    public string ReasonPlaceholder => Kind == BarKind.Decision ? Labels.NeedsYouReasonPlaceholderDecline : Labels.NeedsYouReasonPlaceholder;

    /// <summary>True when the reason can be sent: it has text and nothing is in flight.</summary>
    public bool CanSend => IsSendingBack && Enabled && !IsSending && !string.IsNullOrWhiteSpace(ReasonText);

    /// <summary>The send button's words.</summary>
    public string SendWord => SendBackWord;

    /// <summary>The done card's title.</summary>
    public string DoneTitle => _row is null ? "" : _done switch
    {
        NeedsYouBarOutcome.Revise or NeedsYouBarOutcome.Rework => Labels.NeedsYouSentBackTitle(_row.Title),
        NeedsYouBarOutcome.Drop => Labels.NeedsYouDroppedTitle(_row.Title),
        NeedsYouBarOutcome.None or NeedsYouBarOutcome.Approve or NeedsYouBarOutcome.Answer => Labels.NeedsYouApprovedTitle(_row.Title),
        _ => throw new InvalidOperationException("an outcome the bar doesn't know"),
    };

    /// <summary>The done card's sentence.</summary>
    public string DoneBody => _done switch
    {
        NeedsYouBarOutcome.Revise or NeedsYouBarOutcome.Rework => Labels.NeedsYouSentBackBody,
        NeedsYouBarOutcome.Drop => Labels.NeedsYouDroppedBody,
        NeedsYouBarOutcome.None or NeedsYouBarOutcome.Approve or NeedsYouBarOutcome.Answer => Labels.NeedsYouApprovedBody,
        _ => throw new InvalidOperationException("an outcome the bar doesn't know"),
    };
}
