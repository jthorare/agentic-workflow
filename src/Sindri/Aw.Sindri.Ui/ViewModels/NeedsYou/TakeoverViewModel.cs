using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// A run the user took over (F3 needs-you spec §3, §4): the clock text from the served time left, the footer and badge that say
/// whether the clock stands still, the 2-hour nudge from the served "takeover:" row, and Hand back, which sends
/// "ApplyCommand:handback" and nothing else. The run canvas hosts the inspector block these properties fill.
/// </summary>
internal sealed class TakeoverViewModel : ObservableObject
{
    private const string TakenOverState = "taken-over";
    private const string RowPrefix = "takeover:";
    private static readonly TimeSpan _toastShown = TimeSpan.FromSeconds(5);
    private static readonly string[] _all =
    [
        nameof(IsTakenOver), nameof(Badge), nameof(Footer), nameof(ClockText), nameof(HandBackBox), nameof(NudgeVisible), nameof(NudgeTitle), nameof(ToastVisible),
        nameof(IsSending), nameof(Refusal), nameof(HasRefusal), nameof(IsAskingCancel), nameof(CancelAsk), nameof(TookOverLine), nameof(HasTookOverLine),
    ];

    private long? Left { get; set; }
    private readonly ISindriApi _api;
    private readonly Func<Uri?> _apiUrl;
    private readonly InstanceCommander _commands;
    private readonly TimeProvider _clock;
    private string? _instance;
    private DateTimeOffset? _takenAt;
    private string _address = "";
    private string _confirm = "";
    private bool Nudge { get; set; }
    private DateTimeOffset _since;
    private DateTimeOffset? _dismissed;

    internal TakeoverViewModel(ISindriApi api, Func<Uri?> apiUrl, InstanceCommander commands, TimeProvider clock)
    {
        (_api, _apiUrl, _commands, _clock) = (api, apiUrl, commands, clock);
    }

    /// <summary>The inspector's hand-back button.</summary>
    public string HandBackWord { get; } = Labels.NeedsYouHandBack;

    /// <summary>The inspector's cancel button.</summary>
    public string CancelWord { get; } = Labels.NeedsYouCancelRun;

    /// <summary>The cancel button's help.</summary>
    public string CancelHelp { get; } = Labels.NeedsYouCancelRunHelp;

    /// <summary>The nudge's body.</summary>
    public string NudgeBody { get; } = Labels.NeedsYouNudgeBody;

    /// <summary>The nudge's dismiss button.</summary>
    public string NotYetWord { get; } = Labels.NeedsYouNotYet;

    /// <summary>The toast's title.</summary>
    public string ToastTitle { get; } = Labels.NeedsYouHandedBack;

    /// <summary>The toast's body.</summary>
    public string ToastBody { get; } = Labels.NeedsYouHandedBackBody;

    /// <summary>True while the user has the run: the instance serves a paused line.</summary>
    public bool IsTakenOver { get; private set; }

    /// <summary>The node and inspector badge: "You took over", or "Running" once handed back.</summary>
    public string Badge => IsTakenOver ? Labels.NeedsYouTookOver : Labels.NeedsYouRunning;

    /// <summary>The time left as the engine says it ("2h 50m"); empty when none is served.</summary>
    public string TimeLeft => Left is { } left ? Labels.Duration(left) : "";

    /// <summary>The node footer: the clock stands still while the user has the run, runs again after.</summary>
    public string Footer => Left is null
        ? Labels.NeedsYouClockPausedNoTime
        : IsTakenOver ? Labels.NeedsYouClockPaused(TimeLeft) : Labels.NeedsYouClockRunning(TimeLeft);

    /// <summary>The inspector's clock line, from the served time left.</summary>
    public string ClockText => Footer;

    /// <summary>The inspector's hand-back box.</summary>
    public string HandBackBox => Labels.NeedsYouHandBackBox(TimeLeft);

    /// <summary>True while the 2-hour nudge shows.</summary>
    public bool NudgeVisible => Nudge && IsTakenOver;

    /// <summary>The nudge's title: whole hours since the served row's time, at least two.</summary>
    public string NudgeTitle => Labels.NeedsYouNudgeTitle(Math.Max(2, (int)(_clock.GetUtcNow() - _since).TotalHours));

    /// <summary>True for five seconds after a hand back.</summary>
    public bool ToastVisible { get; private set; }

    /// <summary>"you took over at 14:05 (2h 35m ago)"; empty when the instance serves no takeover time (the badge says "You took over" alone).</summary>
    public string TookOverLine => _takenAt is { } at && IsTakenOver
        ? Labels.NeedsYouTookOverAt(at.ToOffset(_clock.GetLocalNow().Offset).ToString("HH:mm", CultureInfo.InvariantCulture), Labels.Duration((long)(_clock.GetUtcNow() - at).TotalSeconds))
        : "";

    /// <summary>True when the line shows.</summary>
    public bool HasTookOverLine => TookOverLine.Length > 0;

    /// <summary>True while a command is in flight.</summary>
    public bool IsSending { get; private set; }

    /// <summary>The served refusal of the last command, as served; empty for none.</summary>
    public string Refusal { get; private set; } = "";

