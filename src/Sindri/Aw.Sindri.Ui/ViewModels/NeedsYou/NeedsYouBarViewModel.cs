using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The action bar under the inbox's request card (F3 needs-you spec §3, §5). It follows the card's row and decides, per kind of
/// request, what the primary action, send back and the terminal button are, and sends nothing a catalog row calls protected: a desk
/// request is reported once through <see cref="DeskReporter"/>, a decision goes to the shell as its option, and a protected action is
/// only its command copied. A, D and Ctrl+Enter reach the same methods the buttons do. This file holds the state and what the bar
/// shows for a request; the words are in NeedsYouBarViewModel.Look.cs and the presses in NeedsYouBarViewModel.Actions.cs.
/// </summary>
internal sealed partial class NeedsYouBarViewModel : ObservableObject
{
    private const string InstanceKind = "instance";
    private const string CompleteStepAction = "CompleteStep";
    private const string GrantAction = "ApplyCommand:answer-grant";

    private static readonly string[] _all =
    [
        nameof(Mode), nameof(IsPending), nameof(IsSendingBack), nameof(IsConfirmingDrop), nameof(IsApproved), nameof(ShowsActions), nameof(ApproveWord), nameof(ApproveName), nameof(IsAsking), nameof(ShowsPrimary), nameof(PrimaryEnabled),
        nameof(ShowsTerminal), nameof(TerminalWord), nameof(TerminalButton), nameof(TerminalName), nameof(ShowsSendBack), nameof(SendBackEnabled), nameof(SendBackWord), nameof(SendBackName), nameof(SendBackTooltip), nameof(ShowsEditChip),
        nameof(ShowsDrop), nameof(ShowsRowControls), nameof(Hint), nameof(HasHint), nameof(HintIsAttention), nameof(HintIsSuccess), nameof(Refusal), nameof(HasRefusal), nameof(ReasonRequired), nameof(ReasonPlaceholder), nameof(CanSend),
        nameof(SendWord), nameof(IsSending), nameof(DoneTitle), nameof(DoneBody), nameof(HasBar), nameof(MoreName), nameof(BarName), nameof(ShowsTyped), nameof(TypedBox), nameof(ShowsHold), nameof(ShowsConfirm), nameof(ConfirmEnabled),
    ];

    private readonly DeskReporter _desk;
    private readonly IClipboard _clipboard;
    private readonly TimeProvider _clock;
    private readonly NeedsYouConfirm? _confirm;
    private NeedsYouRowViewModel? _row;
    private DeskTarget? _target;
    private bool _targetKnown;
    private NeedsYouBarOutcome _done;

    /// <param name="desk">Reports an open desk run.</param>
    /// <param name="clipboard">Copies a protected action's command.</param>
    /// <param name="clock">The clock for "Copied".</param>
    /// <param name="confirm">The typed and held confirmations of the three protected approvals the app confirms; null shows their terminal copy only.</param>
    internal NeedsYouBarViewModel(DeskReporter desk, IClipboard clipboard, TimeProvider clock, NeedsYouConfirm? confirm = null)
    {
        (_desk, _clipboard, _clock, _confirm) = (desk, clipboard, clock, confirm);
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

    /// <summary>Completes when the copied line has been shown for its time and dropped.</summary>
    internal Task CopiedExpiry { get; private set; } = Task.CompletedTask;

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

    /// <summary>True when the command was just copied (the success line shows).</summary>
    public bool HintIsSuccess { get; private set; }

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

    private NeedsYouActionControl? PrimaryControl => _row?.Controls.FirstOrDefault(c => c.IsPrimary);

    // A desk request is reported by the engine's CompleteStep; the row must serve it ready for the bar to offer it.
    private NeedsYouActionControl? DeskControl => _row?.Controls.FirstOrDefault(c => string.Equals(c.Action.CatalogId, CompleteStepAction, StringComparison.Ordinal) && c.Action.IsReady && c.IsEnabled);

    private NeedsYouActionControl? AcceptControl => _row?.Controls.FirstOrDefault(c => c is { IsPrimary: true, Action.OptionId.Length: > 0 });

    private NeedsYouActionControl? RejectControl => _row?.Controls.FirstOrDefault(c => c.Action is { RejectLike: true, OptionId.Length: > 0 });

    private bool TerminalOnly => Kind == BarKind.Desk && _target?.IsTerminalOnly == true;

    // The command the terminal button copies: the step's served complete command for a merge, else the action's own served command.
    private string TerminalCli => TerminalOnly ? _target?.Complete?.Cli ?? "" : PrimaryControl?.Action.Cli is { Length: > 0 } cli ? cli : _row?.Cli ?? "";

    private static string WordsOf(NeedsYouAction action) =>
        string.Equals(action.CatalogId, GrantAction, StringComparison.Ordinal) ? Labels.NeedsYouMoreTries : action.Label;

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
        (_target, _targetKnown, _done) = (null, false, NeedsYouBarOutcome.None);
        (IsSending, HintIsSuccess) = (false, false);
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
            Refresh();
        }
    }

    private void Refresh()
    {
        foreach (var name in _all)
        {
            OnChanged(name);
        }
    }
}
