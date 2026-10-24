using System.Text.Json;
using Aw.Kernel.Testing;

namespace Aw.BugFix.Tests.Cli;

/// <summary>bugfix-state end to end: the commands and fields the bugFixOrchestrator skill reads, and the exit codes it branches on.</summary>
[Lane(TestLane.Integration)]
public sealed class CliTests : IDisposable
{
    private readonly CliWorkspace _w = new();

    public void Dispose() => _w.Dispose();

    [Fact]
    public void The_test_check_journey_runs_from_init_to_an_unresolved_report()
    {
        _ = Ok("init", "--state", _w.State, "--ticket", _w.Ticket);
        Assert.Equal(1, Field(Ok("advance", "investigate", "--state", _w.State, "--evidence", _w.Handoff), "hypotheses").GetInt32());
        var baseline = Ok("run-test", "--state", _w.State, "--check", "tests/bug.test.sh", "--", "bash", "tests/bug.test.sh");
        Assert.Equal(1, Field(baseline, "exitCode").GetInt32());
        _ = Ok("advance", "reproduce", "--state", _w.State, "--evidence", Field(baseline, "evidence").GetString()!, "--check", "tests/bug.test.sh"); // why: run-test always prints its evidence path.
        Assert.Equal(TempDirectoryGit("rev-parse", "HEAD"), Field(Ok("status", "--state", _w.State), "baseline").GetProperty("commit").GetString());
        _ = Ok("start-attempt", "--state", _w.State, "--mode", "A");
        var worktree = _w.Fix("bugfix-eng-1-a1");
        Assert.Equal("c1", Field(Ok("record-candidate", "--state", _w.State, "--branch", "bugfix-eng-1-a1", "--cwd", worktree), "candidate").GetString());
        var after = Ok("run-test", "--state", _w.State, "--check", "tests/bug.test.sh", "--cwd", worktree, "--", "bash", "tests/bug.test.sh");
        Assert.Equal("passed", Field(after, "outcome").GetString());
        Assert.Equal("judge c1", Field(Ok("record-run", "c1", "--state", _w.State, "--evidence", Field(after, "evidence").GetString()!), "next").GetString()); // why: as above.
        Assert.StartsWith("judge c1", Field(Ok("resume", "--state", _w.State), "next").GetString(), StringComparison.Ordinal);

        // A judge binary that isn't there is no verdict (AW-BUGFIX-016, naming the variable), whatever the box has on PATH.
        var missing = new Dictionary<string, string>(StringComparer.Ordinal) { ["AW_JUDGE_BIN"] = Path.Combine(_w.Root, "no-such-judge") };
        var (judgeExit, judgeOutput, judgeError) = _w.RunWith(missing, "judge", "c1", "--state", _w.State);
        Assert.Equal((3, ""), (judgeExit, judgeOutput));
        Assert.Contains("AW_JUDGE_BIN", judgeError, StringComparison.Ordinal);
        Assert.EndsWith("(AW-BUGFIX-016)", judgeError.TrimEnd(), StringComparison.Ordinal);

        Assert.Equal("unresolved", Field(Ok("advance", "report", "--state", _w.State, "--unresolved"), "status").GetString());
        Assert.StartsWith("done (unresolved)", Field(Ok("resume", "--state", _w.State), "next").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_refusal_exits_3_with_its_reason_and_leaves_the_state_untouched()
    {
        _ = Ok("init", "--state", _w.State, "--ticket", _w.Ticket);
        _ = Ok("advance", "investigate", "--state", _w.State, "--evidence", _w.Handoff);
        var before = _w.StateSha;

        Refused(["run-test", "--state", _w.State, "--check", "tests/bug.test.sh", "--", "sh", "-c", "exit 1", "tests/bug.test.sh"], "AW-BUGFIX-012");
        Refused(["start-attempt", "--state", _w.State, "--mode", "A"], "AW-BUGFIX-005");
        Refused(["advance", "reproduce", "--state", _w.State, "--evidence", _w.Handoff, "--check", "tests/bug.test.sh"], "AW-BUGFIX-009");
        Assert.Equal(before, _w.StateSha);
    }

    [Theory]
    [InlineData("")]
    [InlineData("fix-it --state s")]
    [InlineData("status")]
    [InlineData("status --state s --verbose")]
    [InlineData("start-attempt --state s --mode Z")]
    [InlineData("record-run candidate-1 --state s --evidence e")]
    [InlineData("run-test --state s --check t")]
    [InlineData("init --state --ticket t")]
    [InlineData("status --state ")]
    public void A_usage_error_exits_2_with_the_usage_and_no_output(string line)
    {
        var (exit, output, error) = _w.Run(line.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        Assert.Equal((2, ""), (exit, output));
        Assert.StartsWith("usage: ", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Bad_input_exits_1_with_one_error_line()
    {
        var (exit, output, error) = _w.Run("status", "--state", _w.State);
        var invalid = _w.Write("bad.json", "{\"source\":\"text\"}");
        var (badExit, _, badError) = _w.Run("init", "--state", _w.State, "--ticket", invalid);

        Assert.Equal((1, "", 1), (exit, output, error.TrimEnd().Split('\n').Length));
        Assert.Matches(@"^error: no state at .*state\.json; run bugfix-state init first \(AW-BUGFIX-002\)$", error.TrimEnd());
        Assert.Equal(1, badExit);
        Assert.EndsWith("(AW-BUGFIX-001)", badError.TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_option_value_is_a_usage_error_and_an_unwritable_state_directory_one_line()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var (emptyExit, _, emptyError) = _w.Run("status", "--state", "");
        var readOnly = Directory.CreateDirectory(Path.Combine(_w.Root, "ro")).FullName;
        File.SetUnixFileMode(readOnly, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        var (exit, output, error) = _w.Run("init", "--state", Path.Combine(readOnly, "x"), "--ticket", _w.Ticket);
        File.SetUnixFileMode(readOnly, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        Assert.Equal(2, emptyExit);
        Assert.StartsWith("usage: --state needs one non-empty value", emptyError, StringComparison.Ordinal);
        Assert.Equal((3, ""), (exit, output));
        Assert.Matches(@"^refused: .* \(AW-BUGFIX-02[12]\)$", error.TrimEnd());
    }

    [Fact]
    public void The_check_ignores_the_callers_loader_variables_and_git_overrides()
    {
        _ = Ok("init", "--state", _w.State, "--ticket", _w.Ticket);
        _ = Ok("advance", "investigate", "--state", _w.State, "--evidence", _w.Handoff);
        var pass = _w.Write("pass.sh", "exit 0\n");
        var hostile = new Dictionary<string, string>(StringComparer.Ordinal) { ["BASH_ENV"] = pass, ["GIT_DIR"] = Path.Combine(_w.Root, "nowhere") };

        var (exit, output, error) = _w.RunWith(hostile, "run-test", "--state", _w.State, "--check", "tests/bug.test.sh", "--", "bash", "tests/bug.test.sh");

        Assert.True(exit == 0, error);
        using var run = JsonDocument.Parse(output);
        Assert.Equal("failed", run.RootElement.GetProperty("outcome").GetString());   // BASH_ENV would have made it exit 0
    }

    [Fact]
    public void Disabled_file_locking_is_refused_and_a_mistyped_state_directory_is_not_created()
    {
        var off = new Dictionary<string, string>(StringComparer.Ordinal) { ["DOTNET_SYSTEM_IO_DISABLEFILELOCKING"] = "1" };
        var typo = Path.Combine(_w.Root, "typo");

        var (lockExit, _, lockError) = _w.RunWith(off, "init", "--state", _w.State, "--ticket", _w.Ticket);
        var (typoExit, _, typoError) = _w.Run("resume", "--state", typo);
        var (stepExit, _, _) = _w.Run("start-attempt", "--state", typo, "--mode", "A");

        Assert.Equal(3, lockExit);
        Assert.EndsWith("(AW-BUGFIX-022)", lockError.TrimEnd(), StringComparison.Ordinal);
        Assert.Equal((1, 1), (typoExit, stepExit));
        Assert.EndsWith("(AW-BUGFIX-002)", typoError.TrimEnd(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(typo));
    }

    private JsonElement Ok(params string[] args)
    {
        var (exit, output, error) = _w.Run(args);
        Assert.True(exit == 0, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{string.Join(' ', args)} exited {exit}: {error}"));
        Assert.Equal(string.Empty, error);
        using var document = JsonDocument.Parse(output);
        _ = Assert.Single(output.TrimEnd().Split('\n'));
        return document.RootElement.Clone();
    }

    private void Refused(string[] args, string code)
    {
        var (exit, output, error) = _w.Run(args);
        Assert.Equal((3, ""), (exit, output));
        Assert.StartsWith("refused: ", error, StringComparison.Ordinal);
        Assert.EndsWith($"({code})", error.TrimEnd(), StringComparison.Ordinal);
    }

    private static JsonElement Field(JsonElement element, string name) => element.GetProperty(name);

    private string TempDirectoryGit(params string[] args) => Contracts.TempDirectory.Git(_w.Repo, args);
}
