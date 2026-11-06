namespace Aw.Context.Infrastructure.Linear;

/// <summary>A workflow state in the Lookups operation.</summary>
/// <param name="Id">The Id Linear serves.</param>
/// <param name="Name">The Name Linear serves.</param>
/// <param name="Type">The Type Linear serves.</param>
/// <param name="Position">The Position Linear serves.</param>
internal sealed record LinearLookupsState(string Id, string Name, string Type, double Position = 0);
