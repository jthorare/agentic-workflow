using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Infrastructure.Tools;

namespace Aw.Sindri.Tests.Blocks;

/// <summary>
/// The tool runner against scripted binaries: a graceful call is sent SIGTERM first and killed after the grace, a
/// non-graceful one is killed at once, a cancel propagates, output drains after exit, an early stdin close still counts.
/// Each trapping stub writes 'ready' once its TERM trap is installed, and the stop tests wait for it, so load can't race
/// the trap.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ToolProcessTests : IDisposable
{
    private static readonly ToolSettings _settings = new("judge", "ui-evidence") { StopGrace = TimeSpan.FromSeconds(5) };

    private readonly StubTool _tool = new("trap 'kill $! 2>/dev/null; echo stopped > \"$d/marker\"; exit 130' TERM\necho ready > \"$d/ready\"\nsleep 30 &\nwait $!\n");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _tool.Dispose();

    [Fact]
    public async Task A_cancelled_graceful_call_sends_SIGTERM_so_the_tool_can_clean_up_and_throws()
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var run = ToolProcess.Run(Call(_tool, TimeSpan.FromSeconds(60), graceful: true), _settings, cancel.Token).AsTask();
        await Ready(_tool);
        await cancel.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

        Assert.Equal("stopped", _tool.Read("marker").Trim());
    }

    [Fact]
    public async Task A_graceful_call_past_its_timeout_is_tool_unavailable_after_SIGTERM()
    {
        var running = ToolProcess.Run(Call(_tool, TimeSpan.FromSeconds(3), graceful: true), _settings, Token).AsTask();
        await Ready(_tool);   // the timeout still fires on its own; this only makes sure the trap was in place by then

        var run = await running;

        Assert.Equal(SindriErrors.ToolUnavailable.Code, run.Error.Code);
        Assert.Contains("didn't finish within 3 s", run.Error.Message, StringComparison.Ordinal);
        Assert.Equal("stopped", _tool.Read("marker").Trim());
    }

    [Fact]
    public async Task A_tool_that_ignores_SIGTERM_is_killed_when_the_grace_runs_out()
    {
        using var stubborn = new StubTool("trap '' TERM\necho ready > \"$d/ready\"\nsleep 30\n");
        var settings = _settings with { StopGrace = TimeSpan.FromMilliseconds(300) };
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var run = ToolProcess.Run(Call(stubborn, TimeSpan.FromSeconds(60), graceful: true), settings, cancel.Token).AsTask();
        await Ready(stubborn);
        var started = TimeProvider.System.GetTimestamp();
        await cancel.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

        Assert.True(TimeProvider.System.GetElapsedTime(started) < TimeSpan.FromSeconds(20));
    }

    [Fact]
    public async Task A_non_graceful_call_is_killed_at_once_without_SIGTERM()
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var run = ToolProcess.Run(Call(_tool, TimeSpan.FromSeconds(60), graceful: false), _settings, cancel.Token).AsTask();
        await Ready(_tool);
        await cancel.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

        Assert.False(File.Exists(Path.Combine(_tool.Directory, "marker")));
    }

    [Fact]
    public async Task A_child_left_holding_the_output_doesnt_turn_a_finished_run_into_a_timeout()
    {
        using var leaky = new StubTool("sleep 15 &\nprintf ok\nexit 0\n");
        var started = TimeProvider.System.GetTimestamp();

        var run = await ToolProcess.Run(new ToolCall("tool", leaky.Binary, [], Stdin: null, TimeSpan.FromSeconds(8)), _settings, Token);

        // The child holds stdout for 15 s, past the 8 s timeout: only the 2 s drain grace after exit lets the run finish.
        Assert.Equal((0, "ok"), (run.Value.Exit, Encoding.UTF8.GetString(run.Value.Stdout)));
        Assert.True(TimeProvider.System.GetElapsedTime(started) < TimeSpan.FromSeconds(8));
    }

    [Fact]
    public async Task A_tool_that_closes_stdin_without_reading_it_still_reports_its_exit_and_output()
    {
        using var early = new StubTool("exec 0<&-\nprintf ok\nexit 3\n");

        var run = await ToolProcess.Run(new ToolCall("tool", early.Binary, [], new byte[1 << 20], TimeSpan.FromSeconds(10)), _settings, Token);

        Assert.Equal((3, "ok"), (run.Value.Exit, Encoding.UTF8.GetString(run.Value.Stdout)));
    }

    [Fact]
    public async Task The_error_line_is_the_last_stderr_line_after_progress_lines()
    {
        using var noisy = new StubTool("echo 'ui-evidence: waiting on lock: /l' >&2\necho '{\"level\":\"warning\"}' >&2\necho 'ui-evidence: AW-EVIDENCE-009 busy' >&2\nexit 75\n");

        var run = await ToolProcess.Run(new ToolCall("ui-evidence", noisy.Binary, [], Stdin: null, TimeSpan.FromSeconds(10)), _settings, Token);

        Assert.Equal("ui-evidence: AW-EVIDENCE-009 busy", run.Value.Message);
    }

    private static ToolCall Call(StubTool tool, TimeSpan timeout, bool graceful) => new("tool", tool.Binary, [], Stdin: null, timeout, graceful);

    private static async Task Ready(StubTool tool)
    {
        var ready = Path.Combine(tool.Directory, "ready");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        while (!File.Exists(ready))
        {
            await Task.Delay(20, deadline.Token);
        }
    }
}
