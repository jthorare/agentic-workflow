using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Testing;

/// <summary>Records each tool call and answers with a fixed run, or with what a script says per command; never starts a process. Held to ToolRunnerContract.</summary>
[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "Internal because IToolRunner is; Aw.Sindri.Tests uses it through InternalsVisibleTo, which MA0182 can't see.")]
internal sealed class FakeToolRunner : IToolRunner
{
    private readonly Func<string, IReadOnlyList<string>, ToolRun> _answer;
    private readonly IReadOnlyList<string> _standardErrorLines = [];

    /// <summary>Answers every call with <paramref name="answer"/>.</summary>
    /// <param name="answer">The run.</param>
    public FakeToolRunner(ToolRun answer)
    {
        _answer = (_, _) => answer;
    }

    /// <summary>Answers every call with <paramref name="answer"/>, after handing <paramref name="standardErrorLines"/> to the call's standard error callback.</summary>
    /// <param name="answer">The run.</param>
    /// <param name="standardErrorLines">The lines the tool "printed" on standard error.</param>
    public FakeToolRunner(ToolRun answer, IReadOnlyList<string> standardErrorLines)
    {
        _answer = (_, _) => answer;
        _standardErrorLines = standardErrorLines;
    }

    /// <summary>Answers each call with what <paramref name="script"/> returns for that command and its arguments, and feeds the call's standard error callback <paramref name="standardErrorLines"/> when they are given.</summary>
    /// <param name="script">The script.</param>
    /// <param name="standardErrorLines">The lines every scripted tool "prints" on standard error.</param>
    public FakeToolRunner(Func<string, IReadOnlyList<string>, ToolRun> script, IReadOnlyList<string>? standardErrorLines = null)
    {
        _answer = script;
        _standardErrorLines = standardErrorLines ?? [];
    }

    /// <summary>Gets an optional script of the standard error lines a call "prints", per command and arguments; when set it replaces the fixed lines.</summary>
    public Func<string, IReadOnlyList<string>, IReadOnlyList<string>>? StandardErrorFor { get; init; }

    /// <summary>Gets the standard input each call was given (null when none), in call order.</summary>
    public List<string?> Inputs { get; } = [];

    /// <summary>Gets the working directory each call was given (null when none), in call order.</summary>
    public List<string?> WorkingDirectories { get; } = [];

    public List<(string FileName, IReadOnlyList<string> Arguments, TimeSpan Timeout, bool HadStderrCallback, IReadOnlyDictionary<string, string?>? Environment)> Calls { get; } = [];

    public ToolRun Run(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, ToolRunOptions? options = null, CancellationToken cancellationToken = default)
    {
        var standardErrorLine = options?.StandardErrorLine;
        Inputs.Add(options?.StandardInput);
        WorkingDirectories.Add(options?.WorkingDirectory);
        Calls.Add((fileName, [.. arguments], timeout, standardErrorLine is not null, options?.Environment));
        if (cancellationToken.IsCancellationRequested)
        {
            return ToolRun.Cancelled;
        }

        if (standardErrorLine is not null)
        {
            foreach (var line in StandardErrorFor?.Invoke(fileName, arguments) ?? _standardErrorLines)
            {
                standardErrorLine(line);
            }
        }

        return _answer(fileName, arguments);
    }
}
