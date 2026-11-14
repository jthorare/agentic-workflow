namespace Aw.Sindri.Ui.Terminal;

/// <summary>POST /sindri/sessions/{provider}/{id}/stop (requested of the api lane; not on main yet): stops a session Sindri started, by its recorded pid. Sent once, human token only.</summary>
internal interface ISessionStopper
{
    /// <summary>Stops the session.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="provider">The session's provider.</param>
    /// <param name="id">The session's id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The HTTP status Serve answered; 0 when Sindri could not be reached. 2xx is accepted; 404 means this Sindri has no stop route yet.</returns>
    public Task<int> Stop(Uri api, string provider, string id, CancellationToken cancellationToken);
}
