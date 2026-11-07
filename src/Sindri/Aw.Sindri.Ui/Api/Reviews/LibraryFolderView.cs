namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>One folder of the design library (GET /sindri/library's roots, review page spec §6.3): the aw folder, a vault the owner added, or a repo's docs.</summary>
/// <param name="Name">Its name: "aw", a vault's own, or "repo:owner-name".</param>
/// <param name="Kind">"aw", "vault" or "repo".</param>
/// <param name="Path">The folder on disk.</param>
/// <param name="Scope">A repo root's "owner/name · docs/**"; null for the others.</param>
/// <param name="On">False only for the aw folder turned off.</param>
/// <param name="Deny">Patterns the folder doesn't read.</param>
/// <param name="ScannedAt">When it was last read; null until its first read ("Reading the folder…").</param>
/// <param name="Docs">The docs read from it.</param>
/// <param name="Errors">The docs read with an error.</param>
/// <param name="Unreadable">Why it can't be read; null when it can.</param>
internal sealed record LibraryFolderView(string Name, string Kind, string Path, string? Scope, bool On, IReadOnlyList<string> Deny, DateTimeOffset? ScannedAt, int Docs, int Errors, string? Unreadable);
