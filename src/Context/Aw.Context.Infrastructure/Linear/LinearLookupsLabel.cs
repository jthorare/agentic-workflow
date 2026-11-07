namespace Aw.Context.Infrastructure.Linear;

/// <summary>A label in the Lookups operation.</summary>
/// <param name="Id">The Id Linear serves.</param>
/// <param name="Name">The Name Linear serves.</param>
/// <param name="Color">The Color Linear serves, '#eb5757'.</param>
internal sealed record LinearLookupsLabel(string Id, string? Name = null, string? Color = null);
