using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>Scripted helper processes: the first rule whose predicate matches the program and its arguments answers; every call is recorded.</summary>
internal sealed class FakeProcesses : ISandboxProcesses
{
    private readonly List<(Func<string, IReadOnlyList<string>, bool> Match, ProcessResult Result)> _rules = [];

    /// <summary>Gets or sets what happens on every call, before it is answered (a test moves its fake clock here).</summary>
    internal Action? OnRun { get; set; }

    internal List<(string Program, IReadOnlyList<string> Arguments)> Calls { get; } = [];

    internal static ProcessResult Ok(string output = "") => new(Started: true, ExitCode: 0, Output: output, Error: string.Empty, TimedOut: false);

    internal static ProcessResult Failed(string error, int exit = 1) => new(Started: true, ExitCode: exit, Output: string.Empty, Error: error, TimedOut: false);

    internal FakeProcesses When(Func<string, IReadOnlyList<string>, bool> match, ProcessResult result)
    {
        _rules.Add((match, result));
        return this;
    }

    public ProcessResult Run(string program, IReadOnlyList<string> arguments, TimeSpan timeout)
    {
        Calls.Add((program, arguments));
        OnRun?.Invoke();
        foreach (var (match, result) in _rules)
        {
            if (match(program, arguments))
            {
                return result;
            }
        }

        return ProcessResult.NotStarted;
    }
}
