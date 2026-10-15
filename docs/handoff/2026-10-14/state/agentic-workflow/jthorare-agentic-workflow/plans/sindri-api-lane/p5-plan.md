# API P5 — Event stream and ledger read: implementation plan

Spec: `docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md` (API) §2.2 (`ReadLedger` and `ILedgerReader`),
§2.3 (the projector sleeps on the ledger signal), §3.1 (`GET /events`, `GET /ledger`, session repo scope), §3.4
(public types), §3.6 (agent text, the sanitizer), §5.1–§5.5 (what the stream is for, transport and filters, the public
envelope, back-pressure and limits, latency), §9 (`SND-API-012`, telemetry `sindri.streams`), §10 C4 and C9, §12 (event
stream tests, goldens), Phases P5 row, Parallel delivery; amended by the UI spec `2026-10-11-sindri-ui-design.md` §6.7
E-15 (`invalidates`, `GET /events/catalog`), E-17/§18.1 (goldens), §9.2 (the app's use of the stream) — UI wins (README
X52). README X16, X20 (`SND-API-012`), X50. "§" = API spec unless prefixed.

Base: **P3's contract-unit commit** (on P2's green tip; see §8). Branch `feat/sindri-api-p5-stream`, worktree
`.claude/worktrees/sindri-api-p5`. Rebase onto `main` after P3 merges.

## 0. Goal

The event stream becomes the public "go re-read" signal §5 describes, and the ledger gets an audit read.
`GET /events` sends `PublicEvent`s (S1's `LedgerEvent` flattened, the payload the catalog's public record, sanitized),
public types only, with `item` and `types` filters, the session repo filter, `Last-Event-ID` winning over `after` (C4),
an `event: error` frame instead of a swallowed read error, a `: ping` comment every 15 s, and the stream limits
(16, 4 reserved for `human`; `SND-API-012`). `ILedgerSignal` wakes the projector and every stream within milliseconds
of a committed append (the 1 s poll stays as the fallback). `GET /ledger` (`ReadLedger`, `sindri ledger`) pages the
ledger for audits (humans also see non-public types). `GET /events/catalog` serves each public type's `invalidates`
list (E-15). `sindri status --follow` re-reads the status on each event.

Phase signals (spec P5 row), each mapped to the verification plan:
1. `curl -N '…/sindri/events?after=0&types=service.*'` shows each `service.heartbeat` within 100 ms of its append.
2. Reconnecting with `Last-Event-ID: n` resumes at n+1.
3. `command.handled` never appears.
4. `sindri-prove.sh human ledger --json --after 0` lists it.
Plus E-15, the session repo filter, the stream limits, the error frame, the ping, `status --follow`.

---

## 1. Contract unit (runs first, committed alone, on P3's contract commit)

Goal: every shared type, port, row, error code and JSON-context entry exists; build at 0 warnings; all P1/P2/P3-contract
tests green; **exactly the two generated slice red tests are red** (ReadLedger, GetEventCatalog), plus the listed
contract-binding reds (`LedgerSignalContract`, `LedgerReaderContract` filter cases).

### 1.1 Core — ledger (C9 continued)
```csharp
// Ledger/ILedgerReader.cs — additive member (+ PublicAPI line); the existing ReadAfter stays for the projector
public ValueTask<Result<LedgerScan>> Scan(long sequence, int max, LedgerFilter filter, CancellationToken cancellationToken);
public sealed record LedgerFilter(TypeFilter? Types, ItemId? Item, int ScanLimit = 5_000)   // ScanLimit bounds rows read per call
{ public static LedgerFilter All { get; } }
public sealed record LedgerScan(IReadOnlyList<LedgerEvent> Events, long ScannedTo);          // ScannedTo: the last sequence looked at
// Ledger/ILedgerSignal.cs — §5.5
public interface ILedgerSignal
{
    void Pulse(long sequence);                                                             // the ledger adapter, after a committed Append
    ValueTask<long> WaitAfter(long sequence, TimeSpan limit, CancellationToken cancellationToken);
}                                                                                          // returns at once when a pulse past sequence came;
                                                                                           // else the latest known head after the limit
```
`LedgerSignal` (Core, internal sealed, `TimeProvider`, a `TaskCompletionSource` swapped per pulse) registered once in
`AddSindriCore()`. Contract base `LedgerSignalContract` (`A_pulse_past_the_cursor_wakes_a_waiter_at_once`,
`A_pulse_at_or_before_the_cursor_doesnt`, `Without_a_pulse_the_wait_ends_at_the_limit`, `Many_waiters_wake_on_one_pulse`).
`LedgerReaderContract` gains `Scan_filters_by_type_glob`, `Scan_filters_by_item`, `Scan_stops_at_its_scan_limit_and_reports_ScannedTo`,
`Scan_on_a_missing_ledger_is_empty`.

