using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Transcripts.Core.Refs;
using Aw.Transcripts.Core.Sessions;
using Aw.Transcripts.Core.Turns;
using Aw.Transcripts.Tests.Fixtures;

namespace Aw.Transcripts.Tests.Providers;

[Lane(TestLane.Integration)]
public sealed class ClaudeTranscriptTests : IDisposable
{
    private static readonly DateTimeOffset _t = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);
    private readonly TempRoots _roots = new();

    [Fact]
    public async Task Only_typed_prompts_are_human_turns()
    {
        var read = await Read(
            ClaudeLines.User("the one real prompt", _t),
            ClaudeLines.ToolResult(_t.AddSeconds(1), "tool output"),
            ClaudeLines.User("<system-reminder>reminder</system-reminder>", _t.AddSeconds(2)),
            ClaudeLines.User("<command-name>/review</command-name><command-args>x</command-args>", _t.AddSeconds(3)),
            ClaudeLines.User("<teammate-message teammate_id=\"lead\">build it</teammate-message>", _t.AddSeconds(4)),
            ClaudeLines.User("a peer delivery", _t.AddSeconds(5), l => l["origin"] = new JsonObject { ["kind"] = "peer" }),
            ClaudeLines.User("<task-notification>done</task-notification>", _t.AddSeconds(6), l => l["origin"] = new JsonObject { ["kind"] = "task-notification" }),
            ClaudeLines.User("hook feedback", _t.AddSeconds(7), l => l["isMeta"] = true),
            ClaudeLines.User("This session is being continued from a previous conversation.", _t.AddSeconds(8), l => l["isCompactSummary"] = true),
            ClaudeLines.User("a scripted prompt", _t.AddSeconds(9), l => l["entrypoint"] = "sdk-cli"),
            ClaudeLines.User("Caveat: local command output follows", _t.AddSeconds(10)),
            ClaudeLines.User("a typed prompt with an origin", _t.AddSeconds(11), l => l["origin"] = new JsonObject { ["kind"] = "human" }),
            ClaudeLines.UserBlocks(_t.AddSeconds(12), "a prompt in", "text blocks"),
            ClaudeLines.User("<ide_opened_file>The user opened a.cs</ide_opened_file> fix the null check", _t.AddSeconds(13)),
            ClaudeLines.User("<ide_selection>lines 1-4</ide_selection>", _t.AddSeconds(14)));

        Assert.Equal(["the one real prompt", "a typed prompt with an origin", "a prompt in\ntext blocks", "fix the null check"], read.Turns.Where(t => t.Role == TurnRole.Human).Select(t => t.Text), StringComparer.Ordinal);
        Assert.Equal(4, read.Session.HumanTurns);
    }

    [Fact]
    public async Task Assistant_turns_carry_tool_names_and_no_tool_payloads()
    {
        var read = await Read(
            ClaudeLines.User("go", _t),
            ClaudeLines.Assistant(_t.AddSeconds(1), "Reading.", "Read", "Bash"),
            ClaudeLines.ToolResult(_t.AddSeconds(2), "tool output"),
            ClaudeLines.Assistant(_t.AddSeconds(3), text: null, "Bash"),
            ClaudeLines.Assistant(_t.AddSeconds(4), "Done."));

        var assistant = Assert.Single(read.Turns, t => t.Role == TurnRole.Assistant);
        Assert.Equal("Reading.\nDone.", assistant.Text);
        Assert.Equal(["Read", "Bash"], assistant.Tools);
        Assert.DoesNotContain(read.Turns, t => t.Text.Contains("never read", StringComparison.Ordinal) || t.Text.Contains("tool output", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_garbage_line_is_skipped_and_counted()
    {
        var read = await Read(
            ClaudeLines.User("before", _t),
            "{not json",
            "[1,2,3]",
            "",
            ClaudeLines.User("after", _t.AddSeconds(5)));

        Assert.Equal(["before", "after"], read.Turns.Select(t => t.Text), StringComparer.Ordinal);
        Assert.Equal(2, read.SkippedLines);
        Assert.Equal(5, read.Turns[1].Ref.Line);
    }

    [Fact]
    public async Task A_line_of_an_unknown_shape_is_counted_as_skipped()
    {
        var read = await Read(
            ClaudeLines.User("before", _t),
            """{"foo":1}""",
            """{"type":"user","timestamp":"2026-11-03T09:00:01.000Z","message":{"content":42}}""",
            """{"type":"assistant","timestamp":"2026-11-03T09:00:02.000Z"}""",
            ClaudeLines.Bookkeeping(),
            ClaudeLines.User("after", _t.AddSeconds(5)));

        Assert.Equal(["before", "after"], read.Turns.Select(t => t.Text), StringComparer.Ordinal);
        Assert.Equal(3, read.SkippedLines);
    }

    [Fact]
    public async Task Secrets_in_turn_text_are_scrubbed()
    {
        const string Key = "ghp_" + "abcdefghijklmnopqrstuvwxyz0123456789";
        var read = await Read(
            ClaudeLines.User($"use the token {Key} please", _t),
            ClaudeLines.Assistant(_t.AddSeconds(1), $"Set GITHUB_TOKEN={Key}"));

        Assert.All(read.Turns, t => Assert.DoesNotContain(Key, t.Text, StringComparison.Ordinal));
        Assert.Contains("[REDACTED", read.Turns[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_subagent_file_has_no_human_turn_and_links_its_parent()
    {
        var parent = _roots.Write("claude/projects/-work-alpha/s1.jsonl", _t.AddMinutes(9), ClaudeLines.User("spawn a helper", _t));
        _ = _roots.Write("claude/projects/-work-alpha/s1/subagents/agent-a9.jsonl", _t.AddMinutes(8),
            ClaudeLines.Task("look for the bug", _t.AddMinutes(1)),
            ClaudeLines.Assistant(_t.AddMinutes(2), "Found.", "Grep"));
        var corpus = _roots.Corpus();

        var sessions = (await corpus.ListSessions(new SessionQuery(_t), CancellationToken.None)).Value;

        var sub = Assert.Single(sessions, s => s.IsSubagent);
        Assert.Equal(TranscriptRefs.ForFile(Core.TranscriptProvider.ClaudeCode, parent), sub.Parent);
        Assert.Equal(0, sub.HumanTurns);
        var read = (await corpus.ReadSession(sub.Ref, CancellationToken.None)).Value;
        Assert.Equal([TurnRole.Assistant], read.Turns.Select(t => t.Role));
    }

    public void Dispose() => _roots.Dispose();

    private async Task<SessionTranscript> Read(params string[] lines)
    {
        var path = _roots.Write("claude/projects/-work-alpha/one.jsonl", _t.AddHours(1), lines);
        var read = await _roots.Corpus().ReadSession(TranscriptRefs.ForFile(Core.TranscriptProvider.ClaudeCode, path), CancellationToken.None);
        return read.Value;
    }
}
