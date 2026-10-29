using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Projects;

namespace Aw.Sindri.Features.ChangeProject;

/// <summary>Change an attached or hand-made project at the version the owner saw (POST /sindri/projects/{projectId}/change, 'sindri project edit'; projects page spec §5.3): desk class, appends 'project.changed'.</summary>
/// <param name="Project">The project.</param>
/// <param name="Version">The version the owner saw; a stale one is SND-PROJECT-409.</param>
/// <param name="Edit">What to change.</param>
public sealed record ChangeProjectRequest(ProjectId Project, long Version, ProjectEdit Edit) : ICommand<ProjectAccepted>;
