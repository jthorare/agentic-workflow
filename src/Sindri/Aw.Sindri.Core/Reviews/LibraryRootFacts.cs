namespace Aw.Sindri.Core.Reviews;

/// <summary>What is known about one library root, before the page orders and words it.</summary>
/// <param name="Name">The root's name: <c language="text">aw</c>, the vault's name or <c language="text">repo:&lt;owner&gt;/&lt;name&gt;</c>.</param>
/// <param name="Kind">Which kind of root it is.</param>
/// <param name="Path">The folder, home written <c language="text">~</c>.</param>
/// <param name="Repo">The GitHub repo (<c language="text">owner/name</c>) of a repo root; null for the others.</param>
/// <param name="Deny">The vault's own deny globs.</param>
/// <param name="Off">Whether the profile turned the root off (only the <c language="text">aw</c> root can be).</param>
/// <param name="ScannedAt">When the last pass over the root finished; null before the first.</param>
/// <param name="Docs">The design docs the root holds.</param>
/// <param name="Errors">How many of them carry an error.</param>
/// <param name="Unreadable">Why the root can't be read; null when it can.</param>
public sealed record LibraryRootFacts(string Name, LibraryRootKind Kind, string Path, string? Repo, IReadOnlyList<string> Deny, bool Off, DateTimeOffset? ScannedAt, int Docs, int Errors, string? Unreadable);
