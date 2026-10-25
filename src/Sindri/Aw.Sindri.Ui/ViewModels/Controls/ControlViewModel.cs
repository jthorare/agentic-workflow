using System.Globalization;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Controls;

/// <summary>
/// One catalog row as a control (UI spec U3, §7) and its command lifecycle (§4.1). It maps the row's UI status to how
/// the control is drawn and says what the API answered; it decides nothing about whether the action is allowed: a
/// row with a sender is a button that runs, and a row the app has no form for shows its command to copy.
/// </summary>
internal sealed partial class ControlViewModel : ObservableObject
{
    /// <summary>How long a command is in flight before "Sending…" is announced.</summary>
    internal static readonly TimeSpan SendingAfter = TimeSpan.FromMilliseconds(300);

    /// <summary>How long the accepted outcome shows.</summary>
    internal static readonly TimeSpan AcceptedFor = TimeSpan.FromSeconds(2);

    /// <summary>How long "Copied" shows.</summary>
    internal static readonly TimeSpan CopiedFor = TimeSpan.FromMilliseconds(1500);

    private readonly Func<CancellationToken, Task<Result<CommandAcceptedView>>>? _send;
    private readonly IClipboard _clipboard;
    private readonly TimeProvider _clock;
    private int _runs;
    private int _copies;

    /// <summary>The control for <paramref name="row"/>.</summary>
    /// <param name="row">The catalog row.</param>
    /// <param name="send">Sends the command once; null when nothing is wired to it (it then never runs).</param>
    /// <param name="clipboard">Where Copy puts the CLI.</param>
    /// <param name="subject">The subject the control acts on (an item id), named in its accessible name; null for none.</param>
    /// <param name="clock">The clock for the lifecycle's timings.</param>
    internal ControlViewModel(CommandRowView row, Func<CancellationToken, Task<Result<CommandAcceptedView>>>? send, IClipboard clipboard, TimeProvider clock, string? subject = null)
    {
        _send = send;
        _clipboard = clipboard;
        _clock = clock;
        Subject = subject ?? string.Empty;
        Action = row.Action;
        Label = Labels.Action(row.Action);
        Shortcut = row.Shortcut ?? string.Empty;
        Cli = Safe(row.Cli) ? row.Cli : string.Empty;
        var named = subject is { Length: > 0 } ? Label + " " + subject : Label;
        Name = named;
        IsDestructive = row.Destructive;
        ConfirmQuestion = Labels.ConfirmQuestion(named);
    }

    /// <summary>The catalog action id.</summary>
    public string Action { get; }

    /// <summary>The control's label (keyed by action id; an unknown id shows itself).</summary>
    public string Label { get; }

    /// <summary>The control's accessible name: its label and subject.</summary>
    public string Name { get; }

    /// <summary>The catalog's default key, or empty.</summary>
    public string Shortcut { get; }

    /// <summary>True when the app has no form for the action: the control is the command to copy.</summary>
    public bool IsCopyOnly => _send is null;

    /// <summary>The CLI as served, or empty when it holds a control or bidi character (§7's backstop).</summary>
    public string Cli { get; }

    /// <summary>True when the CLI can be copied.</summary>
    public bool CanCopy => Cli.Length > 0;

