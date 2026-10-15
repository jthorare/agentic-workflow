# Plan Design Review: sindri-api-surface

**Plan:** `docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md` (cited as `API:<line>`)
**Checked against:** `docs/superpowers/specs/2026-10-09-sindri-dashboard-design.md` (cited as `DASH:<line>`), factory addendum §17.27 and §17.29
**Reviewed:** 2026-10-10

Scope of this lens: this is an API spec, not a UI design. The review asks whether the API gives the later Avalonia UI
design what the dashboard spec says the UI must show: its views, states, needs-you signal, empty/error/stale/loading
states and CLI text parity. These owner decisions are taken as given and not reopened: CQRS (D1), v0 local-only auth
with no tokens (D4), webhooks cut (D7).

## Inputs available
| Input | Present? |
|---|---|
| plan (single-file spec, used as plan.md) | ✓ |
| design-brief.md | ✗ (the dashboard spec stands in as the design source) |
| product.md | ✗ |
| .impeccable.md | ✗ |
| design-tokens.json | ✗ (doesn't matter for an API spec) |

Because `.impeccable.md` is absent, Brand/voice is N/A. Accessibility is rated only on what the API must supply for
accessible rendering (text states, stable ids, sanitized text). Rendering is the UI design's job.

## Summary
The command side is in good shape for the UI. The action-class table, `GET /commands` and its UI status, the decision
options with a recommended option, `itemVersion` 409s ("decided elsewhere", "changed since you looked"), copy-the-CLI
for protected actions, `--json` byte parity, and the "re-read on event" stream with `reset` together give the UI
honest action forms and live refresh. The read side covers only the **item/decision queue**, though. The dashboard
spec's main content (the **Agents** view with its 13 session states, **Repos**, **Automatic work**, **Today**, and
three of the four **needs-you** sources: sessions waiting on approval or input, failures, and PRs) has no query, no
event and no phase. The spec doesn't defer it either. `needsYou` is also a sub-field of the item queue, so it can't
hold rows that have no item. Beyond that, view responses carry no freshness or "projector is rebuilding" signal, so
the UI can't tell empty from not-yet-built or show "updated Ns ago" honestly. They also carry no empty-state sentence
or fix line for CLI text parity.

## Ratings
Anchors from `_shared/severity.md`.
| Dimension | Rating | Top gap |
|---|---|---|
| Information architecture | 2/5 | Needs-you is an item-queue sub-field; Agents/Repos/Automatic work/Today have no API and aren't explicitly deferred |
| Interaction states | 3/5 | No freshness or rebuilding signal in views, so empty vs not-built vs stale can't be told apart |
| User flow completeness | 3/5 | Dashboard flows "attach to a session" and "acknowledge a failure" have no command; the per-row primary action isn't in the row |
| Accessibility | 4/5 | `NeedsYouRow` has no stable id (focus and pulse tracking across refreshes); sanitizing of control characters isn't assigned |
| Brand/voice consistency | N/A (missing .impeccable.md) | n/a |

## Surface × state coverage
The surfaces are the dashboard views and UI actions the API must feed. A cell cites the API line that addresses that
state, or `GAP`.

| Surface | Loading | Error | Empty | Success |
|---|---|---|---|---|
| Needs you (DASH:217) | GAP: no "views not built yet" signal (rebuild on start, API:74) | API:324 (ProblemDetails code + message); fix line not in body: GAP | GAP: no CLI empty sentence | API:112 (item and decision rows only) |
| Agents (DASH:224) | GAP | GAP | GAP | GAP: no session query |
| Repos (DASH:229) | GAP | GAP | GAP | GAP |
| Automatic work (DASH:235) | GAP | GAP | GAP | API:110 services only; evolve, index, lock, channels: GAP |
| Today (DASH:242) | GAP | GAP | GAP | GAP (API:374 defers metrics) |
| Item list / item detail / timeline | GAP (same rebuild issue) | API:336 `SND-ITEM-404` | GAP: no empty sentence | API:113–115 |
| Decision form | n/a | API:148–150 (409 decided elsewhere / stale), API:340 `SND-DEC-001` | API:117 `pending=` | API:117, API:132 |
| Job progress + cancel | API:119 `running` + progress lines | API:119 `failed` + code | n/a | API:119, API:133 |
| Desk actions (hold/park/unpark/handback) | n/a | API:148, API:337 | n/a | API:42 `CommandAccepted` + API:82 read-your-writes |
| Protected action (copy CLI) | n/a | API:212 (`SND-AUTH-002` carries the command) | n/a | API:274–275; registry status for "copy-only": GAP |
| Read-your-writes wait | API:82 (2 s wait) | API:83 `SND-API-003`; what the UI shows/does: GAP | n/a | API:85–86 |
| Connection / process | n/a | API:284–286 `SND-API-001`, API:347–348 version mismatch | n/a | API:108 |
| Live stream / stale | n/a | API:252–254 `reset`, API:255 429 | n/a | API:221; "updated Ns ago" / 3-failures banner (DASH:248): GAP, no `asOf` or cursor in views |
| Tray badge (DASH:271) | n/a | GAP | GAP | count derivable from API:112 for item rows only |

