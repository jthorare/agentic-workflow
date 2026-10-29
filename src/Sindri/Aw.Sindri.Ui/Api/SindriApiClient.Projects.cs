using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api.Projects;

namespace Aw.Sindri.Ui.Api;

/// <summary>The project calls (<see cref="SindriApiClient"/>, continued; projects page spec §6.2): the list, one card and the writes.</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<ProjectList>> Projects(Uri api, bool all, CancellationToken cancellationToken) =>
        Get<ProjectList>(api, ProjectRoutes.List(all), cancellationToken, view: true);

    /// <inheritdoc />
    public Task<Result<ProjectOne>> Project(Uri api, string id, CancellationToken cancellationToken) =>
        Get<ProjectOne>(api, ProjectRoutes.One(id), cancellationToken, view: true);

    /// <inheritdoc />
    public Task<Result<ProjectAcceptedView>> ChangeProject(Uri api, string id, ProjectChange change, CancellationToken cancellationToken) =>
        Call<ProjectAcceptedView>(api, HttpMethod.Post, ProjectRoutes.Change(id), cancellationToken, new CallOptions(Content: AwJson.Serialize(change, Json)));

    /// <inheritdoc />
    public Task<Result<ProjectAcceptedView>> DetachProject(Uri api, string id, ProjectDetach detach, CancellationToken cancellationToken) =>
        Call<ProjectAcceptedView>(api, HttpMethod.Post, ProjectRoutes.Detach(id), cancellationToken, new CallOptions(Content: AwJson.Serialize(detach, Json)));

    /// <inheritdoc />
    public Task<Result<ProjectAcceptedView>> HideProject(Uri api, ProjectHide hide, CancellationToken cancellationToken) =>
        Call<ProjectAcceptedView>(api, HttpMethod.Post, ProjectRoutes.Hide, cancellationToken, new CallOptions(Content: AwJson.Serialize(hide, Json)));

    /// <inheritdoc />
    public Task<Result<ProjectAcceptedView>> AttachProject(Uri api, ProjectAttach attach, CancellationToken cancellationToken) =>
        Call<ProjectAcceptedView>(api, HttpMethod.Post, ProjectRoutes.Attach, cancellationToken, new CallOptions(Content: AwJson.Serialize(attach, Json)));
}
