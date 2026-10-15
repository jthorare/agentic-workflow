# API P4 — Requests, decisions and needs-you: implementation plan

Spec: `docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md` (API), Phases row P4; §3.1 `/needs-you`,
`/requests`, `/decisions`; §3.2 `FileRequest`, `Decide`; §3.4 `request.filed`, `decision.recorded`, "How a request
becomes a decision"; §3.6 `NeedsYouRow`, rows by kind; §4.2; §9 `SND-REQ-*`, `SND-DEC-*`, `SND-ITEM-409`; §12.
UI spec `docs/superpowers/specs/2026-10-11-sindri-ui-design.md` §4.3, §4.7, §7, E-7, E-16, E-17, E-18. The UI spec
wins where the two differ (README X52). Base: P3's tip (`feat/sindri-api-p3-items`); P4 needs P3 merged (wave W3).

## 0. Goal

A session (or a human) files a request; it shows up as exactly one pulsing `decision` row in `GET /needs-you`,
`recordedOnly: true`; only a human decides it (`sindri decide`, or the app), which clears the row everywhere, and a
second, racing `decide` gets `SND-ITEM-409 decided elsewhere`. The same list carries `failure` rows (from P2's
`service.ran`) and the `profile` row (from P2's `profile.checked`), each clearing by the spec's rule. The CLI verbs
`request`, `requests`, `decide`, `needs-you`; the UI's U3 lane gets golden JSON for every route and scenario (E-17).

Spec signals (Phases P4), numbered for the verification plan:
- **S1** a session `curl POST /requests` (no caller header) → exactly one `decision` row, `recordedOnly: true`.
- **S2** the same `decide` with no caller header → 403 `SND-AUTH-002`.
- **S3** `sindri-prove.sh human decide DEC-… accept` clears the row.
- **S4** two `decide` calls started together → one succeeds, the other `SND-ITEM-409 decided elsewhere`.
- **S5** editing the live profile adds a `profile` row within 30 s; reverting clears it.
- **S6** a failing service adds a `failure` row; its next good run clears it.
- **S7** a failing service removed from the profile, with the profile approved, clears its row.
Plus: `CloseItem` voids pending decisions (§3.2 note), `needs-decision` item state (§3.6 "Before S2"), the request
caps (`SND-REQ-002`), session repo scope (§3.1), E-7 `actions`/`label`, E-16 `cli`, E-17 goldens.

## 1. Contract unit (runs first, committed alone; UI U3 and P8 code against it)

### 1.1 Core — ids (`Core/Ids`, `docs/csharp/typed-ids.md`)
- `RequestId` (`REQ-` + 32 lower hex) and `DecisionId` (`DEC-` + 32 lower hex): Vogen over string, same shape and
  `New()` path as `ItemId`; `ITypedId<…, string>` so `CliForm.Fill` takes them. JSON and parse tests (P3 deviation 1:
  no RDG route-binding tests; `{decisionId}` binds as a string and goes through `DecisionId.TryParse`).
- `DecisionOption` (Vogen over string, `^[a-z][a-z-]{0,31}$`): an option id, safe for `CliForm.Fill`.

### 1.2 Core — requests and decisions (`Aw.Sindri.Core.Requests`, `Aw.Sindri.Core.Decisions`)
- `RequestKind` enum, kebab JSON: `rescope`, `add-task`, `note`, `handback`.
- `RequestState` enum: `open`, `accepted`, `declined`, `voided` (G3).
- `DecisionKind` enum: `accept-request` (the only P4 kind; §17.12's others come with their phases).
- `DecisionState` enum: `pending`, `decided`, `voided`.
- `DecisionKinds` table (pure): per kind its options `[{id, label, rejectLike}]` and the recommended option.
  `accept-request` → `accept` ("Accept", recommended), `decline` ("Decline", `rejectLike: true`).
- `RequestRules` (pure, `Result`-returning): `Validate(kind, text)` → `SND-REQ-001` for an unknown kind, empty text,
  or text over 2,000 characters (G2: counted in Unicode scalars, before scrubbing); `Cap(openForRepo, openTotal)` →
  `SND-REQ-002` at 20 per repo / 50 total (G4); `File(…)` → the `RequestFiled` payload; `RecordedOnly(...)` → `true`
  in P4 (G6).
- `DecisionRules.Decide(DecisionRow, choice, note, actor, commandId)` → the `DecisionRecorded` payload, or
  `SND-DEC-001` (not an option), `SND-ITEM-409` "decided elsewhere" (already decided), `SND-ITEM-409` "the item was
  closed" (voided). Note ≤ 2,000 characters, scrubbed (G2).
- `DecisionLabels.For(kind, requestKind)` → `decision · re-scope`, `decision · add task`, `decision · note`,
  `decision · hand back` (E-7).

### 1.3 Core — needs-you (`Aw.Sindri.Core.NeedsYou`)
- `NeedsYouKind` (`decision`, `park`, `failure`, `profile`), `Urgency` (`pulse`, `colour`, `calm`), `AnswerKind`
  (engine §5.8 values; null for every P4 row).
- `SubjectRef(SubjectKind kind, string id)`, `SubjectKind`: `item`, `decision`, `instance`, `job`, `service`, and
  `profile` (G8, **owner**).
- `NeedsYouRow` (wire, U3-API-1): `{id, kind, label, subject, item?, title, reason, repo?, urgency, since,
  recordedOnly, notice?, answerKind?, primaryAction, actions, cli}`.
  - `id`: `decision:DEC-…`, `failure:service:<name>`, `profile:drift`.
  - `primaryAction` and every `actions[]` entry is a catalog action id, never `copy:` (U3-API-2); primary ∈ actions.
  - `cli` is built only through `CliForm.Fill` with typed ids (E-16); no free text.
  - `notice`: `EmptyText.RecordedOnly()` when `recordedOnly`, else null (U3-API-7).
- `NeedsYouOrder.Compare`: urgency (`pulse` < `colour` < `calm`), then `since`, then `id` (ordinal) for a stable tie.
- `NeedsYouRows` (pure builders): `ForDecision(DecisionRow, RequestRow)`, `ForFailure(ServiceRow)`,
  `ForProfile(ProfileRow)`. Per kind:

| kind | label | subject | title | reason | urgency | primary · actions | cli |
|---|---|---|---|---|---|---|---|
| `decision` | `decision · re-scope` … | `{decision, DEC-…}`, `item?` | the request's first line, `SafeText.Line(…, 120)` | `"<kind> request from <actor>"` | `pulse` | `Decide` · `[Decide]` | `sindri decide DEC-… <choice>` |
| `failure` | `failure · <service>` | `{service, <name>}` | `"<service> failed"` | `"<code>: <fix>"`, or `"failed at <slot>"` with no code | `colour` | `RunService` · `[RunService, PauseService]` (both `pending:P6` in the catalog) | `sindri services run <name>` |
| `profile` | `profile · drifted` | `{profile, <live hash>}` (G8) | `"The profile changed since approval"` | `"N key paths changed: a, b, …"` (first 3) | `calm` | `ApproveProfile` · `[ApproveProfile]` (catalog `cli-only:typed-confirmation`) | `sindri profile approve <live hash>` |

### 1.4 Core — events, catalog, errors
- Payloads (records, `commandId` first): `RequestFiled(commandId, request, decision, kind, item?, repo?, text)` and
  public `RequestFiledPublic` (text left out, §3.4); `DecisionRecorded(commandId, decision, choice, note?, by)`
  (public as is).
- `EventTypes.RequestFiled = "request.filed"`, `DecisionRecorded = "decision.recorded"`; `EventCatalog` rows with key
  shapes `request.filed:<requestId>`, `decision.recorded:<decisionId>`.
- `SindriErrors`: `SND-REQ-001` Invalid (400), `SND-REQ-002` RateLimited (429), `SND-DEC-404` NotFound (404),
  `SND-DEC-001` Invalid (400); `SND-ITEM-409` reused (two new detail texts). Each with its `(fix: …)` text.
- `TimelineLine` gains the two types: "re-scope request filed by session:cli", "decision DEC-… accepted by
  human:prover" (and "voided: the item was closed").

### 1.5 Core — views and text
- `RequestRow`, `DecisionRow` (view rows: everything the queries return plus `itemVersion`, `repo`, `filedBy`,
  `filedAt`, `decidedBy?`, `decidedAt?`, `choice?`, `note?`, `state`), `NeedsYouRowRecord` (the stored row).
- `ViewNames`: `requests`, `decisions`, `needs_you`. `ViewSchema.Version = 3` (a new fold: the projector rebuilds on
  start, so P2 events already in the ledger produce `failure`/`profile` rows, G9).
- Read ports: `IRequestViews`, `IDecisionViews`, `INeedsYouView` (§2.2).
- `EmptyText` additions: `NeedsYou(running, queued)` → "Nothing needs you. N running, M queued." (zeroes
  shown; U3'-API-11, 2026-10-14: "waiting on you" is retired by screens R8-13 and fixture.md "The human queue", so the
  API §3.6 example sentence gets the same alignment edit; the "Last cleared HH:MM" variant needs new data and waits on
  the owner, rev8-contract-plan Q11); `Decisions(pending, item)` → "No decisions are waiting." / "Nothing decided yet." / "No decisions for
  ITM-…."; `Requests(state, item)` → "No requests filed." / "No open requests." / "No requests for ITM-…."; and
  `RecordedOnly()` → "recorded; acted on once the workflow engine runs" (§3.4 wording).

### 1.6 Features (generator, `--slice-only --package Sindri --surface http`)
- `FileRequest`: `FileRequestRequest(kind, itemId?, text)` → `RequestFiled{accepted:{commandId, sequence},
  requestId, decisionId}` (U3-API-3; P3's `ItemCreated` shape). Class `request`, `ReadsViews`.
- `Decide`: `DecideRequest(decisionId, choice, note?)` → `CommandAccepted`. Class `decision`, `ReadsViews`.
- `ListRequests`: `ListRequestsQuery(RequestState?, ItemId?)` → `RequestList{requests, empty, asOf}`; each
  `RequestView{id, kind, item?, repo?, text, actor, state, decisionId, filedAt}` (U3-API-4 adds `item`).
- `ListDecisions`: `ListDecisionsQuery(bool? pending, ItemId?)` → `DecisionList{decisions, empty, asOf}`; each
  `DecisionView{id, kind, class, label, item?, itemVersion, repo?, question, options:[{id, label, rejectLike, cli}],
  recommended, state, filedBy, filedAt, decidedBy?, decidedAt?, choice?, note?, recordedOnly, notice?, actions, cli,
  cleared?}` (U3-API-3, -6, -7, -8). `question`: "Accept this re-scope request?" plus the text; `actions` `[Decide]`
  while pending, `[]` otherwise.
- `GetNeedsYou`: `GetNeedsYouQuery()` → `NeedsYouList{rows, empty, asOf}`.
- Red stubs throwing `NotSupportedException`; request/response records in `SindriApiJsonContext`.

### 1.7 Host
- Endpoints: `POST /sindri/requests`, `POST /sindri/decisions/{decisionId}`, `GET /sindri/requests?state=&item=`,
  `GET /sindri/decisions?pending=&item=`, `GET /sindri/needs-you`; query binding through P3's route helpers
  (`ItemRoute`, `WithoutParameterNames`).
- CLI verbs: `sindri request <kind> [--item X] "<text>"`, `sindri requests [--state S] [--item X] [--json]`,
  `sindri decide <decisionId> <choice> [--note N]`, `sindri decisions [--pending | --decided] [--item X] [--json]`
  (G11), `sindri needs-you [--json]`. Text output prints `empty`; exit 1 on `rebuilding`/`stalled` (§3.6).
- Catalog: `FileRequest`, `Decide` flip `pending:P4` → `ready` in the same commit as the red slices (P3 deviation 2);
  three new query rows (`ListRequests`, `ListDecisions`, `GetNeedsYou`). `Decide`'s `shortcut` stays null (G10).

### 1.8 Testing support
`RequestBuilder`, `DecisionBuilder`, `NeedsYouRowBuilder` in `Aw.Sindri.Testing`; `FakeTextScrubber` (P3's) reused.

### 1.9 Commit sequence (contract unit)
1. `feat:` ids (`RequestId`, `DecisionId`, `DecisionOption`) with JSON/parse tests.
2. `feat:` request, decision and needs-you vocabulary and records; events and catalog rows; errors; `EmptyText`.
3. `test:` red slices (five) + catalog flip + catalog golden re-record.

## 2. Units after the contract (≈ half a day each; B, C, D can run as parallel worktrees off the contract commit)

### Unit A — Core rules (pure)
`RequestRules`, `DecisionRules`, `DecisionKinds`, `DecisionLabels`, `NeedsYouOrder`, `NeedsYouRows`. Unit lane only.

### Unit B — views.db tables (`0003_requests_decisions_needs_you.sql`, the next free number; P3 lead answer 6)
`view_requests`, `view_decisions`, `view_needs_you` (key, row_json, plus indexed `repo`, `state`, `item`); range and
filtered reads for the three ports; `ViewSchema.Version = 3`.

### Unit C — projections (pure folds, Core)
- `RequestsProjection`: `request.filed` → request `open` + decision `pending` (kind `accept-request`, options from
  `DecisionKinds`, `itemVersion` = the item row's version at fold time); `decision.recorded` → decision `decided`,
  request `accepted`/`declined`; `item.state-changed` to `done`/`cancelled` → that item's pending decisions `voided`,
  open requests `voided` (G3).
- Item projection (P3's) extended: an item with ≥ 1 pending decision and state `new` reads `needs-decision`; back to
  `new` when none are pending (G5: derived, no version bump, no extra event).
- `NeedsYouProjection`: `request.filed` → `decision` row; `decision.recorded` or voiding → row removed;
  `service.ran` failed → `failure` row (upsert: `since` stays the first failure's); `service.ran` ok → removed;
  `profile.checked` → `profile` row added/updated when `drifted`, removed when not, and every `failure` row whose
  service isn't in the approved service names removed.
- Every text field written through `SafeText.Clean(scrubber, …)`.

### Unit D — `FileRequest` (first `request`-class command)
Inside the gate, after the views catch up (§2.1): validate; resolve the item through `IItemResolver` (session scope →
`SND-ITEM-404`; a closed item → `SND-ITEM-409` "already done", G7); repo = item's, else `X-Sindri-Repo`; for a
`session` caller count open requests (repo, total) → `SND-REQ-002`; scrub; append `request.filed` with both ids from
`IIdGenerator`.

### Unit E — `Decide`
Read the decision row (`SND-DEC-404`; a session outside its repo also `SND-DEC-404`, never a hint); `DecisionRules`;
append `decision.recorded`. `AlreadyRecorded` with a foreign `commandId` → `SND-ITEM-409` "decided elsewhere"
(§2.1). Concurrency test: two `Decide` tasks started together → exactly one event, the other 409.

### Unit F — the three queries
`ListRequests`, `ListDecisions`, `GetNeedsYou`: scoped like P3 (`human` sees all; `session` only its repo plus
repo-less rows); `empty` through `EmptyText.For`; `asOf`; `cleared` (U3-API-8) for decided/voided decisions.

### Unit G — `CloseItem` voiding and item state
`CloseItem` on an item with pending decisions: the fold voids them; a later `Decide` → 409 "the item was closed".
Queue/item goldens re-recorded with a `needs-decision` item (P3 §6 note: "#197's extra `needs-decision` row is added
when P4 re-records it").

### Unit H — CLI verbs
The five verbs; `--json` equals the route body (minus nothing); exit codes: 0 ok, 1 attention, 2 refused/invalid.

### Unit I — goldens, docs, smoke, gate
Goldens (§6); `docs/csharp/sindri.md` (routes, CLI, codes, wire names); API §3 spec edits approved below;
`aot-smoke` gains `request`, `needs-you`, `decide`, the session refusal; merge gate.

## 3. Gaps between spec and code (G-numbers)

| # | Gap | Plan |
|---|---|---|
| G1 | `NeedsYouRow` field names and `subject` shape aren't fixed | U3-API-1's names + `item?` + `notice?`; goldens are the wire contract (P3 precedent) |
| G2 | "2,000 characters" isn't defined; scrubbing can change length | Count Unicode scalars of the raw text; refuse over 2,000 (`SND-REQ-001`); store `SafeText.Clean(…, 2000)` (truncates after scrubbing) |
| G3 | No request state for a voided request (item closed) | `RequestState.voided`; decision `voided` |
| G4 | Caps: "open requests"; whose? | Open = decision pending; counted only for `session` callers (§4.2 puts the cap in the `session` cell), across all callers' open requests in the repo / overall (Q2) |
| G5 | `needs-decision` without an event: does the item version bump? | Derived in the item fold, no `item.state-changed`, no version bump; `CloseItem` with the seen version still works; `Decide` checks the decision's `state`, not the version |
| G6 | `recordedOnly` before S2: the engine code exists | `true` for every P4 decision; P11 (engine item states) makes it false for an item with an active instance waiting on `decision.recorded` (Q3) |
| G7 | A request against a closed item | `SND-ITEM-409` "already done" |
| G8 | §3.6's subject kinds have no `profile` | Add `profile` (id = the live hash) — **owner** (spec edit beyond the UI spec). If declined: the profile row's `subject` is null and the UI keys on `id` `profile:drift` |
| G9 | A new fold over P2's existing events | `ViewSchema.Version = 3` → rebuild on start |
| G10 | `X` = Decline: `Decide` is one action id (UI §8 reads the catalog's `shortcut`) | `options[].rejectLike`; no `Decide` variants |
| G11 | No CLI verb for `GET /decisions` in §3.1 | `sindri decisions [--pending\|--decided] [--item X]` (catalog row `ListDecisions`) |
| G12 | `failure` primary before P6: §3.6 says `copy:sindri status`, UI §4.3/§7 say `RunService` | UI wins (X52): `RunService` (`pending:P6`), `cli` `sindri services run <name>` |

## 4. Decisions

- D1. One domain event per command: `FileRequest` → `request.filed` only; `Decide` → `decision.recorded` only. The
  request row, decision row, needs-you row and item `needs-decision` are all projections (§3.4).
- D2. Both `FileRequest` and `Decide` are `ReadsViews` (the pending catalog rows already say so); they wait for the
  head inside the gate, then `SND-API-011` after 2 s, `SND-VIEW-001` stalled, `SND-API-010` rebuilding.
- D3. Read-your-writes: `X-Sindri-After: <sequence>` on the queries; the timeout is `SND-API-011` (503), not
  `SND-API-004` (UI §4.1's number is stale; U3 errata).
- D4. `actions` lists the ids that apply to the subject; ready/pending/cli-only is the catalog's (P3's `ItemSummary`
  rule). So a `failure` row lists `RunService` and `PauseService` while both are `pending:P6`.
- D5. A session's `Decide` → 403 `SND-AUTH-002` with the CLI `sindri decide DEC-… <choice>`; audited `refused`.
- D6. Goldens slug rule (U3-API-5): a path with GET and POST shares one slug; GET scenarios carry `list-` (P3's
  `items/list-*` precedent). So `requests/list-*.json` and `requests/filed.json`.
- D7. `cleared` on a decided or voided `DecisionView`: the `TimelineLine` of its `decision.recorded` (or the void),
  scrubbed (U3-API-8). No `GET /decisions/{id}` route in P4 (Q4).

## 5. Questions for the lead

1. G8: take `profile` as a sixth subject kind (owner approval for the §3.6 edit), or something else?
2. G4: does the 50-total cap count only session-filed open requests or every open request? Plan: every open request,
   checked only when the caller is a session.
3. G6: `recordedOnly` constant `true` in P4, with P11 owning the change — agreed?
4. U3-API-8: `cleared` on decided decisions (plan) is enough for an itemless request; the UI has to find it in
   `GET /decisions?pending=false`. Add `?id=` or `GET /decisions/{id}` (owner) instead?
5. G10: `options[].rejectLike` vs `Decide:accept`/`Decide:decline` catalog variants with `shortcut: "X"` on decline
   (fits UI §8's wording literally, but fixes the variant set to accept-request's options).
6. Decided list order and size: newest first, capped at 200 (no paging in P4)?

## 6. Goldens P4 publishes (`tests/Aw.Sindri.Tests/Golden/Api/<slug>/<scenario>.json`)

Every scenario: `FakeTimeProvider(KnownInstants.Epoch)`, `SequentialIdGenerator` (`REQ-`, `DEC-`, `ITM-` stable), the
golden pid, `Projector.CatchUp` before the request. Names follow U3's provisional list where it gave one.

| Route (slug) | Scenarios |
|---|---|
| `GET /needs-you` (`needs-you`) | `empty`, `fixture` (1 `decision` from a session request, 1 `failure`, 1 `profile`, in order), `one-request`, `stalled`, `rebuilding`, `large` (12 decisions across 2 repos + failure + profile), `session-repo` (session sees its repo + repo-less only), `after-decide` (U3's extra, promoted) |
| `GET /decisions` (`decisions`) | `pending`, `decided` (`accept` by `human:ui`, `cleared` set), `empty`, `empty-decided`, `voided`, `by-item`, `stalled`, `session-other-repo` (empty) |
| `POST /decisions/{decisionId}` (`decisions-id`) | `accepted`, `declined`, `decided-elsewhere` (409), `voided` (409 "the item was closed"), `not-found` (404 `SND-DEC-404`), `bad-choice` (400 `SND-DEC-001`), `bad-id` (400), `refused-session` (403 `SND-AUTH-002`), `while-stalled` (503 `SND-VIEW-001`) |
| `GET /requests` (`requests`, `list-` prefix) | `list-fixture` (one `open` rescope with its decision id), `list-empty`, `list-state-accepted`, `list-by-item`, `list-session-other-repo`, `list-bad-state` (400) |
| `POST /requests` (`requests`) | `filed`, `filed-item`, `invalid` (400 `SND-REQ-001`, empty text), `invalid-kind`, `too-long`, `too-many` (429 `SND-REQ-002`), `item-not-found` (404), `item-closed` (409), `refused-origin` (403) |
| `GET /queue` (`queue`), `GET /items/{id}` (`items-id`) | re-recorded: `queue/fixture` with #197's `needs-decision` row; `items-id/needs-decision`; `items-id/after-decide` (back to `new`, U3's `extra-p4-decided`) |
| `GET /commands` (`commands`) | re-recorded `catalog` (`FileRequest`, `Decide` ready; three query rows) |

## 7. Task list (red first; each behaviour = red commit, then fix; ≈ half a day per line)

| # | Unit | Red test names (first) |
|---|---|---|
| 1 | contract | `RequestId_round_trips_json`, `DecisionId_rejects_wrong_prefix`, `Catalog_rows_for_P4_are_ready`, the five red slices |
| 2 | A | `Validate_refuses_empty_and_over_2000_scalars`, `Cap_refuses_the_21st_in_a_repo`, `Decide_refuses_a_choice_outside_the_options`, `Decide_on_decided_is_decided_elsewhere`, `Needs_you_orders_by_urgency_then_since_then_id` |
| 3 | B | `Decisions_view_filters_by_pending_and_item`, `Schema_bump_rebuilds_on_start` |
| 4 | C (requests) | `Request_filed_derives_one_pending_accept_request_decision`, `Close_voids_pending_decisions_and_open_requests`, `Item_with_pending_decision_reads_needs_decision` |
| 5 | C (needs-you) | `Filed_request_is_one_decision_row`, `Failure_row_keeps_first_since`, `Ok_run_clears_failure`, `Approved_profile_without_the_service_clears_failure`, `Drift_adds_profile_row_and_revert_clears_it` |
| 6 | D | `FileRequest_from_session_appends_one_event_with_repo`, `FileRequest_twenty_first_open_in_repo_is_SND_REQ_002`, `FileRequest_against_other_repo_item_is_404`, `FileRequest_text_is_scrubbed_in_the_ledger` |
| 7 | E | `Decide_appends_decision_recorded_by_caller`, `Two_concurrent_decides_one_event_one_409`, `Decide_from_session_is_403_and_audited`, `Decide_on_voided_is_409_item_closed` |
| 8 | F | `Session_sees_only_its_repo_rows`, `Needs_you_empty_sentence_counts_running_and_queued`, `Decided_view_has_cleared_line`, `Every_cli_is_a_filled_CliForm` (E-16) |
| 9 | G | `Decide_after_close_is_409`, `Queue_fixture_has_needs_decision_in_waitingOnYou` |
| 10 | H | `Cli_request_prints_ids_and_sequence`, `Cli_needs_you_json_equals_route`, `Cli_exits_1_when_stalled` |
| 11 | I | goldens recorded and `GoldenContractTests` green; AOT smoke `expected.txt`; docs; merge gate |

## 8. Risks

| Risk | Mitigation |
|---|---|
| Concurrency test flakes (gate ordering) | Start both tasks on a barrier; assert counts (one event, one 409), never order |
| The rebuild on schema bump surprises a live home with many events | Rebuild is P2's path (bounded, `asOf.rebuilding`); smoke covers a pre-P4 ledger |
| Scrubbing lengthens text past 2,000 | Truncate after scrubbing (G2); test with a secret near the limit |
| P5's `invalidates` map lands in parallel | If P5 merged first, P4 adds rows for `request.filed` and `decision.recorded` (→ `needs-you`, `decisions`, `requests`, `queue`, `items`, `items-id`, `items-id-timeline`); else P5 adds them |
| U3 fixtures diverge | U3's provisional names kept; this plan's §6 is the list sent to the UI lane |

## UI U3-API-1..8 (UI U3 plan, "API edits needed")

UI spec wins where it differs (README X52). Marks: **plan-only** (wire detail fixed by goldens and
`docs/csharp/sindri.md`, no spec text), **alignment** (API spec edited to match the UI spec; no owner approval),
**owner** (spec edit beyond the UI spec).

| # | Request | Answer | Cite | Mark |
|---|---|---|---|---|
| 1 | `NeedsYouRow` wire shape; `subject {kind,id}`; `label` for every kind | Taken: `{id, kind, label, subject, item?, title, reason, repo?, urgency, since, recordedOnly, notice?, answerKind?, primaryAction, actions, cli}`; `label` on every kind (`failure · <service>`, `profile · drifted`). `item?` added so "Open in <agent>" has its item on `decision` rows. `subject.kind` `profile` is G8 | API §3.6; UI E-7, §4.3 | plan-only (names, `item?`); API §3.6 gets "`actions`, `label`" per E-7 → alignment; `profile` subject kind → **owner** |
| 2 | `primaryAction` catalog ids only; profile → `ApproveProfile`, failure → `RunService` | Taken (G12, D4). `cli` is the one copy string; `copy:` is never served | API §3.6 vs UI §4.3, §7 | alignment (edit §3.6's `primaryAction` row) |
| 3 | `DecisionView` / `RequestView` / `RequestFiled` fields | Taken: options `[{id, label, rejectLike, cli}]`, `recommended`, `filedBy`, `filedAt`, `decidedBy`, `decidedAt`, `choice`, `note?`; `RequestFiled{accepted:{commandId, sequence}, requestId, decisionId}` | API §3.1, §3.2; UI §4.3 ("who filed it"), §4.7 ("who decided, when, the choice") | alignment for "who filed it, when decided, the choice" in §3.1's decisions row; field names plan-only |
| 4 | `GET /requests?item=` | Taken: `ListRequestsQuery(RequestState?, ItemId?)`, CLI `--item` | API §3.1; UI §4.6 ("requests filed against it") | alignment (§3.1 row) |
| 5 | Slug rule for GET + POST `/requests` | One slug `requests`; GET scenarios prefixed `list-` (P3's `items/list-*`). So not `requests-post/` | UI §18.1 E-17; P3 plan §6 | plan-only |
| 6 | A reject-like marker per option | `options[].rejectLike` (Decline `true`); `Decide`'s catalog `shortcut` stays null (G10, Q5) | UI §8, E-8 | plan-only on the API; UI §8's "from the catalog's `shortcut`" needs a UI-spec wording fix → **owner** (UI lane raises it) |
| 7 | Serve the `recordedOnly` sentence | `EmptyText.RecordedOnly()` served as `notice` on the row and the decision when `recordedOnly` | API §3.4; UI §2, §4.3, §12.4 | plan-only |
| 8 | A clearing line for itemless subjects | `cleared` (the `TimelineLine`) on decided/voided `DecisionView`s; no new route in P4 (Q4) | UI §4.1; API §3.1 | plan-only (a `GET /decisions/{id}` route would be **owner**) |

**Spec edits in the P4 PR.**
- Alignment (no owner approval): API §3.6 `NeedsYouRow` gains `actions` and `label` (E-7) and `primaryAction` is a
  catalog action id for every kind (`failure` → `RunService`, `profile` → `ApproveProfile`, its command in `cli`);
  §3.1 decisions row adds "who filed it and when; when decided and the choice"; §3.1 requests row gains `item=`.
- Needs owner approval: G8 (`profile` subject kind in §3.6); UI §8's `X` wording (U3-API-6).
- Not written into the spec: wire names (goldens + `docs/csharp/sindri.md`), `notice`, `cleared`, `rejectLike`.

## Lead answers (2026-10-14)
- U3-API-6: keep `options[].rejectLike` on one `Decide` row. No `Decide:accept`/`:decline` variants.
- U3-API-8: `cleared` on decided and voided DecisionViews. No `GET /decisions/{id}` route.
- Owner approval pending: G8, `profile` as a needs-you subject kind in API §3.6 (if declined, subject is null). The UI §8 `shortcut` wording is the UI lane's to raise with the owner.

## Owner decisions (2026-10-14, relayed by the orchestrator)
- G8 approved: API §3.6 adds `profile` as a needs-you subject kind. The profile-drift row has `subject: {profile, <live hash>}`.
- X key: X picks the option with `rejectLike`, and is hidden when no option has it. The UI lane rewords UI §8.
- The API serves both the recordedOnly sentence and the voided sentence (`notice`), and the UI spec drops its own copy of them.
- UI §4.3's failure row stands. API §3.6's `copy:sindri status` becomes `RunService` (pending:P6), as U3-API-2 asks.
- The spec edits ship in the P4 PR, test-first.
- 2026-10-14 lead: P4 must also test that a needs-you failure row clears on the first IDLE ok (P2 E1 covers only the services view; needs-you is P4's).
