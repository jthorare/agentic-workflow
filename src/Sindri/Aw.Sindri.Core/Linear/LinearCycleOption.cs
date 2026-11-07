using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Linear;

/// <summary>A cycle in a team's lookups.</summary>
/// <param name="Uuid">Linear's id.</param>
/// <param name="Number">The cycle's number in its team.</param>
/// <param name="Name">Its name; 'Cycle N' when Linear gave none.</param>
/// <param name="StartsAt">When it starts.</param>
/// <param name="EndsAt">When it ends.</param>
/// <param name="Current">Whether it is the running cycle now.</param>
public sealed record LinearCycleOption([property: JsonPropertyName("id")] string Uuid, int Number, string Name, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool Current);
