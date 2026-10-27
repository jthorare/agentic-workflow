using System.Text.Json;
using Aw.BugFix.Tests.Contracts;
using Aw.BugFix.Tests.Cutover;
using Aw.Kernel.Testing;

namespace Aw.BugFix.Tests.Cli;

/// <summary>#1248: the judge is shown what the frozen check printed when it failed on the unfixed code, not only that it failed.</summary>
[Lane(TestLane.Integration)]
public sealed class JudgeInputFailureLinesTests : IDisposable
{
    // The failure line is built at run time, so the check's own source (which the judge input also carries) doesn't contain it.
    private const string Check = "if grep -q keep src/app.txt; then echo \"ok: cookie kept\"; exit 0; fi\necho \"FAIL: expected $(echo keep)-cookie in src/app.txt\"\nexit 1\n";
    private const string FailureLine = "FAIL: expected keep-cookie in src/app.txt";

    private static readonly string _script = string.Join('\n',
    [
        """#!/bin/bash""",
        """case "$1" in """,
        """  resolution-check) """,
        """    cat > @DIR@/stdin """,
        """    printf '{"decision":"resolved","confidence":0.9,"model":"jev","reasonCode":"judged","id":"d-42","reasons":["ok"]}\n' """,
        """    ;; """,
        """  why) """,
        """    digest="$(sha256sum @DIR@/stdin | cut -c1-16)" """,
        """    printf '{"id":"%s","question":"resolution-check","decision":"resolved","reason_code":"judged","input_digest":"%s","outcome":"decided"}\n' "$2" "$digest" """,
        """    ;; """,
        """  *) exit 64 ;; """,
        """esac """,
        "",
    ]);

    private readonly CliWorkspace _w = new();
    private readonly StubBinary _judge = new("judge", _script);

    public void Dispose()
    {
        _judge.Dispose();
        _w.Dispose();
    }

    [Fact]
    public void The_judge_input_carries_the_failure_lines_of_the_baseline_run()
    {
        File.WriteAllText(Path.Combine(_w.Repo, "tests", "bug.test.sh"), Check);
        _ = TempDirectory.Git(_w.Repo, "commit", "-q", "-am", "check prints its failure");
        _ = Ok("init", "--state", _w.State, "--ticket", _w.Ticket);
        _ = Ok("advance", "investigate", "--state", _w.State, "--evidence", _w.Handoff);
        var baseline = Ok("run-test", "--state", _w.State, "--check", "tests/bug.test.sh", "--", "bash", "tests/bug.test.sh");
        _ = Ok("advance", "reproduce", "--state", _w.State, "--evidence", baseline.GetProperty("evidence").GetString()!, "--check", "tests/bug.test.sh");
        _ = Ok("start-attempt", "--state", _w.State, "--mode", "A");
        var worktree = _w.Fix("bugfix-eng-1-a1");
        _ = Ok("record-candidate", "--state", _w.State, "--branch", "bugfix-eng-1-a1", "--cwd", worktree);
        var after = Ok("run-test", "--state", _w.State, "--check", "tests/bug.test.sh", "--cwd", worktree, "--", "bash", "tests/bug.test.sh");
        _ = Ok("record-run", "c1", "--state", _w.State, "--evidence", after.GetProperty("evidence").GetString()!);

        var environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["AW_JUDGE_BIN"] = _judge.Path };
        var (exit, _, error) = _w.RunWith(environment, "judge", "c1", "--state", _w.State);

        Assert.True(exit == 0, error);
        Assert.Contains(FailureLine, _judge.Read("stdin"), StringComparison.Ordinal);
    }

    private JsonElement Ok(params string[] args)
    {
        var (exit, output, error) = _w.Run(args);
        Assert.True(exit == 0, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{string.Join(' ', args)} exited {exit}: {error}"));
        using var document = JsonDocument.Parse(output);
        return document.RootElement.Clone();
    }
}