### Accessibility sub-table
These are the API-side prerequisites only. Keyboard, contrast and focus rendering belong to the UI design.

| Surface | Keyboard | Screen reader | Contrast | Focus mgmt |
|---|---|---|---|---|
| Needs you | n/a (UI) | States as text enums: API:112 kinds; live-region count derivable | n/a (UI) | GAP: no stable row id to keep focus and "seen" across refreshes |
| Item / decision lists | n/a (UI) | API:113, API:117 text fields | n/a (UI) | Ids present (API:151) |
| Timeline | n/a (UI) | API:115 "one scrubbed line" | n/a (UI) | API:115 `sequence` stable |
| Untrusted text everywhere | n/a | GAP: escaping control and invisible characters (DASH:287) isn't assigned to API or UI | n/a | n/a |

## Detailed findings

| id | severity | lens | location | finding | recommended fix |
|---|---|---|---|---|---|
| D1 | HIGH (structural) | IA | API:112, DASH:196–210 | `needsYou` is a field of `QueueView`, built from item and decision projections. The dashboard's needs-you is **one list across four sources** (sessions in approval/input/plan ready/auth; Sindri decisions; failures incl. doctor `fail`, unhealthy judge, stale index, weekly `attn`; PRs), sorted by state priority then oldest. Session, failure and PR rows have no item, so they can't fit this shape | Promote it to its own query `GET /needs-you` → `NeedsYouList(rows, asOf)` with a source-neutral row: `id` (stable), `kind`, `subject` (`{item\|session\|job\|repo\|pr}` ref), `title`, `reason` (scrubbed), `repo?`, `urgency` (`pulse` \| `colour` \| `calm`, per DASH:258), `since` (timestamp), `primaryAction` (command name from the registry, or `attach`, or `copy:<cli>`), `cli`. Sources plug in as projections; P4 ships only the item and decision sources, and the others are reserved |
| D2 | HIGH (structural) | IA | API:98–120 vs DASH:224–242 | Four of the five dashboard views (Agents with 13 states and nesting, Repos, Automatic work beyond services, Today) and deep links `/agent/<provider>/<id>` and `/repo/<name>` (DASH:215) have no route, event or phase. §11 Later doesn't mention them, and "Unblocks the UI: P1–P5" (API:417) reads as if the UI is fully fed | Add one row to §11 (or a short §3.5) that names the fleet/repos/automatic-work/today queries (`GET /sessions`, `/sessions/{provider}/{id}`, `/sessions/.../timeline`, `/repos`, `/work`, `/today`), who owns them (the collector service from DASH §4 inside the process), and the phase or spec that brings them. Change API:417 to say exactly which dashboard views P1–P5 feed |
| D3 | HIGH | Interaction states | API:73–75, API:58–65 | On a first start or a schema change the projector rebuilds from 0, and until it catches up every query answers from half-built views. The UI would show the **empty** state ("the same sentence the CLI prints") when the truth is "loading". `X-Sindri-After` only helps a client that knows a target sequence | Return the projector state with every view: `asOf {cursor, ledgerHead, rebuilding}` in the body (or `Sindri-View-Cursor` / `Sindri-Ledger-Head` headers), and report it in `GET /status`. The UI shows loading while `rebuilding` or while `cursor` is far behind `ledgerHead` |
| D4 | MEDIUM | Interaction states | DASH:248, API:255 | Stale state: "updated Ns ago", and after 3 failures "sindri not responding: run `sindri doctor`". With SSE instead of polling, the rules for the UI's staleness clock aren't stated (last successful query? last ping?) and views carry no time | Use `asOf` (D3) with a server `occurredAt`. State the rule: stale = no `: ping` for 2 intervals (30 s) or 3 failed re-queries, then the banner text from DASH:248 |
| D5 | MEDIUM | Interaction states / parity | DASH:246–247, API:324–326 | Error banners need "the error code and the CLI command that shows more", and the CLI prints a fix line from `docs/sindri/errors.md`. ProblemDetails carries only `title` = code and `detail` = message, so the UI has to keep its own copy of the fix lines and drift | Put the fix line in ProblemDetails as an extension (`fix`, e.g. `"run: sindri doctor"`), generated from the same `SindriErrors` registry the CLI reads |
| D6 | MEDIUM | Interaction states / parity | DASH:246 | The empty state is "the same sentence the CLI prints", but nothing in the API or a shared registry holds those sentences, so CLI and UI copies will drift | Either return `empty: "<sentence>"` in list views, or keep the sentences in one Features-level table that both the CLI renderer and the UI view models read, with a parity test |
| D7 | MEDIUM | Interaction states | API:112, API:113 | `age` is stored in a view. Age changes with time, so a projection can't hold it, and if it's computed at query time `sindri queue --json` can't byte-match a later `curl` (P3's signal, API:408). The UI also needs an absolute time to tick "waiting 4m" without re-querying | Use `since` (UTC timestamp) in `ItemSummary` and needs-you rows; let the clients format the age |
| D8 | MEDIUM | User flow | DASH:217–222, API:109, API:155–160 | Each needs-you row has "one primary action", and safe actions run in place. The registry lists commands globally and `ItemSummary` has a free-text "next action", so the UI can't tell which command applies to *this* row in *this* state without re-implementing state rules (forbidden by §17.27.1: "no logic of its own") | Add `actions: [{command, enabled, reason?}]` (or the single `primaryAction` from D1) to item and needs-you rows, computed by the same `Core` transition rules that guard the commands |
| D9 | MEDIUM | User flow | DASH:222, DASH:219, DASH:311–330 | "Acknowledge a failure" (a safe in-place action) and **Attach** (the primary action on agent rows) have no command. `OpenItem` (P10) covers "open in Warp" for items only | Reserve `AcknowledgeFailure` (desk) and `AttachSession` (desk, `ILauncher`, same live-never-resumed rule) in §11 with the fleet work (D2) |
| D10 | MEDIUM | IA / parity | API:109, API:274–275 | The registry's UI status is `ready \| pending:<phase>`. Protected commands are deliberately copy-only until passkeys, and that is neither "ready" nor "pending", so the UI can't render them from the registry alone, and `doctor` would wrongly list them as pending | Add a third status `cli-only:<reason>` (e.g. `cli-only:typed-confirmation`) and have the registry test accept it only for `protected` |
| D11 | MEDIUM | IA | API:112–113 | `ItemState` and the queue groups (`waitingOnYou`, `withYou`, `parked`, `running`, `queued`) aren't enumerated, and the spec doesn't say which state lands in which group or how the groups are ordered. The UI design needs the vocabulary and the order, and both clients must word them the same way | List the `ItemState` values, map them to the groups, and fix the group order and in-group sort (oldest first, as DASH:196) |
| D12 | MEDIUM | Accessibility | DASH:256, DASH:268, API:112 | The pulse stops once a row is "seen", and polling (now re-query) must not move focus. Both need a stable row identity across refreshes, which `NeedsYouRow` (kind, item, title, age, command) lacks: an item can have several needs-you rows | Stable `id` on each row (D1); the UI keys focus and "seen" on it |
| D13 | MEDIUM | Interaction states | API:82–83 | `SND-API-003` (503 after 2 s) is defined, but the UI's response isn't: retry, show stale, or error banner? After a successful desk action, the user would otherwise see an error for a write that succeeded | Say that 503 `SND-API-003` after an accepted command means "accepted, view catching up": the UI keeps the optimistic "done" toast and retries once on the next event |
| D14 | LOW | Accessibility / safety | DASH:287–288, API:115, API:245 | Text is "scrubbed" (secrets), but nothing says who escapes control and invisible characters (the shared sanitizer) | State that the API returns scrubbed raw text and the UI's sanitizer escapes it (or the reverse), and add an OSC/ANSI payload to the endpoint tests |
| D15 | LOW | Interaction states | API:255 | A 17th stream gets 429. A user with the app plus `status --follow` plus a few CLI followers is fine, but the UI's response to 429 (fall back to re-querying on a timer?) isn't stated | One line: on `SND-API-004` the client falls back to a 5 s re-query (DASH:108's old poll) |

### Information architecture (2/5)
- Evidence: `QueueView` "groups … and `needsYou` (`IReadOnlyList<NeedsYouRow>`: kind, item, title, age, the CLI command)" (API:112). The dashboard's needs-you holds "Sessions in `approval`, `input`…", "Sindri decisions", "Failures", "PRs" (DASH:196–210), most with no item.
- Evidence: "**Unblocks the UI:** P1 (registry), P2 (views), P3 (queue and items), P4 (decisions, needs-you), P5 (live stream)" (API:417). The Agents, Repos, Automatic work and Today views (DASH:224–242) appear nowhere in §3 or §11.
- Strong: one registry drives both clients' action lists (API:155–160), which matches §17.27.4.
- Recommended changes: D1 (`GET /needs-you` with a source-neutral row); D2 (name and own the fleet/repo/work/today queries, and correct API:417); D10, D11.

### Interaction states (3/5)
- Evidence: rebuild "on start when `views.db` is missing or its schema version differs" (API:74–75), with no matching state in any view response.
- Evidence: errors map to ProblemDetails `title` = code, `detail` = message; "The CLI prints `SND-… message` and the fix line from `docs/sindri/errors.md`" (API:324–326). The fix line never reaches the UI.
- Strong: conflicts and decided-elsewhere (API:148–150), version mismatch copy (API:347–348), `reset` instead of silent drops (API:252–254), job states (API:119).
- Recommended changes: D3 (`asOf`/rebuilding), D5 (fix line in ProblemDetails), D6 (shared empty sentences), D7 (`since` not `age`), D4/D13 (stale and 503 behaviour).

### User flow completeness (3/5)
- Evidence: decision flow is complete end to end: request becomes decision (API:181–185), options and recommended option (API:117), first wins and second gets 409 (API:149–150), the session is refused with the command to hand over (API:212).
- Evidence: the dashboard's primary actions "Agent rows: **Attach**" and "acknowledge a failure" (DASH:219–222) have no command; `OpenItem` covers items only (API:143).
- Evidence: the per-row action is free text ("next action", API:113), so the UI would have to work out applicability itself, against §17.27.1.
- Recommended changes: D8 (structured per-row actions from `Core` rules), D9 (reserve Attach and Acknowledge).

### Accessibility (4/5)
- Evidence: states are enums returned as text (API:112–119), which meets "state is always written out in words" (DASH:266).
- Evidence: `NeedsYouRow` has no id (API:112), but DASH:256 and DASH:268 need row identity to stop the pulse and keep focus.
- Recommended changes: D12 (stable row id), D14 (assign control-character escaping).

### Brand/voice consistency (N/A: missing .impeccable.md)
- Not scored. Note for the UI design: user-facing strings in this spec ("ENG-142 changed since you looked; re-read it", "Sindri isn't running; ask the human to run `sindri`", "Sindri and the app are different versions") are plain and consistent, and D5/D6 would keep them in one place.

## Verdict
Information architecture is rated 2 because of two structural HIGH findings: needs-you is modelled as an item-queue
sub-field that can't hold the dashboard's session, failure and PR rows (D1), and four of the dashboard's five views
have no API or explicit deferral (D2). Interaction states and user flow are rated 3 because of the missing
freshness/rebuilding signal (D3). The fixes are bounded: a `GET /needs-you` query with a source-neutral row, one
scoping row for the fleet queries, and `asOf` on views. None of them touch CQRS, v0 auth or the webhook cut.
verdict: BLOCKED
