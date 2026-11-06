namespace Aw.Context.Infrastructure.Linear;

/// <summary>A cycle in the Lookups operation.</summary>
/// <param name="Id">The Id Linear serves.</param>
/// <param name="Number">The Number Linear serves.</param>
/// <param name="Name">The Name Linear serves.</param>
/// <param name="StartsAt">The StartsAt Linear serves.</param>
/// <param name="EndsAt">The EndsAt Linear serves.</param>
internal sealed record LinearLookupsCycle(string Id, int Number, string? Name, DateTimeOffset StartsAt, DateTimeOffset EndsAt);
