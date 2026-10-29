using Aw.Kernel;
using Aw.Sindri.Ui.Api.Projects;

namespace Aw.Sindri.Ui.Api;

/// <summary>The project calls (<see cref="SindriApiClient"/>, continued; projects page spec §6.2): the list and one card.</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<ProjectList>> Projects(Uri api, bool all, CancellationToken cancellationToken) =>
        throw new NotSupportedException(ProjectRoutes.List(all));

    /// <inheritdoc />
    public Task<Result<ProjectOne>> Project(Uri api, string id, CancellationToken cancellationToken) =>
        throw new NotSupportedException(ProjectRoutes.One(id));
}
