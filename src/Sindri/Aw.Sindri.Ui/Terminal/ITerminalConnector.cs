namespace Aw.Sindri.Ui.Terminal;

/// <summary>Opens the terminal WebSocket of a session: GET /sindri/sessions/{provider}/{id}/terminal, the human token in the first message (never the URL).</summary>
internal interface ITerminalConnector
{
    /// <summary>Connects, sends hello and returns the link.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="provider">The session's provider.</param>
    /// <param name="id">The session's id.</param>
    /// <param name="cols">The columns the view fits.</param>
    /// <param name="rows">The rows the view fits.</param>
    /// <param name="cancellationToken">Cancels the connect.</param>
    /// <returns>The link; null when the socket could not open (the HTTP refusal before the upgrade, SND-AGENT-015 or 016, is the close code 4015 or 4016 on a link that is already closed).</returns>
    public Task<ITerminalLink?> Connect(Uri api, string provider, string id, int cols, int rows, CancellationToken cancellationToken);
}
