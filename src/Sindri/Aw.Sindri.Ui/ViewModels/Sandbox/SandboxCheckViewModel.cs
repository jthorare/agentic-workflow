using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Sandbox;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Sandbox;

/// <summary>
/// Check again and its outcome (sandbox spec §8). One instance serves every place the card shows (Health, Settings):
/// pressing it runs every probe and lists every blocker. The button stays enabled for the keyboard while a check runs and a second press does nothing; the
/// polite live region rests on its label before it says an outcome, so a repeated "still blocked" is a change.
/// </summary>
internal sealed class SandboxCheckViewModel : ObservableObject
{
    private readonly ISindriApi _api;
    private readonly TimeProvider _clock;
    private readonly Func<Uri?> _apiUrl;
    private readonly IClipboard _clipboard;
    private int _checks;

    /// <summary>Check again over the API, a clock, the endpoint and the clipboard.</summary>
    /// <param name="api">The API.</param>
    /// <param name="clock">For the 300 ms "Sending…" and the age of a check.</param>
    /// <param name="apiUrl">The API's base URL while connected, else null.</param>
    /// <param name="clipboard">Where a blocker's command is copied.</param>
    internal SandboxCheckViewModel(ISindriApi api, TimeProvider clock, Func<Uri?> apiUrl, IClipboard clipboard)
    {
        (_api, _clock, _apiUrl, _clipboard) = (api, clock, apiUrl, clipboard);
        Live = new LiveLine(Labels.SandboxCheckAgain);
    }

    /// <summary>How long a check runs before "Sending…" is announced.</summary>
    internal static TimeSpan SendingAfter { get; } = TimeSpan.FromMilliseconds(300);

    /// <summary>The button's label, kept while a check runs: "Check now" until a check has run, then "Check again".</summary>
    public string Label { get; private set => Set(ref field, value); } = Labels.SandboxCheckAgain;

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

    /// <summary>The checks the last answer says did not run, as captions under the blockers (informational).</summary>
    public IReadOnlyList<string> NotChecked { get; private set => Set(ref field, value); } = [];

    /// <summary>True when the last answer names a check that did not run.</summary>
    public bool HasNotChecked => NotChecked.Count > 0;

    /// <summary>"Checked just now" or "Checked 12m ago"; empty when never checked.</summary>
    public string CheckedText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when a check has run.</summary>
    public bool HasChecked => CheckedText.Length > 0;

    /// <summary>When the last check ran; null when never.</summary>
    internal DateTimeOffset? CheckedAt { get; private set; }

    /// <summary>Records when the last check ran (from the status or from a check), and words its age.</summary>
    /// <param name="checkedAt">When it ran; null when never.</param>
    internal void Seen(DateTimeOffset? checkedAt)
    {
        CheckedAt = checkedAt;
        CheckedText = checkedAt is { } at ? Labels.SandboxChecked(Max(_clock.GetUtcNow() - at)) : string.Empty;
        OnChanged(nameof(HasChecked));
        Label = checkedAt is null ? Labels.SandboxCheckNow : Labels.SandboxCheckAgain;
        Live.Rest(Label);
    }

    private static TimeSpan Max(TimeSpan age) => age < TimeSpan.Zero ? TimeSpan.Zero : age;

    /// <summary>Check again: runs every probe and shows every blocker, or the Steps a resume would start. A press while one runs does nothing.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task that completes when the outcome is shown.</returns>
    internal async Task Check(CancellationToken cancellationToken)
    {
        if (IsChecking || _apiUrl() is not { } url)
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
            ShowNotChecked([]);
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
        ShowNotChecked(check.NotChecked ?? []);
        if (!check.Ready || check.Blockers.Count > 0)
        {
            Move(SandboxCheckPhase.StillFailing);
            Say(Labels.SandboxStillBlocked(check.Blockers.Count));
            return;
        }

        Move(SandboxCheckPhase.AllClear);
        Say(Labels.SandboxReadyNothingWaiting);
    }

    private void ShowNotChecked(IReadOnlyList<string> lines)
    {
        NotChecked = lines;
        OnChanged(nameof(HasNotChecked));
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
        OnChanged(nameof(HasResult));
    }
}
