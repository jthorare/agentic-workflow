using Aw.Kernel;
using Aw.Sindri.Ui.Api.Projects;

namespace Aw.Sindri.Ui.Api;

/// <summary>The project reads of the API (<see cref="ISindriApi"/>, continued; projects page spec §6.2).</summary>
internal partial interface ISindriApi
{
    /// <summary>GET /sindri/projects[?all=true] (projects page spec §6.2): the project cards in three groups. A view read: it waits on the views.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="all">True to fill the hidden group too (?all=true).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The list, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<ProjectList>> Projects(Uri api, bool all, CancellationToken cancellationToken);

    /// <summary>GET /sindri/projects/{id} (projects page spec §6.2): one card, with the viewer, the freshness and whether it is hidden. A view read.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="id">A "prj_…" id or a "linear:project/&lt;uuid&gt;" key.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The card, the API's refusal for an unknown project, or SND-UI-002/003.</returns>
    public Task<Result<ProjectOne>> Project(Uri api, string id, CancellationToken cancellationToken);
}
