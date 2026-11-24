namespace Aw.Transcripts.Core;

/// <summary>
/// A directory the host keeps a repo's worktrees under, and that repo's main checkout. A session whose cwd sat under the
/// root and no longer exists (the worktree was swept) belongs to the main checkout. The host supplies the pairs; the
/// library guesses none.
/// </summary>
/// <param name="Root">The absolute worktree root, such as ~/repos/aw-wt/web expanded.</param>
/// <param name="MainCheckout">The absolute main checkout of the repo whose worktrees sit under the root.</param>
public sealed record WorktreeRoot(string Root, string MainCheckout);
