namespace Aw.Context.Infrastructure.Linear;

/// <summary>The data of the Lookups operation.</summary>
/// <param name="Teams">The Teams Linear serves.</param>
internal sealed record LinearLookupsData(LinearConnection<LinearLookupsTeam> Teams);