    /// <summary>The lifecycle phase.</summary>
    public CommandPhase Phase
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(CanRun));
            OnChanged(nameof(IsSending));
            OnChanged(nameof(IsAccepted));
            OnChanged(nameof(IsFailed));
        }
    }

    /// <summary>True when a click sends it: a row with a sender, not already in flight.</summary>
    public bool CanRun => _send is not null && Phase != CommandPhase.Sending;

    /// <summary>True when the control is a live button: a row with a sender, even while it sends (so focus stays on it).</summary>
    public bool IsSendable => _send is not null;

    /// <summary>True while sending (the spinner).</summary>
    public bool IsSending => Phase == CommandPhase.Sending;

    /// <summary>True while the accepted outcome shows.</summary>
    public bool IsAccepted => Phase == CommandPhase.Accepted;

    /// <summary>True after a refusal or failure.</summary>
    public bool IsFailed => Phase == CommandPhase.Failed;

    /// <summary>The accepted outcome in words, or empty.</summary>
    public string Outcome { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The refusal's SND code (ProblemDetails title), or empty.</summary>
    public string ErrorCode { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The refusal's detail as served, or empty.</summary>
    public string ErrorDetail { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The Copy button's accessible name: "Copy: Cancel job", then "Copied".</summary>
    public string CopyName => string.Equals(CopyLabel, Labels.Copied, StringComparison.Ordinal) ? Labels.Copied : Labels.Copy + ": " + Label;

    /// <summary>"Copy", or "Copied" for 1.5 s after a copy.</summary>
    public string CopyLabel
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(CopyName));
        }
    } = Labels.Copy;

    /// <summary>
    /// Asked just before a run starts; false sends nothing (the owner has said why where it belongs, as the create form does for a
    /// blank title). Null for a control with nothing to check.
    /// </summary>
    internal Func<bool>? Precondition { get; set; }

    /// <summary>Sends the command once (a second click while sending does nothing; the app never retries on its own).</summary>
    /// <param name="cancellationToken">Cancels the wait for the answer.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    private async Task Run(CancellationToken cancellationToken)
    {
        if (!CanRun || _send is not { } send || Precondition?.Invoke() == false)
        {
            return;
        }

        var run = ++_runs;
        IsAnnouncing = false;   // dark first, then the resting text: an outcome the same as the last one is still a change, and the label is not spoken
        Announce(Label);
        Phase = CommandPhase.Sending;   // before the old outcome is cleared: an entry showing Failed or Accepted never reads half-cleared (", :")
        (ErrorCode, ErrorDetail, Outcome) = (string.Empty, string.Empty, string.Empty);
        _ = AnnounceSending(run, cancellationToken);
        Result<CommandAcceptedView> answer;
        try
        {
            answer = await send(cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Cancelled (the window is closing): nothing was answered, so the control is not left sending.
            IsAnnouncingSending = false;   // first: the palette reads the name on every change, and Idle with this still set says "ready"
            Phase = CommandPhase.Idle;
            Rest();
            throw;
        }

        if (answer.IsError)
        {
            (ErrorCode, ErrorDetail) = (answer.Error.Code, answer.Error.Message);
            Announce($"{ErrorCode}: {ErrorDetail}");
            Phase = CommandPhase.Failed;
            IsAnnouncingSending = false;   // last: nothing shows "ready" between "Sending…" and the outcome
            return;
        }

        Outcome = Labels.Done(Label);
        Announce(Outcome);
        Phase = CommandPhase.Accepted;
        IsAnnouncingSending = false;
        _ = Settle(run, cancellationToken);
    }

    /// <summary>Copies the CLI (never one with a control or bidi character): "Copied" for 1.5 s, announced.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the clipboard answered.</returns>
    internal async Task Copy(CancellationToken cancellationToken)
    {
        if (!CanCopy || !await _clipboard.SetText(Cli, cancellationToken).ConfigureAwait(true))
        {
            return;
        }

        CopyLabel = Labels.Copied;
        Announce(Labels.Copied);
        _ = Uncopy(++_copies, cancellationToken);
    }

    private async Task Uncopy(int copy, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(CopiedFor, _clock, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (copy == _copies)
        {
            CopyLabel = Labels.Copy;
            Rest();
        }
    }

    private async Task AnnounceSending(int run, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(SendingAfter, _clock, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (run == _runs && Phase == CommandPhase.Sending)
        {
            IsAnnouncingSending = true;
            Announce(Labels.Sending);
        }
    }

    private async Task Settle(int run, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(AcceptedFor, _clock, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (run == _runs && Phase == CommandPhase.Accepted)
        {
            Phase = CommandPhase.Idle;
            Rest();
        }
    }

    // §7: the app refuses to show or copy a cli with a control, bidi or invisible character (E-16 makes this never happen).
    internal static bool Safe(string cli) => cli.Length > 0 && !cli.EnumerateRunes().Any(r => r.Value != ' ' && Rune.GetUnicodeCategory(r) is
        UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator
        or UnicodeCategory.SpaceSeparator or UnicodeCategory.PrivateUse or UnicodeCategory.Surrogate or UnicodeCategory.OtherNotAssigned);
}
