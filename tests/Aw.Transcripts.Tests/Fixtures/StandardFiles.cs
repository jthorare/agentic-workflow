using Aw.Transcripts.Core;
using Aw.Transcripts.Core.Refs;
using Aw.Transcripts.Testing;

namespace Aw.Transcripts.Tests.Fixtures;

/// <summary>Writes <see cref="StandardCorpus"/> as Claude Code files: one project directory with main, its fork, its subagent and an old session.</summary>
internal static class StandardFiles
{
    private const string Project = "claude/projects/-work-alpha";

    public static Func<string, SessionRef> Write(TempRoots roots)
    {
        var s = StandardCorpus.Start;
        var exchange = new[]
        {
            ClaudeLines.User("fix the login bug", s),
            ClaudeLines.Assistant(s.AddMinutes(1), "Looking at it.", "Read"),
            ClaudeLines.ToolResult(s.AddMinutes(1), "file contents"),
            ClaudeLines.Assistant(s.AddMinutes(2), "Fixed.", "Edit"),
        };
        Dictionary<string, string> paths = new(StringComparer.Ordinal)
        {
            ["main"] = roots.Write($"{Project}/main.jsonl", s.AddMinutes(30),
            [
                ClaudeLines.Bookkeeping(),
                .. exchange,
                ClaudeLines.User("<system-reminder>a reminder</system-reminder>", s.AddMinutes(3)),
                ClaudeLines.User("now the tests", s.AddMinutes(5)),
                ClaudeLines.Assistant(s.AddMinutes(6), "Tests pass."),
            ]),
            ["fork"] = roots.Write($"{Project}/fork.jsonl", s.AddHours(1),
            [
                ClaudeLines.Bookkeeping(),
                .. exchange,
                ClaudeLines.User("ship it", s.AddMinutes(40)),
                ClaudeLines.Assistant(s.AddMinutes(41), "Shipped.", "Bash"),
            ]),
            ["sub"] = roots.Write($"{Project}/main/subagents/agent-a1.jsonl", s.AddMinutes(20),
                ClaudeLines.Task("find the bug", s.AddMinutes(2)),
                ClaudeLines.Assistant(s.AddMinutes(3), "Found it.", "Grep")),
            ["old"] = roots.Write($"{Project}/old.jsonl", s.AddDays(-10).AddMinutes(5),
                ClaudeLines.User("an old prompt", s.AddDays(-10))),
        };
        return name => TranscriptRefs.ForFile(TranscriptProvider.ClaudeCode, paths[name]);
    }
}
