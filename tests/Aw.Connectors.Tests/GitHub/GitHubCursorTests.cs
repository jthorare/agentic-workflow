using Aw.Connectors.Core.GitHub;
using Aw.Connectors.Testing;

namespace Aw.Connectors.Tests.GitHub;

public sealed class GitHubCursorTests
{
    private static DateTimeOffset T => new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact, Trait("Lane", "unit")]
    public void An_entry_before_the_cursor_is_not_admitted() => Assert.False(new GitHubCursor(T, []).Admits(A.GitHubIssue(1).UpdatedAt(T.AddSeconds(-1))));

    [Fact, Trait("Lane", "unit")]
    public void An_entry_at_the_cursor_instant_is_admitted_unless_its_id_was_seen()
    {
        var cursor = new GitHubCursor(T, [1]);

        Assert.False(cursor.Admits(A.GitHubIssue(1).UpdatedAt(T)));
        Assert.True(cursor.Admits(A.GitHubIssue(2).UpdatedAt(T)));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_entry_after_the_cursor_is_admitted_even_with_a_seen_id() => Assert.True(new GitHubCursor(T, [1]).Admits(A.GitHubIssue(1).UpdatedAt(T.AddSeconds(1))));

    [Fact, Trait("Lane", "unit")]
    public void Advance_keeps_the_ids_at_the_newest_instant_only()
    {
        GitHubIssue[] read = [A.GitHubIssue(1).UpdatedAt(T), A.GitHubIssue(2).UpdatedAt(T.AddSeconds(5)), A.GitHubIssue(3).UpdatedAt(T.AddSeconds(5))];

        var next = GitHubCursor.From(T).Advance(read);

        Assert.Equal(T.AddSeconds(5), next.UpdatedAt);
        Assert.Equal<long>([2, 3], next.SeenIds);
    }

    [Fact, Trait("Lane", "unit")]
    public void Advance_over_the_same_instant_adds_to_the_seen_ids()
    {
        var next = new GitHubCursor(T, [1]).Advance([A.GitHubIssue(2).UpdatedAt(T)]);

        Assert.Equal(T, next.UpdatedAt);
        Assert.Equal<long>([1, 2], next.SeenIds);
    }

    [Fact, Trait("Lane", "unit")]
    public void Advance_with_nothing_read_is_the_same_cursor()
    {
        var cursor = new GitHubCursor(T, [4, 9]);

        var next = cursor.Advance([]);

        Assert.Equal(T, next.UpdatedAt);
        Assert.Equal<long>([4, 9], next.SeenIds);
    }
}
