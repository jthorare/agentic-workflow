namespace Aw.Sindri.Core.Checks;

/// <summary>What a branch changed between a base and a head: the paths, the lines it added and its commits.</summary>
/// <param name="Files">The changed paths, in path order.</param>
/// <param name="AddedLines">The added lines, in path then line order.</param>
/// <param name="Commits">The commits after the base up to the head, oldest first.</param>
public sealed record BranchChanges(IReadOnlyList<ChangedFile> Files, IReadOnlyList<AddedLine> AddedLines, IReadOnlyList<ChangedCommit> Commits);
