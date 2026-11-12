using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The Not running page's start states (first-run spec §4.1, decision 2a). The shell calls <see cref="Begin"/> once when the
/// app first finds Sindri down; Start Sindri and Retry call <see cref="Start"/>. The follower's 2 s looks decide when Sindri
/// answered (<see cref="Reset"/>); this class only waits <see cref="Wait"/> for it and then says why it didn't.
/// </summary>
/// <param name="starter">Starts the program; null when the head has none.</param>
/// <param name="clock">The clock for the wait.</param>
internal sealed class SindriStartViewModel(ISindriStarter? starter, TimeProvider clock) : ObservableObject
{
    /// <summary>How long the app waits for Sindri to answer after a start.</summary>
    internal static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

    private const string AlreadyRunningCode = "SND-LEASE-001";
    private int _attempt;
    private bool _begun;

    /// <summary>Which state shows.</summary>
    public StartStage Stage { get; private set => Set(ref field, value); }

    /// <summary>True when there is a starter to use.</summary>
    public bool CanStart { get; } = starter is not null;

    /// <summary>The line under the title, or empty when the page's own reason shows.</summary>
    public string Message { get; private set => Set(ref field, value); } = starter is null ? string.Empty : Labels.StartSentence;

    /// <summary>True when <see cref="Message"/> has words.</summary>
    public bool HasMessage => Message.Length > 0;

    /// <summary>True when <see cref="Details"/> has words.</summary>
    public bool HasDetails => Details.Length > 0;

    /// <summary>True in the plain page (nothing started), where the page's own reason and, with no starter, the terminal command show.</summary>
    public bool IsIdle => Stage == StartStage.Idle;

    /// <summary>True when the card shows the terminal command: nothing started and no starter to do it.</summary>
    public bool ShowsTerminalCommand => IsIdle && !CanStart;

    /// <summary>The disclosure text, or empty.</summary>
    public string Details { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The primary button's label, or empty when the state has none.</summary>
    public string Primary { get; private set => Set(ref field, value); } = starter is null ? string.Empty : Labels.StartSindri;

    /// <summary>True when the primary button shows.</summary>
    public bool HasPrimary => Primary.Length > 0;

    /// <summary>Starts Sindri once, when the app first finds it down. Later outages show the page with its button.</summary>
    public void Begin()
    {
        if (_begun || starter is null)
        {
            return;
        }

        _begun = true;
        Start();
    }

    /// <summary>Starts Sindri now (Start Sindri, Retry).</summary>
    public void Start()
    {
        if (starter is null)
        {
            return;
        }

        _begun = true;
        var attempt = ++_attempt;
        var result = starter.Start();
        if (result.IsError)
        {
            starter.Release();
            Show(StartStage.DidntStart, Labels.DidntStart(result.Error.Message), Labels.StartDetails(starter.LogPath), Labels.Retry);
            return;
        }

        if (result.Value == SindriStartOutcome.NotInstalled)
        {
            Show(StartStage.NotInstalled, Labels.NotInstalled, Labels.InstallDetails, string.Empty);
            return;
        }

        Show(StartStage.Starting, Labels.StartingSindri, string.Empty, string.Empty);
        _ = Expire(attempt);
    }

    /// <summary>Sindri answered or the page left: back to the plain page.</summary>
    public void Reset()
    {
        _attempt++;
        starter?.Release();
        if (Stage != StartStage.Idle)
        {
            Show(StartStage.Idle, CanStart ? Labels.StartSentence : string.Empty, string.Empty, CanStart ? Labels.StartSindri : string.Empty);
        }
    }

    // Waits Wait; if no Reset or newer start came, says why nothing answered.
    private async Task Expire(int attempt)
    {
        try
        {
            await Task.Delay(Wait, clock).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (attempt != _attempt || starter is null)
        {
            return;
        }

        starter.Release();
        var error = starter.LastError();
        if (error is not null && error.Contains(AlreadyRunningCode, StringComparison.Ordinal))
        {
            Show(StartStage.AlreadyStarting, Labels.AlreadyStarting, string.Empty, string.Empty);
            return;
        }

        Show(StartStage.DidntStart, Labels.DidntStart(error ?? Labels.NoErrorLine), Labels.StartDetails(starter.LogPath), Labels.Retry);
    }

    private void Show(StartStage stage, string message, string details, string primary)
    {
        Stage = stage;
        Message = message;
        Details = details;
        Primary = primary;
        OnChanged(nameof(HasPrimary));
        OnChanged(nameof(HasMessage));
        OnChanged(nameof(HasDetails));
        OnChanged(nameof(IsIdle));
        OnChanged(nameof(ShowsTerminalCommand));
    }
}
