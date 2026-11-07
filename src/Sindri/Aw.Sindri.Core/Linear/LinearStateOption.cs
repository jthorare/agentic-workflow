using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Linear;

/// <summary>A workflow state in a team's lookups.</summary>
/// <param name="Uuid">Linear's id.</param>
/// <param name="Name">The state's name.</param>
/// <param name="Type">backlog, unstarted, started, completed, canceled or triage.</param>
/// <param name="Position">Where Linear orders it on the board.</param>
public sealed record LinearStateOption([property: JsonPropertyName("id")] string Uuid, string Name, string Type, double Position);
