namespace Aw.Context.Testing.Fixtures;

/// <summary>A pull request in the fixture history: one commit on its branch, merged into 'main' with '--no-ff'.</summary>
/// <param name="Number">The pull request number ('Merge pull request #N').</param>
/// <param name="IssueKey">The Linear identifier it closes, e.g. 'SPL-103'.</param>
/// <param name="Branch">The branch name; starts with the lower-cased issue key.</param>
/// <param name="Title">The title; the branch commit is '&lt;IssueKey&gt;: &lt;Title&gt;'.</param>
/// <param name="Dir">The resource folder holding the files it writes (full contents, new or changed).</param>
/// <param name="CommittedAt">The branch commit's date.</param>
/// <param name="MergedAt">The merge commit's date.</param>
public sealed record FixturePullRequest(
    int Number, string IssueKey, string Branch, string Title, string Dir, DateTimeOffset CommittedAt, DateTimeOffset MergedAt);
