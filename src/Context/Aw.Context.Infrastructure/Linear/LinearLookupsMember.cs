namespace Aw.Context.Infrastructure.Linear;

/// <summary>A team member in the Lookups operation.</summary>
/// <param name="Id">The Id Linear serves.</param>
/// <param name="Name">The Name Linear serves.</param>
/// <param name="DisplayName">The DisplayName Linear serves.</param>
/// <param name="Active">The Active Linear serves; a member who left is not offered.</param>
internal sealed record LinearLookupsMember(string Id, string Name, string? DisplayName = null, bool Active = true);
