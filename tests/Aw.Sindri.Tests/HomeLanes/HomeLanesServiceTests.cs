using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lanes;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.HomeLanes;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.HomeLanes;

/// <summary>The 'home-lanes' service: opens the rows whose trigger holds, clears the open ones whose trigger stopped, and changes nothing when aw-meta does not answer.</summary>
[Lane(TestLane.Unit)]
public sealed class HomeLanesServiceTests
{
    private static readonly ScheduledRun _run = new(new LeaseGrant(FakeProcessProbe.Holder(100), 1, KnownInstants.Epoch), KnownInstants.Epoch);

    private readonly FakeLaneSource _source = new();
    private readonly FakeLaneRows _rows = new();

    private HomeLanesService Service() => new(_source, _rows);

    [Fact]
    public async Task A_recycling_lane_a_limited_agent_and_queued_prs_are_opened_once()
    {
        _source.Set(LaneFixtures.Status, LaneFixtures.ContextRecycle, LaneFixtures.UsageLimited);

        var first = await Service().Run(_run, TestContext.Current.CancellationToken);
        var second = await Service().Run(_run, TestContext.Current.CancellationToken);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal("agent-limited:engine lane-recycle:api pr-ready-merge:2897 pr-ready-merge:2950 pr-ready-merge:3214", string.Join(' ', _rows.Raised.Order(StringComparer.Ordinal)));
        Assert.Equal(5, _rows.RaiseCalls);   // the second tick found all five open and raised none
        Assert.Empty(_rows.Cleared);
    }

    [Fact]
    public async Task A_row_whose_trigger_stopped_holding_is_cleared_and_the_rest_stay()
    {
        _rows.Preopen("lane-recycle:api", "pr-ready-merge:2897", "pr-ready-merge:9999");
        _source.Set(LaneFixtures.Status, LaneFixtures.ContextOk, LaneFixtures.UsageOk);

        var ran = await Service().Run(_run, TestContext.Current.CancellationToken);

        Assert.True(ran.IsSuccess);
        Assert.Equal("lane-recycle:api pr-ready-merge:9999", string.Join(' ', _rows.Cleared.Order(StringComparer.Ordinal)));
        Assert.Equal(["pr-ready-merge:2897"], _rows.OpenNow);
    }

    [Fact]
    public async Task A_quiet_box_with_nothing_open_is_an_idle_run()
    {
        _source.Set(LaneFixtures.StatusEmpty, LaneFixtures.ContextOk, LaneFixtures.UsageOk);

        var ran = await Service().Run(_run, TestContext.Current.CancellationToken);

        Assert.True(ran.Value.Idle);
        Assert.Equal(0, _rows.RaiseCalls);
    }

    [Fact]
    public async Task When_aw_meta_does_not_answer_no_row_changes_and_the_summary_says_lanes_unavailable()
    {
        _rows.Preopen("lane-recycle:api");
        _source.Fail(SindriErrors.LanesUnavailable.With("aw-meta is not on PATH"));

        var ran = await Service().Run(_run, TestContext.Current.CancellationToken);

        Assert.True(ran.IsSuccess);
        Assert.Contains("lanes-unavailable", ran.Value.Summary, StringComparison.Ordinal);
        Assert.Equal(0, _rows.RaiseCalls);
        Assert.Empty(_rows.Cleared);
    }

    [Fact]
    public async Task Output_that_does_not_parse_is_the_same_as_an_unanswered_aw_meta()
    {
        _rows.Preopen("pr-ready-merge:1");
        _source.Set("not json", LaneFixtures.ContextOk, LaneFixtures.UsageOk);

        var ran = await Service().Run(_run, TestContext.Current.CancellationToken);

        Assert.True(ran.IsSuccess);
        Assert.Contains("lanes-unavailable", ran.Value.Summary, StringComparison.Ordinal);
        Assert.Empty(_rows.Cleared);
    }

    private sealed class FakeLaneSource : ILaneSource
    {
        private Result<string> _status = string.Empty;
        private Result<string> _context = string.Empty;
        private Result<string> _usage = string.Empty;

        public void Set(string status, string context, string usage)
        {
            _status = status;
            _context = context;
            _usage = usage;
        }

        public void Fail(Error error)
        {
            _status = error;
            _context = error;
            _usage = error;
        }

        public ValueTask<Result<string>> Status(CancellationToken cancellationToken) => ValueTask.FromResult(_status);

        public ValueTask<Result<string>> Context(CancellationToken cancellationToken) => ValueTask.FromResult(_context);

        public ValueTask<Result<string>> Usage(CancellationToken cancellationToken) => ValueTask.FromResult(_usage);
    }

    private sealed class FakeLaneRows : ILaneRows
    {
        private readonly List<string> _open = [];

        public List<string> Raised { get; } = [];

        public List<string> Cleared { get; } = [];

        public int RaiseCalls { get; private set; }

        public IReadOnlyList<string> OpenNow => _open;

        public void Preopen(params string[] ids) => _open.AddRange(ids);

        public ValueTask<Result<IReadOnlyList<NeedsYouRowId>>> Open(CancellationToken cancellationToken) => ValueTask.FromResult<Result<IReadOnlyList<NeedsYouRowId>>>(_open.Select(static id => NeedsYouRowId.From(id)).ToArray());

        public ValueTask<Result<Unit>> Raise(NeedsYouKind kind, string key, string title, string reason, CancellationToken cancellationToken)
        {
            RaiseCalls++;
            var id = LaneTriggers.Id(kind, key);
            Raised.Add(id.Value);
            _open.Add(id.Value);
            return ValueTask.FromResult<Result<Unit>>(Unit.Value);
        }

        public ValueTask<Result<Unit>> Clear(NeedsYouRowId id, CancellationToken cancellationToken)
        {
            Cleared.Add(id.Value);
            _ = _open.Remove(id.Value);
            return ValueTask.FromResult<Result<Unit>>(Unit.Value);
        }
    }
}
