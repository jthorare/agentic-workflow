namespace Aw.Sindri.Core.Projects;

/// <summary>A Linear project the owner named: a key, or the slug id of a url (the key behind it is in the store).</summary>
/// <param name="Key">The 'linear:project/&lt;id&gt;' key, when the owner typed one.</param>
/// <param name="Slug">The id at the end of a project url's slug, when the owner typed a url.</param>
public sealed record LinearProjectRef(string? Key, string? Slug);
