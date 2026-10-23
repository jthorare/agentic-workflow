using System.Text.Json;
using System.Text.Json.Serialization;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.GetInstanceFact;
using Aw.Sindri.Features.Pr;

namespace Aw.Sindri.Testing.Views;

/// <summary>
/// The bodies behind the fact rows (the facts route, API spec section 3.8; screens spec section 4.2): a scope-map table with
/// its graph and items columns, a packet with its PKT-* results (frozen with every check passing, plus an unfrozen one with a failing check), an evidence summary (the packs and how many tasks are done), a payload tree, text, and a
/// truncated payload. A body is an object for scope-map, packet, evidence and payload (also when truncated) and a string for text. Each is built from small shapes
/// and serialized, so no JSON is written by hand.
/// </summary>
public static class FactFixtures
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    private sealed record Surface(string Name, string Graph, string[] Files, string[] Items);

    private sealed record ScopeMap(string Brief, Surface[] Surfaces);

    private sealed record Task(string Id, string Title, string Item, int Acceptance, string[] Writes);

    private sealed record Stream(string Id, string Label, Task[] Tasks);

    private sealed record PacketCheck(string Id, string Result, string? Detail, string[] Tasks);

    private sealed record Packet(string Id, bool Frozen, Stream[] Workstreams, PacketCheck[] Checks);

    private sealed record EvidencePack(string Task, string Verdict);

    private sealed record EvidenceSummary(EvidencePack[] Packs, int Done, int Total);

    private sealed record Incident(string Id, string Title, string Urgency, string Service, string Status, string CreatedAt);

    private sealed record Alert(string Source, Incident Incident, string[] Tags);

    private sealed record Event(int N, string Kind);

    // The lens loop's escalation entries (Escalation.All's shape, member order included): 'options' only when the question offers choices.
    private sealed record Escalation(string Kind, [property: JsonPropertyName("question")] string Ask, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string[]? Options, string Lens, string Finding, string Id);

    private sealed record EventLog(string Source, Event[] Events);

    // Packet 3f1c09 (screens fixture.md): nine tasks in four workstreams. t4 is ITM-01JD6, t6 ITM-01JF3, t7 ITM-01JF4, t8 ITM-01JF8.
    private static Stream[] Streams() =>
    [
        new Stream("ws-1", "UI",
        [
            new Task("t3", "Sync status pill", "ITM-01JC3", 3, ["ui/sync-pill.tsx"]),
            new Task("t9", "Conflict resolver dialog", "ITM-01JC5", 4, ["ui/conflict-dialog.tsx"]),
        ]),
        new Stream("ws-2", "Store",
        [
            new Task("t1", "PUT /sync/changes", "ITM-01JC1", 5, ["src/sync/changes.ts"]),
            new Task("t2", "Outbox queue", "ITM-01JC2", 3, ["src/sync/outbox.ts"]),
            new Task("t4", "Services page job panel", "ITM-01JD6", 4, ["ui/services-jobs.tsx"]),
        ]),
        new Stream("ws-3", "Mobile",
        [
            new Task("t5", "Background sync (iOS)", "ITM-01JD7", 4, ["ios/BackgroundSync.swift"]),
            new Task("t6", "doctor views line", "ITM-01JF3", 2, ["src/doctor/views.ts"]),
            new Task("t7", "Tray badge on Linux", "ITM-01JF4", 2, ["ui/tray-linux.ts"]),
        ]),
        new Stream("ws-4", "Rollout", [new Task("t8", "Receipt verify route", "ITM-01JF8", 3, ["src/receipts/verify.ts"])]),
    ];

    // The unfrozen case: t9 writes the queue file t4 also writes, so PKT-SCOPE-002 fails and freeze stays disabled.
    private static Stream[] UnfrozenStreams() =>
    [
        .. Streams().Select(w => w with
        {
            Tasks = [.. w.Tasks.Select(t => t.Id == "t9" ? t with { Writes = ["ui/conflict-dialog.tsx", "src/sync/queue.ts"] } : t.Id == "t4" ? t with { Writes = ["ui/services-jobs.tsx", "src/sync/queue.ts"] } : t)],
        }),
    ];

    private static PacketCheck[] PassingChecks() =>
    [
        new PacketCheck("PKT-SCHEMA-001", "pass", Detail: null, []),
        new PacketCheck("PKT-ACCEPT-001", "pass", Detail: null, []),
        new PacketCheck("PKT-SCOPE-002", "pass", Detail: null, []),
    ];

    private static JsonElement Body<T>(T value) => JsonSerializer.SerializeToElement(value, _options);

    // The babysitter's merge-stop summary, as the snapshot Step writes it: 5 passed, 2 failing, 1 pending, no recheck of the head (proof null), docs missing.
    private static JsonElement PrStatus()
    {
        var pull = new PullView("https://forge.test/acme/app/pull/12", PullState.Open, "aaaa1111", Mergeable: true, ReviewDecision: null, UnresolvedThreads: 1,
            new PullChecks(PullChecksState.Failing, "aaaa1111", ["build", "lint"], Passed: 5, Pending: 1), [], "main");
        var threads = new[] { Thread("t1", resolved: false), Thread("t2", resolved: true), Thread("t3", resolved: true) };
        return Body(PrStatusFact.Of(recheck: null, docsResult: null, pull, threads, docs: null));
    }

    private static PullThread Thread(string key, bool resolved) =>
        new(key, PullCommentKind.Thread, "alice", "MEMBER", IsBot: false, resolved, Outdated: false, "a.cs", $"fix {key}", [], new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero), Line: null, EditedAt: null, Url: null);

    /// <summary>The body behind one fact row, by scenario: scope-map, packet, evidence, text, payload or truncated.</summary>
    /// <param name="scenario">The scenario name.</param>
    /// <returns>The fact view.</returns>
    public static FactView Fact(string scenario) => scenario switch
    {
        "scope-map" => new FactView("fact:scope", FactType.ScopeMap, Body(new ScopeMap("PRD-sync.md",
        [
            new Surface("sync store", "mapped", ["src/sync/store.ts", "src/sync/outbox.ts", "src/sync/queue.ts"], ["ITM-01JC1", "ITM-01JC2"]),
            new Surface("status pill", "mapped", ["ui/sync-pill.tsx"], ["ITM-01JC3"]),
            new Surface("conflict dialog", "new", ["ui/conflict-dialog.tsx", "ui/conflict-diff.tsx"], ["ITM-01JC5"]),
            new Surface("background sync (iOS)", "unmapped", ["ios/BackgroundSync.swift", "ios/SyncScheduler.swift", "ios/Reachability.swift"], ["ITM-01JD7"]),
        ])), Truncated: false),
        "packet" => new FactView("fact:packet", FactType.Packet, Body(new Packet("3f1c09", Frozen: true, Streams(), PassingChecks())), Truncated: false),
        "packet-unfrozen" => new FactView("fact:packet", FactType.Packet, Body(new Packet("3f1c09", Frozen: false, UnfrozenStreams(),
        [
            .. PassingChecks().Where(c => c.Id != "PKT-SCOPE-002"),
            new PacketCheck("PKT-SCOPE-002", "fail", "t4 and t9 both write src/sync/queue.ts", ["t4", "t9"]),
        ])), Truncated: false),
        "evidence" => new FactView("fact:evidence", FactType.Evidence, Body(new EvidenceSummary(
            [new EvidencePack("t1", "pass"), new EvidencePack("t2", "pass"), new EvidencePack("t3", "pass"), new EvidencePack("t9", "pass")], Done: 4, Total: 9)), Truncated: false),
        "text" => new FactView("fact:severity", FactType.Text, Body("sev2: the outbox dispatcher retries without backoff"), Truncated: false),
        "payload" => new FactView("fact:alert", FactType.Payload, Body(new Alert("pagerduty",
            new Incident("P-2211", "Outbox retry storm", "high", "outbox-dispatcher", "triggered", "2026-10-12T11:02:00Z"), ["prod", "outbox"])), Truncated: false),
        "truncated" => new FactView("fact:history", FactType.Payload, Body(new EventLog("pagerduty", [new Event(1, "triggered"), new Event(2, "acknowledged")])), Truncated: true),
        "lens-ask-escalation" => new FactView("fact:escalation", FactType.Payload, Body(new Escalation[]
        {
            new("productQuestion", "Keep the legacy flow?", ["keep the queue", "drop the queue"], "product", "P1", "q1"),
            new("ambiguousFix", "Which table owns expiry?", Options: null, "arch", "A2", "q2"),
            new("productQuestion", "Who is the expiry for?", Options: null, "product", "P3", "q3"),
        }), Truncated: false),
        "pr-status" => new FactView("fact:prStatus", FactType.Payload, PrStatus(), Truncated: false),
        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "no such fact scenario"),
    };
}
