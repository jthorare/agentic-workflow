using Aw.Kernel.Testing;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Adapters;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Integration)]
public sealed class DirectoryLaneFilesContractTests : LaneFilesContract, IDisposable
{
    private readonly TempDirectory _dir = new();

    // Writes what scripts/meta-lane.sh and the session-id hook write: <lane> holds the id, <lane>.agent the facts, <lane>.ambiguous marks a refusal.
    protected override ValueTask<(ILaneFiles Files, Func<LaneRecord, Task> Put)> Create() =>
        ValueTask.FromResult<(ILaneFiles, Func<LaneRecord, Task>)>((new DirectoryLaneFiles(_dir.Path), Put));

    private async Task Put(LaneRecord record)
    {
        if (record.Session is { } session)
        {
            await File.WriteAllTextAsync(Path.Combine(_dir.Path, record.Lane), session.Value + "\n", TestContext.Current.CancellationToken);
        }

        if (record.Agent is { } agent)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["agent"] = agent.Agent,
                ["processName"] = agent.ProcessName,
                ["hooks"] = agent.Hooks,
                ["transcripts"] = agent.Transcripts,
            });
            await File.WriteAllTextAsync(Path.Combine(_dir.Path, record.Lane + ".agent"), json, TestContext.Current.CancellationToken);
        }

        if (record.Ambiguous)
        {
            await File.WriteAllTextAsync(Path.Combine(_dir.Path, record.Lane + ".ambiguous"), string.Empty, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task A_lane_name_that_could_escape_the_directory_is_never_read()
    {
        await File.WriteAllTextAsync(Path.Combine(_dir.Path, "secret"), "x", TestContext.Current.CancellationToken);
        var files = new DirectoryLaneFiles(Path.Combine(_dir.Path, "lanes"));

        var record = await files.Record("../secret", CancellationToken.None);

        Assert.Null(record.Value);
    }

    [Fact]
    public async Task A_lane_name_with_a_dot_or_an_underscore_reads_as_team_init_allows_but_only_dots_never_does()
    {
        await File.WriteAllTextAsync(Path.Combine(_dir.Path, "a_b.c"), "sess-1\n", TestContext.Current.CancellationToken);
        var files = new DirectoryLaneFiles(_dir.Path);

        var named = await files.Record("a_b.c", CancellationToken.None);
        var dots = await files.Record("..", CancellationToken.None);

        Assert.Equal("sess-1", named.Value!.Session!.Value.Value);
        Assert.Null(dots.Value);
    }

    [Fact]
    public async Task A_broken_agent_file_counts_as_absent()
    {
        await File.WriteAllTextAsync(Path.Combine(_dir.Path, "api.agent"), "{ not json", TestContext.Current.CancellationToken);

        var record = await new DirectoryLaneFiles(_dir.Path).Record("api", CancellationToken.None);

        Assert.Null(record.Value);
    }

    [Fact]
    public async Task A_hook_trace_line_reads_back_with_its_four_fields()
    {
        await File.WriteAllTextAsync(Path.Combine(_dir.Path, "api.hook"), "2026-11-05T04:55:00.0000000+00:00 meta-lane-gate UserPromptSubmit not-own-agent\n", TestContext.Current.CancellationToken);

        var trace = await new DirectoryLaneFiles(_dir.Path).HookTrace("api", CancellationToken.None);

        Assert.Equal(new LaneHookTrace(new DateTimeOffset(2026, 11, 5, 4, 55, 0, TimeSpan.Zero), "meta-lane-gate", "UserPromptSubmit", "not-own-agent"), trace.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("2026-11-05T04:55:00Z meta-lane-gate UserPromptSubmit")]
    [InlineData("not-a-time meta-lane-gate UserPromptSubmit ran")]
    [InlineData("2026-11-05T04:55:00Z meta-lane-gate UserPromptSubmit ran extra")]
    public async Task A_garbage_or_partial_hook_trace_line_counts_as_none(string line)
    {
        await File.WriteAllTextAsync(Path.Combine(_dir.Path, "api.hook"), line + "\n", TestContext.Current.CancellationToken);

        var trace = await new DirectoryLaneFiles(_dir.Path).HookTrace("api", CancellationToken.None);

        Assert.True(trace.IsSuccess);
        Assert.Null(trace.Value);
    }

    public void Dispose() => _dir.Dispose();
}
