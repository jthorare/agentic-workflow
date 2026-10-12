using System.Diagnostics;
using System.Globalization;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Processes;
using Xunit;

namespace Aw.Judge.Testing;

/// <summary>The IProcessRunner contract: <see cref="FakeProcessRunner"/> and the system runner both pass it.</summary>
public abstract class ProcessRunnerContract : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("judge-runner-contract-").FullName;

    /// <summary>A runner on which exactly these programs are installed, each by its name.</summary>
    /// <param name="programs">The programs.</param>
    /// <returns>The runner.</returns>
    protected abstract IProcessRunner Create(IReadOnlyDictionary<string, FakeProgram> programs);

    /// <summary>Exit code and both streams come back.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_exit_code_and_both_streams_come_back()
    {
        var result = await Run(new FakeProgram { StandardOutput = "out", StandardError = "err", ExitCode = 3 });

        Expect.That(result == ProcessResult.Exited(3, "out", "err"), $"exit 3 with 'out' and 'err', got {result}");
    }

    /// <summary>The standard input reaches the program.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_standard_input_reaches_the_program()
    {
        var result = await Run(new FakeProgram { EchoesStandardInput = true }, request => request with { StandardInput = "{\"a\":1}" });

        Expect.That(result.StandardOutput == "{\"a\":1}", $"the program read its stdin, got '{result.StandardOutput}'");
    }

    /// <summary>Without standard input the program sees a closed stream, so it doesn't wait for one.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Without_input_the_program_sees_a_closed_standard_input()
    {
        var result = await Run(new FakeProgram { EchoesStandardInput = true, StandardOutput = "done" });

        Expect.That(result == ProcessResult.Exited(0, "done", ""), $"the program read an empty stdin and exited, got {result}");
    }

    /// <summary>The program runs in the requested working directory.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_program_runs_in_the_working_directory()
    {
        var workspace = Directory.CreateDirectory(Path.Combine(_directory, "workspace")).FullName;
        await File.WriteAllTextAsync(Path.Combine(workspace, "marker.txt"), "", TestContext.Current.CancellationToken);

        var result = await Run(new FakeProgram { ListsWorkingDirectory = true }, request => request with { WorkingDirectory = workspace });

        Expect.That(result.StandardOutput.Contains("marker.txt", StringComparison.Ordinal), $"the program listed its working directory, got '{result.StandardOutput}'");
    }

    /// <summary>The request's variables are in the program's environment.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_request_variables_reach_the_program()
    {
        var result = await Run(
            new FakeProgram { PrintsVariable = "AW_JUDGE_CONTRACT" },
            request => request with { Environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["AW_JUDGE_CONTRACT"] = "set" } });

        Expect.That(result.StandardOutput == "set", $"the program saw the variable, got '{result.StandardOutput}'");
    }

    /// <summary>A program past its timeout is stopped near the timeout and reported timed out.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_program_past_its_timeout_is_stopped_and_reported()
    {
        var clock = Stopwatch.StartNew();

        var result = await Run(new FakeProgram { Runs = TimeSpan.FromSeconds(30), StandardOutput = "late" }, request => request with { Timeout = TimeSpan.FromMilliseconds(300) });

        Expect.That(result.TimedOut && result.ExitCode is null, $"the run is timed out, got {result}");
        Expect.That(clock.Elapsed < TimeSpan.FromSeconds(10), $"the run stopped near its 300 ms timeout, took {clock.Elapsed}");
    }

    /// <summary>A program that isn't installed isn't started, and nothing throws.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_missing_program_is_not_started()
    {
        var runner = Create(new Dictionary<string, FakeProgram>(StringComparer.Ordinal));

        var result = await runner.Run(new ProcessRequest { FileName = "judge-contract-missing", Timeout = TimeSpan.FromSeconds(10) }, CancellationToken.None);

        Expect.That(result == ProcessResult.NotStarted(), $"a missing program is not started, got {result}");
    }

    /// <summary>A program that is installed but can't be run is a start failure, not a missing program, and nothing throws.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_program_the_system_refuses_to_start_failed_to_start()
    {
        var result = await Run(new FakeProgram { FailsToStart = true });

        Expect.That(result.End == ProcessEnd.FailedToStart && result.ExitCode is null, $"the start failed, got {result}");
    }

    /// <summary>
    /// An installed program asked to run in a working directory that doesn't exist failed to start: the program is there,
    /// the caller's path is wrong, so it must not read as a missing program.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_missing_working_directory_failed_to_start()
    {
        var missing = Path.Combine(_directory, "no-such-directory");

        var result = await Run(new FakeProgram { StandardOutput = "ran" }, request => request with { WorkingDirectory = missing });

        Expect.That(result.End == ProcessEnd.FailedToStart && result.ExitCode is null, $"the start failed, got {result}");
    }

    /// <summary>Each stream is kept up to the request's output limit; the rest is dropped and the run still ends normally.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Output_past_the_limit_is_cut_at_it()
    {
        var result = await Run(new FakeProgram { OutputSize = 5_000, StandardOutput = "!" }, request => request with { OutputLimit = 1_000 });

        Expect.That(result.ExitCode == 0 && result.StandardOutput == new string('a', 1_000) && result.StandardError == new string('a', 1_000), $"both streams cut at 1,000 chars, got exit {result.ExitCode?.ToString(CultureInfo.InvariantCulture)}, {result.StandardOutput.Length.ToString(CultureInfo.InvariantCulture)} and {result.StandardError.Length.ToString(CultureInfo.InvariantCulture)} chars");
    }

    /// <summary>A megabyte on each stream at once doesn't deadlock on full pipes.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Large_output_on_both_streams_comes_back_whole()
    {
        const int Size = 1 << 20;

        var result = await Run(new FakeProgram { OutputSize = Size, StandardOutput = "!" }, request => request with { Timeout = TimeSpan.FromSeconds(30) });

        Expect.That(result.ExitCode == 0 && result.StandardOutput.Length == Size + 1 && result.StandardError.Length == Size, $"both streams came back whole, got exit {result.ExitCode?.ToString(CultureInfo.InvariantCulture)}, {result.StandardOutput.Length.ToString(CultureInfo.InvariantCulture)} and {result.StandardError.Length.ToString(CultureInfo.InvariantCulture)} chars");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Deletes the contract's temporary directory.</summary>
    /// <param name="disposing">Whether called from Dispose.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing && Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private async Task<ProcessResult> Run(FakeProgram program, Func<ProcessRequest, ProcessRequest>? shape = null)
    {
        var runner = Create(new Dictionary<string, FakeProgram>(StringComparer.Ordinal) { ["judge-contract-program"] = program });
        var request = new ProcessRequest { FileName = "judge-contract-program", Timeout = TimeSpan.FromSeconds(10) };
        return await runner.Run(shape is null ? request : shape(request), CancellationToken.None);
    }
}
