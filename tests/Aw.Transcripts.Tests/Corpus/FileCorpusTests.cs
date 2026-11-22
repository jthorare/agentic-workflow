using Aw.Kernel.Testing;
using Aw.Transcripts.Core;
using Aw.Transcripts.Core.Refs;
using Aw.Transcripts.Core.Sessions;
using Aw.Transcripts.Tests.Fixtures;

namespace Aw.Transcripts.Tests.Corpus;

[Lane(TestLane.Integration)]
public sealed class FileCorpusTests : IDisposable
{
    private static readonly DateTimeOffset _t = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);
    private readonly TempRoots _roots = new();

    [Fact]
    public async Task A_file_written_before_the_time_is_not_listed()
    {
        _ = _roots.Write("claude/projects/-a/new.jsonl", _t.AddMinutes(1), ClaudeLines.User("new", _t));
        _ = _roots.Write("claude/projects/-a/stale.jsonl", _t.AddMinutes(-1), ClaudeLines.User("stale", _t.AddMinutes(-2)));

        var listed = (await _roots.Corpus().ListSessions(new SessionQuery(_t), CancellationToken.None)).Value;

        Assert.Equal([_t.AddMinutes(1)], listed.Select(s => s.LastWrittenAt));
    }

    [Fact]
    public async Task Missing_roots_list_nothing()
    {
        var listed = await _roots.Corpus().ListSessions(new SessionQuery(DateTimeOffset.MinValue), CancellationToken.None);

        Assert.True(listed.IsSuccess);
        Assert.Empty(listed.Value);
    }

    [Fact]
    public async Task A_resume_whose_original_is_older_than_the_time_still_counts_only_its_own_prompts()
    {
        var copied = ClaudeLines.User("the original prompt", _t);
        _ = _roots.Write("claude/projects/-a/original.jsonl", _t.AddMinutes(10), copied, ClaudeLines.Assistant(_t.AddMinutes(1), "ok"));
        _ = _roots.Write("claude/projects/-a/resume.jsonl", _t.AddHours(2), copied, ClaudeLines.Assistant(_t.AddMinutes(1), "ok"), ClaudeLines.User("a new prompt", _t.AddHours(1)));

        var listed = (await _roots.Corpus().ListSessions(new SessionQuery(_t.AddHours(1)), CancellationToken.None)).Value;

        Assert.Equal(1, Assert.Single(listed).HumanTurns);
    }

    [Fact]
    public async Task The_same_prompt_at_another_time_is_not_a_copy()
    {
        _ = _roots.Write("claude/projects/-a/one.jsonl", _t.AddMinutes(10), ClaudeLines.User("continue", _t));
        _ = _roots.Write("claude/projects/-a/two.jsonl", _t.AddMinutes(20), ClaudeLines.User("continue", _t.AddMinutes(5)));

        var listed = (await _roots.Corpus().ListSessions(new SessionQuery(_t), CancellationToken.None)).Value;

        Assert.All(listed, s => Assert.Equal(1, s.HumanTurns));
    }

    [Fact]
    public async Task Extra_roots_are_read_like_the_default_ones()
    {
        var extra = Path.Combine(_roots.Root, "agent-homes", "claude-code", "projects");
        _ = _roots.Write("agent-homes/claude-code/projects/-a/s.jsonl", _t.AddMinutes(1), ClaudeLines.User("in an agent home", _t));
        var corpus = new Infrastructure.FileTranscriptCorpus(new Infrastructure.TranscriptsSettings([.. _roots.Roots, new TranscriptRoot(TranscriptProvider.ClaudeCode, extra)]), new Infrastructure.Reading.TranscriptFileReader(Redaction.Redactor.Default));

        var listed = (await corpus.ListSessions(new SessionQuery(_t), CancellationToken.None)).Value;

        Assert.Equal(1, Assert.Single(listed).HumanTurns);
    }

    [Fact]
    public async Task A_ref_of_a_deleted_file_is_not_found()
    {
        var path = _roots.Write("claude/projects/-a/gone.jsonl", _t, ClaudeLines.User("x", _t));
        var session = TranscriptRefs.ForFile(TranscriptProvider.ClaudeCode, path);
        File.Delete(path);

        var read = await _roots.Corpus().ReadSession(session, CancellationToken.None);

        Assert.Equal(TranscriptsErrors.SessionNotFound.Code, read.Error.Code);
    }

    public void Dispose() => _roots.Dispose();
}