### 1.2 Core — events (`Aw.Sindri.Core.Events`)
```csharp
public sealed record TypeFilter                               // §5.2 'types=<glob,glob>', ReadLedger 'type='
{   public static Result<TypeFilter> Parse(string text);      // comma list, each [a-z0-9-]+(\.[a-z0-9-]+)* with an optional final ".*"; ≤ 16 globs; else SND-CLI-003
    public bool Matches(string type);
    public IReadOnlyList<string> Globs { get; } }
public static class ApiViews                                  // E-15's view names: what the app re-queries, not views.db tables
{   public const string Status = "status", Services = "services", Profile = "profile", Commands = "commands",
        Queue = "queue", Items = "items", Item = "item", Timeline = "timeline";            // P4/P6 add needs-you, decisions, requests, jobs
    public static IReadOnlyList<string> All { get; } }
public static class StreamLimits                              // §5.4, pure
{   public const int Total = 16, HumanReserved = 4, Session = Total - HumanReserved;
    public static bool Admits(CallerClass caller, int open, int openSessions); }        // human: open < 16; session: openSessions < 12 and open < 16
public interface IStreamSlots { bool TryEnter(CallerClass caller, out IDisposable slot); int Open(CallerClass caller); }
```
- `EventCatalogEntry` gains `public IReadOnlyList<string> Invalidates { get; init; } = [];` (an init property, so P3's
  and later rows compile unchanged). P5 fills every public row (G7 table).
- `SindriErrors`: `StreamsFull` `SND-API-012` RateLimited "Too many event streams are open; re-query every 5 s".

### 1.3 Features
- `StreamEvents` changes (C4; same slice, S1): `StreamEventsRequest(long After, ItemRef? Item, TypeFilter? Types,
  int Max = 100) : IQuery<StreamEventsResponse>`; `StreamEventsResponse(IReadOnlyList<PublicEvent> Events, long ScannedTo)`.
  `EventView` is deleted and `PublicEvent` takes its place (G3).
- `Features/Events/PublicEvent.cs` (shared by StreamEvents and ReadLedger; owned by the contract unit):
  `PublicEvent(long Sequence, Guid Id, string Type, int SchemaVersion, DateTimeOffset OccurredAt, string Actor,
  string Tenant, int Ring, string? Item, Guid CorrelationId, Guid? CausationId, string? TraceId, JsonElement Payload)` —
  the §5.3 field set and order. `Features/Events/PublicEvents.cs`: `For(LedgerEvent, JsonSerializerOptions,
  bool includeNonPublic) → Result<PublicEvent?>` (null when not public and not asked; stub in the contract unit).
