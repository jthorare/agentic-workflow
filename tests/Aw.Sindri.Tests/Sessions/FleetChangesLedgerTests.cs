using System.Globalization;
using System.Text.Json;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.Sessions;
using static Aw.Sindri.Tests.Sessions.SessionData;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>'fleet.changed' in the ledger (collector spec CA-3, CO3): the key, the public payload with no text, the restart restore.</summary>
public sealed class FleetChangesLedgerTests
{
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private RecordFleetChangesService Recorder => new(_h.Store, _h.Events, new CollectorMemory());

    private static FleetChange Change(string id, FleetChangeKind kind, DateTimeOffset at) =>
        new(Session("claude-code", id).Ref, kind, SessionState.Running, at) { HostKind = "tmux" };

    [Fact, Trait("Lane", "unit")]
    public async Task Fleet_changes_are_recorded_with_a_key_that_names_the_change_and_a_payload_without_text()
    {
        var grant = await _h.HoldLease();
        var change = Change("a1", FleetChangeKind.Appeared, T0) with { Reason = null, LaunchId = Core.Launch.LaunchId.From("LCH-00000000000000000000000000000001") };

        var result = await Recorder.Handle(new RecordFleetChangesRequest(grant, [change]), Token);

        Assert.Equal(1, result.Value.Recorded);
        var recorded = Assert.Single(_h.Store.Events, static e => e.Envelope.Type == EventTypes.FleetChanged);
        Assert.Equal(string.Create(CultureInfo.InvariantCulture, $"fleet.changed:claude-code:a1:appeared:{T0.ToUnixTimeMilliseconds()}"), recorded.IdempotencyKey);
        using var payload = JsonDocument.Parse(recorded.Envelope.Payload);
        Assert.Equal(["provider", "sessionId", "change", "state", "reason", "repo", "host", "launchId"], payload.RootElement.EnumerateObject().Select(static p => p.Name), StringComparer.Ordinal);
        Assert.Equal(("claude-code", "a1", "appeared", "running", "tmux", "LCH-00000000000000000000000000000001"), (Str(payload, "provider"), Str(payload, "sessionId"), Str(payload, "change"), Str(payload, "state"), Str(payload, "host"), Str(payload, "launchId")));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Recording_a_change_twice_writes_one_row()
    {
        var grant = await _h.HoldLease();
        FleetChange[] changes = [Change("a1", FleetChangeKind.Gone, T0)];

        var first = await Recorder.Handle(new RecordFleetChangesRequest(grant, changes), Token);
        var again = await Recorder.Handle(new RecordFleetChangesRequest(grant, changes), Token);

        Assert.Equal((1, 0), (first.Value.Recorded, again.Value.Recorded));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_event_is_public_and_invalidates_status()
    {
        var entry = EventCatalog.All.Single(static e => e.Type == "fleet.changed");

        Assert.Equal((true, typeof(FleetChanged)), (entry.Public, entry.PublicPayload));
        Assert.Equal(["status"], entry.Invalidates, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Restart_rebuilds_the_reported_set_from_the_ledger()
    {
        var grant = await _h.HoldLease();
        _ = await Recorder.Handle(new RecordFleetChangesRequest(grant,
            [Change("a1", FleetChangeKind.Appeared, _h.Clock.GetUtcNow()), Change("b2", FleetChangeKind.Appeared, _h.Clock.GetUtcNow()), Change("b2", FleetChangeKind.Gone, _h.Clock.GetUtcNow().AddSeconds(5))]), Token);
        var memory = new CollectorMemory();

        var restored = await new RestoreFleetHistoryService(_h.Store, _h.Events, memory, _h.Clock).Handle(new RestoreFleetHistoryRequest(), Token);

        Assert.Equal(1, restored.Value.Reported);
        Assert.Equal(["claude-code/a1"], memory.Tracker.Reported.Keys.Select(static r => r.ToString()), StringComparer.Ordinal);
        Assert.True(memory.Restored);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Rows_older_than_24_hours_are_not_restored()
    {
        var grant = await _h.HoldLease();
        _ = await Recorder.Handle(new RecordFleetChangesRequest(grant, [Change("a1", FleetChangeKind.Appeared, _h.Clock.GetUtcNow())]), Token);
        _h.Clock.Advance(TimeSpan.FromHours(25));
        var memory = new CollectorMemory();

        var restored = await new RestoreFleetHistoryService(_h.Store, _h.Events, memory, _h.Clock).Handle(new RestoreFleetHistoryRequest(), Token);

        Assert.Equal(0, restored.Value.Reported);
    }

    private static string? Str(JsonDocument doc, string name) => doc.RootElement.GetProperty(name).GetString();
}
