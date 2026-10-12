using System.Text;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Processes;

namespace Aw.Judge.Testing;

/// <summary>
/// The IProcessRunner fake: runs <see cref="FakeProgram"/>s by file name, instantly (a program that runs past the
/// request's timeout is reported timed out without waiting); an unknown name isn't started, and one marked FailsToStart, or asked to run in a working directory that doesn't
/// exist, fails to start. Passes ProcessRunnerContract.
/// </summary>
public sealed class FakeProcessRunner : IProcessRunner
{
    private readonly Dictionary<string, Func<ProcessRequest, FakeProgram>> _programs = new(StringComparer.Ordinal);
    private readonly List<ProcessCall> _calls = [];

    /// <summary>Every run so far, in order.</summary>
    public IReadOnlyList<ProcessCall> Calls => _calls;

    /// <summary>Installs a program.</summary>
    /// <param name="fileName">The name it runs under.</param>
    /// <param name="program">What it does.</param>
    /// <returns>This runner.</returns>
    public FakeProcessRunner With(string fileName, FakeProgram program) => With(fileName, _ => program);

    /// <summary>Installs a program whose behavior depends on the request (e.g. reads a file in its working directory).</summary>
    /// <param name="fileName">The name it runs under.</param>
    /// <param name="program">What it does for a request.</param>
    /// <returns>This runner.</returns>
    public FakeProcessRunner With(string fileName, Func<ProcessRequest, FakeProgram> program)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        _programs[fileName] = program;
        return this;
    }

    /// <inheritdoc />
    public ValueTask<ProcessResult> Run(ProcessRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var files = Files(request.WorkingDirectory);
        _calls.Add(new ProcessCall(request, files));
        if (!_programs.TryGetValue(request.FileName, out var install))
        {
            return ValueTask.FromResult(ProcessResult.NotStarted());
        }

        var program = install(request);
        if (Ended(program, request) is { } ended)
        {
            return ValueTask.FromResult(ended);
        }

        var stdout = new StringBuilder();
        if (program.ListsWorkingDirectory && files is not null)
        {
            stdout.AppendJoin("", files.Select(name => name + "\n"));
        }

        if (program.PrintsVariable is { } variable && request.Environment.TryGetValue(variable, out var value))
        {
            stdout.Append(value);
        }

        if (program.EchoesStandardInput)
        {
            stdout.Append(request.StandardInput);
        }

        var padding = new string('a', program.OutputSize);
        stdout.Append(padding).Append(program.StandardOutput);
        return ValueTask.FromResult(ProcessResult.Exited(program.ExitCode, Kept(stdout.ToString(), request), Kept(padding + program.StandardError, request)));
    }

    // A run that ends before the program writes anything: a missing working directory or a refused start (not started),
    // or a program past its timeout.
    private static ProcessResult? Ended(FakeProgram program, ProcessRequest request) =>
        request.WorkingDirectory is { Length: > 0 } directory && !Directory.Exists(directory) ? ProcessResult.FailedToStart($"working directory not found: {directory}")
        : program.FailsToStart ? ProcessResult.FailedToStart("Exec format error")
        : program.Runs >= request.Timeout ? ProcessResult.TimedOutAfter("", "")
        : null;

    private static string Kept(string text, ProcessRequest request) => text.Length <= request.OutputLimit ? text : text[..request.OutputLimit];

    private static List<string>? Files(string? directory) =>
        directory is not null && Directory.Exists(directory)
            ? [.. Directory.EnumerateFileSystemEntries(directory).Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal)]
            : null;
}
