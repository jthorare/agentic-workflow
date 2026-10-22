namespace Aw.Sindri.Core.Forge;

/// <summary>A merge of a pull request.</summary>
/// <param name="Link">The pull request's link.</param>
/// <param name="Method">How to merge.</param>
/// <param name="ExpectedHead">The head sha the caller saw; the merge happens only if it is still the head.</param>
public sealed record PullMerge(string Link, PullMergeMethod Method, string ExpectedHead);
