namespace Aw.Context.Infrastructure.Docs;

/// <summary>One row of a document joined to one of its chunks: the chunk text is null for a document with no chunks.</summary>
/// <param name="Path">The repo-relative path.</param>
/// <param name="Blob">The blob sha it was indexed at.</param>
/// <param name="Blocked">True when the scrubber withheld the document.</param>
/// <param name="Text">The chunk's cleaned text, or null.</param>
internal sealed record DocTextRow(string Path, string Blob, bool Blocked, string? Text);
