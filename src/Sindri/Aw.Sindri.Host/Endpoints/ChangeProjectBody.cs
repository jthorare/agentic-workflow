using System.Text.Json.Serialization;
using Aw.Kernel;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Core.Projects.Vocabulary;
using Aw.Sindri.Features.ChangeProject;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/projects/{projectId}/change: the version the owner saw and what to change; the project comes from the route. A field left out stays as it was; a 'clear' flag drops it.</summary>
/// <param name="Version">The version the owner saw (required: a missing one is a 400).</param>
/// <param name="Name">A new name (hand-made projects only), or null.</param>
/// <param name="State">A new state, or null.</param>
/// <param name="ClearState">Drops the owner's state.</param>
/// <param name="Links">The new whole list of links, or null.</param>
/// <param name="Target">A new target date, or null.</param>
/// <param name="ClearTarget">Drops the target date.</param>
/// <param name="Notes">New notes, or null.</param>
/// <param name="ClearNotes">Drops the notes.</param>
/// <param name="Note">Alias of <paramref name="Notes"/>; removed one release after notes ships (owner q158).</param>
/// <param name="ClearNote">Alias of <paramref name="ClearNotes"/>; removed with <paramref name="Note"/>.</param>
internal sealed record ChangeProjectBody(
    long Version,
    string? Name = null,
    ProjectState? State = null,
    bool ClearState = false,
    IReadOnlyList<string>? Links = null,
    DateOnly? Target = null,
    bool ClearTarget = false,
    string? Notes = null,
    bool ClearNotes = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Note = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool ClearNote = false)
{
    /// <summary>The request for <paramref name="project"/>.</summary>
    /// <param name="project">The route's project: a prj_ id, or a led project's Linear key.</param>
    /// <returns>The request, or SND-PROJECT-004 when 'notes' and 'note' disagree.</returns>
    internal Result<ChangeProjectRequest> ToRequest(string project) =>
        ProjectDecisions.NotesAliasConflict(Notes, Note) is { } conflict
            ? conflict
            : new ChangeProjectRequest(project, Version, Edit());

    private ProjectEdit Edit() => new(Name, State, ClearState, Links, Notes ?? Note, ClearNotes || ClearNote, Target, ClearTarget);
}
