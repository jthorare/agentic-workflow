namespace Aw.Context.Testing.Fixtures;

/// <summary>A pull request as merged into a built <see cref="FixtureRepo"/>: the backtest's file truth for its issue.</summary>
/// <param name="Number">The pull request number.</param>
/// <param name="IssueKey">The Linear identifier it closes.</param>
/// <param name="Branch">Its branch (kept in the repo).</param>
/// <param name="Title">Its title.</param>
/// <param name="MergeCommit">The merge commit's sha on 'main'.</param>
/// <param name="MergedAt">The merge commit's date.</param>
/// <param name="Files">The repo-relative paths it changed, ordinal order.</param>
public sealed record MergedPullRequest(
    int Number, string IssueKey, string Branch, string Title, string MergeCommit, DateTimeOffset MergedAt, IReadOnlyList<string> Files);
