namespace Aw.Sindri.Ui.Terminal;

/// <summary>
/// What the pane header does to a session Sindri started (#3023, #3334): Stop and Interrupt. Assumed routes until the api lane's are on main:
/// Stop is <c>POST /sindri/sessions/{provider}/{id}/ops</c> with the op "stop" (SIGTERM to the recorded pid, then kill-session on a one-off), and Interrupt is one
/// terminal-socket <c>key</c> message, <c>C-c</c> (§15.2 foreign list). The drawing never starts a process; both go through Serve.
/// </summary>
internal interface ISessionControl
{
    /// <summary>Stops the session.</summary>
    /// <param name="sessionRef">The row's ref, provider/id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when Serve accepted the stop.</returns>
    public Task<bool> Stop(string sessionRef, CancellationToken cancellationToken);

    /// <summary>Sends Ctrl-C to the session's pane.</summary>
    /// <param name="sessionRef">The row's ref, provider/id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when the key was sent.</returns>
    public Task<bool> Interrupt(string sessionRef, CancellationToken cancellationToken);
}
