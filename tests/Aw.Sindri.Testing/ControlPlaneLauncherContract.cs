using Aw.Sindri.Core.Control;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The contract every <see cref="IControlPlaneLauncher"/> honours: it starts a child with a pid and a log, reports when
/// the child exits, and answers the last 'SND-' line of its log (or a note without that prefix). Run against the fake and,
/// over a stub executable, the real launcher.
/// </summary>
public abstract class ControlPlaneLauncherContract
{
    /// <summary>A launcher whose child writes <paramref name="output"/> to its log and exits.</summary>
    /// <param name="output">The child's log text, lines separated by '\n'.</param>
    /// <returns>The launcher.</returns>
    protected abstract IControlPlaneLauncher ExitingWith(string output);

    /// <summary>A child that exits reports it, and its last error line is the last 'SND-' line of its log.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_child_that_exits_reports_its_last_error_line()
    {
        using var child = ExitingWith("starting\nSND-API-003 first\nSND-LEASE-001 held by host/1 since then (epoch 4)\nbye").Launch().Value;

        await Exited(child);

        Expect.True(child.Pid > 0);
        Expect.True(child.LogPath.Length > 0);
        Expect.Equal("SND-LEASE-001 held by host/1 since then (epoch 4)", child.LastError());
    }

    /// <summary>A log without an error line answers a note, never an 'SND-' line.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_log_without_an_error_line_says_so()
    {
        using var child = ExitingWith("hello").Launch().Value;

        await Exited(child);

        Expect.True(!child.LastError().StartsWith("SND-", StringComparison.Ordinal));
    }

    /// <summary>Waits up to 5 s for <paramref name="child"/> to exit.</summary>
    /// <param name="child">The child.</param>
    /// <returns>The wait.</returns>
    protected static async Task Exited(ILaunchedProcess child)
    {
        for (var waited = 0; !child.HasExited; waited += 20)
        {
            Expect.True(waited < 5000);
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
    }
}
