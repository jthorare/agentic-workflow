using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The ledger adapter of the agent session port, over the in-memory ledger: the same contract the fake honours.</summary>
[Lane(TestLane.Integration)]
public sealed class LedgerAgentSessionsContractTests : AgentSessionsContract
{
    private readonly SliceHarness _slice = new();
    private LeaseGrant? _grant;

    protected override async ValueTask<IAgentSessions> Create()
    {
        _grant = await _slice.HoldLease();
        return new LedgerAgentSessions(new StubLaunchViews(), _slice.Store, _slice.Events);
    }

    protected override async ValueTask End(IAgentSessions sessions, LaunchId launch, SessionEndReason reason, int? exitCode) =>
        await Append(EventTypes.SessionEnded, new SessionEnded(launch, reason, exitCode), $"{EventTypes.SessionEnded}:{launch.Value}");

    protected override async ValueTask EndByLimit(IAgentSessions sessions, LaunchId launch, LimitKind kind, long limit, long peak) =>
        await Append(EventTypes.SessionEnded, SessionEnded.Limited(launch, new LimitEnd(kind, limit, peak)), $"{EventTypes.SessionEnded}:{launch.Value}");

    protected override async ValueTask Spend(IAgentSessions sessions, LaunchId launch, int sample, long tokens) =>
        await Append(EventTypes.SessionUsage, new SessionUsage(launch, sample, tokens / 2, tokens - (tokens / 2), AgentId.From("claude-code")), string.Create(CultureInfo.InvariantCulture, $"{EventTypes.SessionUsage}:{launch.Value}:{sample}"));

    /// <summary>A Step's budget counts fresh input, cache writes and output, not the cache reads a session re-counts every turn.</summary>
    [Fact]
    public async Task Usage_leaves_the_cache_reads_out_of_the_budgeted_tokens()
    {
        var sessions = await Create();
        await Append(EventTypes.SessionUsage, new SessionUsage(A.FirstLaunchId, 1, InputTokens: 1_000, OutputTokens: 100, AgentId.From("claude-code"), Model: null, CacheReadTokens: 800), $"{EventTypes.SessionUsage}:{A.FirstLaunchId.Value}:1");

        var spent = await sessions.Usage(A.FirstLaunchId, TestContext.Current.CancellationToken);

        Assert.Equal(300L, spent.Value);
    }

    /// <summary>A sample from before the split has no cache field: its input stands as it was.</summary>
    [Fact]
    public async Task Usage_of_a_sample_without_the_split_counts_all_its_input()
    {
        var sessions = await Create();
        await Append(EventTypes.SessionUsage, new SessionUsage(A.FirstLaunchId, 1, 1_000, 100, AgentId.From("claude-code")), $"{EventTypes.SessionUsage}:{A.FirstLaunchId.Value}:1");

        var spent = await sessions.Usage(A.FirstLaunchId, TestContext.Current.CancellationToken);

        Assert.Equal(1_100L, spent.Value);
    }

    private async ValueTask Append<T>(string type, T payload, string key)
    {
        var appended = await _slice.Store.Append(_slice.Events.Entry(type, payload, key, A.FirstItemId.Value), _grant!, TestContext.Current.CancellationToken);   // why: Create ran first
        Assert.True(appended.IsSuccess);
    }

    /// <summary>Every launch the contract names belongs to the first item.</summary>
    private sealed class StubLaunchViews : ILaunchViews
    {
        public ValueTask<Result<LaunchRow?>> Get(LaunchId launch, CancellationToken cancellationToken) => ValueTask.FromResult<Result<LaunchRow?>>(
            new LaunchRow(launch, A.FirstItemId, Repo: null, LaunchMode.Open, AgentId.From("claude-code"), "engine", LaunchState.Started, DateTimeOffset.UnixEpoch, OutcomeAt: null,
                Code: null, Message: null, Stage: null, SessionId: null, ReportedSessionId: null, TabTitle: null, Viewer: null, Late: false, Attach: null, CatalogSource: null, ArgvHash: null, Ended: null, LaunchStep.None, Sequence: 1));

        public ValueTask<Result<IReadOnlyList<LaunchRow>>> ForItem(ItemId item, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<LaunchRow>>> ForRepo(RepoName repo, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<LaunchRow>>> Since(DateTimeOffset since, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
