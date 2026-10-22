using Aw.Sindri.Core.Checks;
using Xunit;

namespace Aw.Sindri.Testing.Checks;

/// <summary>
/// The contract every <see cref="ICheckCommands"/> honours: a command's exit code and output come back as an answer (a failure is an answer), a command past its timeout is
/// killed and says so, what it printed is cut to <see cref="CheckRun.TailLimit"/> characters from the end, a command that cannot start is an error, and a cancelled run throws.
/// </summary>
public abstract class CheckCommandsContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A port and a command that prints <paramref name="output"/> and exits with <paramref name="exitCode"/>.</summary>
    /// <param name="exitCode">The exit code.</param>
    /// <param name="output">The text it prints.</param>
    /// <returns>The port, the command and the working directory to run it in.</returns>
    protected abstract (ICheckCommands Port, CheckInvocation Command, string WorkingDirectory) Prints(int exitCode, string output);

    /// <summary>A port and a command that runs far past its timeout.</summary>
    /// <returns>The port, the command (a timeout of a few hundred milliseconds) and the working directory.</returns>
    protected abstract (ICheckCommands Port, CheckInvocation Command, string WorkingDirectory) Hangs();

    /// <summary>A port and a command that cannot be started (no such program).</summary>
    /// <returns>The port, the command and the working directory.</returns>
    protected abstract (ICheckCommands Port, CheckInvocation Command, string WorkingDirectory) Missing();

    /// <summary>A command that exits 0 passes.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_command_that_exits_0_passes_and_returns_its_output()
    {
        var (port, command, directory) = Prints(0, "all green");

        var run = await port.Run(command, directory, Token);

        Expect.True(run.IsSuccess);
        Expect.True(run.Value.Passed);
        Expect.Equal(0, run.Value.ExitCode);
        Expect.True(run.Value.Tail.Contains("all green", StringComparison.Ordinal));
    }

    /// <summary>A command that exits non-zero is an answer with its code, not an error.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_command_that_exits_non_zero_is_an_answer_with_its_code()
    {
        var (port, command, directory) = Prints(3, "2 failed");

        var run = await port.Run(command, directory, Token);

        Expect.True(run.IsSuccess);
        Expect.True(!run.Value.Passed);
        Expect.Equal(3, run.Value.ExitCode);
        Expect.True(!run.Value.TimedOut);
        Expect.True(run.Value.Tail.Contains("2 failed", StringComparison.Ordinal));
    }

    /// <summary>A command past its timeout is killed and reported as timed out.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_command_past_its_timeout_is_timed_out()
    {
        var (port, command, directory) = Hangs();

        var run = await port.Run(command, directory, Token);

        Expect.True(run.IsSuccess);
        Expect.True(run.Value.TimedOut);
        Expect.True(!run.Value.Passed);
    }

    /// <summary>The tail keeps the end of a long output, within the limit.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_long_output_is_cut_to_its_last_characters()
    {
        var (port, command, directory) = Prints(1, new string('a', CheckRun.TailLimit * 2) + "THE-END");

        var run = await port.Run(command, directory, Token);

        Expect.True(run.Value.Tail.Length <= CheckRun.TailLimit);
        Expect.True(run.Value.Tail.TrimEnd().EndsWith("THE-END", StringComparison.Ordinal));
    }

    /// <summary>A command that cannot be started is an error.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_command_that_cannot_start_is_an_error()
    {
        var (port, command, directory) = Missing();

        var run = await port.Run(command, directory, Token);

        Expect.True(run.IsError);
    }

    /// <summary>A run cancelled before it starts throws.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_cancelled_run_throws()
    {
        var (port, command, directory) = Prints(0, "x");
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var threw = false;
        try
        {
            _ = await port.Run(command, directory, cancelled.Token);
        }
        catch (OperationCanceledException)
        {
            threw = true;
        }

        Expect.True(threw);
    }
}
