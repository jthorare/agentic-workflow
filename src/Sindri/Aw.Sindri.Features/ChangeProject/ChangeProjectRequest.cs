using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Projects;

namespace Aw.Sindri.Features.ChangeProject;

/// <summary>Change an attached or hand-made project at the version the owner saw (POST /sindri/projects/{projectId}/change, 'sindri project edit'; projects page spec §5.3): desk class, appends 'project.changed'; for a Linear project the owner leads, only its notes change and 'project.led.noted' is appended (owner q158).</summary>
/// <param name="Project">The project's 'prj_…' id, or a led project's Linear key ('linear:project/&lt;id&gt;').</param>
/// <param name="Version">The version the owner saw; a stale one is SND-PROJECT-409.</param>
/// <param name="Edit">What to change.</param>
public sealed record ChangeProjectRequest(string Project, long Version, ProjectEdit Edit) : ICommand<ProjectAccepted>;
