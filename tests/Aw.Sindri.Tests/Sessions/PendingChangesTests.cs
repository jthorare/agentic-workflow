using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.Sessions;
using static Aw.Sindri.Tests.Sessions.SessionData;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>A failed ledger append loses nothing (collector spec CO3, §4.1): the changes wait and go out next pass, 'appeared' before 'gone'.</summary>
public sealed class PendingChangesTests
{
    private const string Id = "aaaaaaaa-0000-4000-8000-000000000010";

    private readonly CollectorHarness _c = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_append_keeps_the_appeared_row_for_the_next_pass_and_a_later_gone_never_comes_first()
    {
        _ = _c.Fake.With(Proc(10, 1, "claude", argv0: "claude", ticks: 100), cwd: "/w/api");
        _ = _c.Records.With(new ClaudeRecord(10, "100") { Session = SessionId.From(Id), Kind = "interactive", Cwd = "/w/api", Status = "busy", StatusUpdatedAt = _c.Now });
        var grant = await _c.Base.HoldLease();
        var failing = new AppendFailingLedger(_c.Base.Store);
        _ = await _c.Pass();
        var first = await _c.After(TimeSpan.FromSeconds(2));   // appeared takes two good passes
        Assert.Equal([FleetChangeKind.Appeared], first.Changes.Select(static c => c.Kind));
        var failed = await new RecordFleetChangesService(failing, _c.Base.Events, _c.Memory).Handle(new RecordFleetChangesRequest(grant, first.Changes), Token);
        Assert.True(failed.IsError);

        _c.Fake.Exit(10);   // the session ends before the retry
        var second = await _c.After(TimeSpan.FromSeconds(2));
        var recorded = await new RecordFleetChangesService(_c.Base.Store, _c.Base.Events, _c.Memory).Handle(new RecordFleetChangesRequest(grant, second.Changes), Token);

        Assert.True(recorded.IsSuccess);
        Assert.Equal(["appeared", "gone"], _c.Base.Store.Events.Where(static e => e.Envelope.Type == EventTypes.FleetChanged).Select(static e => e.IdempotencyKey.Split(':')[3]), StringComparer.Ordinal);
        Assert.Empty(_c.Memory.Pending);
    }
}
