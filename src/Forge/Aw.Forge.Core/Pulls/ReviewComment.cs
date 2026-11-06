namespace Aw.Forge.Core.Pulls;

/// <summary>A comment on one line of a file, sent with a review.</summary>
/// <param name="Path">The file.</param>
/// <param name="Line">The line in the head's version of the file.</param>
/// <param name="Body">The text; the caller has already redacted it.</param>
public sealed record ReviewComment(string Path, int Line, string Body);
