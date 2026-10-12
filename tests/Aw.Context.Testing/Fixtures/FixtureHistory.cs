namespace Aw.Context.Testing.Fixtures;

/// <summary>The fixture repo's history ('history.json'): the initial commit, then the merged pull requests in order.</summary>
/// <param name="Repository">The synthetic 'owner/name' the merge messages name.</param>
/// <param name="Author">Author and committer of every commit.</param>
/// <param name="Initial">The first commit on 'main'.</param>
/// <param name="PullRequests">The merged pull requests, oldest first.</param>
public sealed record FixtureHistory(string Repository, FixtureAuthor Author, FixtureCommit Initial, IReadOnlyList<FixturePullRequest> PullRequests);
