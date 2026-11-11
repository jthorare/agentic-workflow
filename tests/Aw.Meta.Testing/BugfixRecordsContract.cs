using Aw.Meta.Core.Ports;
using Xunit;

namespace Aw.Meta.Testing;

/// <summary>The IBugfixRecords contract: the same tests run against the in-memory fake and the state.json reader.</summary>
public abstract class BugfixRecordsContract
{
    /// <summary>The adapter under test, holding the candidates given (grouped into runs by <see cref="BugfixCandidate.Run"/>, each run's outcome taken from its rows).</summary>
    /// <param name="candidates">The recorded candidates.</param>
    /// <returns>The adapter.</returns>
    protected abstract IBugfixRecords Create(params BugfixCandidate[] candidates);

    private static BugfixCandidate Row(string candidate, string worktree, bool resolves = false) =>
        new("gh-9", candidate, worktree, "bugfix/gh-9-" + candidate, "sha-" + candidate, Evaluated: true, resolves, Finished: true, ResolvedBy: "c2");

    /// <summary>The candidate that names the worktree comes back with its run's outcome.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_candidate_naming_the_worktree_comes_back_with_its_runs_outcome()
    {
        var records = Create(Row("c1", "/wt/gh-9-a1"), Row("c2", "/wt/gh-9-a2", resolves: true));

        var result = await records.Naming("/wt/gh-9-a1", CancellationToken.None);

        Expect.That(result.IsSuccess && result.Value.Count == 1, "it reads one row");
        var row = result.Value[0];
        Expect.That(row with { Worktree = "/wt/gh-9-a1" } == Row("c1", "/wt/gh-9-a1"), "every field is kept");
    }

    /// <summary>A trailing slash or a ".." segment on either side still names the same worktree.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Paths_are_compared_normalized()
    {
        var records = Create(Row("c1", "/wt/gh-9-a1/"));

        var result = await records.Naming("/wt/other/../gh-9-a1", CancellationToken.None);

        Expect.That(result.IsSuccess && result.Value.Count == 1, "the same directory matches");
    }

    /// <summary>A run's GitHub issue comes back on every row, and its red tree comes back as a baseline row with no branch (#2753).</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_runs_issue_and_its_red_tree_come_back()
    {
        var candidate = Row("c1", "/wt/gh-9-a1") with { Issue = 9 };
        var red = new BugfixCandidate("gh-9", "red", "/wt/gh-9", string.Empty, "sha-red", Evaluated: true, Resolves: false, Finished: true, ResolvedBy: "c2") { Issue = 9, Baseline = true };
        var records = Create(candidate, red);

        var named = await records.Naming("/wt/gh-9", CancellationToken.None);
        var other = await records.Naming("/wt/gh-9-a1", CancellationToken.None);

        Expect.That(named.IsSuccess && named.Value.Count == 1 && named.Value[0] == red, "the red tree is a baseline row with the issue");
        Expect.That(other.IsSuccess && other.Value.Count == 1 && other.Value[0] == candidate, "the candidate carries the issue");
    }

    /// <summary>No run names the worktree: an empty list, not an error.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_worktree_no_run_names_gives_an_empty_list()
    {
        var records = Create(Row("c1", "/wt/gh-9-a1"));

        var result = await records.Naming("/wt/elsewhere", CancellationToken.None);

        Expect.That(result.IsSuccess && result.Value.Count == 0, "nothing names it");
    }

    /// <summary>No records at all: an empty list.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task No_records_give_an_empty_list()
    {
        var result = await Create().Naming("/wt/gh-9-a1", CancellationToken.None);

        Expect.That(result.IsSuccess && result.Value.Count == 0, "nothing recorded");
    }
}
