using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>The body of POST /sindri/linear/issues; the optional fields are left out when null.</summary>
/// <param name="ClientId">The app's uuid; a retry sends the same one.</param>
/// <param name="Team">The team key (ENG).</param>
/// <param name="Title">The title, 1 to 255 characters.</param>
/// <param name="Description">The description, or null.</param>
/// <param name="StateId">A state of the team's stored lookups, or null for Linear's default.</param>
/// <param name="AssigneeId">A member, or null for Unassigned.</param>
/// <param name="Priority">0 (none) to 4, or null.</param>
/// <param name="ProjectId">A project, or null.</param>
/// <param name="CycleId">A cycle, or null.</param>
/// <param name="LabelIds">The labels, or null.</param>
/// <param name="ParentKey">The parent issue's key, or null.</param>
internal sealed record LinearCreateBody(
    string ClientId,
    string Team,
    string Title,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Description = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? StateId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? AssigneeId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Priority = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ProjectId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CycleId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? LabelIds = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ParentKey = null);
