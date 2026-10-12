using System.Diagnostics;
using System.Globalization;
using Aw.Context.Testing.Fixtures;
using Aw.Kernel.Testing;

namespace Aw.Context.Testing.Tests;

/// <summary>The fixture app built as a git repo whose merged PRs are the backtest's file truth (spec §9.1, §10).</summary>
[Lane(TestLane.Integration)]
public sealed class FixtureRepoTests
{
    // The head every machine must build (README "History"). It changes only with a reviewed change to the app files,
    // history.json or the builder; .gitattributes keeps the fixture's bytes free of line-ending conversion.
    private const string GoldenHead = "ad683496d8de39923e97e02c5936e599ff934050";

    [Fact]
    public void Build_merges_one_pull_request_per_history_entry_with_the_issue_key_in_branch_and_messages()
    {
        using var repo = FixtureRepo.Build();

        Assert.Equal(SplitPanesFixture.History.PullRequests.Select(p => p.IssueKey), repo.PullRequests.Select(p => p.IssueKey), StringComparer.Ordinal);
        foreach (var pr in repo.PullRequests)
        {
            var merge = GitOut(repo.Path, "log", "-1", "--format=%P%n%s", pr.MergeCommit).Split('\n');
            Assert.Equal(2, merge[0].Split(' ').Length);
            Assert.Equal(string.Create(CultureInfo.InvariantCulture, $"Merge pull request #{pr.Number} from fixture-labs/{pr.Branch}"), merge[1]);
            Assert.StartsWith(pr.IssueKey.ToLowerInvariant() + "-", pr.Branch, StringComparison.Ordinal);
            Assert.Equal($"{pr.IssueKey}: {pr.Title}", GitOut(repo.Path, "log", "-1", "--format=%s", pr.MergeCommit + "^2"));
        }
    }

    [Fact]
    public void Each_merge_changes_exactly_the_files_its_pull_request_lists()
    {
        using var repo = FixtureRepo.Build();

        foreach (var pr in repo.PullRequests)
        {
            var changed = GitOut(repo.Path, "diff", "--name-only", pr.MergeCommit + "^1", pr.MergeCommit, "--")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal);
            Assert.Equal(pr.Files, changed, StringComparer.Ordinal);
        }
    }

    [Fact]
    public void The_history_is_deterministic_and_carries_the_recorded_dates()
    {
        using var first = FixtureRepo.Build();
        using var second = FixtureRepo.Build();

        Assert.Equal(first.Head, second.Head);
        Assert.Equal(GoldenHead, first.Head);
        Assert.Equal(first.Head, GitOut(first.Path, "rev-parse", "main"));
        var last = first.PullRequests[^1];
        Assert.Equal(last.MergedAt, DateTimeOffset.Parse(GitOut(first.Path, "log", "-1", "--format=%cI", last.MergeCommit), CultureInfo.InvariantCulture));
        Assert.Equal("Avery Fixture <avery@fixture-labs.invalid>", GitOut(first.Path, "log", "-1", "--format=%an <%ae>", last.MergeCommit));
    }

    [Fact]
    public void The_final_tree_holds_the_app_and_no_pull_request_for_the_unmerged_issue()
    {
        using var repo = FixtureRepo.Build();

        foreach (var file in new[] { "src/layout/PaneLayout.ts", "src/layout/SplitPane.ts", "src/layout/PaneResize.ts", "src/tabs/TabManager.ts", "settings/default-settings.json" })
        {
            Assert.True(File.Exists(Path.Combine(repo.Path, file)), file);
        }

        Assert.Contains("MIN_PANE_COLUMNS = 10", File.ReadAllText(Path.Combine(repo.Path, "src/layout/PaneResize.ts")), StringComparison.Ordinal);
        Assert.DoesNotContain(repo.PullRequests, p => p.IssueKey == "SPL-106");
        Assert.Empty(GitOut(repo.Path, "status", "--porcelain"));
        Assert.Empty(GitOut(repo.Path, "remote"));
    }

    [Fact]
    public void A_second_build_into_the_same_parent_is_refused_and_leaves_the_first_intact()
    {
        var parent = Directory.CreateTempSubdirectory("aw-split-panes-parent-").FullName;
        try
        {
            using var first = FixtureRepo.Build(parent);

            var error = Assert.Throws<InvalidOperationException>(() => FixtureRepo.Build(parent));

            Assert.Contains("already exists", error.Message, StringComparison.Ordinal);
            Assert.Equal(first.Head, GitOut(first.Path, "rev-parse", "main"));
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void Dispose_removes_the_repo()
    {
        var repo = FixtureRepo.Build();
        var path = repo.Path;

        repo.Dispose();

        Assert.False(Directory.Exists(path));
    }

    private static string GitOut(string dir, params string[] args)
    {
        var start = new ProcessStartInfo("git") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = dir };
        foreach (var name in Environment.GetEnvironmentVariables().Keys.Cast<string>().Where(n => n.StartsWith("GIT_", StringComparison.Ordinal)))
        {
            _ = start.Environment.Remove(name);
        }

        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        using var git = Process.Start(start)!; // why: Process.Start returns null only for a reused shell process
        var output = git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        Assert.Equal(0, git.ExitCode);
        return output.TrimEnd('\n');
    }
}
