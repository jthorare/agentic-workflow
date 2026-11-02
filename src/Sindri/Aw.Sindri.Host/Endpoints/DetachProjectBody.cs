using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.DetachProject;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/projects/{projectId}/detach: '{version}'; the project comes from the route.</summary>
/// <param name="Version">The version the owner saw (required: a missing one is a 400).</param>
internal sealed record DetachProjectBody(long Version)
{
    /// <summary>The request for <paramref name="project"/>.</summary>
    /// <param name="project">The route's project.</param>
    /// <returns>The request.</returns>
    internal DetachProjectRequest ToRequest(ProjectId project) => new(project, Version);
}
