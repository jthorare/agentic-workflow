using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>A retried 'run_agent' launch after its session ended starts no second agent: the answer is the first start, and the watch then reports the end.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentLaunchEndedTests
{
    private static readonly LaunchId _launch = A.FirstLaunchId;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static LaunchRequest Request() => A.LaunchRequest().WithLaunch(_launch).InWorktree("/work/web/.claude/worktrees/wf-test").Build();

    private readonly SliceHarness _slice = new();

    private AgentLaunchEnded Guard(LaunchState? state, FakeAgentSessions sessions) =>
        new(new Views(state), sessions, new FakeSindriHome("/home/sindri"), _slice.Store);

    [Fact]
    public async Task A_started_launch_whose_session_ended_answers_with_its_worktree_and_starts_nothing()
    {
        var sessions = new FakeAgentSessions();
        sessions.End(_launch, SessionEndReason.Exit, exitCode: 0);

        var answer = await Guard(LaunchState.Started, sessions).Find(Request(), Token);

        Assert.NotNull(answer);
        Assert.Equal("/work/web/.claude/worktrees/wf-test", answer.Worktree);
        Assert.Null(answer.Failure);
        Assert.False(answer.Waiting);
        Assert.Equal(AgentSessionEndOf(sessions), AgentSessionEnd.Exit);
    }

    [Fact]
    public async Task A_started_launch_whose_session_still_runs_is_left_to_the_launcher()
        => Assert.Null(await Guard(LaunchState.Started, new FakeAgentSessions()).Find(Request(), Token));

    [Theory]
    [InlineData(null)]
    [InlineData(LaunchState.Failed)]
    public async Task A_launch_that_never_started_is_left_to_the_launcher(LaunchState? state)
    {
        var sessions = new FakeAgentSessions();
        sessions.End(_launch, SessionEndReason.Lost);

        Assert.Null(await Guard(state, sessions).Find(Request(), Token));
    }

    /// <summary>
    /// #2482: the retry comes before the projector has written 'launch.started' into the launches view (the view has no row yet). The ledger already holds the start and the session's end,
    /// so the guard answers from it and the launcher is never asked for a second session.
    /// </summary>
    [Fact]
    public async Task A_retry_in_the_projector_lag_after_the_session_ended_answers_from_the_ledger()
    {
        var grant = await _slice.HoldLease();
        await Append(grant, EventTypes.LaunchStarted, Started(), "launch.outcome:" + _launch.Value);
        await Append(grant, EventTypes.SessionEnded, new SessionEnded(_launch, SessionEndReason.Exit, ExitCode: 0), $"{EventTypes.SessionEnded}:{_launch.Value}");
        var guard = new AgentLaunchEnded(new Views(state: null), new LedgerAgentSessions(new Views(state: null), _slice.Store, _slice.Events), new FakeSindriHome("/home/sindri"), _slice.Store);

        var answer = await guard.Find(Request(), Token);

        Assert.NotNull(answer);   // today null: the launcher is asked again and starts a second agent
        Assert.Equal("/work/web/.claude/worktrees/wf-test", answer.Worktree);
        Assert.Null(answer.Failure);
    }

    /// <summary>The same lag with the session still running: the guard leaves it to the launcher, whose running check finds the session.</summary>
    [Fact]
    public async Task A_retry_in_the_projector_lag_while_the_session_runs_is_left_to_the_launcher()
    {
        var grant = await _slice.HoldLease();
        await Append(grant, EventTypes.LaunchStarted, Started(), "launch.outcome:" + _launch.Value);
        var guard = new AgentLaunchEnded(new Views(state: null), new LedgerAgentSessions(new Views(state: null), _slice.Store, _slice.Events), new FakeSindriHome("/home/sindri"), _slice.Store);

        Assert.Null(await guard.Find(Request(), Token));
    }

    private static LaunchStarted Started() =>
        new(_launch, A.FirstItemId, LaunchMode.Open, AgentId.From("claude-code"), SessionId: null, "sindri", new LaunchViewer("warp", "Warp", ViewerOutcomeKind.Unconfirmed, Code: null, Detail: null), ["tmux"], CatalogSource.Builtin, "sha256:abc");

    private async Task Append<T>(Core.Lease.LeaseGrant grant, string type, T payload, string key) =>
        Assert.True((await _slice.Store.Append(_slice.Events.Entry(type, payload, key, A.FirstItemId.Value), grant, Token)).IsSuccess);

    private static AgentSessionEnd? AgentSessionEndOf(FakeAgentSessions sessions) =>
        sessions.Watch(_launch, Token).AsTask().Result.Value.EndedAs;

    private sealed class Views(LaunchState? state) : ILaunchViews
    {
        public ValueTask<Result<LaunchRow?>> Get(LaunchId launch, CancellationToken cancellationToken)
        {
            if (state is not { } known)
            {
                return ValueTask.FromResult(Results.Success<LaunchRow?>(value: null));
            }

            var row = new LaunchRow(launch, A.FirstItemId, Repo: null, LaunchMode.Open, AgentId.From("claude-code"), "engine", known, DateTimeOffset.UnixEpoch, OutcomeAt: null,
                Code: null, Message: null, Stage: null, SessionId: null, ReportedSessionId: null, TabTitle: null, Viewer: null, Late: false, Attach: null, CatalogSource: null, ArgvHash: null, Ended: null,
                new LaunchStep(), Sequence: 1);
            return ValueTask.FromResult(Results.Success<LaunchRow?>(row));
        }

        public ValueTask<Result<IReadOnlyList<LaunchRow>>> ForItem(ItemId item, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<LaunchRow>>> ForRepo(RepoName repo, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<LaunchRow>>> Since(DateTimeOffset since, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
