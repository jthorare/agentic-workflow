namespace Aw.Sindri.Core.Forge;

/// <summary>A comment on one line of a file, sent with a review.</summary>
/// <param name="Path">The file.</param>
/// <param name="Line">The line in the head's version of the file.</param>
/// <param name="Body">The text. The forge adapter redacts it before it is sent.</param>
public sealed record PullReviewComment(string Path, int Line, string Body);