- `Features/Events/EventScope.cs` (session repo filter, stub): `Visible(CallerContext, LedgerEvent, IItemRepos)`.
  `Features/Events/IItemRepos.cs`: `ValueTask<Result<RepoName?>> RepoOf(ItemId item, CancellationToken)` — the item's repo
  from its `item.created` event through `ILedgerReader.Scan` (never the views, so a just-created item isn't missed while
  the projector catches up; G5). Cached per stream (an item's repo never changes).
- Generator (`--slice-only --package Sindri --surface http`):
  `ReadLedger` (`--behavior pages_public_envelopes_after_a_cursor_with_sanitized_payloads --route "GET /ledger"`):
  `ReadLedgerRequest(long After, ItemRef? Item, string? Type, int Max = 100) : IQuery<LedgerPage>`,
  `LedgerPage(IReadOnlyList<PublicEvent> Events, long? Next)`;
  `GetEventCatalog` (`--behavior lists_each_public_type_with_the_views_it_invalidates --route "GET /events/catalog"`):
  `GetEventCatalogRequest() : IQuery<EventCatalogView>`, `EventCatalogView(IReadOnlyList<EventTypeView> Types)`,
  `EventTypeView(string Type, string KeyShape, IReadOnlyList<string> Invalidates)`.
- Registration lines at the anchors (`AddQuery` ReadLedger, GetEventCatalog).

### 1.4 Host
- Endpoint lines: `queries.MapGetEventCatalog()`, `queries.MapReadLedger()`; `MapStreamEvents` keeps its line (the route
  is unchanged; its body moves in unit E). `/events/catalog` is mapped before `/events` so it never reads as a stream.
- `SindriJsonContext`: `PublicEvent`, `LedgerPage`, `EventCatalogView`, `EventTypeView`, `StreamErrorFrame(string Code, string Detail)`;
  `EventView` removed.
- No catalog rows: P5 adds no command. `CatalogRules.ShippedPhases += "P5"` waits for P5's rebase onto merged P3 (§8).

### 1.5 Testing support
`InMemorySindriStore` implements `Scan` and pulses an injected `ILedgerSignal` after `Append` (constructor overload; the
default keeps a private signal so existing tests don't change); `FakeStreamSlots`; `A.cs` builders from the generator;
`AnsiPayload` fixture (an `item.created` whose title holds ESC `]8;;` and CSI sequences).

### 1.6 Commit sequence (contract unit)
`feat: ILedgerReader.Scan, LedgerFilter and ILedgerSignal contracts` · `feat: TypeFilter, ApiViews, stream limits and SND-API-012` ·
`feat: PublicEvent replaces EventView; catalog entries gain invalidates` · `test: red ReadLedger and GetEventCatalog slices`.

---

## 2. Units after the contract (≈ half a day each; A, B and C can run as parallel worktrees)

### Unit A — public envelopes and filters (pure + Features)
`TypeFilter` real; `PublicEvents.For`: not public → null (unless `includeNonPublic`); public → payload text deserialized
to the catalog's `PublicPayload` type through `JsonSerializerOptions.GetTypeInfo` (unknown members dropped, so a public
record leaves text out by construction) and serialized to a `JsonElement`; every string in the element passes
`SafeText.Escape` (P3's); a non-public payload (human ledger reads) is parsed and escaped as is. A payload that doesn't
parse → the envelope with `payload: null` and a logged warning naming the sequence (the stream must not stall on one row;
G11). Tests: `Typefilter_*` (Theory: exact, `service.*`, list, bad glob), `Command_handled_is_never_public`,
`Public_payload_drops_fields_the_public_record_lacks`, `Ansi_and_osc_in_a_payload_arrive_escaped`,
`Unparseable_payload_is_sent_with_a_null_payload`, `Envelope_fields_match_the_ledger_row`.

### Unit B — the ledger's filtered read
`LedgerSql.ScanAfter` (`WHERE seq > $seq AND seq <= $seq + $scan AND (type filter) AND (item = $item OR $item IS NULL)
ORDER BY seq LIMIT $max`; globs become `type = $t` / `type LIKE $p ESCAPE '\'` with bound parameters only; no new index,
so no ledger migration, G9), `SqliteLedgerReader.Scan`, `InMemorySindriStore.Scan`; `ScannedTo` = min(head, seq + scan)
or the last row returned when the page filled. Tests: `SqliteLedgerReaderContractTests` and the in-memory binding pass the
new contract cases.

### Unit C — `ILedgerSignal` wiring
`LedgerSignal` real; `SqliteLedger.Append` pulses after the commit (never inside the transaction, never on
`AlreadyRecorded`); `ProjectorHost` replaces its 1 s delay with `signal.WaitAfter(cursor, 1 s)` (a full page still
continues at once, P2). Tests: `LedgerSignalContractTests`, `Append_pulses_after_commit`, `Replay_doesnt_pulse`,
`Projector_wakes_on_a_pulse_before_its_poll` (`FakeTimeProvider` never advanced; the pass runs on the pulse),
`Projector_still_polls_without_a_pulse`.

### Unit D — `StreamEvents`: filters and scope
`StreamEventsService(ILedgerReader, IItemResolver, IItemRepos, ICallerContextAccessor, JsonSerializerOptions)`:
`Item` resolved once (alias → id; unknown → `SND-ITEM-404`; a session asking for another repo's item → `SND-ITEM-404`);
`Scan(after, max, filter)`; each event → `PublicEvents.For` (public only) → `EventScope.Visible` (session: the item's
repo equals `X-Sindri-Repo`, or no item; and a payload `repo` naming another repo drops it, §5.2); returns
`ScannedTo` so the endpoint's cursor moves past filtered rows. `IItemRepos` real (`LedgerItemRepos`). Tests:
`Only_public_types_are_streamed`, `Types_filter`, `Item_filter_by_id_and_by_alias`, `Session_sees_its_repo_and_item_less_events`,
`Session_without_repo_sees_only_item_less_events`, `Payload_repo_of_another_repo_is_dropped`, `ScannedTo_moves_past_filtered_rows`.

### Unit E — the `/events` endpoint (C4, §5.2–§5.5)
Rewrite `Host/Endpoints/EventStream.cs`: cursor = `Last-Event-ID` when present and valid, else `after`, else 0 (C4);
`after` past `ILedgerReader.Head()` → 400 `SND-CLI-003` before any frame (§5.4); bad `types` or `item` → 400; slot
from `IStreamSlots` or 429 `SND-API-012` (gauge `sindri.streams` by caller); loop: `Handle` → frames (`id` = sequence,
`event` = type, `data` = `PublicEvent` JSON) → cursor = `ScannedTo`; a full page continues at once; else
`signal.WaitAfter(cursor, min(1 s, time to next ping))`; a `: ping` comment when 15 s pass without a frame; a read error
→ `event: error` `data: {"code":"SND-…","detail":"…"}` and the stream ends (no swallow, C4); stop/abort ends cleanly (S1).
A small Host SSE writer (`Endpoints/SseWriter.cs`: `text/event-stream`, `Cache-Control: no-store`, flush per frame)
replaces `TypedResults.ServerSentEvents` because comments and the error frame need raw control (G4).
Tests (integration, `WebApplicationFactory` + `FakeTimeProvider`): `Last_event_id_wins_over_after`,
`Resume_at_n_starts_at_n_plus_1`, `After_past_the_head_is_400`, `A_read_error_ends_with_an_error_frame`,
`A_ping_comment_every_15_seconds`, `An_append_reaches_the_stream_without_waiting_for_the_poll` (pulse; clock not advanced),
`Seventeenth_stream_is_429_SND_API_012`, `Thirteenth_session_stream_is_429_but_a_human_gets_in`,
`A_closed_stream_frees_its_slot`, `Stop_ends_open_streams` (S1 test kept).

### Unit F — `ReadLedger` and `sindri ledger`
`ReadLedgerService(ILedgerReader, IItemResolver, IItemRepos, ICallerContextAccessor, JsonSerializerOptions)`: `max`
1–500 (else 400 `SND-CLI-003`), `after` past the head → 400 (G8), `type` parsed by `TypeFilter`; human callers get
non-public types (`includeNonPublic`), sessions public only and repo-scoped (as the stream); `Next` = `ScannedTo` when
it's below the head, else null. CLI `Host/Commands/LedgerVerbs.cs`: `sindri ledger [--after N] [--item X] [--type T]
[--max N] [--json] [--no-start]`: text one line per event `<seq>  <occurredAt>  <type>  <actor>  [<item>]`, then
`next: N` when more remain; `--json` the raw body; a human verb (starts the process unless `--no-start`, never inside
an agent session). Tests: the red slice test green, `Human_sees_command_handled`, `Session_never_does`,
`Max_over_500_is_400`, `Next_pages_to_the_end`, `Payloads_are_sanitized`, `Cli_ledger_prints_lines_and_next`,
`Cli_ledger_json_is_the_raw_body`.

### Unit G — `GET /events/catalog` (E-15)
`GetEventCatalogService`: the public entries of `EventCatalog.All` with `KeyShape` and `Invalidates`, sorted by type.
Fill `Invalidates` for every public row (G7). Catalog test: `Every_public_type_invalidates_at_least_one_view`,
`Invalidates_names_only_ApiViews`. Tests: the red slice test green.

### Unit H — `sindri status --follow`
`Host/Commands/FollowVerbs.cs` (G13; `Verbs.Status` untouched except for the arm): prints the status summary, then opens
`GET /events?after=<head>` with the CLI's headers; on each frame (debounced 250 ms) re-reads `GET /status` with
`X-Sindri-After: <seq>` and prints the summary again only when it changed (`--json`: one status body per line, NDJSON);
`: ping` and unchanged reads print nothing; the stream ending (a stop) prints `not running` and exits 1; Ctrl-C exits 0;
a 429 falls back to re-reading every 5 s (§5.4). Never starts the process (§7, like `status`). Streaming client:
`ControlPlaneClient.Stream(route, ct)` (an `IAsyncEnumerable` of frames, a line parser; no new package). Tests:
`Follow_reprints_when_status_changes`, `Follow_ignores_pings`, `Follow_exits_1_when_the_process_stops`,
`Follow_falls_back_to_polling_on_429`, `Follow_json_prints_one_body_per_line`.

### Unit I — prover, goldens, docs, smoke, gate
- `scripts/sindri-prove.sh curl --stream`: passes `-N`, writes the body straight to stdout (no buffered file, no status
  line) so a human stream can be watched; case in `scripts/tests/` for the prover (`--stream` sends the human headers
  and the token; still refuses a non-temp home) (G12).
- Goldens (§6); `docs/csharp/sindri.md` (`/events` row rewritten, `/ledger` and `/events/catalog` rows, an
  `## Event stream` section: envelope, filters, ping, limits, signal; `sindri ledger`, `status --follow`; `SND-API-012`);
  `AGENTS.md` (`ledger`, `status --follow`); `deps.yaml` Sindri note ("API P5: stream, ledger read");
  `.env.example` unchanged. AOT smoke: `ledger --json --after 0` (human), `ledger` as a session (public only),
  `curl` of `/events/catalog`; re-record `expected.txt`. Full merge gate.

---

## 3. Gaps between spec and code (G-numbers) and decisions

| # | Gap | Decision (default unless listed under Questions) |
|---|---|---|
| G1 | `ILedgerReader` reads only `ReadAfter(seq, max)`; filters and `ReadLedger` need type and item | Additive `Scan(seq, max, LedgerFilter)` with a scan bound, returning `ScannedTo`; the projector keeps `ReadAfter` |
| G2 | The session repo filter needs each event's repo; the ledger row has `item` but no repo | `IItemRepos` reads the item's `item.created` (`repo` field) from the ledger, cached per stream; never the views (they lag). A payload `repo` field is read from the public payload (§5.2) |
| G3 | S1's `EventView` sends `payload` as a JSON **string** and omits `id`, `schemaVersion`, `item`, correlation, causation, trace | `PublicEvent` (§5.3) replaces it: payload an object; the `events/process-started` golden is re-recorded. **The UI lane's S1b follower must switch** (told via the contract unit) |
| G4 | `TypedResults.ServerSentEvents` (`SseItem<T>`) can't write a `: ping` comment or a hand-built error frame | A small Host `SseWriter` writes frames itself (AOT-safe, flush per frame); if a reviewer prefers, `event: ping` frames are the fallback (Q6) |
| G5 | S1's stream sleeps 1 s after every page, even a full one; it swallows read errors; `after` beats `Last-Event-ID` | Full page → continue; caught up → wait on the signal (1 s fallback); read error → `event: error` + end; `Last-Event-ID` wins (C4) |
| G6 | UI §9.2 says the stream's 429 is `SND-API-005`; API §5.4/§9 and README X20 say `SND-API-012` (005 is S1's internal error) | `SND-API-012`; the UI lane fixes its §9.2 text (Q1) |
| G7 | E-15's `invalidates` names "views" but no list of names exists; `ViewNames` are `views.db` tables | `ApiViews` (route resources). Rows: `process.*` → status; `profile.approved` → status, profile, services; `service.heartbeat` → status; `service.ran` → status, services; `profile.checked` → status, profile; `item.created`, `item.state-changed` → queue, items, item, timeline; `item.linked` → items, item, timeline. Later phases fill their own rows |
| G8 | `after` past the head is a 400 for the stream (§5.4); `ReadLedger` doesn't say | Also 400 (`SND-CLI-003`), so the two reads behave alike (Q4) |
| G9 | Item filtering might want an `events(item, seq)` index — a ledger migration, numbered contiguously like `views.db`, where the engine lane may also add `ledger.db` migrations | No ledger migration in P5: the scan walks `seq` (primary key) from the cursor, bounded by `ScanLimit`; an index is a later, coordinated migration |
| G10 | `GET /events/catalog`: all types or public ones | Public only: the app maps stream types, and the stream sends only public ones (Q5) |
| G11 | A payload the public record can't read (a hand-inserted bad row, the §18.2 stall trigger) | The stream sends the envelope with `payload: null` and logs it; it never stalls or ends on one row (the projector is what stalls) |
| G12 | `sindri-prove.sh curl` buffers the body to a file, so a human stream can't be watched | `curl --stream` mode (unit I) with its bash case |
| G13 | `status --follow` (§5.1, Phases) has no defined output; `Verbs.Status` is edited by P2 (health exit) | **Q3.** Default as unit H; its own `FollowVerbs.cs`, one arm change in `CommandTable` |
| G14 | `ReadLedger`'s spec param is `type=` (one); the stream's is `types=` (globs) | Both parse with `TypeFilter`, so `type=service.*` and a comma list work in both |
| G15 | "The item filter is tested once P3 lands" | P5 stacks on P3's contract unit: unit tests append `item.*` events directly; the hand proof inserts `item.created` rows with `sqlite3` until P3's verbs are on the branch, then uses `human item add` after the rebase |
| G16 | `next` on `LedgerPage` with filters | `ScannedTo` while it's below the head (a filtered page can be short and still have more), null at the head |
| G17 | Human ledger reads of public types: the public payload or the full one (P4's `request.filed` text) | **Q2.** Default: the public payload for public types for every caller; the full payload, escaped, for non-public types (human only) |

## 4. Questions for the lead
- **Q1 (G6).** Confirm `SND-API-012` and ask the UI lane to correct its §9.2 (`SND-API-005` there).
- **Q2 (G17).** Should a human's `GET /ledger` show the full payload of public types (P4's request text), or the public
  payload like everyone (default)?
- **Q3 (G13).** `status --follow`: re-print the summary on change (default), or print one line per event?
- **Q4 (G8).** `GET /ledger?after=<past head>`: 400 (default) or an empty page?
- **Q5 (G10).** `GET /events/catalog`: public types only (default)?
- **Q6 (G4).** A custom SSE writer for the `: ping` comment (default), or `event: ping` frames through
  `TypedResults.ServerSentEvents`?

## 5. Not in this PR
- Stream `reset` (no retention, §5.4); session-class tokens on the stream (§11); webhooks.
- `invalidates` rows for P4/P6/P10 types (each phase fills its own; the catalog test enforces it).
- The UI's switch to `PublicEvent` and `GET /events/catalog` (UI lane U1).

## 6. Goldens P5 publishes (`tests/Aw.Sindri.Tests/Golden/Api/<slug>/<scenario>.json`)
Stream scenarios store the frames' `data` as a JSON array of `PublicEvent` (framing — `id`, `event`, `: ping`, the error
frame — is asserted in unit E's tests and written once as `events/framing.sse`).
| Route | Scenarios |
|---|---|
| `GET /events` (`events`) | re-recorded `process-started`; `types-service`, `item-filter`, `session-repo`, `session-no-repo`, `ansi-escaped`, `framing.sse`; ProblemDetails `after-past-head` (400), `bad-types` (400), `streams-full` (429) |
| `GET /ledger` (`ledger`) | `human-all` (with `command.handled`), `session-public`, `session-repo`, `type-filter`, `item-filter`, `paged` (`next`), `max-over-500` (400), `after-past-head` (400), `ansi-escaped` |
| `GET /events/catalog` (`events-catalog`) | `catalog` |
Every scenario runs on `FakeTimeProvider(KnownInstants.Epoch)`, `SequentialIdGenerator`, the golden pid; the ANSI
scenario appends the `AnsiPayload` fixture.

## 7. Task list (red first; each behaviour = red commit, then fix; ≈ half a day per line)
1. Contract unit (§1), on P3's contract commit. Gate: build 0 warnings; unit lane shows only the two slice reds + the listed contract reds.
2. A: red filter/envelope/sanitizer tests → `TypeFilter`, `PublicEvents`.
3. B: red `Scan` contract cases → SQL and in-memory `Scan`.
4. C: red signal contract + pulse + projector-wake tests → `LedgerSignal`, `SqliteLedger` pulse, `ProjectorHost`.
5. D: red scope/filter tests → `StreamEventsService`, `LedgerItemRepos`.
6. E1: red endpoint tests (C4, 400s, error frame, ping, wake) → `SseWriter`, `EventStream`.
7. E2: red limit tests → `StreamSlots`, 429, `sindri.streams` gauge.
8. F: `ReadLedger` green + CLI `ledger`.
9. G: `GetEventCatalog` green; `invalidates` for every public row.
10. H: `status --follow` red → `FollowVerbs`, `ControlPlaneClient.Stream`.
11. I1: prover `curl --stream` (bash test first); goldens.
12. I2: rebase onto merged P3 (`ShippedPhases += "P5"`, re-record `expected.txt`, merge AGENTS.md line), docs, full merge gate, prover run.

## 8. File overlap with P3 (run in parallel) and how to merge cleanly

**Sequencing.** P3's contract unit lands first, alone. P5's branch starts from it (P5 uses `ItemId`, `ItemRef`,
`IItemResolver`, `EventTypes.ItemCreated` and its `repo` field, `SafeText`, and the item catalog rows it fills with
`invalidates`). Then both fan out in parallel. P3 merges first; P5 rebases onto `main` (with `--onto` if P3 was
squash-merged) and does its last unit there. P5 never edits a P3-owned file below; P3 never edits a P5-owned one.

| File | P3 | P5 | How it merges |
|---|---|---|---|
| `Core/Events/EventCatalog.cs` | item rows (contract unit only) | `with { Invalidates = … }` on every public row | P5's edit sits on P3's commit: no conflict. Rule for both lanes: P3 adds no rows after its contract unit |
| `Core/Events/EventCatalogEntry.cs` | — | `Invalidates` init property | P5 only |
| `Core/SindriErrors.cs` + `All` + the errors table in `docs/csharp/sindri.md` | `SND-ITEM-404/409` | `SND-API-012` | adjacent lines; P5 resolves at rebase |
| `Core/Registration/SindriCoreServiceCollectionExtensions.cs` | `IProjection` lines | `ILedgerSignal` line | different lines |
| `Core/Ledger/*`, `Infrastructure/Ledger/*`, `LedgerReaderContract`, `InMemorySindriStore` | — | owner | P5 only |
| `Core/Text/*`, `Infrastructure/Text/*` | owner | reads only | — |
| `Core/Views/*`, `Infrastructure/Views/*`, `EmptyText` | owner | — | — |
| `Host/Server/ProjectorHost.cs` | — | signal wait | P5 only. `Projector.cs`: neither lane |
| `Features/SindriFeaturesServiceCollectionExtensions.cs` | 6 lines | 2 lines | sorted anchors; keep both |
| `Host/Endpoints/EndpointTable.cs` | 6 lines | 2 lines | keep both |
| `Host/Endpoints/EventStream.cs`, `StreamEventsEndpoint.cs`, `Features/StreamEvents/*` | — | owner | P5 only |
| `Host/SindriJsonContext.cs` | item types | stream/ledger types, `EventView` removed | keep both attribute blocks |
| `Host/Commands/CommandTable.cs` | 4 new arms | `status` arm + `ledger` arm | different arms |
| `Host/Commands/Verbs.cs` | — | — (`FollowVerbs.cs`, `LedgerVerbs.cs`) | neither lane edits it (P2 does) |
| `Host/Commands/ControlPlaneClient.cs` | — | `Stream` method | P5 only |
| `Host/Commands/Usage.cs` | 4 lines | 2 lines | keep both |
| `tests/…/Catalog/CatalogRules.cs` `ShippedPhases` | `"P3"` | `"P5"` at rebase | one line, after P3's |
| `PublicAPI.Unshipped.txt` (Core, Features), `A.cs`, `IdsJsonContext.cs` | generator lines | generator lines | re-derive from `RS0016` on conflict |
| `docs/csharp/sindri.md` | `## Items`, its routes rows, `views.db` tables | `/events` row, `/ledger`, `/events/catalog` rows, `## Event stream` | separate sections; route table rows adjacent |
| `AGENTS.md` | `queue`, `items`, `item add/close` | `ledger`, `status --follow` | one line: P5 merges at rebase |
| `aot-smoke/run.sh`, `expected.txt` | item steps | ledger steps | P5 re-records `expected.txt` after rebasing (a real run), never hand-merges |
| `scripts/sindri-prove.sh` (+ its bash test) | — | `curl --stream` | P5 only |
| `deps.yaml` | Redaction edge, note | note | P5 rebases the note |
| Goldens | item folders | `events/`, `ledger/`, `events-catalog/` | disjoint |

## 9. Risks
- **Latency signal (100 ms):** depends on `ILedgerSignal` firing after commit and the writer flushing each frame;
  `Kestrel` response buffering is off for the SSE writer. The integration test asserts the wake with a frozen clock.
- **Slow readers:** one page at a time, writes wait on TCP (§5.4); a reader stuck forever holds a slot until the
  socket closes; `RequestAborted` frees it.
- **AOT:** `JsonSerializerOptions.GetTypeInfo(PublicPayload)` needs every public payload in `SindriJsonContext` (a test
  resolves each catalog row's type); `JsonElement` is source-gen safe.
- **Analyzers:** `StreamEventsService`/`ReadLedgerService` 5 deps; the endpoint loop split (`NextPage`, `WaitOrPing`)
  for CA1502/MA0051.
- **Hook contract: unchanged.** No hook reads `/events` or `/ledger` from the C# build (the TS sindri keeps its own).

## Lead answers (2026-10-12)
1. The stream's 429 is `SND-API-012`. The API spec owns the error catalog and 005 is S1's internal error. The lead has reported UI §9.2's `SND-API-005` to the UI lane.
2. A human's `ReadLedger` shows the full payload of public types, scrubbed. A session sees public types for its own repo only, by the session repo filter. No caller ever sees `command.handled`.
3. `status --follow` prints the summary again each time it changes. It exits 0 on Ctrl-C, and 1 if the process stops while it is being followed.
4. `/ledger?after=` past the head is a 400.
5. `/events/catalog` lists public types only.
6. A custom SSE writer is approved so the server can send `: ping` comments. It needs a test for flushing and for disconnect.
- Sequencing as planned: P3's contract unit lands first, P5 branches from it, and P3 merges first.
