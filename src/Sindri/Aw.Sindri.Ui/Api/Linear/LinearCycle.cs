namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>A cycle a team runs.</summary>
/// <param name="Id">The cycle's Linear id.</param>
/// <param name="Number">The cycle number.</param>
/// <param name="Name">The cycle's name.</param>
/// <param name="StartsAt">When it starts.</param>
/// <param name="EndsAt">When it ends.</param>
/// <param name="Current">True for the cycle running now.</param>
internal sealed record LinearCycle(string Id, int Number, string Name, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool Current);
