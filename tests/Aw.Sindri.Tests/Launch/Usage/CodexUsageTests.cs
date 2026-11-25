using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Infrastructure.Launch.Usage;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch.Usage;

/// <summary>The Codex usage reader over fixture rollouts (never the real ~/.codex).</summary>
[Lane(TestLane.Unit)]
public sealed class CodexUsageTests : IDisposable
{
    private const string Id = "0198f2a1-0000-7000-8000-000000000001";
    private static readonly DateTimeOffset _from = new(2026, 10, 17, 7, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _to = _from.AddDays(1);
    private readonly UsageFixture _files = new();

    public void Dispose() => _files.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_separate_login_launchs_rollout_under_its_agent_home_is_found_by_session()
    {
        using var home = new UsageFixture();
        home.Write($"2026/10/17/rollout-2026-10-17T08-00-00-{Id}.jsonl",
            UsageFixture.CodexModel("2026-10-17T08:00:00.000Z", "gpt-5"),
            UsageFixture.CodexTokens("2026-10-17T08:00:01.000Z", total: 110, input: 100, cached: 40, output: 10));

        var used = await new CodexUsage(_files.Root, [home.Root]).OfSession(SessionId.From(Id), Token);

        Assert.Equal("gpt-5", Assert.Single(used).Model);
    }

    [Fact]
    public async Task An_owner_copy_launch_still_reads_the_owner_sessions_when_an_agent_home_is_empty()
    {
        using var home = new UsageFixture();
        _files.Write($"2026/10/17/rollout-2026-10-17T08-00-00-{Id}.jsonl",
            UsageFixture.CodexModel("2026-10-17T08:00:00.000Z", "gpt-5"),
            UsageFixture.CodexTokens("2026-10-17T08:00:01.000Z", total: 110, input: 100, cached: 40, output: 10));

        var used = await new CodexUsage(_files.Root, [home.Root]).OfSession(SessionId.From(Id), Token);

        Assert.Equal("gpt-5", Assert.Single(used).Model);
    }

    [Fact]
    public async Task Turns_inside_the_window_count_with_cached_input_split_out()
    {
        _files.Write($"2026/10/17/rollout-2026-10-17T08-00-00-{Id}.jsonl",
            UsageFixture.CodexModel("2026-10-17T08:00:00.000Z", "gpt-5"),
            UsageFixture.CodexTokens("2026-10-17T06:00:00.000Z", total: 5, input: 4, cached: 0, output: 1),   // before the window
            UsageFixture.CodexTokens("2026-10-17T08:00:01.000Z", total: 110, input: 100, cached: 40, output: 10),
            UsageFixture.CodexTokens("2026-10-17T08:00:01.500Z", total: 110, input: 100, cached: 40, output: 10),   // the same turn reported again
            UsageFixture.CodexTokens("2026-10-17T08:05:00.000Z", total: 200, input: 80, cached: 0, output: 10));

        var used = await new CodexUsage(_files.Root).Between(_from, _to, Token);

        Expect.Equal(new ModelTokens("gpt-5", Input: 60 + 80, CacheRead: 40, CacheWrite: 0, Output: 20), Assert.Single(used));
    }

    /// <summary>Pins #3129: a second read parses only the lines appended since the first. An already-read line is rewritten in place with the same length, so a reader that re-reads the file sees the new numbers.</summary>
    [Fact]
    public async Task A_second_read_parses_only_the_appended_lines()
    {
        var relative = $"2026/10/17/rollout-2026-10-17T08-00-00-{Id}.jsonl";
        var path = Path.Combine(_files.Root, relative);
        var first = UsageFixture.CodexTokens("2026-10-17T08:00:01.000Z", total: 110, input: 100, cached: 40, output: 10);
        _files.Write(relative, UsageFixture.CodexModel("2026-10-17T08:00:00.000Z", "gpt-5"), first);
        var reader = new CodexUsage(_files.Root);
        Expect.Equal(new ModelTokens("gpt-5", Input: 60, CacheRead: 40, CacheWrite: 0, Output: 10), Assert.Single(await reader.Between(_from, _to, Token)));

        var sameLength = UsageFixture.CodexTokens("2026-10-17T08:00:01.000Z", total: 220, input: 200, cached: 40, output: 20);
        Assert.Equal(first.Length, sameLength.Length);
        await File.WriteAllTextAsync(path, (await File.ReadAllTextAsync(path, Token)).Replace(first, sameLength, StringComparison.Ordinal), Token);
        await File.AppendAllLinesAsync(path, [UsageFixture.CodexTokens("2026-10-17T08:05:00.000Z", total: 330, input: 80, cached: 0, output: 10)], Token);

        var again = await reader.Between(_from, _to, Token);

        // the first turn keeps the numbers read the first time; the appended turn is priced at the model the earlier turn_context named
        Expect.Equal(new ModelTokens("gpt-5", Input: 60 + 80, CacheRead: 40, CacheWrite: 0, Output: 10 + 10), Assert.Single(again));
    }

    [Fact]
    public async Task A_rollout_rewritten_shorter_is_read_again_from_the_start()
    {
        var relative = $"2026/10/17/rollout-2026-10-17T08-00-00-{Id}.jsonl";
        _files.Write(relative,
            UsageFixture.CodexModel("2026-10-17T08:00:00.000Z", "gpt-5"),
            UsageFixture.CodexTokens("2026-10-17T08:00:01.000Z", total: 110, input: 100, cached: 40, output: 10),
            UsageFixture.CodexTokens("2026-10-17T08:05:00.000Z", total: 200, input: 80, cached: 0, output: 10));
        var reader = new CodexUsage(_files.Root);
        _ = await reader.Between(_from, _to, Token);

        _files.Write(relative, UsageFixture.CodexModel("2026-10-17T09:00:00.000Z", "gpt-5-mini"), UsageFixture.CodexTokens("2026-10-17T09:00:01.000Z", total: 5, input: 4, cached: 0, output: 1));

        var again = await reader.Between(_from, _to, Token);

        Assert.Equal<ModelTokens>(await new CodexUsage(_files.Root).Between(_from, _to, Token), again);
        Expect.Equal(new ModelTokens("gpt-5-mini", Input: 4, CacheRead: 0, CacheWrite: 0, Output: 1), Assert.Single(again));
    }

    [Fact]
    public async Task A_second_read_after_an_append_equals_a_fresh_full_read()
    {
        var relative = $"2026/10/17/rollout-2026-10-17T08-00-00-{Id}.jsonl";
        var path = Path.Combine(_files.Root, relative);
        _files.Write(relative,
            UsageFixture.CodexModel("2026-10-17T08:00:00.000Z", "gpt-5"),
            UsageFixture.CodexTokens("2026-10-17T08:00:01.000Z", total: 110, input: 100, cached: 40, output: 10));
        var reader = new CodexUsage(_files.Root);
        var first = await reader.Between(_from, _to, Token);

        await File.AppendAllLinesAsync(path,
            [
                UsageFixture.CodexTokens("2026-10-17T08:05:00.000Z", total: 200, input: 80, cached: 0, output: 10),
                UsageFixture.CodexModel("2026-10-17T09:00:00.000Z", "gpt-5-mini"),
                UsageFixture.CodexTokens("2026-10-17T09:00:01.000Z", total: 260, input: 50, cached: 10, output: 10),
            ],
            Token);

        var again = await reader.Between(_from, _to, Token);

        Assert.NotEqual<ModelTokens>(first, again);
        Assert.Equal<ModelTokens>(await new CodexUsage(_files.Root).Between(_from, _to, Token), again);
        Assert.Equal(["gpt-5", "gpt-5-mini"], again.Select(static m => m.Model).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_session_is_read_whole_by_its_id()
    {
        _files.Write($"2026/01/01/rollout-2026-01-01T08-00-00-{Id}.jsonl",
            UsageFixture.CodexModel("2026-01-01T08:00:00.000Z", "gpt-5"),
            UsageFixture.CodexTokens("2026-01-01T08:00:01.000Z", total: 11, input: 10, cached: 0, output: 1));
        _files.Write("2026/10/17/rollout-2026-10-17T08-00-00-0198f2a1-0000-7000-8000-000000000002.jsonl",
            UsageFixture.CodexTokens("2026-10-17T08:00:01.000Z", total: 99, input: 90, cached: 0, output: 9));

        var used = await new CodexUsage(_files.Root).OfSession(SessionId.From(Id), Token);

        Expect.Equal(new ModelTokens("gpt-5", 10, 0, 0, 1), Assert.Single(used));
    }
}
