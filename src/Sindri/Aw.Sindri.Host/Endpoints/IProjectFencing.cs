using Aw.Sindri.Features.GetProject;
using Aw.Sindri.Features.ListProjects;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// Writes a Projects answer for a session caller with the Linear-sourced text inside the untrusted fence (projects page spec §6.2 "Untrusted text"). A port
/// because the fence lives in the MCP namespace, which already depends on this one (ARCH-CYCLE-001); the Host registers the MCP fence behind it.
/// </summary>
internal interface IProjectFencing
{
    /// <summary>The page as a session reads it.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The JSON answer with the Linear text fenced.</returns>
    public IResult Page(ListProjectsResponse page);

    /// <summary>One project as a session reads it.</summary>
    /// <param name="project">The project.</param>
    /// <returns>The JSON answer with the Linear text fenced.</returns>
    public IResult One(GetProjectResponse project);
}
