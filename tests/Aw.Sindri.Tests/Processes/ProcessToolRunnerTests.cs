using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Tests.Processes;

// The real runner, over harmless system tools (printf, sh); never security or op.
[Collection(ProcessEnvironment.Name)]
public sealed class ProcessToolRunnerTests
{
    private readonly ProcessToolRunner _runner = new();

    [Fact, Trait("Lane", "integration")]
    public void It_returns_the_exit_code_and_standard_output()
    {
        var run = _runner.Run("printf", ["%s|%s", "a b", "$HOME"], TimeSpan.FromSeconds(5), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new ToolRun(ToolOutcome.Exited, 0, "a b|$HOME"), run);
    }

    [Fact, Trait("Lane", "integration")]
    public void A_non_zero_exit_is_reported()
        => Assert.Equal(3, _runner.Run("sh", ["-c", "exit 3"], TimeSpan.FromSeconds(5), cancellationToken: TestContext.Current.CancellationToken).ExitCode);

    [Fact, Trait("Lane", "integration")]
    public void A_tool_not_on_PATH_is_missing()
        => Assert.Equal(ToolRun.Missing, _runner.Run("aw-no-such-tool-7f3c2a", [], TimeSpan.FromSeconds(5), cancellationToken: TestContext.Current.CancellationToken));

    [Fact, Trait("Lane", "integration")]
    public void A_tool_past_its_timeout_is_killed()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var run = _runner.Run("sleep", ["10"], TimeSpan.FromMilliseconds(200), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(ToolRun.TimedOut, run);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Fact, Trait("Lane", "integration")]
    public void A_cancelled_run_kills_the_child_and_returns_cancelled_quickly()
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pid = "";
        var options = new ToolRunOptions(StandardErrorLine: line =>
        {
            pid = line;
            cancel.CancelAfter(TimeSpan.FromMilliseconds(200));   // the child is up: cancel it shortly after
        });
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var run = _runner.Run("sh", ["-c", "echo $$ >&2; exec sleep 10"], TimeSpan.FromSeconds(30), options: options, cancellationToken: cancel.Token);
        var elapsed = watch.Elapsed;
        var alive = _runner.Run("kill", ["-0", pid], TimeSpan.FromSeconds(5), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(ToolRun.Cancelled, run);
        Assert.True(elapsed < TimeSpan.FromSeconds(2), $"took {elapsed}");
        Assert.NotEqual(0, alive.ExitCode);   // `kill -0` fails: the child is gone
    }

    [Fact, Trait("Lane", "integration")]
    public void An_already_cancelled_token_returns_cancelled_without_starting_the_tool()
    {
        var marker = Path.Combine(Path.GetTempPath(), "aw-runner-started-" + Guid.NewGuid().ToString("N"));
        try
        {
            var run = _runner.Run("sh", ["-c", $"touch {marker}"], TimeSpan.FromSeconds(5), cancellationToken: new CancellationToken(canceled: true));

            Assert.Equal(ToolRun.Cancelled, run);
            Assert.False(File.Exists(marker));
        }
        finally
        {
            File.Delete(marker);
        }
    }

    [Fact, Trait("Lane", "integration")]
    public void A_cancel_that_arrives_after_the_child_exited_returns_its_real_result()
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var options = new ToolRunOptions(StandardErrorLine: _ =>
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(500));   // the child printed its line and exited long ago
            cancel.Cancel();
        });

        var run = _runner.Run("sh", ["-c", "echo done >&2; printf out; exit 7"], TimeSpan.FromSeconds(10), options: options, cancellationToken: cancel.Token);

        Assert.Equal(new ToolRun(ToolOutcome.Exited, 7, "out", "done\n"), run);
    }

    [Fact, Trait("Lane", "integration")]
    public void An_unset_variable_is_absent_in_the_child_and_a_set_one_is_present()
    {
        Environment.SetEnvironmentVariable("AW_TEST_INHERITED", "leaks");
        try
        {
            var options = new ToolRunOptions(Environment: new Dictionary<string, string?>(StringComparer.Ordinal) { ["AW_TEST_INHERITED"] = null, ["AW_TEST_ADDED"] = "added" });

            var run = _runner.Run("sh", ["-c", "printf '%s|%s' \"${AW_TEST_INHERITED-absent}\" \"${AW_TEST_ADDED-absent}\""], TimeSpan.FromSeconds(5), options: options, cancellationToken: TestContext.Current.CancellationToken);
            var untouched = _runner.Run("sh", ["-c", "printf '%s' \"${AW_TEST_INHERITED-absent}\""], TimeSpan.FromSeconds(5), cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal("absent|added", run.Output);
            Assert.Equal("leaks", untouched.Output);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AW_TEST_INHERITED", value: null);
        }
    }
}
