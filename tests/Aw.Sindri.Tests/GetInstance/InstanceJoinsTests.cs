using System.Text;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.GetInstance;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>The two read-time joins of the instance detail (q65): the session of the open agent run from its launch row, and where each reviewed fact opens from its stored value.</summary>
[Lane(TestLane.Unit)]
public sealed class InstanceJoinsTests
{
    private const string Launch = "LCH-0123456789abcdef0123456789abcdef";
    private const string DesignSha = "00000000000000000000000000000000000000000000000000000000000000d1";
    private const string PrSha = "00000000000000000000000000000000000000000000000000000000000000a2";
    private static readonly SessionId _started = SessionId.From("0b1c2d3e-0000-4000-8000-000000000001");
    private static readonly SessionId _reported = SessionId.From("0b1c2d3e-0000-4000-8000-000000000002");

    [Fact]
    public async Task The_session_is_the_launchs_target_and_the_id_known_at_start()
    {
        var joins = Joins(Launched(_started, _reported));

        var session = await joins.Session(AgentView(), TestContext.Current.CancellationToken);

        Assert.Equal(new SessionHandle(AgentId.From("claude-code"), _started), session);
    }

    [Fact]
    public async Task The_session_falls_back_to_the_id_the_session_reported()
    {
        var session = await Joins(Launched(started: null, _reported)).Session(AgentView(), TestContext.Current.CancellationToken);

        Assert.Equal(new SessionHandle(AgentId.From("claude-code"), _reported), session);
    }

    [Fact]
    public async Task A_launch_that_knows_no_id_a_missing_launch_and_an_unreadable_one_serve_no_session()
    {
        var cancel = TestContext.Current.CancellationToken;

        Assert.Null(await Joins(Launched(started: null, reported: null)).Session(AgentView(), cancel));
        Assert.Null(await Joins(new Launches(row: null)).Session(AgentView(), cancel));
        Assert.Null(await Joins(new Launches(row: null, KernelErrors.Unavailable.With("views.db is locked"))).Session(AgentView(), cancel));
    }

    [Fact]
    public async Task A_launch_that_waits_for_a_slot_says_how_full_the_cap_is_until_it_has_an_outcome()
    {
        var slice = new SliceHarness();
        var grant = await slice.HoldLease();
        var cancel = TestContext.Current.CancellationToken;
        var joins = new InstanceJoins(Launched(started: null, reported: null), new Contents([]), slice.Store, slice.Events);
        Assert.Null(await joins.Waiting(AgentView(), cancel));   // nothing recorded yet

        _ = await slice.Store.Append(slice.Events.Entry(EventTypes.LaunchWaiting, new LaunchWaiting(LaunchId.From(Launch), ItemId.From("ITM-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"), Running: 3, Cap: 3), "launch.waiting:" + Launch), grant, cancel);
        Assert.Equal(new StatusWaiting(3, 3), await joins.Waiting(AgentView(), cancel));

        var started = new LaunchStarted(LaunchId.From(Launch), ItemId.From("ITM-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"), LaunchMode.Open, AgentId.From("claude-code"), SessionId: null, "sindri",
            new LaunchViewer("warp", "Warp", ViewerOutcomeKind.Opened, Code: null, Detail: null), ["tmux"], CatalogSource.Builtin, "sha256:abc");
        _ = await slice.Store.Append(slice.Events.Entry(EventTypes.LaunchStarted, started, "launch.outcome:" + Launch), grant, cancel);
        Assert.Null(await joins.Waiting(AgentView(), cancel));   // the session started: no longer waiting
    }

