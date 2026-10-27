namespace Aw.Context.Core.Docs;

/// <summary>One document of the repo-docs index, as the map build reads it (#1373).</summary>
/// <param name="Path">The repo-relative path.</param>
/// <param name="Blob">The blob sha the document was indexed at.</param>
/// <param name="Text">The scrubbed text (its chunks joined in order), or null when the scrubber withheld the document.</param>
public sealed record IndexedDoc(string Path, string Blob, string? Text);
