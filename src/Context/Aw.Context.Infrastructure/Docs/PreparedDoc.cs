namespace Aw.Context.Infrastructure.Docs;

/// <summary>A document read from git and cleaned, ready to write: blocked documents carry no chunks.</summary>
/// <param name="Path">The repo-relative path.</param>
/// <param name="Blob">The blob sha.</param>
/// <param name="Title">The cleaned title.</param>
/// <param name="Blocked">True when the scrubber withheld any of it.</param>
/// <param name="Chunks">The cleaned chunks.</param>
internal sealed record PreparedDoc(string Path, string Blob, string Title, bool Blocked, IReadOnlyList<DocChunk> Chunks);
