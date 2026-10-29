using Aw.Sindri.Core.Ids;
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
/// <param name="Note">A new note, or null.</param>
/// <param name="ClearNote">Drops the note.</param>
/// <param name="Target">A new target date, or null.</param>
/// <param name="ClearTarget">Drops the target date.</param>
internal sealed record ChangeProjectBody(
    long Version,
    string? Name = null,
    ProjectState? State = null,
    bool ClearState = false,
    IReadOnlyList<string>? Links = null,
    string? Note = null,
    bool ClearNote = false,
    DateOnly? Target = null,
    bool ClearTarget = false)
{
    /// <summary>The request for <paramref name="project"/>.</summary>
    /// <param name="project">The route's project.</param>
    /// <returns>The request.</returns>
    internal ChangeProjectRequest ToRequest(ProjectId project) =>
        new(project, Version, new ProjectEdit(Name, State, ClearState, Links, Note, ClearNote, Target, ClearTarget));
}
