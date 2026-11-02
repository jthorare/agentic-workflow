using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.GetProject;

/// <summary>One project's card in full (GET /sindri/projects/{id}; projects page spec §5.3, §6.2).</summary>
/// <param name="Project">A 'prj_…' id, a 'linear:project/&lt;id&gt;' key, or a Linear project url.</param>
public sealed record GetProjectRequest(string Project) : IQuery<GetProjectResponse>;
