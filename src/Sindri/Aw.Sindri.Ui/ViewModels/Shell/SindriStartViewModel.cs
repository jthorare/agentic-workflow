using Aw.Sindri.Ui.Platform;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The Not running page's start states (first-run spec §4.1, decision 2a). The shell calls <see cref="Begin"/> once when the
/// app first finds Sindri down; Start Sindri and Retry call <see cref="Start"/>. The follower's 2 s looks decide when Sindri
/// answered (<see cref="Reset"/>); this class only waits <see cref="Wait"/> for it and then says why it didn't.
/// </summary>
/// <param name="starter">Starts the program; null when the head has none.</param>
/// <param name="clock">The clock for the wait.</param>
/// <param name="log">The app's log; none when null.</param>
internal sealed class SindriStartViewModel(ISindriStarter? starter, TimeProvider clock, ILogger? log = null) : ObservableObject
{
    /// <summary>How long the app waits for Sindri to answer after a start.</summary>
    internal static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

    private const string AlreadyRunningCode = "SND-LEASE-001";
    private readonly ILogger _log = log ?? NullLogger.Instance;
    private SynchronizationContext? _ui;
    private bool _watching;
    private int _attempt;
    private int _launch;
    private bool _begun;

    /// <summary>Which state shows.</summary>
    public StartStage Stage { get; private set => Set(ref field, value); }

    /// <summary>True when there is a starter to use.</summary>
    public bool CanStart { get; } = starter is not null;

    /// <summary>The line under the title, or empty when the page's own reason shows.</summary>
    public string Message { get; private set => Set(ref field, value); } = starter is null ? string.Empty : Labels.StartSentence;

    /// <summary>True when <see cref="Message"/> has words.</summary>
    public bool HasMessage => Message.Length > 0;

    /// <summary>True while the card says why the start failed: the Not running line (no endpoint record) would answer the same question a second time.</summary>
    public bool HidesNotRunningReason => false;   // stub (red)

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
        if (!_watching)
        {
            // The program's exit arrives on a pool thread: take the UI context now, on the UI thread, to answer on it.
            _ui = SynchronizationContext.Current;
            starter.Exited += OnProgramExited;
            _watching = true;
        }

        var attempt = ++_attempt;
        SindriStartLog.Began(_log, attempt);
        var result = starter.Start();
        _launch = starter.Launch;
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

    // The started program ended while the page waits: say why now, not after the 30 s.
    private void OnProgramExited(object? sender, SindriExitedEventArgs e)
    {
        if (_ui is { } ui)
        {
            ui.Post(static state => { var (model, args) = ((SindriStartViewModel, SindriExitedEventArgs))state!; model.ProgramExited(args); }, (this, e));   // why: Post's state is the object it was handed
        }
        else
        {
            ProgramExited(e);
        }
    }

    // The current start's program ended while the page waits: say why now, with its exit code. An older launch's exit changes nothing.
    private void ProgramExited(SindriExitedEventArgs e)
    {
        SindriStartLog.ProgramExited(_log, e.Launch, e.ExitCode, _attempt, Stage);
        if (Stage == StartStage.Starting && e.Launch == _launch)
        {
            Ended(_attempt, e.ExitCode);
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

        SindriStartLog.WaitEnded(_log, attempt, _attempt, Stage);
        Ended(attempt);
    }

    // The wait is over for this attempt (the 30 s ran out or the program ended): nothing answered, so say why. A later call for a start that already ended does nothing.
    private void Ended(int attempt, int? exitCode = null)
    {
        if (attempt != _attempt || starter is null || Stage != StartStage.Starting)
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

        var said = StartError.Reason(error);
        Show(StartStage.DidntStart, exitCode is { } code ? Labels.DidntStartExit(said, code) : Labels.DidntStart(said), Labels.StartDetails(starter.LogPath), Labels.Retry);
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
