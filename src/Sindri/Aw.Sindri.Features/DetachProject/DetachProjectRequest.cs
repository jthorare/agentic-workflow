using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Projects;

namespace Aw.Sindri.Features.DetachProject;

/// <summary>Remove an attached or hand-made project at the version the owner saw (POST /sindri/projects/{projectId}/detach, 'sindri project remove'; projects page spec §5.3): desk class, appends 'project.detached'.</summary>
/// <param name="Project">The project.</param>
/// <param name="Version">The version the owner saw; a stale one is SND-PROJECT-409.</param>
public sealed record DetachProjectRequest(ProjectId Project, long Version) : ICommand<ProjectAccepted>;
