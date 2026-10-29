using System.Text.Json.Serialization;
using Aw.Kernel;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Core.Projects.Vocabulary;
using Aw.Sindri.Features.AttachProject;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/projects: <see cref="AttachProjectRequest"/>'s fields, plus the old name of the notes field for one release.</summary>
/// <param name="Linear">A Linear project key ('linear:project/&lt;id&gt;') or url, or null for a hand-made project.</param>
/// <param name="Name">The name; required when <paramref name="Linear"/> is null.</param>
/// <param name="State">The owner's state, or null.</param>
/// <param name="Links">The links, or null.</param>
/// <param name="Notes">The free-form notes, or null.</param>
/// <param name="Target">The target date, or null.</param>
/// <param name="Note">Alias of <paramref name="Notes"/>; removed one release after notes ships (owner q158).</param>
internal sealed record AttachProjectBody(
    string? Linear = null,
    string? Name = null,
    ProjectState? State = null,
    IReadOnlyList<string>? Links = null,
    string? Notes = null,
    DateOnly? Target = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Note = null)
{
    /// <summary>The request; SND-PROJECT-004 when 'notes' and 'note' disagree.</summary>
    /// <returns>The request, or the refusal.</returns>
    internal Result<AttachProjectRequest> ToRequest() =>
        ProjectDecisions.NotesAliasConflict(Notes, Note) is { } conflict
            ? conflict
            : new AttachProjectRequest(Linear, Name, State, Links, Notes ?? Note, Target);
}
