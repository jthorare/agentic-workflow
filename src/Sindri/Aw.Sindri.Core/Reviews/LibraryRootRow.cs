namespace Aw.Sindri.Core.Reviews;

/// <summary>One library root as Settings → Library shows it.</summary>
/// <param name="Name">The root's name.</param>
/// <param name="Kind">Which kind: <c language="text">aw</c>, <c language="text">vault</c> or <c language="text">repo</c>.</param>
/// <param name="Path">The folder, home written <c language="text">~</c>.</param>
/// <param name="Scope">A repo root's <c language="text">owner/name · docs/**</c>; null for the others.</param>
/// <param name="On">False only for the <c language="text">aw</c> root while it is turned off.</param>
/// <param name="Deny">The vault's own deny globs.</param>
/// <param name="ScannedAt">When the last pass finished; null while the root has not been read yet.</param>
/// <param name="Docs">The design docs the root holds.</param>
/// <param name="Errors">How many of them carry an error.</param>
/// <param name="Unreadable">Why the root can't be read; null when it can.</param>
public sealed record LibraryRootRow(string Name, string Kind, string Path, string? Scope, bool On, IReadOnlyList<string> Deny, DateTimeOffset? ScannedAt, int Docs, int Errors, string? Unreadable);
