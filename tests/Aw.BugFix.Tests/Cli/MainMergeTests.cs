using System.Text.Json;
using Aw.BugFix.Tests.Contracts;
using Aw.Kernel.Testing;

namespace Aw.BugFix.Tests.Cli;

/// <summary>A fix branch that merged main after the baseline: only the fix counts as the candidate's change, in the judge input and in the test-change guard.</summary>
[Lane(TestLane.Integration)]
public sealed class MainMergeTests : IDisposable
{
    private const string Branch = "bugfix-eng-1-a1";

    private readonly CliWorkspace _w = new();

    public void Dispose() => _w.Dispose();

    [Fact]
    public void Judge_diff_after_a_main_merge_holds_only_the_fix()
    {
        var worktree = FixThenMergeMain();
        // Approved only so the candidate is recorded at all; the test asserts what the judge is shown.
        _ = Ok("record-candidate", "--state", _w.State, "--branch", Branch, "--cwd", worktree, "--allow-test-changes", "--reason", "main's own test");
        var after = Ok("run-test", "--state", _w.State, "--check", "tests/bug.test.sh", "--cwd", worktree, "--", "bash", "tests/bug.test.sh");
        _ = Ok("record-run", "c1", "--state", _w.State, "--evidence", after.GetProperty("evidence").GetString()!);

        // No judge binary: no verdict, but the input the helper wrote for it stays in the state directory.
        var missing = new Dictionary<string, string>(StringComparer.Ordinal) { ["AW_JUDGE_BIN"] = Path.Combine(_w.Root, "no-such-judge") };
        _ = _w.RunWith(missing, "judge", "c1", "--state", _w.State);

        using var input = JsonDocument.Parse(File.ReadAllText(Path.Combine(_w.State, "judge-c1.json")));
        var stat = input.RootElement.GetProperty("diffStat").GetString()!;
        var full = input.RootElement.GetProperty("diff").GetString()!;
        Assert.Contains("src/app.txt", stat, StringComparison.Ordinal);
        Assert.Contains("src/app.txt", full, StringComparison.Ordinal);
        foreach (var fromMain in new[] { "src/other.txt", "tests/Foo.Tests/MainTest.cs" })
        {
            Assert.DoesNotContain(fromMain, stat, StringComparison.Ordinal);
            Assert.DoesNotContain(fromMain, full, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Record_candidate_after_a_main_merge_counts_only_the_fix_and_is_not_refused_for_mains_test_file()
    {
        var worktree = FixThenMergeMain();

        var (exit, output, error) = _w.Run("record-candidate", "--state", _w.State, "--branch", Branch, "--cwd", worktree);

        Assert.True(exit == 0, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"record-candidate without --allow-test-changes exited {exit}: {error}"));
        using var document = JsonDocument.Parse(output);
        Assert.Equal(["src/app.txt"], document.RootElement.GetProperty("changedFiles").EnumerateArray().Select(static p => p.GetString()).ToArray());
    }

    /// <summary>
    /// Reproduces the bug at the baseline, starts a fix branch from it with the fix committed, advances main with a
    /// source file and a test file, and merges main into the fix branch. Returns the fix worktree.
    /// </summary>
    private string FixThenMergeMain()
    {
        _ = Ok("init", "--state", _w.State, "--ticket", _w.Ticket);
        _ = Ok("advance", "investigate", "--state", _w.State, "--evidence", _w.Handoff);
        var baseline = Ok("run-test", "--state", _w.State, "--check", "tests/bug.test.sh", "--", "bash", "tests/bug.test.sh");
        _ = Ok("advance", "reproduce", "--state", _w.State, "--evidence", baseline.GetProperty("evidence").GetString()!, "--check", "tests/bug.test.sh");
        _ = Ok("start-attempt", "--state", _w.State, "--mode", "A");

        var worktree = _w.Fix(Branch);

        TempDirectory.Write(_w.Repo, "src/other.txt", "from main\n");
        TempDirectory.Write(_w.Repo, "tests/Foo.Tests/MainTest.cs", "// from main\n");
        _ = TempDirectory.Git(_w.Repo, "add", "-A");
        _ = TempDirectory.Git(_w.Repo, "commit", "-q", "-m", "main advances");
        _ = TempDirectory.Git(worktree, "merge", "-q", "--no-edit", "main");
        return worktree;
    }

    private JsonElement Ok(params string[] args)
    {
        var (exit, output, error) = _w.Run(args);
        Assert.True(exit == 0, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{string.Join(' ', args)} exited {exit}: {error}"));
        using var document = JsonDocument.Parse(output);
        return document.RootElement.Clone();
    }
}
