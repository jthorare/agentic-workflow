using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Infrastructure.Launch.Usage;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch.Usage;

/// <summary>The Claude Code usage reader over fixture transcripts (never the real ~/.claude).</summary>
[Lane(TestLane.Unit)]
public sealed class ClaudeCodeUsageTests : IDisposable
{
    private static readonly DateTimeOffset _from = new(2026, 10, 17, 7, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _to = _from.AddDays(1);
    private readonly UsageFixture _files = new();

    public void Dispose() => _files.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Only_messages_stamped_inside_the_window_count()
    {
        _files.Write("-repo/a.jsonl",
            UsageFixture.Claude("m1", "2026-10-17T06:59:59.000Z", "claude-sonnet-4-5", 1, 0, 0, 1),   // just before: yesterday's local day
            UsageFixture.Claude("m2", "2026-10-17T07:00:00.000Z", "claude-sonnet-4-5", 100, 50, 10, 20),
            UsageFixture.Claude("m3", "2026-10-18T06:59:59.000Z", "claude-sonnet-4-5", 1, 1, 1, 1),
            UsageFixture.Claude("m4", "2026-10-18T07:00:00.000Z", "claude-sonnet-4-5", 9, 9, 9, 9));   // just after

        var used = await new ClaudeCodeUsage(_files.Root).Between(_from, _to, Token);

        Expect.Equal(new ModelTokens("claude-sonnet-4-5", 101, 51, 11, 21), Assert.Single(used));
    }

    [Fact]
    public async Task A_separate_login_launchs_transcript_under_its_agent_home_is_found_by_session()
    {
        using var home = new UsageFixture();
        home.Write("-repo/sess-9.jsonl", UsageFixture.Claude("m9", "2026-10-17T08:00:00.000Z", "claude-sonnet-4-5", 7, 0, 0, 3));

        var used = await new ClaudeCodeUsage(_files.Root, [home.Root]).OfSession(SessionId.From("sess-9"), Token);

        Expect.Equal(new ModelTokens("claude-sonnet-4-5", 7, 0, 0, 3), Assert.Single(used));
    }

    [Fact]
    public async Task An_owner_copy_launch_still_reads_the_owner_projects_and_an_empty_agent_home_adds_nothing()
    {
        using var home = new UsageFixture();
        _files.Write("-repo/sess-1.jsonl", UsageFixture.Claude("m1", "2026-10-17T08:00:00.000Z", "claude-sonnet-4-5", 10, 0, 0, 5));

        var used = await new ClaudeCodeUsage(_files.Root, [home.Root]).Between(_from, _to, Token);

        Expect.Equal(new ModelTokens("claude-sonnet-4-5", 10, 0, 0, 5), Assert.Single(used));
    }

    [Fact]
    public async Task A_message_written_twice_counts_once_and_models_stay_apart()
    {
        _files.Write("-repo/a.jsonl",
            UsageFixture.Claude("m1", "2026-10-17T08:00:00.000Z", "claude-sonnet-4-5", 10, 0, 0, 5),
            UsageFixture.Claude("m1", "2026-10-17T08:00:01.000Z", "claude-sonnet-4-5", 10, 0, 0, 5),
            UsageFixture.Claude("m2", "2026-10-17T09:00:00.000Z", "claude-haiku-4-5", 1, 2, 3, 4),
            "not json", """{"type":"user","timestamp":"2026-10-17T09:00:00.000Z"}""");
        _files.Write("-repo/a/subagents/agent-1.jsonl", UsageFixture.Claude("m3", "2026-10-17T10:00:00.000Z", "claude-sonnet-4-5", 1, 1, 1, 1));

        var used = (await new ClaudeCodeUsage(_files.Root).Between(_from, _to, Token)).OrderBy(static m => m.Model, StringComparer.Ordinal).ToList();

        Assert.Equal<ModelTokens>([new ModelTokens("claude-haiku-4-5", 1, 2, 3, 4), new ModelTokens("claude-sonnet-4-5", 11, 1, 1, 6)], used);
    }

    /// <summary>Pins #2777: a reader that keeps its place in each file answers like a fresh full read after lines are appended (a streamed message repeated with a higher count).</summary>
    [Fact]
    public async Task A_second_read_after_an_append_equals_a_fresh_full_read()
    {
        var path = Path.Combine(_files.Root, "-repo/a.jsonl");
        _files.Write("-repo/a.jsonl",
            UsageFixture.Claude("m1", "2026-10-17T08:00:00.000Z", "claude-sonnet-4-5", 10, 0, 0, 5),
            UsageFixture.Claude("m2", "2026-10-17T09:00:00.000Z", "claude-haiku-4-5", 1, 2, 3, 4));
        var reader = new ClaudeCodeUsage(_files.Root);
        _ = await reader.Between(_from, _to, Token);

        await File.AppendAllLinesAsync(path,
            [UsageFixture.Claude("m3", "2026-10-17T10:00:00.000Z", "claude-sonnet-4-5", 2, 0, 0, 2), UsageFixture.Claude("m1", "2026-10-17T08:00:00.000Z", "claude-sonnet-4-5", 10, 0, 0, 50)],
            Token);

        var again = await reader.Between(_from, _to, Token);

        Assert.Equal<ModelTokens>(await new ClaudeCodeUsage(_files.Root).Between(_from, _to, Token), again);
        Expect.Equal(new ModelTokens("claude-sonnet-4-5", 12, 0, 0, 52), again.Single(static m => string.Equals(m.Model, "claude-sonnet-4-5", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_line_still_being_written_is_read_once_it_is_complete()
    {
        var path = Path.Combine(_files.Root, "-repo/a.jsonl");
        _files.Write("-repo/a.jsonl", UsageFixture.Claude("m1", "2026-10-17T08:00:00.000Z", "claude-sonnet-4-5", 10, 0, 0, 5));
        var reader = new ClaudeCodeUsage(_files.Root);
        var half = UsageFixture.Claude("m2", "2026-10-17T09:00:00.000Z", "claude-sonnet-4-5", 1, 0, 0, 1);
        await File.AppendAllTextAsync(path, half[..40], Token);
        Expect.Equal(new ModelTokens("claude-sonnet-4-5", 10, 0, 0, 5), Assert.Single(await reader.Between(_from, _to, Token)));

        await File.AppendAllTextAsync(path, half[40..] + "\n", Token);

        Expect.Equal(new ModelTokens("claude-sonnet-4-5", 11, 0, 0, 6), Assert.Single(await reader.Between(_from, _to, Token)));
    }

    [Fact]
    public async Task A_file_rewritten_shorter_is_read_again_from_the_start()
    {
        _files.Write("-repo/a.jsonl",
            UsageFixture.Claude("m1", "2026-10-17T08:00:00.000Z", "claude-sonnet-4-5", 10, 0, 0, 5),
            UsageFixture.Claude("m2", "2026-10-17T09:00:00.000Z", "claude-sonnet-4-5", 20, 0, 0, 5));
        var reader = new ClaudeCodeUsage(_files.Root);
        _ = await reader.Between(_from, _to, Token);

        _files.Write("-repo/a.jsonl", UsageFixture.Claude("m9", "2026-10-17T11:00:00.000Z", "claude-haiku-4-5", 1, 0, 0, 1));

        var again = await reader.Between(_from, _to, Token);

        Assert.Equal<ModelTokens>(await new ClaudeCodeUsage(_files.Root).Between(_from, _to, Token), again);
        Expect.Equal(new ModelTokens("claude-haiku-4-5", 1, 0, 0, 1), Assert.Single(again));
    }

    [Fact]
    public async Task A_session_is_read_whole_by_its_id()
    {
        _files.Write("-repo/sess-1.jsonl", UsageFixture.Claude("m1", "2026-01-01T00:00:00.000Z", "claude-sonnet-4-5", 5, 0, 0, 5));
        _files.Write("-repo/sess-2.jsonl", UsageFixture.Claude("m2", "2026-10-17T08:00:00.000Z", "claude-sonnet-4-5", 7, 0, 0, 7));

        var used = await new ClaudeCodeUsage(_files.Root).OfSession(SessionId.From("sess-1"), Token);

        Expect.Equal(new ModelTokens("claude-sonnet-4-5", 5, 0, 0, 5), Assert.Single(used));
    }

    [Fact]
    public async Task A_missing_root_is_no_usage() => Assert.Empty(await new ClaudeCodeUsage(Path.Combine(_files.Root, "nowhere")).Between(_from, _to, Token));
}
