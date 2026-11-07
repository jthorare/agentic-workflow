using System.Security.Cryptography;
using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Testing.Skills;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>
/// What 'WatchSkillStep' returns (skill flows spec §3.3), one row per way the session can end: idle, exit with and without a result file, a result read at a settled turn end, and a block-author bug.
/// Real result files in a temp directory, so the integration lane.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class SkillStepWatchingTests : IDisposable
{
    private const string Findings = "/home/u/.agentic-workflow/acme-api/reviews/42.json";
    private static readonly DateTimeOffset _started = new(2026, 11, 5, 9, 0, 0, TimeSpan.Zero);

    // why: a watch of a live session returns only on a result or an end; one that never comes fails the row here instead of hanging the run
    private static readonly TimeSpan _watchLimit = TimeSpan.FromSeconds(10);

    private readonly string _directory = Directory.CreateTempSubdirectory("aw-skill-watching-").FullName;
    private readonly FakeTimeProvider _clock = new(_started.AddMinutes(10));
    private readonly FakeAgentSessions _sessions = new();
    private readonly FakeLaunchFiles _files = new();
    private readonly InMemoryFactStore _facts = new();
    private readonly SliceHarness _slice = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static LaunchId Launch => A.FirstLaunchId;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private SkillWatch Watch() => new(Launch.Value, ItemId.From("ITM-00000000000000000000000000000001"), "review", SkillFixtures.Review, _directory, _started);

    private void Result(string json)
    {
        var path = SkillLaunchEnv.ResultFileOf(_directory, Launch);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json, Encoding.UTF8);
        File.SetLastWriteTimeUtc(path, _started.AddMinutes(5).UtcDateTime);
    }

    private async Task<AgentSessionState> Watched()
    {
        _ = await _slice.HoldLease();
        var recorder = new AgentLaunchRecorder(_slice.Store, _slice.Held, _slice.Events, new LaunchLiveness(), _slice.Clock);
        var watching = new SkillStepWatching(_sessions, _files, _facts, recorder, new AgentRuntimeOptions(PollInterval: TimeSpan.FromMilliseconds(10)), _clock);
        using var limit = new CancellationTokenSource(_watchLimit, TimeProvider.System);
        using var token = CancellationTokenSource.CreateLinkedTokenSource(Token, limit.Token);
        return await watching.Watch(Watch(), static () => { }, token.Token);
    }

    // Watches a live session for a few polls (the interval is 10 ms), then idles it so the watch returns.
    private async Task<AgentSessionState> WatchedForAWhile()
    {
        var watched = Watched();
        await Task.Delay(TimeSpan.FromMilliseconds(300), TimeProvider.System, Token);
        _sessions.End(Launch, SessionEndReason.Idle);
        return await watched;
    }

    private List<string> OwnerWaitingRows() =>
        [.. _slice.Store.Events.Where(static e => e.Envelope.Type == EventTypes.LaunchOwnerWaiting).Select(static e => e.Envelope.Payload)];

    private void SettledTurnEnd() => _files.EndTurn(Launch, _clock.GetUtcNow() - SkillStepWatching.Settle - TimeSpan.FromSeconds(1));

    private static string Review(string verdict) => $$"""{"skill":"review","verdict":"{{verdict}}","review_file":"{{Findings}}"}""";

    [Fact]
    public async Task A_session_that_goes_idle_with_no_result_ends_idle_with_no_result()
    {
        _sessions.End(Launch, SessionEndReason.Idle);

        var state = await Watched();

        Assert.Equal(AgentSessionEnd.Idle, state.EndedAs);
        Assert.Null(state.Skill);
        Assert.Null(state.SkillFailure);
    }

    [Fact]
    public async Task A_session_that_exits_with_no_result_file_ends_with_its_exit_code_and_no_result()
    {
        _sessions.End(Launch, SessionEndReason.Exit, exitCode: 1);

        var state = await Watched();

        Assert.Equal((AgentSessionEnd.Exit, 1), (state.EndedAs, state.ExitCode));
        Assert.Null(state.Skill);
        Assert.Null(state.SkillFailure);
    }

    [Fact]
    public async Task A_session_that_exits_after_writing_a_finished_result_returns_the_result_with_its_outputs()
    {
        Result(Review("NEEDS_CHANGES"));
        _sessions.End(Launch, SessionEndReason.Exit, exitCode: 0);

        var state = await Watched();

        var finished = Assert.IsType<SkillFinished>(state.Skill);
        Assert.Equal("needsChanges", finished.Outcome);
        var stored = Assert.Single(finished.Outputs);
        Assert.Equal("fact:reviewFindings", stored.Key.Value);
        Assert.Equal(Findings, Encoding.UTF8.GetString(_facts.Stored[stored.Value.Sha256]));
        Assert.True(state.FoundEnded);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(SkillLaunchEnv.ResultFileOf(_directory, Launch), Token))), finished.ResultFileSha256);
    }

    [Fact]
    public async Task A_session_that_exits_after_a_verdict_the_block_does_not_map_fails_the_step_with_its_code()
    {
        Result(Review("MAYBE"));
        _sessions.End(Launch, SessionEndReason.Exit, exitCode: 0);

        var state = await Watched();

        Assert.Null(state.Skill);
        Assert.StartsWith("SND-SKILL-003: ", state.SkillFailure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_result_read_at_a_settled_turn_end_returns_while_the_session_is_still_live()
    {
        Result(Review("PASS"));
        SettledTurnEnd();

        var state = await Watched();

        Assert.True(state.IsLive);
        Assert.Equal("pass", Assert.IsType<SkillFinished>(state.Skill).Outcome);
    }

    [Fact]
    public async Task A_verdict_the_block_does_not_map_at_a_settled_turn_end_fails_the_step_with_its_code()
    {
        Result(Review("MAYBE"));
        SettledTurnEnd();

        var state = await Watched();

        Assert.Null(state.Skill);
        Assert.StartsWith("SND-SKILL-003: ", state.SkillFailure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_settled_turn_end_with_a_waiting_result_records_one_owner_waiting_across_polls()
    {
        Result("""{"skill":"review","waiting":"Which branch is the base?"}""");
        SettledTurnEnd();

        _ = await WatchedForAWhile();

        Assert.Contains("Which branch is the base?", Assert.Single(OwnerWaitingRows()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_turn_end_that_has_not_settled_records_nothing()
    {
        Result("""{"skill":"review","waiting":"Which branch is the base?"}""");
        _files.EndTurn(Launch, _clock.GetUtcNow() - SkillStepWatching.Settle + TimeSpan.FromSeconds(5));

        _ = await WatchedForAWhile();

        Assert.Empty(OwnerWaitingRows());
    }

    [Fact]
    public async Task A_turn_end_with_a_tool_still_running_records_nothing()
    {
        Result("""{"skill":"review","waiting":"Which branch is the base?"}""");
        SettledTurnEnd();
        _files.SetToolRunning(Launch, running: true);

        _ = await WatchedForAWhile();

        Assert.Empty(OwnerWaitingRows());
    }

    [Fact]
    public async Task A_question_holding_a_token_is_recorded_scrubbed()
    {
        var secret = "gh" + "p_" + new string('a', 36);
        Result($$"""{"skill":"review","waiting":"Use {{secret}} for the push?"}""");
        SettledTurnEnd();

        _ = await WatchedForAWhile();

        var row = Assert.Single(OwnerWaitingRows());
        Assert.DoesNotContain(secret, row, StringComparison.Ordinal);
        Assert.Contains("for the push?", row, StringComparison.Ordinal);
    }
}
