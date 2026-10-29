using Aw.Kernel;
using Aw.Sindri.Ui.Api.Projects;

namespace Aw.Sindri.Ui.Api;

/// <summary>The project reads and writes of the API (<see cref="ISindriApi"/>, continued; projects page spec §6.2).</summary>
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

    /// <summary>POST /sindri/projects/{id}/change (projects page spec §5.3, desk): edit, set or clear the state, or save the notes. A led card takes notes only.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="id">A "prj_…" id, or a led project's "linear:project/&lt;uuid&gt;" key (its notes).</param>
    /// <param name="change">The version the page showed and what to change.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The accepted write; SND-PROJECT-409 when the card changed since, -004 for a field the card doesn't take, -404; or SND-UI-002/003.</returns>
    public Task<Result<ProjectAcceptedView>> ChangeProject(Uri api, string id, ProjectChange change, CancellationToken cancellationToken);

    /// <summary>POST /sindri/projects/{id}/detach (projects page spec §5.3, desk): remove an attached or hand-made project from the page.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="id">The "prj_…" id.</param>
    /// <param name="detach">The version the page showed.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The accepted write; SND-PROJECT-409 or -404; or SND-UI-002/003.</returns>
    public Task<Result<ProjectAcceptedView>> DetachProject(Uri api, string id, ProjectDetach detach, CancellationToken cancellationToken);

    /// <summary>POST /sindri/projects/hide (projects page spec §5.3, desk): hide or unhide a led project.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="hide">The key, the direction and the hide version.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The accepted write; SND-PROJECT-409 or -404; or SND-UI-002/003.</returns>
    public Task<Result<ProjectAcceptedView>> HideProject(Uri api, ProjectHide hide, CancellationToken cancellationToken);
}
