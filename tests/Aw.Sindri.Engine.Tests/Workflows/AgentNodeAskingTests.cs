using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>What 'AskingAgentNode' reads (#3151): a launch's session waits on a permission prompt only when its fleet state file says asking for an approval.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentNodeAskingTests
{
    private static readonly LaunchId _launch = A.FirstLaunchId;
    private static readonly AgentId _agent = AgentId.From("claude-code");
    private static readonly SessionId _session = SessionId.From("s1");
    private static readonly DateTimeOffset _at = new(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static FleetStateFile File(HookState state, AskingReason? reason = null, string? tool = null) =>
        new(_agent, _session, state, _at) { Reason = reason, Tool = tool };

    private static AgentNodeAsking Asking(params FleetStateFile[] files) => Asking(_session, files);

    private static AgentNodeAsking Asking(SessionId? session, params FleetStateFile[] files)
    {
        var fleet = new FakeFleetStates();
        foreach (var file in files)
        {
            _ = fleet.With(file);
        }

        return new AgentNodeAsking(new Views(session), fleet);
    }

    private static ValueTask<NodeSessionAsk?> Ask(SessionId? session, params FleetStateFile[] files) => Asking(session, files).Asking(_launch, Token);

    [Fact]
    public async Task A_session_asking_for_an_approval_gives_the_tool_and_when()
    {
        var ask = await Ask(_session, File(HookState.Asking, AskingReason.Approval, "Bash"));

        Assert.Equal(new NodeSessionAsk("Bash", _at), ask);
    }

    [Fact]
    public async Task A_tool_name_that_does_not_match_the_hook_pattern_is_dropped_but_the_ask_stays()
    {
        var odd = await Ask(_session, File(HookState.Asking, AskingReason.Approval, "Bash; whoami"));
        var long64 = new string('a', 64);
        var tooLong = await Ask(_session, File(HookState.Asking, AskingReason.Approval, long64 + "b"));
        var fits = await Ask(_session, File(HookState.Asking, AskingReason.Approval, long64));

        Assert.Equal(new NodeSessionAsk(Tool: null, _at), odd);
        Assert.Equal(new NodeSessionAsk(Tool: null, _at), tooLong);
        Assert.Equal(new NodeSessionAsk(long64, _at), fits);
    }

    [Fact]
    public async Task A_watch_returns_at_once_when_the_ask_differs_from_the_one_seen()
    {
        var asking = Asking(File(HookState.Asking, AskingReason.Approval, "Bash"));

        var ask = await asking.Watch(new AgentNodeAskWatch(_launch.Value, Seen: null, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(10)), static () => { }, Token);

        Assert.Equal(new NodeSessionAsk("Bash", _at), ask);
    }

    [Fact]
    public async Task A_watch_hands_back_after_its_cap_when_the_ask_is_the_one_seen_and_beats_on_each_read()
    {
        var asking = Asking(File(HookState.Asking, AskingReason.Approval, "Bash"));
        var beats = 0;

        var ask = await asking.Watch(new AgentNodeAskWatch(_launch.Value, new NodeSessionAsk("Bash", _at), TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(100)), () => beats++, Token);

        Assert.Equal(new NodeSessionAsk("Bash", _at), ask);
        Assert.InRange(beats, 4, 6);
    }

    [Fact]
    public async Task A_session_asking_for_input_gives_nothing() =>
        Assert.Null(await Ask(_session, File(HookState.Asking, AskingReason.Input)));

    [Fact]
    public async Task A_session_that_is_working_gives_nothing() =>
        Assert.Null(await Ask(_session, File(HookState.Running)));

    [Fact]
    public async Task A_launch_with_no_session_id_gives_nothing() =>
        Assert.Null(await Ask(session: null, File(HookState.Asking, AskingReason.Approval, "Bash")));

    [Fact]
    public async Task A_session_with_no_state_file_gives_nothing() =>
        Assert.Null(await Ask(_session));

    [Fact]
    public async Task Another_sessions_state_file_gives_nothing() =>
        Assert.Null(await Ask(_session, new FleetStateFile(_agent, SessionId.From("other"), HookState.Asking, _at) { Reason = AskingReason.Approval, Tool = "Bash" }));

    private sealed class Views(SessionId? session) : ILaunchViews
    {
        public ValueTask<Result<LaunchRow?>> Get(LaunchId launch, CancellationToken cancellationToken)
        {
            var row = new LaunchRow(launch, A.FirstItemId, Repo: null, LaunchMode.Open, _agent, "engine", LaunchState.Started, DateTimeOffset.UnixEpoch, OutcomeAt: null,
                Code: null, Message: null, Stage: null, session, ReportedSessionId: null, TabTitle: null, Viewer: null, Late: false, Attach: null, CatalogSource: null, ArgvHash: null, Ended: null,
                new LaunchStep(), Sequence: 1);
            return ValueTask.FromResult(Results.Success<LaunchRow?>(row));
        }

        public ValueTask<Result<IReadOnlyList<LaunchRow>>> ForItem(ItemId item, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<LaunchRow>>> ForRepo(RepoName repo, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<LaunchRow>>> Since(DateTimeOffset since, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
