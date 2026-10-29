using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Sandbox;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Sandbox;

/// <summary>
/// Check again, its outcome and the resume list (sandbox spec §8). One instance serves every place the card shows (Health, the park card):
/// pressing it runs every probe and lists every blocker; when all clear it lists the Steps it would resume before sending anything, and
/// Resume answers each one as the human. The button stays enabled for the keyboard while a check runs and a second press does nothing; the
/// polite live region rests on its label before it says an outcome, so a repeated "still blocked" is a change.
/// </summary>
internal sealed class SandboxCheckViewModel : ObservableObject
{
    private readonly ISindriApi _api;
    private readonly TimeProvider _clock;
    private readonly Func<Uri?> _apiUrl;
    private readonly Func<ApplyCommandBody, Task<Result<ApplyCommandAccepted>?>> _commands;
    private readonly IClipboard _clipboard;
    private int _checks;
    private bool _resuming;

    /// <summary>Check again over the API, a clock, the endpoint, the command sender and the clipboard.</summary>
    /// <param name="api">The API.</param>
    /// <param name="clock">For the 300 ms "Sending…" and the age of a check.</param>
    /// <param name="apiUrl">The API's base URL while connected, else null.</param>
    /// <param name="commands">Sends the answer-resume of one instance.</param>
    /// <param name="clipboard">Where a blocker's command is copied.</param>
    internal SandboxCheckViewModel(ISindriApi api, TimeProvider clock, Func<Uri?> apiUrl, Func<ApplyCommandBody, Task<Result<ApplyCommandAccepted>?>> commands, IClipboard clipboard)
    {
        (_api, _clock, _apiUrl, _commands, _clipboard) = (api, clock, apiUrl, commands, clipboard);
        Live = new LiveLine(Labels.SandboxCheckAgain);
    }

    /// <summary>How long a check runs before "Sending…" is announced.</summary>
    internal static TimeSpan SendingAfter { get; } = TimeSpan.FromMilliseconds(300);

    /// <summary>The button's label, kept while a check runs.</summary>
    public string Label { get; } = Labels.SandboxCheckAgain;

    /// <summary>Where the check stands.</summary>
    public SandboxCheckPhase Phase { get; private set; }

    /// <summary>True while a check runs: the spinner shows and a press does nothing.</summary>
    public bool IsChecking => Phase == SandboxCheckPhase.InFlight;

    /// <summary>The polite live region: "Sending…" after 300 ms, then the outcome.</summary>
    public LiveLine Live { get; }

