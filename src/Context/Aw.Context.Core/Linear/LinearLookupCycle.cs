using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Linear;

/// <summary>A cycle of a team, for the cycle picker.</summary>
/// <param name="Id">Linear's id.</param>
/// <param name="Number">The cycle's number in its team.</param>
/// <param name="Name">Its name, or null (Linear shows 'Cycle 12' then).</param>
/// <param name="StartsAt">When it starts.</param>
/// <param name="EndsAt">When it ends.</param>
public sealed record LinearLookupCycle(ExternalId Id, int Number, string? Name, DateTimeOffset StartsAt, DateTimeOffset EndsAt);
