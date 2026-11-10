using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Resources;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>A 'run_agent' launch makes the workflow's resources only once its session is known not to have ended (#2586): a retry after the end answers with the first start, even when the resources would fail now.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentLaunchStartResourcesTests
{
    private const string Worktree = "/work/web/.claude/worktrees/wf-test";

    private static readonly LaunchId _launch = A.FirstLaunchId;

    private readonly SliceHarness _slice = new();
    private readonly FakeLauncher _launcher = new();
    private readonly FakeAgentSessions _sessions = new();
    private int _ensures;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static LaunchRequest Request() => A.LaunchRequest().WithLaunch(_launch).InWorktree(Worktree).Build();

    private async Task<AgentLaunchStart> Start(LaunchState? state)
    {
        _ = await _slice.HoldLease();
        var recorder = new AgentLaunchRecorder(_slice.Store, _slice.Held, _slice.Events, new LaunchLiveness(), _slice.Clock);
        var waiting = new AgentLaunchWaiting(_launcher, new AgentRuntimeOptions(PollInterval: TimeSpan.FromMilliseconds(10)), recorder);
        return new AgentLaunchStart(waiting, recorder, new FakeRunDiff(), new AgentLaunchEnded(new Views(state), _sessions, new FakeSindriHome("/home/sindri")));
    }

    private ValueTask<Result<ProvisionedResources>> FailingResources(CancellationToken cancellationToken)
    {
        _ensures++;
        return ValueTask.FromResult<Result<ProvisionedResources>>(SindriErrors.ResourceDiskOverSize.With("the disk 'cache' is over 1GiB"));
    }

    [Fact]
    public async Task A_retry_after_the_session_ended_answers_with_the_first_start_and_never_makes_the_resources()
    {
        _sessions.End(_launch, SessionEndReason.Exit, exitCode: 0);

        var answer = await (await Start(LaunchState.Started)).Start(Request(), FailingResources, Token);

        Assert.Null(answer.Failure);
        Assert.Equal(Worktree, answer.Worktree);
        Assert.Equal(0, _ensures);
        Assert.Empty(_launcher.Requests);
    }

    [Fact]
    public async Task A_first_launch_whose_resources_fail_is_a_step_failure_and_starts_no_session()
    {
        var answer = await (await Start(state: null)).Start(Request(), FailingResources, Token);

        Assert.Equal(AgentLaunchFailureKind.StepFailed, answer.Failure?.Kind);
        Assert.Equal("SND-RES-001", answer.Failure?.Code);
        Assert.Equal(1, _ensures);
        Assert.Empty(_launcher.Requests);
    }

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
