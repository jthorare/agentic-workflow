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
        await Append(EventTypes.SessionUsage, new SessionUsage(launch, sample, tokens / 2, tokens - (tokens / 2)), string.Create(CultureInfo.InvariantCulture, $"{EventTypes.SessionUsage}:{launch.Value}:{sample}"));

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
                Code: null, Message: null, Stage: null, SessionId: null, ReportedSessionId: null, TabTitle: null, Viewer: null, Late: false, Attach: null, CatalogSource: null, ArgvHash: null, Ended: null, Sequence: 1));

        public ValueTask<Result<IReadOnlyList<LaunchRow>>> ForItem(ItemId item, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<LaunchRow>>> ForRepo(RepoName repo, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<LaunchRow>>> Since(DateTimeOffset since, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
