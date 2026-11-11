using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Memory;

/// <summary>POST /sindri/memories/{name}/facts: which fact and what happened to it.</summary>
/// <param name="Key">The fact's key (on the wire: 'id').</param>
/// <param name="Action">'written' or 'refreshed'.</param>
internal sealed record MemoryFactWritten([property: JsonPropertyName("id")] string Key, string Action);
