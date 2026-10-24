using System.Globalization;
using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Infrastructure;
using Aw.BugFix.Tests.Contracts;
using Aw.Kernel.Testing;

namespace Aw.BugFix.Tests.Cutover;

/// <summary>
/// They run against a stub `ui-evidence` script.
/// replaces the stub body; they run against a stub `ui-evidence` script.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class UiEvidenceCliTests : IDisposable
{
    private static readonly string _script = string.Join('\n',
    [
        """#!/bin/bash""",
        """printf '%s\n' "$@" > @DIR@/argv """,
        """env | grep '^AW_' > @DIR@/env || true """,
        """mkdir -p "$3" """,
        """cp @DIR@/summary.json "$3/summary.json" 2>/dev/null || true """,
        """exit "$(cat @DIR@/exit)" """,
        "",
    ]);

    private readonly StubBinary _stub = new("ui-evidence", _script);
    private readonly TempDirectory _work = new();

    public void Dispose()
    {
        _stub.Dispose();
        _work.Dispose();
    }

    [Fact]
    public void It_runs_ui_evidence_run_with_the_script_the_run_directory_and_the_commit()
    {
        _stub.Set("exit", "0");
        _stub.Set("summary.json", Summary("passed"));
        var request = Request();

        var run = Adapter(("AW_UI_EVIDENCE_BIN", _stub.Path)).Run(request);

        Assert.True(run.IsSuccess);
        Assert.Equal(["run", request.Script, request.RunDirectory, "--app-build", request.Commit], _stub.Read("argv").Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void Exit_0_is_a_passing_run_with_its_steps_and_the_hash_of_the_script_it_executed()
    {
        _stub.Set("exit", "0");
        _stub.Set("summary.json", Summary("passed", "passed"));

        var run = Adapter(("AW_UI_EVIDENCE_BIN", _stub.Path)).Run(Request()).Value;

        Assert.Equal([RunOutcome.Passed, RunOutcome.Passed], run.Steps);
        Assert.Equal(new string('a', 64), run.ScriptSha256);
        Assert.Equal((RunOutcome.Passed, 0), (run.Outcome, run.ExitCode));
    }

    [Fact]
    public void Exit_2_is_a_failed_check_not_an_error()
    {
        _stub.Set("exit", "2");
        _stub.Set("summary.json", Summary("passed", "failed"));

        var run = Adapter(("AW_UI_EVIDENCE_BIN", _stub.Path)).Run(Request()).Value;

        Assert.Equal((RunOutcome.Failed, 2), (run.Outcome, run.ExitCode));
    }

    [Fact]
    public void Exit_4_is_a_broken_run()
    {
        _stub.Set("exit", "4");
        _stub.Set("summary.json", Summary("broken"));

        var run = Adapter(("AW_UI_EVIDENCE_BIN", _stub.Path)).Run(Request()).Value;

        Assert.Equal((RunOutcome.Broken, 4), (run.Outcome, run.ExitCode));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(75)]
    [InlineData(130)]
    public void Any_other_exit_is_refused_as_AW_BUGFIX_017(int exit)
    {
        _stub.Set("exit", exit.ToString(CultureInfo.InvariantCulture));

        var run = Adapter(("AW_UI_EVIDENCE_BIN", _stub.Path)).Run(Request());

        Assert.Equal(BugFixErrors.UiEvidenceUnavailable.Code, run.Error.Code);
    }

    [Fact]
    public void A_run_that_wrote_no_summary_is_refused_as_AW_BUGFIX_017()
    {
        _stub.Set("exit", "0");

        var run = Adapter(("AW_UI_EVIDENCE_BIN", _stub.Path)).Run(Request());

        Assert.Equal(BugFixErrors.UiEvidenceUnavailable.Code, run.Error.Code);
    }

    [Fact]
    public void Without_AW_UI_EVIDENCE_BIN_the_binary_is_found_on_PATH()
    {
        _stub.Set("exit", "0");
        _stub.Set("summary.json", Summary("passed"));

        var run = Adapter(("PATH", _stub.Directory)).Run(Request());

        Assert.True(run.IsSuccess);
        Assert.True(File.Exists(Path.Combine(_stub.Directory, "argv")));
    }

    [Fact]
    public void A_missing_binary_is_refused_as_AW_BUGFIX_017_naming_the_variable()
    {
        var run = Adapter(("AW_UI_EVIDENCE_BIN", Path.Combine(_work.Path, "no-such-ui-evidence"))).Run(Request());

        Assert.Equal(BugFixErrors.UiEvidenceUnavailable.Code, run.Error.Code);
        Assert.Contains("AW_UI_EVIDENCE_BIN", run.Error.Message, StringComparison.Ordinal);
    }

    private static UiEvidenceCli Adapter(params (string Name, string Value)[] variables) => new(StubBinary.Environment(variables));

    private UiRunRequest Request() => new(Path.Combine(_work.Path, "script.json"), Path.Combine(_work.Path, "run"), new string('c', 40), _work.Path, Path.Combine(_work.Path, "run.log"));

    // The part of ui-evidence's summary.json the adapter reads (docs/csharp/evidence.md): outcome per step, the executed script's hash.
    private static string Summary(params string[] steps) =>
        $$"""{"schema":"evidence.summary/1","scriptSha256":"{{new string('a', 64)}}","steps":[{{string.Join(',', steps.Select(static (s, i) => string.Create(CultureInfo.InvariantCulture, $$"""{"index":{{i + 1}},"status":"{{s}}"}""")))}}]}""";
}
