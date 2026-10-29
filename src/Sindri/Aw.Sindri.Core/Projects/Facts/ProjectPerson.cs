namespace Aw.Sindri.Core.Projects.Facts;

/// <summary>A Linear user: the owner or a project's lead.</summary>
/// <param name="Ref">'linear:user/&lt;id&gt;'.</param>
/// <param name="Name">The display name (from Linear: untrusted), or null.</param>
public sealed record ProjectPerson(string Ref, string? Name);
