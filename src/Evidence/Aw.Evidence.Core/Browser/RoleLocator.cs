namespace Aw.Evidence.Core.Browser;

/// <summary>An element by ARIA role and exact accessible name. Several matches are an error unless <paramref name="Nth"/> picks one.</summary>
/// <param name="Role">The lower-case ARIA role ('button').</param>
/// <param name="Name">The exact accessible name.</param>
/// <param name="Nth">Which match, from 1; null needs exactly one.</param>
public sealed record RoleLocator(string Role, string Name, int? Nth) : Locator;
