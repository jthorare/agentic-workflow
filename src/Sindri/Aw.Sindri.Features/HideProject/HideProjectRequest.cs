using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Projects;

namespace Aw.Sindri.Features.HideProject;

/// <summary>Hide or unhide a Linear project the owner leads (POST /sindri/projects/hide, 'sindri project hide|unhide'; projects page spec §5.3): desk class, appends 'project.hidden'.</summary>
/// <param name="Linear">The led project ('linear:project/&lt;id&gt;').</param>
/// <param name="Hidden">True to hide, false to unhide.</param>
/// <param name="Version">The hide version the owner saw (0 when it was never hidden); a stale one is SND-PROJECT-409.</param>
public sealed record HideProjectRequest(string Linear, bool Hidden, long Version) : ICommand<ProjectAccepted>;
