using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Todos;

/// <summary>POST /sindri/todos's body (todo spec §4.4): a new todo.</summary>
/// <param name="Text">The text.</param>
/// <param name="Due">The due day as yyyy-MM-dd, or null for none.</param>
/// <param name="Links">The link refs; null for none.</param>
/// <param name="ClientId">One per Enter; a retry sends the same, so a lost answer makes one todo.</param>
internal sealed record TodoAdd(
    string Text,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Due,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Links,
    string ClientId);
