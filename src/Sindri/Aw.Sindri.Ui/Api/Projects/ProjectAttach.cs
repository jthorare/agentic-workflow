using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Projects;

/// <summary>POST /sindri/projects' body (projects page spec §5.3): a Linear project to attach, or a hand-made project's fields. A field left out is not written.</summary>
/// <param name="Linear">A Linear project key ('linear:project/&lt;id&gt;') or url to attach, or null for a hand-made project.</param>
/// <param name="Name">The name; required when <paramref name="Linear"/> is null.</param>
/// <param name="State">The owner's state word ("backlog", "planned", "inProgress", "paused", "done", "canceled"), or null.</param>
/// <param name="Links">The links, or null.</param>
/// <param name="Notes">The owner's notes, or null.</param>
/// <param name="Target">The target date (yyyy-MM-dd), or null.</param>
internal sealed record ProjectAttach(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Linear = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Name = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? State = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Links = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Notes = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Target = null);
