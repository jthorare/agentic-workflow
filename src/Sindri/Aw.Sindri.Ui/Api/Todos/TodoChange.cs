using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Todos;

/// <summary>POST /sindri/todos/{id}/change's body (todo spec §4.4): the version the row showed and what to change. A field left out stays as it was.</summary>
/// <param name="Version">The todo's version as the row showed it.</param>
/// <param name="Text">A new text, or null.</param>
/// <param name="Due">A new due day as yyyy-MM-dd; "" clears it; null leaves it.</param>
/// <param name="Links">The new whole list of link refs; empty clears; null leaves it.</param>
internal sealed record TodoChange(
    long Version,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Text = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Due = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Links = null);
