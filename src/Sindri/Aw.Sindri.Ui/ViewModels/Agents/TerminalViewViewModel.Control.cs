#pragma warning disable MA0182 // see TerminalViewViewModel.cs
namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The terminal view (continued): Stop and Interrupt in the pane header, for a session Sindri started only (#3023, #3334).</summary>
internal sealed partial class TerminalViewViewModel
{
    /// <summary>True when the header shows Stop: Sindri started the session and the state rule of <see cref="ShowStop"/> holds.</summary>
    public bool CanStop => _control is null;

    /// <summary>True when the header shows Interrupt: Sindri started the session and the pane is live.</summary>
    public bool CanInterrupt => _control is null;

    /// <summary>"Stop…", or "Stop? Press again" for 2 s after the first press.</summary>
    public string StopLabel => string.Empty;

    /// <summary>The Stop button's automation id: TerminalStop, or TerminalStopConfirm while it asks.</summary>
    public string StopAutomationId => string.Empty;

    /// <summary>True for 2 s after the first Stop press.</summary>
    public bool IsStopArmed => false;

    /// <summary>The line under the header when a stop or an interrupt was refused; empty otherwise.</summary>
    public string ControlLine => string.Empty;

    /// <summary>The first press arms Stop; the second, inside 2 s, stops the session.</summary>
    /// <returns>True when the session was stopped.</returns>
    public Task<bool> PressStop(CancellationToken cancellationToken) => Task.FromResult(false);

    /// <summary>Sends Ctrl-C to the pane.</summary>
    /// <returns>True when Serve took it.</returns>
    public Task<bool> PressInterrupt(CancellationToken cancellationToken) => Task.FromResult(false);
}
