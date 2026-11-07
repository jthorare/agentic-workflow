using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
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
        return await watching.Watch(Watch(), static () => { }, Token);
    }

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
    }

    [Fact]
    public async Task A_session_that_exits_after_a_verdict_the_block_does_not_map_fails_the_step_with_its_code()
    {
        Result(Review("MAYBE"));
        _sessions.End(Launch, SessionEndReason.Exit, exitCode: 0);

        var state = await Watched();

        Assert.Null(state.Skill);
        Assert.StartsWith(SindriErrors.SkillVerdictUnmapped.Code, state.SkillFailure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_result_read_at_a_settled_turn_end_returns_while_the_session_is_still_live()
    {
        Result(Review("PASS"));
        _files.EndTurn(Launch, _clock.GetUtcNow() - SkillStepWatching.Settle - TimeSpan.FromSeconds(1));

        var state = await Watched();

        Assert.True(state.IsLive);
        Assert.Equal("pass", Assert.IsType<SkillFinished>(state.Skill).Outcome);
    }

    [Fact]
    public async Task A_verdict_the_block_does_not_map_at_a_settled_turn_end_fails_the_step_with_its_code()
    {
        Result(Review("MAYBE"));
        _files.EndTurn(Launch, _clock.GetUtcNow() - SkillStepWatching.Settle - TimeSpan.FromSeconds(1));

        var state = await Watched();

        Assert.Null(state.Skill);
        Assert.StartsWith(SindriErrors.SkillVerdictUnmapped.Code, state.SkillFailure, StringComparison.Ordinal);
    }
}
