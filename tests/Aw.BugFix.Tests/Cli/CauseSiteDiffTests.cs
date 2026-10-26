using System.Text.Json;
using Aw.BugFix.Tests.Contracts;
using Aw.Kernel.Testing;

namespace Aw.BugFix.Tests.Cli;

/// <summary>The judge sees the fix a "Part of" commit already put on main, and still not main's unrelated changes (#1189).</summary>
[Lane(TestLane.Integration)]
public sealed class CauseSiteDiffTests : IDisposable
{
    private const string Branch = "bugfix-eng-1-a1";

    private readonly CliWorkspace _w = new();

    public void Dispose() => _w.Dispose();

    [Fact]
    public void The_judge_diff_holds_a_part_of_commit_main_already_had_when_the_candidate_adds_only_a_test()
    {
        _ = _w.Write("ticket.json", """{"source":"github","id":"#1189","title":"Login","brief":"Login shows a blank page","expected":"The dashboard opens","actual":"Blank page"}""");
        var worktree = StartedWorktree();
        TempDirectory.Write(_w.Repo, "src/app.txt", "keep cookie\n");
        _ = TempDirectory.Git(_w.Repo, "add", "-A");
        _ = TempDirectory.Git(_w.Repo, "commit", "-q", "-m", "fix: keep the cookie", "-m", "Part of #1189");
        TempDirectory.Write(_w.Repo, "src/other.txt", "from main\n");
        _ = TempDirectory.Git(_w.Repo, "add", "-A");
        _ = TempDirectory.Git(_w.Repo, "commit", "-q", "-m", "chore: another change");
        _ = TempDirectory.Git(worktree, "merge", "-q", "--no-edit", "main");
        TempDirectory.Write(worktree, "tests/extra.test.sh", "grep -q keep src/app.txt\n");
        _ = TempDirectory.Git(worktree, "add", "-A");
        _ = TempDirectory.Git(worktree, "commit", "-q", "-m", "regression test");

        var (stat, full) = JudgeInput(worktree);

        Assert.Contains("src/app.txt", stat, StringComparison.Ordinal);
        Assert.Contains("keep cookie", full, StringComparison.Ordinal);
        Assert.DoesNotContain("src/other.txt", stat, StringComparison.Ordinal);
        Assert.DoesNotContain("src/other.txt", full, StringComparison.Ordinal);
    }

    [Fact]
    public void A_file_the_candidate_changed_appears_once()
    {
        StartAttempt();
        var worktree = _w.Fix(Branch);
        TempDirectory.Write(_w.Repo, "src/other.txt", "from main\n");
        _ = TempDirectory.Git(_w.Repo, "add", "-A");
        _ = TempDirectory.Git(_w.Repo, "commit", "-q", "-m", "main advances");
        _ = TempDirectory.Git(worktree, "merge", "-q", "--no-edit", "main");

        var (stat, full) = JudgeInput(worktree);

        Assert.Equal(1, full.Split("diff --git a/src/app.txt", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("src/other.txt", full, StringComparison.Ordinal);
        Assert.DoesNotContain("src/other.txt", stat, StringComparison.Ordinal);
    }

    /// <summary>Reproduces at the baseline, starts attempt one and returns a fix worktree on a new branch from the baseline with nothing committed on it yet.</summary>
    private string StartedWorktree()
    {
        StartAttempt();
        var worktree = Path.Combine(_w.Root, Branch);
        _ = TempDirectory.Git(_w.Repo, "worktree", "add", "-q", "-b", Branch, worktree);
        return worktree;
    }

    private void StartAttempt()
    {
        _ = Ok("init", "--state", _w.State, "--ticket", _w.Ticket);
        _ = Ok("advance", "investigate", "--state", _w.State, "--evidence", _w.Handoff);
        var baseline = Ok("run-test", "--state", _w.State, "--check", "tests/bug.test.sh", "--", "bash", "tests/bug.test.sh");
        _ = Ok("advance", "reproduce", "--state", _w.State, "--evidence", baseline.GetProperty("evidence").GetString()!, "--check", "tests/bug.test.sh");
        _ = Ok("start-attempt", "--state", _w.State, "--mode", "A");
    }

    private (string Stat, string Full) JudgeInput(string worktree)
    {
        _ = Ok("record-candidate", "--state", _w.State, "--branch", Branch, "--cwd", worktree, "--allow-test-changes", "--reason", "regression test");
        var after = Ok("run-test", "--state", _w.State, "--check", "tests/bug.test.sh", "--cwd", worktree, "--", "bash", "tests/bug.test.sh");
        _ = Ok("record-run", "c1", "--state", _w.State, "--evidence", after.GetProperty("evidence").GetString()!);
        var missing = new Dictionary<string, string>(StringComparer.Ordinal) { ["AW_JUDGE_BIN"] = Path.Combine(_w.Root, "no-such-judge") };
        _ = _w.RunWith(missing, "judge", "c1", "--state", _w.State);
        using var input = JsonDocument.Parse(File.ReadAllText(Path.Combine(_w.State, "judge-c1.json")));
        return (input.RootElement.GetProperty("diffStat").GetString()!, input.RootElement.GetProperty("diff").GetString()!);
    }

    private JsonElement Ok(params string[] args)
    {
        var (exit, output, error) = _w.Run(args);
        Assert.True(exit == 0, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{string.Join(' ', args)} exited {exit}: {error}"));
        using var document = JsonDocument.Parse(output);
        return document.RootElement.Clone();
    }
}
