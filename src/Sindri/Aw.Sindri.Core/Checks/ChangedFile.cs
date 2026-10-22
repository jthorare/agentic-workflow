namespace Aw.Sindri.Core.Checks;

/// <summary>A path the branch changed.</summary>
/// <param name="Path">The repo-relative path, with '/' separators.</param>
/// <param name="Kind">What happened to it.</param>
public sealed record ChangedFile(string Path, ChangeKind Kind);
