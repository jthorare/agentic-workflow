using System.Text.Json;
using Aw.BugFix.Tests.Contracts;
using Aw.Kernel.Testing;

namespace Aw.BugFix.Tests.Cli;

/// <summary>A frozen check file that main changed (a shared project file, say) and the candidate merged: the change is main's, not the fix's.</summary>
[Lane(TestLane.Integration)]
public sealed class CheckPinTests : IDisposable
{
    private const string Branch = "bugfix-eng-1-a1";
    private const string Check = "tests/bug.test.sh";

    private readonly CliWorkspace _w = new();

    public void Dispose() => _w.Dispose();

    [Fact]
    public void Record_run_accepts_a_check_file_that_only_main_changed()
    {
        var worktree = StartAndMergeMainThatChangesTheCheck();
        _ = Ok("record-candidate", "--state", _w.State, "--branch", Branch, "--cwd", worktree);
        var after = Ok("run-test", "--state", _w.State, "--check", Check, "--cwd", worktree, "--", "bash", Check);

        var (exit, _, error) = _w.Run("record-run", "c1", "--state", _w.State, "--evidence", after.GetProperty("evidence").GetString()!);

        Assert.True(exit == 0, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"record-run exited {exit}: {error}"));
    }

    [Fact]
    public void Record_run_still_refuses_a_check_file_the_fix_changed()
    {
        var worktree = StartAndMergeMainThatChangesTheCheck();
        File.WriteAllText(Path.Combine(worktree, Check), "true\n");
        _ = TempDirectory.Git(worktree, "commit", "-q", "-am", "weaken the check");
        _ = Ok("record-candidate", "--state", _w.State, "--branch", Branch, "--cwd", worktree, "--allow-test-changes", "--reason", "test");
        var after = Ok("run-test", "--state", _w.State, "--check", Check, "--cwd", worktree, "--", "bash", Check);

        var (exit, _, error) = _w.Run("record-run", "c1", "--state", _w.State, "--evidence", after.GetProperty("evidence").GetString()!);

        Assert.Equal(3, exit);
        Assert.Contains("AW-BUGFIX-008", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Start_attempt_can_leave_behind_a_candidate_that_has_no_run()
    {
        var worktree = StartAndMergeMainThatChangesTheCheck();
        _ = Ok("record-candidate", "--state", _w.State, "--branch", Branch, "--cwd", worktree);

        var (refused, _, _) = _w.Run("start-attempt", "--state", _w.State, "--mode", "A");
        var started = Ok("start-attempt", "--state", _w.State, "--mode", "A", "--abandon-unrun");

        Assert.NotEqual(0, refused);
        Assert.Equal(2, started.GetProperty("attempt").GetInt32());
    }

    [Fact]
    public void Start_attempt_abandon_unrun_is_refused_for_a_candidate_that_has_a_run()
    {
        var worktree = StartAndMergeMainThatChangesTheCheck();
        _ = Ok("record-candidate", "--state", _w.State, "--branch", Branch, "--cwd", worktree);
        var after = Ok("run-test", "--state", _w.State, "--check", Check, "--cwd", worktree, "--", "bash", Check);
        _ = Ok("record-run", "c1", "--state", _w.State, "--evidence", after.GetProperty("evidence").GetString()!);

        var (exit, _, error) = _w.Run("start-attempt", "--state", _w.State, "--mode", "A", "--abandon-unrun");

        Assert.Equal(3, exit);
        Assert.Contains("AW-BUGFIX-010", error, StringComparison.Ordinal);
    }

    /// <summary>Reproduces at the baseline, starts a fix branch with the fix committed, advances main with a change to the check file only, and merges main in. Returns the fix worktree.</summary>
    private string StartAndMergeMainThatChangesTheCheck()
    {
        _ = Ok("init", "--state", _w.State, "--ticket", _w.Ticket);
        _ = Ok("advance", "investigate", "--state", _w.State, "--evidence", _w.Handoff);
        var baseline = Ok("run-test", "--state", _w.State, "--check", Check, "--", "bash", Check);
        _ = Ok("advance", "reproduce", "--state", _w.State, "--evidence", baseline.GetProperty("evidence").GetString()!, "--check", Check);
        _ = Ok("start-attempt", "--state", _w.State, "--mode", "A");

        var worktree = _w.Fix(Branch);

        TempDirectory.Write(_w.Repo, Check, "echo checking\ngrep -q keep src/app.txt\n# changed on main\n");
        _ = TempDirectory.Git(_w.Repo, "add", "-A");
        _ = TempDirectory.Git(_w.Repo, "commit", "-q", "-m", "main changes the check");
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
