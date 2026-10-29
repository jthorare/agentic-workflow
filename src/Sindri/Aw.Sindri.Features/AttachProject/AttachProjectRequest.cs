using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Core.Projects.Vocabulary;

namespace Aw.Sindri.Features.AttachProject;

/// <summary>Attach a Linear project to the Projects page, or add one by hand (POST /sindri/projects, 'sindri project add'; projects page spec §5.3): desk class, appends 'project.attached'.</summary>
/// <param name="Linear">A Linear project key ('linear:project/&lt;id&gt;') or url, or null for a hand-made project.</param>
/// <param name="Name">The name; required when <paramref name="Linear"/> is null.</param>
/// <param name="State">The owner's state, or null.</param>
/// <param name="Links">The links (https, no query or fragment, at most 10), or null.</param>
/// <param name="Notes">The free-form notes (at most 16,384 characters, line breaks allowed), or null.</param>
/// <param name="Target">The target date, or null.</param>
public sealed record AttachProjectRequest(string? Linear = null, string? Name = null, ProjectState? State = null, IReadOnlyList<string>? Links = null, string? Notes = null, DateOnly? Target = null)
    : ICommand<ProjectAccepted>;
