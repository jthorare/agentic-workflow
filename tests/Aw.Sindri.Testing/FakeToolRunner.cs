using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Infrastructure.Secrets;

namespace Aw.Sindri.Testing;

/// <summary>Records each tool call and answers with a fixed run, or with what a script says per command; never starts a process. Held to ToolRunnerContract.</summary>
[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "Internal because IToolRunner is; Aw.Sindri.Tests uses it through InternalsVisibleTo, which MA0182 can't see.")]
internal sealed class FakeToolRunner : IToolRunner
{
    private readonly Func<string, IReadOnlyList<string>, ToolRun> _answer;

    /// <summary>Answers every call with <paramref name="answer"/>.</summary>
    /// <param name="answer">The run.</param>
    public FakeToolRunner(ToolRun answer)
    {
        _answer = (_, _) => answer;
    }

    /// <summary>Answers each call with what <paramref name="script"/> returns for that command and its arguments.</summary>
    /// <param name="script">The script.</param>
    public FakeToolRunner(Func<string, IReadOnlyList<string>, ToolRun> script)
    {
        _answer = script;
    }

    public List<(string FileName, IReadOnlyList<string> Arguments, TimeSpan Timeout)> Calls { get; } = [];

    public ToolRun Run(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout)
    {
        Calls.Add((fileName, [.. arguments], timeout));
        return _answer(fileName, arguments);
    }
}
