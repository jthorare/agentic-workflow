namespace Aw.Context.Infrastructure.Docs;

/// <summary>What listing a library root came to: its markdown files, or why it could not be listed.</summary>
/// <param name="Files">The files, in path order; empty when <paramref name="Problem"/> is set.</param>
/// <param name="Problem">Why the root could not be listed, or null.</param>
internal sealed record LibraryListing(IReadOnlyList<LibraryFile> Files, string? Problem);