    /// <summary>The outcome in words; empty before any check.</summary>
    public string Result { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when there is an outcome to show.</summary>
    public bool HasResult => Result.Length > 0;

    /// <summary>Every blocker the last check found.</summary>
    public IReadOnlyList<SandboxBlockerRow> Blockers { get; private set => Set(ref field, value); } = [];

    /// <summary>True when the last check is still failing and lists blockers.</summary>
    public bool ShowsBlockers => Phase == SandboxCheckPhase.StillFailing && Blockers.Count > 0;

    /// <summary>The Steps a resume would start, oldest first.</summary>
    public IReadOnlyList<SandboxResumeRow> ResumeRows { get; private set => Set(ref field, value); } = [];

    /// <summary>True when all is clear and Steps wait: the list shows before [Resume] [Not now].</summary>
    public bool ShowsResume => Phase == SandboxCheckPhase.AllClear && ResumeRows.Count > 0;

    /// <summary>"Checked just now" or "Checked 12m ago"; empty when never checked.</summary>
    public string CheckedText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when a check has run.</summary>
    public bool HasChecked => CheckedText.Length > 0;

    /// <summary>When the last check ran; null when never.</summary>
    internal DateTimeOffset? CheckedAt { get; private set; }

    /// <summary>Raised when focus should go back to the Check again button (Not now).</summary>
    internal event EventHandler? FocusCheckRequested;

    /// <summary>Raised when Resume sent its answers: the parked cards are about to go, and focus should move to their neighbour.</summary>
    internal event EventHandler? Resolved;

    /// <summary>Records when the last check ran (from the status or from a check), and words its age.</summary>
    /// <param name="checkedAt">When it ran; null when never.</param>
    internal void Seen(DateTimeOffset? checkedAt)
    {
        CheckedAt = checkedAt;
        CheckedText = checkedAt is { } at ? Labels.SandboxChecked(Max(_clock.GetUtcNow() - at)) : string.Empty;
        OnChanged(nameof(HasChecked));
    }

    private static TimeSpan Max(TimeSpan age) => age < TimeSpan.Zero ? TimeSpan.Zero : age;

    /// <summary>Check again: runs every probe and shows every blocker, or the Steps a resume would start. A press while one runs does nothing.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task that completes when the outcome is shown.</returns>
    internal async Task Check(CancellationToken cancellationToken)
    {
        if (IsChecking || _resuming || _apiUrl() is not { } url)
        {
            return;
        }

        var run = ++_checks;
        Move(SandboxCheckPhase.InFlight);
        _ = AnnounceSending(run, cancellationToken);
        var read = await _api.SandboxCheck(url, cancellationToken).ConfigureAwait(true);
        if (read.IsError)
        {
            Blockers = [];
            ResumeRows = [];
            Move(SandboxCheckPhase.Idle);
            Say(Labels.SandboxCheckFailed);
            return;
        }

        Show(read.Value);
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

        if (run == _checks && IsChecking)
        {
            Live.Say(Labels.Sending);
        }
    }

    private void Show(SandboxCheckView check)
    {
        Seen(check.CheckedAt ?? _clock.GetUtcNow());
        Blockers = [.. check.Blockers.Select(SandboxBlockerRow.Of)];
        if (!check.Ready || check.Blockers.Count > 0)
        {
            ResumeRows = [];
            Move(SandboxCheckPhase.StillFailing);
            Say(Labels.SandboxStillBlocked(check.Blockers.Count));
            return;
        }

        ResumeRows = [.. (check.Resume ?? []).Select(r => new SandboxResumeRow(r.Instance, r.Title ?? r.Item ?? Labels.SandboxResumeNoTitle))];
        Move(SandboxCheckPhase.AllClear);
        Say(ResumeRows.Count > 0 ? Labels.SandboxReadyResume(ResumeRows.Count) : Labels.SandboxReadyNothingWaiting);
    }

    /// <summary>Resume: sends the answer-resume of each listed Step as the human, oldest first; a Step that refuses has moved on and the rest stay parked.</summary>
    /// <param name="cancellationToken">Cancels the sends.</param>
    /// <returns>A task that completes when every answer is sent.</returns>
    internal async Task Resume(CancellationToken cancellationToken)
    {
        if (!ShowsResume || _resuming)
        {
            return;
        }

        _resuming = true;
        try
        {
            var rows = ResumeRows;
            Live.Say(Labels.SandboxResuming(rows.Count));
            List<string> movedOn = [];
            for (var i = 0; i < rows.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sent = await _commands(ApplyCommandBody.Resume(rows[i].Instance)).ConfigureAwait(true);
                if (sent is null)
                {
                    // Not sent (a command is already on its way, or no endpoint): this Step and the rest stay listed for the next press.
                    ResumeRows = [.. rows.Skip(i)];
                    Say(Labels.SandboxResumeNotSent);
                    return;
                }

                if (!sent.IsSuccess)
                {
                    movedOn.Add(rows[i].Title);
                }
            }

            Settle(rows.Count, movedOn);
        }
        finally
        {
            _resuming = false;
        }
    }

    // Every answer is in: all resumed, or the Steps that moved on are named and the rest stay parked for the next press.
    private void Settle(int listed, List<string> movedOn)
    {
        ResumeRows = [];
        var resumed = listed - movedOn.Count;
        if (movedOn.Count == 0)
        {
            Move(SandboxCheckPhase.Resumed);
            Say(Labels.SandboxResumed(resumed));
        }
        else
        {
            Move(SandboxCheckPhase.ResumedPartial);
            Result = Labels.SandboxResumedPartial(resumed, listed, movedOn[0]);
            Live.Say(Labels.SandboxResumedPartialAnnounce(resumed, listed));
        }

        Resolved?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Not now: the list goes, nothing is sent, and focus goes back to Check again.</summary>
    internal void NotNow()
    {
        ResumeRows = [];
        Result = string.Empty;
        Move(SandboxCheckPhase.Idle);
        FocusCheckRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Copies a blocker's command.</summary>
    /// <param name="row">The blocker.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the clipboard answered.</returns>
    internal Task Copy(SandboxBlockerRow row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.Command is { Length: > 0 } command ? _clipboard.SetText(command, cancellationToken) : Task.CompletedTask;
    }

    private void Say(string sentence)
    {
        Result = sentence;
        OnChanged(nameof(HasResult));
        Live.Say(sentence);
    }

    private void Move(SandboxCheckPhase phase)
    {
        Phase = phase;
        OnChanged(nameof(Phase));
        OnChanged(nameof(IsChecking));
        OnChanged(nameof(ShowsBlockers));
        OnChanged(nameof(ShowsResume));
        OnChanged(nameof(HasResult));
    }
}
