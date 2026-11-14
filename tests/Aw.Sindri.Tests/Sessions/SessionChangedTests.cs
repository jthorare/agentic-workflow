using System.Globalization;
using System.Text.Json;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.Sessions;
using static Aw.Sindri.Tests.Sessions.SessionData;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>'session.changed' (Agents page spec §7.2): every state change a pass sees reaches the ledger and so the event stream, with the provider, id and new state and no text.</summary>
public sealed class SessionChangedTests
{
    private const string Id = "aaaaaaaa-0000-4000-8000-000000000010";

    private readonly CollectorHarness _c = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ClaudeRecord Busy(DateTimeOffset? at = null, string status = "busy") =>
        new(10, "100") { Session = SessionId.From(Id), Kind = "interactive", Cwd = "/w/api", Name = "api work", Status = status, StatusUpdatedAt = at ?? _c.Now };

    private static List<SessionState> Changes(CollectSessionsResponse pass) => [.. pass.StateChanges.Select(static c => c.State)];

    [Fact, Trait("Lane", "unit")]
    public void Diff_names_each_session_whose_state_differs_from_the_last_pass_and_nothing_else()
    {
        var running = Session("claude-code", "a1", SessionState.Running);
        var other = Session("codex", "b2", SessionState.Idle);
        var previous = new[] { running, other }.ToDictionary(static s => s.Ref);

        var changes = SessionStateChanges.Diff(previous, [running with { State = SessionState.Asking }, other, Session("cursor", "c3", SessionState.Running)], T0);

        var change = Assert.Single(changes);   // c3 is new: that is fleet.changed appeared
        Assert.Equal((running.Ref, SessionState.Asking, T0), (change.Ref, change.State, change.ObservedAt));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_running_asking_running_sequence_gives_one_change_per_transition_and_an_unchanged_pass_none()
    {
        _ = _c.Fake.With(Proc(10, 1, "claude", argv0: "claude", ticks: 100), cwd: "/w/api");
        _ = _c.Records.With(Busy());
        var first = await _c.Pass();   // the session is new
        _ = _c.Fleet.With(Hook(HookState.Asking, _c.Now.AddSeconds(2), AskingReason.Approval, "Bash") with { Session = SessionId.From(Id) });
        var asking = await _c.After(TimeSpan.FromSeconds(2));
        var still = await _c.After(TimeSpan.FromSeconds(2));
        _ = _c.Records.With(Busy(_c.Now.AddSeconds(2)));   // a newer record status than the hook: running again
        var running = await _c.After(TimeSpan.FromSeconds(2));

        Assert.Empty(first.StateChanges);
        Assert.Equal([SessionState.Asking], Changes(asking));
        Assert.Empty(still.StateChanges);
        Assert.Equal([SessionState.Running], Changes(running));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Changes_reach_the_ledger_as_session_changed_with_a_payload_of_provider_id_and_state_only()
    {
        var grant = await _c.Base.HoldLease();
        var change = new SessionStateChange(Session("claude-code", "a1").Ref, SessionState.Asking, T0);
        var recorder = new RecordFleetChangesService(_c.Base.Store, _c.Base.Events, _c.Memory);

        var first = await recorder.Handle(new RecordFleetChangesRequest(grant, []) { StateChanges = [change] }, Token);
        var again = await recorder.Handle(new RecordFleetChangesRequest(grant, []) { StateChanges = [change] }, Token);

        Assert.Equal((1, 0), (first.Value.Recorded, again.Value.Recorded));
        var row = Assert.Single(_c.Base.Store.Events, static e => e.Envelope.Type == EventTypes.SessionChanged);
        Assert.Equal(string.Create(CultureInfo.InvariantCulture, $"session.changed:claude-code:a1:asking:{T0.ToUnixTimeMilliseconds()}"), row.IdempotencyKey);
        using var payload = JsonDocument.Parse(row.Envelope.Payload);
        Assert.Equal(["provider", "sessionId", "state"], payload.RootElement.EnumerateObject().Select(static p => p.Name), StringComparer.Ordinal);
        Assert.Equal(("claude-code", "a1", "asking"), (payload.RootElement.GetProperty("provider").GetString(), payload.RootElement.GetProperty("sessionId").GetString(), payload.RootElement.GetProperty("state").GetString()));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_event_is_public_and_invalidates_status()
    {
        var entry = EventCatalog.All.Single(static e => e.Type == "session.changed");

        Assert.Equal((true, typeof(SessionChanged)), (entry.Public, entry.PublicPayload));
        Assert.Equal(["status"], entry.Invalidates, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_event_payload_holds_the_pane_text_of_an_asking_session()
    {
        var grant = await _c.Base.HoldLease();
        const string Secret = "Do you want to export TOKEN=hunter2?";
        var change = new SessionStateChange(Session("claude-code", "a1").Ref, SessionState.Asking, T0);

        _ = await new RecordFleetChangesService(_c.Base.Store, _c.Base.Events, _c.Memory).Handle(new RecordFleetChangesRequest(grant, []) { StateChanges = [change] }, Token);

        Assert.DoesNotContain(_c.Base.Store.Events, static e => e.Envelope.Payload.Contains("question", StringComparison.OrdinalIgnoreCase) || e.Envelope.Payload.Contains(Secret, StringComparison.Ordinal));
    }
}
