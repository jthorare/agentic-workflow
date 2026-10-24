using Aw.Evidence.Core;
using Aw.Evidence.Core.Desktop;
using Xunit;

namespace Aw.Evidence.Testing;

/// <summary>The IDesktopCommands contract: the exit code and both outputs read back, and a program that would not start is AW-EVIDENCE-015.</summary>
public abstract class DesktopCommandsContract
{
    /// <summary>Arranges a program with this behavior.</summary>
    /// <param name="program">What the program does.</param>
    /// <returns>The runner, the program and its folder.</returns>
    protected abstract Task<DesktopCommandsUnderTest> ArrangeAsync(CliProgram program);

    /// <summary>Arranges a program that is not there.</summary>
    /// <returns>The runner, the missing program and a folder.</returns>
    protected abstract Task<DesktopCommandsUnderTest> ArrangeMissingAsync();

    /// <summary>The exit code and standard output read back.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_exit_code_and_stdout_read_back()
    {
        var run = await ArrangeAsync(new CliProgram(3, "hello"));

        var outcome = await run.Commands.RunAsync(Spec(run), CancellationToken.None);

        Expect.That(outcome.IsSuccess && outcome.Value.ExitCode == 3 && outcome.Value.StandardOutput == "hello", "exit code 3 and stdout 'hello' come back");
    }

    /// <summary>A non-zero exit is an outcome, not an error.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_failing_exit_is_an_outcome_not_an_error()
    {
        var run = await ArrangeAsync(new CliProgram(1, string.Empty));

        var outcome = await run.Commands.RunAsync(Spec(run), CancellationToken.None);

        Expect.That(outcome.IsSuccess && outcome.Value.ExitCode == 1, "exit 1 is an outcome");
    }

    /// <summary>A program that is not there is a run failure.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_program_that_would_not_start_is_a_run_failure()
    {
        var run = await ArrangeMissingAsync();

        var outcome = await run.Commands.RunAsync(Spec(run), CancellationToken.None);

        Expect.That(outcome.IsError && outcome.Error.Code == EvidenceErrors.RunFailed.Code, "a program that cannot start is AW-EVIDENCE-015");
    }

    private static CommandSpec Spec(DesktopCommandsUnderTest run) =>
        new([run.Program], run.WorkingDirectory, new Dictionary<string, string>(StringComparer.Ordinal), TimeSpan.FromSeconds(20));
}
