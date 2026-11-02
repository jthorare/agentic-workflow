namespace Aw.Context.Core.Extract.Imports;

/// <summary>One import: the file that imports, and the file in the same repo it resolves to.</summary>
/// <param name="From">The importing file's repo-relative path.</param>
/// <param name="To">The imported file's repo-relative path.</param>
internal sealed record ImportPair(string From, string To);
