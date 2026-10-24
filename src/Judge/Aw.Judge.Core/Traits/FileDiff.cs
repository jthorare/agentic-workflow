namespace Aw.Judge.Core.Traits;

/// <summary>A file's hunks.</summary>
/// <param name="Path">The repo-relative path.</param>
/// <param name="Hunks">The hunks, in file order.</param>
public sealed record FileDiff(string Path, IReadOnlyList<DiffHunk> Hunks);
