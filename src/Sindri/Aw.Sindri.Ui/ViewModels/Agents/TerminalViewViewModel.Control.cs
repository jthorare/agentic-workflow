#pragma warning disable MA0182 // see TerminalViewViewModel.cs
namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The terminal view (continued): Stop and Interrupt in the pane header, for a session Sindri started only (#3023, #3334).</summary>
internal sealed partial class TerminalViewViewModel
{
    private static readonly TimeSpan _stopWindow = TimeSpan.FromSeconds(2);

    private DateTimeOffset? _armedUntil;

    /// <summary>Raised on the first Stop press: the view calls <see cref="Tick"/> after the window so the label goes back.</summary>
    internal event EventHandler? StopArmed;

    /// <summary>How long the first Stop press waits for the second (#3023).</summary>
    internal static TimeSpan StopWindow => _stopWindow;

    /// <summary>True when the header shows Stop: Sindri started the session and the state rule of <see cref="ShowStop"/> holds.</summary>
    public bool CanStop => ShowStop && Target is { StartedBySindri: true };

    /// <summary>True when the header shows Interrupt: Sindri started the session and the pane is live.</summary>
    public bool CanInterrupt => Target is { StartedBySindri: true } && State is TerminalState.Attached or TerminalState.Starting;

    /// <summary>True for 2 s after the first Stop press.</summary>
    public bool IsStopArmed => _armedUntil is { } until && Clock.GetUtcNow() < until;

    /// <summary>"Stop…", or "Stop? Press again" for 2 s after the first press.</summary>
    public string StopLabel => IsStopArmed ? Labels.TerminalStopAgain : Labels.TerminalStop;

    /// <summary>The Stop button's automation id: TerminalStop, or TerminalStopConfirm while it asks.</summary>
    public string StopAutomationId => IsStopArmed ? "TerminalStopConfirm" : "TerminalStop";

    /// <summary>The Interrupt button's label.</summary>
    public static string InterruptLabel => Labels.TerminalInterrupt;

    /// <summary>The line under the header when a stop or an interrupt was refused; empty otherwise.</summary>
    public string ControlLine { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The first press arms Stop; the second, inside 2 s, stops the session.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when the session was stopped.</returns>
    public async Task<bool> PressStop(CancellationToken cancellationToken)
    {
        if (!CanStop || Target is not { } target)
        {
            return false;
        }

        if (!IsStopArmed)
        {
            _armedUntil = Clock.GetUtcNow() + _stopWindow;
            ControlLine = string.Empty;
            Refresh();
            Raise(StopArmed);
            return false;
        }

        _armedUntil = null;
        var stopped = _control is not null && await _control.Stop(target.Key, cancellationToken).ConfigureAwait(false);
        ControlLine = stopped ? string.Empty : _control is PaneControl { Failure.Length: > 0 } pane ? Labels.TerminalStopFailed[..^1] + " (" + pane.Failure + ")." : Labels.TerminalStopFailed;
        Refresh();
        return stopped;
    }

    /// <summary>Sends Ctrl-C to the pane.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when Serve took it.</returns>
    public async Task<bool> PressInterrupt(CancellationToken cancellationToken)
    {
        if (!CanInterrupt || Target is not { } target)
        {
            return false;
        }

        var sent = _control is not null && await _control.Interrupt(target.Key, cancellationToken).ConfigureAwait(false);
        ControlLine = sent ? string.Empty : Labels.TerminalInterruptFailed;
        Refresh();
        return sent;
    }
}
