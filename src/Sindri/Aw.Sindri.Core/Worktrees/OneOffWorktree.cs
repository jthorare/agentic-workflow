namespace Aw.Sindri.Core.Worktrees;

/// <summary>A worktree a one-off start made (agents spec §15.4).</summary>
/// <param name="Path">The worktree folder.</param>
/// <param name="Branch">The branch it checks out.</param>
public sealed record OneOffWorktree(string Path, string Branch);
