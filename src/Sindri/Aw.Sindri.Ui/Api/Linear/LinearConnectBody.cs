using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>The body of POST /sindri/linear/connection.</summary>
/// <param name="Kind">"read" or "write".</param>
/// <param name="Key">The key.</param>
/// <param name="Teams">The team keys to read; for a read key only (left out when null).</param>
internal sealed record LinearConnectBody(string Kind, string Key, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Teams = null);
