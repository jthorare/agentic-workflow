namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>The action bar (<see cref="NeedsYouBarViewModel"/>, continued): what it shows, as the view reads it.</summary>
internal sealed partial class NeedsYouBarViewModel
{
    /// <summary>The More menu's words.</summary>
    public string MoreWord { get; } = Labels.NeedsYouMore;

    /// <summary>The More menu's accessible name.</summary>
    public string MoreName => Labels.NeedsYouMoreName(Subject);

    /// <summary>The reason box's accessible name.</summary>
    public string ReasonName { get; } = Labels.NeedsYouReasonName;

    /// <summary>Closes the reason box or the drop question.</summary>
    public string CancelWord { get; } = Labels.NeedsYouCancel;

    /// <summary>The reason box's caption.</summary>
    public string ReasonLabel { get; } = Labels.NeedsYouReasonLabel;

    /// <summary>The bar's accessible name.</summary>
    public string BarName => Labels.NeedsYouBarGroupName(Subject);

    /// <summary>True when the request has a bar of its own.</summary>
    public bool HasBar => Kind != BarKind.None && (ShowsPrimary || ShowsSendBack || IsApproved);

    /// <summary>True in the resting modes (Approve and Send back show).</summary>
    public bool IsPending => Mode is NeedsYouBarMode.Pending or NeedsYouBarMode.Confirming;

    /// <summary>True while the reason box shows.</summary>
    public bool IsSendingBack => Mode == NeedsYouBarMode.SendingBack;

    /// <summary>True once the request was answered.</summary>
    public bool IsApproved => Mode == NeedsYouBarMode.Approved;

    /// <summary>True when the buttons show (the resting modes).</summary>
    public bool ShowsActions => IsPending;

    /// <summary>True while the primary asks first.</summary>
    public bool IsAsking => Mode == NeedsYouBarMode.Confirming;

    /// <summary>The primary button's words: the decision's own, else "Approve", or "Approve?" while it asks.</summary>
    public string ApproveWord => Kind == BarKind.Decision ? AcceptControl?.Action.Label ?? Labels.NeedsYouApprove : IsAsking ? Labels.NeedsYouApproveAsk : Labels.NeedsYouApprove;

    /// <summary>The primary button's accessible name: the words and the request.</summary>
    public string ApproveName => Labels.NeedsYouBarName(ApproveWord, Subject);

    /// <summary>True when the primary button shows: a decision with a recommended option, or an approval.</summary>
    public bool ShowsPrimary => Kind switch
    {
        BarKind.Decision => AcceptControl is not null,
        BarKind.Settings => ConfirmKind != NeedsYouConfirmKind.None,
        BarKind.None => false,
        _ => throw new InvalidOperationException("a kind the bar doesn't know"),
    };

    // The kind's own condition for the primary: a decision's accept is enabled, an approval can be sent.
    private bool KindAllowsPrimary => Kind switch
    {
        BarKind.Decision => AcceptControl is { IsEnabled: true },
        BarKind.Settings => _confirm?.CanApprove == true,
        BarKind.None => true,
        _ => throw new InvalidOperationException("a kind the bar doesn't know"),
    };

    /// <summary>True when the primary can be pressed now.</summary>
    public bool PrimaryEnabled => ShowsPrimary && Enabled && !IsSending && KindAllowsPrimary;

    /// <summary>True when the send back button shows (disabled with its reason on a settings request).</summary>
    public bool ShowsSendBack => Kind switch
    {
        BarKind.Decision => RejectControl is not null,
        BarKind.Settings => true,
        BarKind.None => false,
        _ => throw new InvalidOperationException("a kind the bar doesn't know"),
    };

    /// <summary>True when send back can be pressed.</summary>
    public bool SendBackEnabled => Kind switch
    {
        BarKind.Decision => Enabled && RejectControl is { IsEnabled: true },
        BarKind.None or BarKind.Settings => false,
        _ => throw new InvalidOperationException("a kind the bar doesn't know"),
    };

    /// <summary>The send back button's words: the decision's own, else "Send back".</summary>
    public string SendBackWord => Kind == BarKind.Decision ? RejectControl?.Action.Label ?? Labels.NeedsYouDecline : Labels.NeedsYouSendBack;

    /// <summary>The send back button's accessible name.</summary>
    public string SendBackName => Labels.NeedsYouBarName(SendBackWord, Subject);

    /// <summary>Why send back is off; empty when it is on.</summary>
    public string SendBackTooltip => Kind == BarKind.Settings ? Labels.NeedsYouSendBackLocked : "";

    /// <summary>True when the card keeps the row's own controls: no bar takes the request's primary.</summary>
    public bool ShowsRowControls => Kind == BarKind.None || (Kind == BarKind.Settings && ConfirmKind == NeedsYouConfirmKind.None);

    /// <summary>The line under the buttons.</summary>
    public string Hint => Mode switch
    {
        NeedsYouBarMode.Approved => ApprovedHint,
        NeedsYouBarMode.Confirming => Labels.NeedsYouConfirmHint,
        NeedsYouBarMode.Pending or NeedsYouBarMode.SendingBack => RestingHint,
        _ => throw new InvalidOperationException("a mode the bar doesn't know"),
    };

    private static string ApprovedHint => Labels.NeedsYouApprovedHint;

    private string RestingHint =>
        Kind == BarKind.Settings ? Labels.NeedsYouKeepSettings : "";

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
    public string DoneTitle => _row is null ? "" : Labels.NeedsYouApprovedTitle(_row.Title);

#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    /// <summary>The done card's sentence.</summary>
    public string DoneBody => Labels.NeedsYouApprovedBody;
#pragma warning restore CA1822, MA0041
}
