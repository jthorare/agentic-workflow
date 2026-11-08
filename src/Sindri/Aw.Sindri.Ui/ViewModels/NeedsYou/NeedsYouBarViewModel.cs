namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The action bar under the inbox's request card (F3 needs-you spec §3, §5). It follows the card's row and decides, per kind of
/// request, what the primary action and send back are: a decision
/// goes to the shell as its option, and an approval is one plain click through <see cref="NeedsYouConfirm"/>. A, D and Ctrl+Enter reach the same methods the buttons do. This file holds the state and what the bar
/// shows for a request; the words are in NeedsYouBarViewModel.Look.cs and the presses in NeedsYouBarViewModel.Actions.cs.
/// </summary>
internal sealed partial class NeedsYouBarViewModel : ObservableObject
{
    private static readonly string[] _all =
    [
        nameof(Mode), nameof(IsPending), nameof(IsSendingBack), nameof(IsApproved), nameof(ShowsActions), nameof(ApproveWord), nameof(ApproveName), nameof(IsAsking), nameof(ShowsPrimary), nameof(PrimaryEnabled),
        nameof(ShowsSendBack), nameof(SendBackEnabled), nameof(SendBackWord), nameof(SendBackName), nameof(SendBackTooltip),
        nameof(ShowsRowControls), nameof(Hint), nameof(HasHint), nameof(HintIsAttention), nameof(Refusal), nameof(HasRefusal), nameof(ReasonRequired), nameof(ReasonPlaceholder), nameof(CanSend),
        nameof(SendWord), nameof(IsSending), nameof(DoneTitle), nameof(DoneBody), nameof(HasBar), nameof(MoreName), nameof(BarName), nameof(ConfirmEnabled),
    ];

    private readonly NeedsYouConfirm? _confirm;
    private NeedsYouRowViewModel? _row;

    /// <param name="confirm">The approvals the app does with one click; null offers none.</param>
    internal NeedsYouBarViewModel(NeedsYouConfirm? confirm = null)
    {
        _confirm = confirm;
        _confirm?.Changed += OnConfirmChanged;
    }

    /// <summary>Raised when the bar asks to answer a decision: the shell sends it as the decision sheet would.</summary>
    internal event EventHandler<NeedsYouDecisionEventArgs>? DecisionRequested;

    /// <summary>Raised when the reason box opened: focus goes into it.</summary>
    internal event EventHandler? ReasonRequested;

    /// <summary>What the bar shows.</summary>
    public NeedsYouBarMode Mode { get; private set; }

    /// <summary>Completes when the shown row's approval has been read.</summary>
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
        Decision,
        Settings,
    }

    private string Subject => _row?.Title ?? "";

    private BarKind Kind => _row is null ? BarKind.None : NeedsYouGrouping.KindOf(_row.Entry) switch
    {
        NeedsYouGroupKind.Decisions => BarKind.Decision,
        NeedsYouGroupKind.Settings => BarKind.Settings,
        NeedsYouGroupKind.Failures or NeedsYouGroupKind.Other => NeedsYouConfirm.IsApproval(_row.Entry) ? BarKind.Settings : BarKind.None,
        _ => throw new InvalidOperationException("a group the app doesn't know"),
    };

    private NeedsYouActionControl? AcceptControl => _row?.Controls.FirstOrDefault(c => c is { IsPrimary: true, Action.OptionId.Length: > 0 });

    private NeedsYouActionControl? RejectControl => _row?.Controls.FirstOrDefault(c => c.Action is { RejectLike: true, OptionId.Length: > 0 });

    /// <summary>True while the bar asks something Esc withdraws: the reason box or "Approve?".</summary>
    internal bool HasOpenQuestion => Mode is NeedsYouBarMode.SendingBack or NeedsYouBarMode.Confirming;

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
        IsSending = false;
        (Mode, ReasonText, Refusal, ReadToEnd) = (NeedsYouBarMode.Pending, "", "", false);
        Loaded = _confirm?.Show(row) ?? Task.CompletedTask;
        Refresh();
    }

    private void Refresh()
    {
        foreach (var name in _all)
        {
            OnChanged(name);
        }
    }
}
