using Aw.Sindri.Features.ListLinearIssues;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// Writes the Linear issue list for a session caller with the Linear-typed text inside the untrusted fence (Linear hub spec §3). A port for the same reason as <see cref="IProjectFencing"/>:
/// the fence lives in the MCP namespace, which already depends on this one (ARCH-CYCLE-001).
/// </summary>
internal interface ILinearIssueFencing
{
    /// <summary>The list as a session reads it.</summary>
    /// <param name="list">The list.</param>
    /// <returns>The JSON answer with titles and the names of states, people, projects, cycles and labels fenced.</returns>
    public IResult List(ListLinearIssuesResponse list);
}