    /// <summary>True when there is a refusal to show.</summary>
    public bool HasRefusal => Refusal.Length > 0;

    /// <summary>Completes when the toast has been shown for its time and dropped.</summary>
    internal Task ToastExpiry { get; private set; } = Task.CompletedTask;

    /// <summary>Reads the instance: whether the user has it, and the clock's time left.</summary>
    /// <param name="instanceId">The instance.</param>
    /// <returns>A task that completes when the read was applied.</returns>
    internal async Task Load(string instanceId)
    {
        _instance = instanceId;
        if (_apiUrl() is not { } url)
        {
            return;
        }

        var read = await _api.Instance(url, instanceId, CancellationToken.None).ConfigureAwait(true);
        if (read.IsError || !string.Equals(_instance, instanceId, StringComparison.Ordinal))
        {
            return;
        }

        var status = read.Value.Current.Status;
        (_address, _confirm) = (status.Header.Address, status.Commands.FirstOrDefault(c => c.Verb == StatusVerb.Cancel)?.Confirm ?? "");
        _takenAt = status.TakenOverAt;
        IsTakenOver = status.Paused is not null || string.Equals(status.Header.State, TakenOverState, StringComparison.Ordinal);
        Left = status.Paused?.TimeoutLeftSeconds ?? Left;   // handed back, the instance serves none: the footer keeps the time that was left
        Refresh();
    }

    /// <summary>Takes the served rows: the "takeover:" row of this instance shows the nudge, unless "Not yet" dismissed that very row.</summary>
    /// <param name="rows">The rows as served.</param>
    internal void Observe(IEnumerable<NeedsYouEntry> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var row = _instance is null ? null : rows.FirstOrDefault(r => string.Equals(r.Id, RowPrefix + _instance, StringComparison.Ordinal));
        Nudge = row is not null && _dismissed != row.Since;
        _since = row?.Since ?? _since;
        if (row is null)
        {
            _dismissed = null;   // the row left: the next one served is a new nudge
        }

        Refresh();
    }

    /// <summary>"Not yet": hides the nudge until the next served row.</summary>
    internal void NotYet()
    {
        _dismissed = _since;
        Nudge = false;
        Refresh();
    }

    /// <summary>Hand back, in the inspector or the nudge: sends "ApplyCommand:handback", once per press, and nothing else.</summary>
    /// <returns>A task that completes when the answer came.</returns>
    internal async Task HandBack()
    {
        if (_instance is not { } instance || IsSending || !IsTakenOver)
        {
            return;
        }

        var answer = await Send(new ApplyCommandBody(instance, CommandKind.Handback)).ConfigureAwait(true);
        if (answer is { IsError: false })
        {
            (IsTakenOver, Nudge, ToastVisible) = (false, false, true);
            ToastExpiry = DropToast();
            Refresh();
        }
    }

    /// <summary>True while the inspector asks "Cancel ...?" before it cancels.</summary>
    public bool IsAskingCancel { get; private set; }

    /// <summary>The cancel question: the served confirm text, else the app's own.</summary>
    public string CancelAsk => _confirm.Length > 0 ? _confirm : Labels.CancelAsk(_address);

    /// <summary>The cancel question's confirm button.</summary>
    public string ConfirmCancelWord { get; } = Labels.ConfirmCancel;

    /// <summary>The cancel question's back-out button.</summary>
    public string BackOutWord { get; } = Labels.NeedsYouBackOut;

    /// <summary>Cancel run, in the inspector: asks first; nothing is sent by this press.</summary>
    internal void AskCancel()
    {
        if (_instance is not null && !IsSending)
        {
            IsAskingCancel = true;
            Refresh();
        }
    }

    /// <summary>Backs out of the cancel question: nothing is sent.</summary>
    internal void BackOutOfCancel()
    {
        IsAskingCancel = false;
        Refresh();
    }

    /// <summary>The cancel question's confirm: sends "ApplyCommand:cancel", once per press.</summary>
    /// <returns>A task that completes when the answer came.</returns>
    internal async Task ConfirmCancel()
    {
        if (_instance is { } instance && IsAskingCancel && !IsSending)
        {
            IsAskingCancel = false;
            _ = await Send(new ApplyCommandBody(instance, CommandKind.Cancel)).ConfigureAwait(true);
        }
    }

    // One command per press: the flag is set before the first await. A refusal shows as served and leaves the control pressable.
    private async Task<Result<ApplyCommandAccepted>?> Send(ApplyCommandBody body)
    {
        (IsSending, Refusal) = (true, "");
        Refresh();
        var answer = await _commands.Send(body).ConfigureAwait(true);
        IsSending = false;
        if (answer is { IsError: true } refused)
        {
            Refusal = refused.Error.Message;
        }

        Refresh();
        return answer;
    }

    private async Task DropToast()
    {
        await Task.Delay(_toastShown, _clock).ConfigureAwait(true);
        ToastVisible = false;
        Refresh();
    }

    private void Refresh()
    {
        foreach (var name in _all)
        {
            OnChanged(name);
        }

        OnChanged(nameof(TimeLeft));
    }
}