    [Fact]
    public async Task A_desk_run_never_waits_for_a_slot()
    {
        var slice = new SliceHarness();
        var deskView = InstanceViews.Of(StatusCases.Instance("desk-task"), StatusCases.Recipe, StatusCases.Now, timeoutAt: null, new InstanceViewContext());

        Assert.Null(await new InstanceJoins(Launched(started: null, reported: null), new Contents([]), slice.Store, slice.Events).Waiting(deskView, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_desk_run_is_never_looked_up()
    {
        var launches = Launched(_started, reported: null);
        var deskView = InstanceViews.Of(StatusCases.Instance("desk-task"), StatusCases.Recipe, StatusCases.Now, timeoutAt: null, new InstanceViewContext());

        Assert.Null(await Joins(launches).Session(deskView, TestContext.Current.CancellationToken));
        Assert.Empty(launches.Asked);
    }

    [Fact]
    public async Task Each_reviewed_facts_stored_value_gives_its_target()
    {
        var contents = new Contents(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [DesignSha] = """{"hash":"sha256:ab","dir":"/packets/design/2"}""",
            [PrSha] = """{"pr":7,"url":"https://github.com/o/r/pull/7"}""",
        });

        var targets = await new InstanceJoins(new Launches(row: null), contents).ReviewTargets(DeskView(), TestContext.Current.CancellationToken);

        Assert.Equal(
            [KeyValuePair.Create("fact:design", new DeskReviewTarget("/packets/design/2", Url: null)), KeyValuePair.Create("fact:prStatus", new DeskReviewTarget(Path: null, "https://github.com/o/r/pull/7"))],
            targets.OrderBy(static t => t.Key, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task A_fact_not_held_or_not_readable_has_no_target_and_a_desk_without_reviews_reads_nothing()
    {
        var cancel = TestContext.Current.CancellationToken;
        var contents = new Contents(new Dictionary<string, string>(StringComparer.Ordinal) { [PrSha] = """{"url":"https://github.com/o/r/pull/7"}""" });
        var onlyPr = DeskView() with { FactShas = new Dictionary<string, string>(StringComparer.Ordinal) { ["fact:prStatus"] = PrSha, ["fact:design"] = "not-stored" } };

        var targets = await new InstanceJoins(new Launches(row: null), contents).ReviewTargets(onlyPr, cancel);
        var plain = InstanceViews.Of(StatusCases.Instance("desk-task"), StatusCases.Recipe, StatusCases.Now, timeoutAt: null, new InstanceViewContext());

        Assert.Equal(["fact:prStatus"], [.. targets.Keys]);
        Assert.Empty(await new InstanceJoins(new Launches(row: null), contents).ReviewTargets(plain, cancel));
        Assert.DoesNotContain(contents.Asked, sha => sha is not (PrSha or "not-stored"));
    }

    private static InstanceJoins Joins(Launches launches) => new(launches, new Contents([]));

    private static Launches Launched(SessionId? started, SessionId? reported) => new(new LaunchRow(
        LaunchId.From(Launch), ItemId.From("ITM-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"), Repo: null, LaunchMode.Open, AgentId.From("claude-code"), "engine", LaunchState.Started, DateTimeOffset.UnixEpoch, OutcomeAt: null,
        Code: null, Message: null, Stage: null, started, reported, TabTitle: null, Viewer: null, Late: false, Attach: null, CatalogSource: null, ArgvHash: null, Ended: null, LaunchStep.None, Sequence: 1));

    private static InstanceView AgentView()
    {
        var instance = StatusCases.Instance("desk-task");
        instance = instance with { Frame = instance.Frame with { Run = instance.Frame.Run! with { Runtime = StepRuntime.Agent, Launch = Launch, Task = null } } };
        return InstanceViews.Of(instance, StatusCases.Recipe, StatusCases.Now, timeoutAt: null, new InstanceViewContext());
    }

    private static InstanceView DeskView()
    {
        var view = InstanceViews.Of(StatusCases.Instance("desk-task"), DeskReviewsTests.Reviewed(), StatusCases.Now, timeoutAt: null, new InstanceViewContext());
        return view with { FactShas = new Dictionary<string, string>(StringComparer.Ordinal) { ["fact:design"] = DesignSha, ["fact:prStatus"] = PrSha } };
    }

    private sealed class Launches(LaunchRow? row, Error? failure = null) : ILaunchViews
    {
        public List<LaunchId> Asked { get; } = [];

        public ValueTask<Result<LaunchRow?>> Get(LaunchId launch, CancellationToken cancellationToken)
        {
            Asked.Add(launch);
            return ValueTask.FromResult(failure is { } failed ? (Result<LaunchRow?>)failed : Results.Success(row));
        }

        public ValueTask<Result<IReadOnlyList<LaunchRow>>> ForItem(ItemId item, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<LaunchRow>>> ForRepo(RepoName repo, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<LaunchRow>>> Since(DateTimeOffset since, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class Contents(Dictionary<string, string> bySha) : IFactContents
    {
        public List<string> Asked { get; } = [];

        public ValueTask<Result<ReadOnlyMemory<byte>>> Read(FactValue fact, CancellationToken cancellationToken)
        {
            Asked.Add(fact.Sha256);
            return ValueTask.FromResult(bySha.TryGetValue(fact.Sha256, out var text)
                ? Results.Success((ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(text))
                : (Result<ReadOnlyMemory<byte>>)KernelErrors.Unavailable.With("not stored"));
        }
    }
}
