using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>Binds the project routes' raw values (P1 binding rule): a '{projectId}' segment, a 400 SND-CLI-003 when it isn't a project id.</summary>
internal static class ProjectRoute
{
    /// <summary>The project in a '{projectId}' segment.</summary>
    /// <param name="projectId">The segment.</param>
    /// <returns>The id, or SND-CLI-003.</returns>
    internal static Result<ProjectId> Project(string projectId) =>
        ProjectId.TryFrom(Segment(projectId), out var id) ? id : SindriErrors.InvalidInput.With("'" + SafeText.Line(SafeText.Escape(projectId), 80) + "' isn't a project id (prj_…)");

    /// <summary>The project a '{project}' or '{projectId}' segment names. A led project's key is "linear:project/&lt;uuid&gt;", sent escaped as one segment, and ASP.NET keeps an escaped '/' as "%2F" in a route value, so the segment is unescaped here.</summary>
    /// <param name="segment">The segment as routed.</param>
    /// <returns>The unescaped id or key.</returns>
    internal static string Segment(string segment) => Uri.UnescapeDataString(segment);
}
