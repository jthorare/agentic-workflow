using Aw.Kernel.Testing;
using Aw.Transcripts.Core;
using Aw.Transcripts.Core.Refs;
using Aw.Transcripts.Core.Sessions;
using Aw.Transcripts.Core.Turns;
using Aw.Transcripts.Tests.Fixtures;

namespace Aw.Transcripts.Tests.Providers;

[Lane(TestLane.Integration)]
public sealed class CodexTranscriptTests : IDisposable
{
    private const string Main = "019a0000-0000-7000-8000-000000000001";
    private const string Child = "019a0000-0000-7000-8000-000000000002";
    private static readonly DateTimeOffset _t = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);
    private readonly TempRoots _roots = new();

    [Fact]
    public async Task A_rollout_reads_as_its_prompts_and_assistant_turns()
    {
        var path = _roots.Write($"codex/sessions/2026/11/03/rollout-2026-11-03T09-00-00-{Main}.jsonl", _t.AddHours(1),
            CodexLines.Meta(_t, Main, "/work/beta"),
            CodexLines.UserItem(_t, "# AGENTS.md instructions for /work/beta"),
            CodexLines.UserItem(_t, "<environment_context>cwd</environment_context>"),
            CodexLines.UserItem(_t.AddSeconds(1), "rename the flag"),
            CodexLines.UserMessage(_t.AddSeconds(1), "rename the flag"),
            CodexLines.Call(_t.AddSeconds(2), "exec_command"),
            CodexLines.CallOutput(_t.AddSeconds(3), "tool output"),
            CodexLines.Said(_t.AddSeconds(4), "Renamed."),
            CodexLines.UserMessage(_t.AddSeconds(5), "thanks"));

        var read = (await _roots.Corpus().ReadSession(TranscriptRefs.ForFile(TranscriptProvider.Codex, path), CancellationToken.None)).Value;

        Assert.Equal(["Human rename the flag", "Assistant Renamed. [exec_command]", "Human thanks"],
            read.Turns.Select(t => $"{t.Role} {t.Text}{(t.Tools.Count > 0 ? $" [{string.Join(',', t.Tools)}]" : "")}"), StringComparer.Ordinal);
        Assert.Equal(5, read.Turns[0].Ref.Line);
        Assert.Equal("/work/beta", read.Session.Repo);
        Assert.Equal(TranscriptProvider.Codex, read.Session.Provider);
        Assert.Equal(_t, read.Session.StartedAt);
    }

    [Fact]
    public async Task A_spawned_agent_has_no_human_turn_and_links_its_parent()
    {
        var parent = _roots.Write($"codex/sessions/2026/11/03/rollout-2026-11-03T09-00-00-{Main}.jsonl", _t.AddHours(1),
            CodexLines.Meta(_t, Main, "/work/beta"),
            CodexLines.UserMessage(_t.AddSeconds(1), "split the work"));
        _ = _roots.Write($"codex/sessions/2026/11/03/rollout-2026-11-03T09-01-00-{Child}.jsonl", _t.AddMinutes(30),
            CodexLines.Meta(_t.AddMinutes(1), Child, "/work/beta", CodexLines.Spawned(Main)),
            CodexLines.UserMessage(_t.AddMinutes(1), "do your half"),
            CodexLines.Said(_t.AddMinutes(2), "Half done."));

        var sessions = (await _roots.Corpus().ListSessions(new SessionQuery(_t) { Provider = TranscriptProvider.Codex }, CancellationToken.None)).Value;

        var child = Assert.Single(sessions, s => s.IsSubagent);
        Assert.Equal(TranscriptRefs.ForFile(TranscriptProvider.Codex, parent), child.Parent);
        Assert.Equal(0, child.HumanTurns);
        Assert.Equal(1, Assert.Single(sessions, s => !s.IsSubagent).HumanTurns);
    }

    [Fact]
    public async Task A_line_of_an_unknown_shape_is_counted_as_skipped()
    {
        var path = _roots.Write($"codex/sessions/2026/11/03/rollout-2026-11-03T09-00-00-{Main}.jsonl", _t.AddHours(1),
            CodexLines.Meta(_t, Main, "/work/beta"),
            """{"foo":1}""",
            """{"timestamp":"2026-11-03T09:00:01.000Z","type":"event_msg"}""",
            CodexLines.UserMessage(_t.AddSeconds(2), "go"));

        var read = (await _roots.Corpus().ReadSession(TranscriptRefs.ForFile(TranscriptProvider.Codex, path), CancellationToken.None)).Value;

        Assert.Equal(1, read.Session.HumanTurns);
        Assert.Equal(2, read.SkippedLines);
    }

    [Fact]
    public async Task An_exec_run_has_no_human_turn()
    {
        _ = _roots.Write($"codex/sessions/2026/11/03/rollout-2026-11-03T09-00-00-{Main}.jsonl", _t.AddHours(1),
            CodexLines.Meta(_t, Main, "/work/beta", "exec"),
            CodexLines.UserMessage(_t.AddSeconds(1), "a scripted prompt"),
            CodexLines.Said(_t.AddSeconds(2), "Done."));

        var session = Assert.Single((await _roots.Corpus().ListSessions(new SessionQuery(_t), CancellationToken.None)).Value);

        Assert.Equal(0, session.HumanTurns);
    }

    [Fact]
    public async Task A_response_only_rollout_reads_its_typed_prompts_as_human_turns()
    {
        var path = _roots.Write($"codex/sessions/2026/11/19/rollout-2026-11-19T09-00-00-{Main}.jsonl", _t.AddHours(1),
            CodexLines.Meta(_t, Main, "/work/beta"),
            CodexLines.UserItem(_t, "# AGENTS.md instructions for /work/beta"),
            CodexLines.UserItem(_t, "<environment_context>cwd</environment_context>"),
            CodexLines.UserItem(_t.AddSeconds(1), "make the startup command repeatable"),
            CodexLines.Said(_t.AddSeconds(2), "Done."),
            CodexLines.UserItem(_t.AddSeconds(3), "also add a flag"));

        var read = (await _roots.Corpus().ReadSession(TranscriptRefs.ForFile(TranscriptProvider.Codex, path), CancellationToken.None)).Value;

        Assert.Equal(["Human make the startup command repeatable", "Assistant Done.", "Human also add a flag"],
            read.Turns.Select(t => $"{t.Role} {t.Text}"), StringComparer.Ordinal);
        Assert.Equal(2, read.Session.HumanTurns);
    }

    [Fact]
    public async Task A_prompt_in_both_records_counts_once_whichever_record_comes_first()
    {
        var path = _roots.Write($"codex/sessions/2026/11/03/rollout-2026-11-03T09-00-00-{Main}.jsonl", _t.AddHours(1),
            CodexLines.Meta(_t, Main, "/work/beta"),
            CodexLines.UserItem(_t.AddSeconds(1), "first prompt"),
            CodexLines.UserMessage(_t.AddSeconds(1), "first prompt"),
            CodexLines.Said(_t.AddSeconds(2), "ok"),
            CodexLines.UserMessage(_t.AddSeconds(3), "second prompt"),
            CodexLines.UserItem(_t.AddSeconds(3), "second prompt"));

        var read = (await _roots.Corpus().ReadSession(TranscriptRefs.ForFile(TranscriptProvider.Codex, path), CancellationToken.None)).Value;

        Assert.Equal(["Human first prompt", "Assistant ok", "Human second prompt"],
            read.Turns.Select(t => $"{t.Role} {t.Text}"), StringComparer.Ordinal);
        Assert.Equal(2, read.Session.HumanTurns);
    }

    [Fact]
    public async Task A_resumed_rollout_mixing_both_formats_keeps_every_prompt()
    {
        var path = _roots.Write($"codex/sessions/2026/11/03/rollout-2026-11-03T09-00-00-{Main}.jsonl", _t.AddHours(1),
            CodexLines.Meta(_t, Main, "/work/beta"),
            CodexLines.UserItem(_t, "<environment_context>cwd</environment_context>"),
            CodexLines.UserItem(_t.AddSeconds(1), "old style"),
            CodexLines.UserMessage(_t.AddSeconds(1), "old style"),
            CodexLines.Said(_t.AddSeconds(2), "ok"),
            CodexLines.UserItem(_t.AddSeconds(3), "new style"),
            CodexLines.Said(_t.AddSeconds(4), "ok"),
            CodexLines.UserMessage(_t.AddSeconds(5), "old style again"),
            CodexLines.UserItem(_t.AddSeconds(5), "old style again"));

        var read = (await _roots.Corpus().ReadSession(TranscriptRefs.ForFile(TranscriptProvider.Codex, path), CancellationToken.None)).Value;

        Assert.Equal(["old style", "new style", "old style again"],
            read.Turns.Where(t => t.Role == TurnRole.Human).Select(t => t.Text), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_response_only_prompt_of_an_exec_run_or_a_spawned_agent_is_not_human()
    {
        _ = _roots.Write($"codex/sessions/2026/11/03/rollout-2026-11-03T09-00-00-{Main}.jsonl", _t.AddHours(1),
            CodexLines.Meta(_t, Main, "/work/beta", "exec"),
            CodexLines.UserItem(_t.AddSeconds(1), "a scripted prompt"));
        _ = _roots.Write($"codex/sessions/2026/11/03/rollout-2026-11-03T09-01-00-{Child}.jsonl", _t.AddMinutes(30),
            CodexLines.Meta(_t.AddMinutes(1), Child, "/work/beta", CodexLines.Spawned(Main)),
            CodexLines.UserItem(_t.AddMinutes(1), "do your half"));

        var sessions = (await _roots.Corpus().ListSessions(new SessionQuery(_t), CancellationToken.None)).Value;

        Assert.All(sessions, s => Assert.Equal(0, s.HumanTurns));
    }

    [Fact]
    public async Task A_response_only_compaction_summary_is_not_human()
    {
        _ = _roots.Write($"codex/sessions/2026/11/03/rollout-2026-11-03T09-00-00-{Main}.jsonl", _t.AddHours(1),
            CodexLines.Meta(_t, Main, "/work/beta"),
            CodexLines.UserItem(_t.AddSeconds(1), "This session is being continued from a previous conversation that ran out of context."),
            CodexLines.UserItem(_t.AddSeconds(2), "real prompt"));

        var session = Assert.Single((await _roots.Corpus().ListSessions(new SessionQuery(_t), CancellationToken.None)).Value);

        Assert.Equal(1, session.HumanTurns);
    }

    public void Dispose() => _roots.Dispose();
}
