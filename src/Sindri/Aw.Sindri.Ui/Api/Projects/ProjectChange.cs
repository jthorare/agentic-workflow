using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Projects;

/// <summary>POST /sindri/projects/{id}/change's body (projects page spec §5.3): the version the sheet showed and what to change. A field left out stays as it was, so nulls and false flags are not written: a led card takes notes only, and any other field on it is SND-PROJECT-004.</summary>
/// <param name="Version">The card's version as the page showed it.</param>
/// <param name="Name">A new name (hand-made projects only), or null.</param>
/// <param name="State">A new state word ("backlog", "planned", "inProgress", "paused", "done", "canceled"), or null.</param>
/// <param name="ClearState">Drops the owner's state, back to Linear's.</param>
/// <param name="Links">The new whole list of links, or null.</param>
/// <param name="Target">A new target date (yyyy-MM-dd), or null.</param>
/// <param name="ClearTarget">Drops the target date.</param>
/// <param name="Notes">New notes, or null.</param>
/// <param name="ClearNotes">Drops the notes.</param>
internal sealed record ProjectChange(
    long Version,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Name = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? State = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool ClearState = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Links = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Target = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool ClearTarget = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Notes = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool ClearNotes = false);
