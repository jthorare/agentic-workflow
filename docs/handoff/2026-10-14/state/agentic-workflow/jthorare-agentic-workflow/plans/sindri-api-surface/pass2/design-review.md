# Plan Design Review: sindri-api-surface (pass 2, slim)

**Plan:** `docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md` rev 2 (cited as `API:<line>`)
**Checked against:** pass 1 (`pass1/design-review.md`), the dashboard spec (`DASH:<line>`), S1's contract `docs/csharp/sindri.md` on `feat/sindri-s1-app-shell` (`S1:<line>`)
**Reviewed:** 2026-10-10

These owner decisions are not reopened: CQRS, v0 with no tokens, webhooks cut, and dashboard views beyond v0 scoped to a later collector spec. This pass reports only (a) unresolved pass-1 findings and (b) new issues in rev 2.

## Inputs available
| Input | Present? |
|---|---|
| plan (single-file spec) | ✓ |
| design-brief.md | ✗ (the dashboard spec stands in) |
| product.md | ✗ |
| .impeccable.md | ✗ |
| design-tokens.json | ✗ (doesn't matter for an API spec) |

Brand/voice is N/A because `.impeccable.md` is missing.

## Pass-1 findings: status

| id | Status | Evidence |
|---|---|---|
| D1 needs-you as a queue field | Resolved | `GET /needs-you`, a source-neutral `NeedsYouRow` (API:216, 344–365) |
| D2 dashboard views not scoped | Resolved (accepted answer) | "Dashboard views fed by v0" and "Fed later" (API:378–385); collector queries named in §11 (API:652–660); Phases say what v0 gives the app (API:759) |
| D3 freshness / rebuilding | Resolved | `asOf {cursor, ledgerHead, rebuilding, stalled}` (API:129–131, 150–162) |
| D4 stale clock and banner rule | Partly resolved, see N7 | API:131 says `asOf` is the "updated Ns ago" clock, but `asOf` holds no time |
| D5 fix line in errors | Resolved | `detail` carries S1's `(fix: …)` (API:371, 579) |
| D6 shared empty sentences | Resolved, see N6 | `EmptyText` in `Core`, `empty` on list views (API:367–371) |
| D7 `age` vs `since` | Resolved | API:133, 213, 355 |
| D8 per-row actions | Resolved, see N4 | `actions` (API:213–214), `primaryAction` (API:356), `ItemActions.For(state)` (API:672) |
| D9 Attach / Acknowledge | Resolved (reserved) | API:658–659 |
| D10 `cli-only` status | Resolved | API:209, 269–270, 282 |
| D11 states → groups | Resolved, see N5 | API:330–342 |
| D12 stable row id | Resolved | API:349 |
| D13 503 after an accepted command | Resolved | API:177–178 |
| D14 who escapes control characters | Resolved | Escaped at projection write; the UI renders plain text (API:373–376); stream test (API:680) |
| D15 429 fallback | Resolved | API:469 |

## New findings (rev 2)

| id | severity | lens | location | finding | recommended fix |
|---|---|---|---|---|---|
| N1 | HIGH | User flow / IA | API:361–362 vs S1:101, S1:126–127, API:626–636 | Two of the three non-decision v0 needs-you sources have no ledger event to fold. **Failed service runs** cite "`service.failed`, from S1", but S1 has no such event: "A failed run is logged (`SND-SCHED-001`) and the next slot still runs", and a service's run event is `service.<name>`. **A profile that changed since approval** is a file on disk compared once a second; S1 records only `profile.approved`. The needs-you projection (and `ListServices`' "last run (… outcome, code)", API:210) can't produce these rows or outcomes from the ledger, and §10 lists no conflict for it. So the UI's Needs-you and Automatic-work views would never show a failure or a profile change in v0 | Add conflict C8: the scheduler appends `service.run-failed {service, slot, code}` (key `service.run-failed:<name>:<slot>`) and `profile.drifted {approved, live}` (key `profile.drifted:<liveHash>`) when its once-a-second re-read sees a new live hash. Assign both to a phase (P2 for the service outcome, P4 for the profile) and add them to the §3.4 table. Alternatively, say these two sources are computed at query time outside the projection, and give `asOf` a meaning for them |
| N2 | MEDIUM | User flow | API:359–362, API:659 | A v0 failure row has no way to leave needs-you. Acknowledge is deferred to Later (API:659), and nothing says when a `failure:service:<name>` row clears. Without a rule it pulses forever after one bad heartbeat, and the profile row's end condition isn't stated either | State the clearing rule per v0 source: a service failure row clears at that service's next successful run; a profile row clears on `profile.approved` of the live hash; a decision or request row clears on `decision.recorded`. Give the failure row a `primaryAction` (e.g. `RunService`, or `copy:sindri doctor`) |
| N3 | MEDIUM | IA | API:350, API:304–309, API:769 | `kind` has both `request` and `decision`, but a filed request *is* a pending `accept-request` decision (API:306). Read literally, one request gives two rows (`request:REQ-…` and `decision:DEC-…`), so the count, badge and pulse double up. The P4 signal ("appears … with `primaryAction` `Decide`") suggests a single row | Say that a pending request shows as one row: `kind: request`, `id: decision:<DEC>`, subject = the decision, `primaryAction: Decide`. Keep `kind: decision` for decisions the engine raises |
| N4 | MEDIUM | User flow | API:213, API:252–253, API:276, API:319 | `actions` and `primaryAction` are **command names**, but once S2 runs the item actions the human needs (answer, handback, cancel, and takeover) are all one command, `ApplyCommand`. A list holding `ApplyCommand` can't tell the UI which button to draw or which form to open | Make the catalog's unit a verb, not a type: one catalog row per `ApplyCommand` variant (`ApplyCommand:handback`, …) with its own CLI form and UI status. Or give `actions` entries `{command, variant?}`. Note it in §3.5 so the engine spec registers rows that way |
| N5 | MEDIUM | User flow | API:341–342, API:304–313 | Before S2, the item state path is incomplete. "An item stays `new` until a decision or request points at it": then it's `needs-decision`, but nothing says where it goes after `Decide` (accepted with `recordedOnly`, or declined), and no v0 source produces `needs-answer`. In v0 no command appends `item.state-changed`, so the queue group an item lands in after a decision is undefined | Add the pre-S2 fold: `request.filed` with an item → `needs-decision`; `decision.recorded` → back to `new` (declined) or `queued` (accepted, recorded only). Mark `needs-answer` and `taken-over` "from S2" in the §3.6 table |
| N6 | MEDIUM | Interaction states / parity | API:153–154, API:162, API:367–371 | The UI shows loading while `rebuilding` and an error banner while `stalled`, but the CLI's text for those states isn't defined. `sindri queue` during a rebuild would print the `EmptyText` sentence ("Nothing is waiting on you"), which is the exact wrong answer the UI avoids. The UI's loading and stalled text also has no shared source. Separately, `empty` doesn't appear in any §3.1 response shape | Put the rebuilding and stalled sentences in the same `EmptyText` table (e.g. "Views are rebuilding (cursor 812 of 4211); try again shortly." and "Views stopped at 4211: run `sindri views rebuild`."), have the CLI print them instead of the empty sentence and exit 1, and add `empty` to the `QueueView`, `ItemList`, `NeedsYouList`, `RequestList` and `DecisionList` shapes |
| N7 | LOW | Interaction states | API:129–131, DASH:248 | "It also gives the UI's 'updated Ns ago' its clock," but `asOf` holds sequences and flags, no time. The 3-failures banner rule under SSE (failed re-queries? missed pings?) is still unstated | Add `asOf.at` (when the cursor was last advanced, from `TimeProvider`), or say the UI uses its receive time. One line: stale = no `: ping` for 30 s or 3 failed re-queries, then DASH:248's banner |
| N8 | LOW | Interaction states | API:354, API:365, DASH:258 | `urgency` and the "by urgency" order are defined only through the dashboard's session states (`approval`/`input` pulse, `failed` colour). The v0 kinds (`decision`, `request`, `failure`, `profile`) have no stated urgency, so the sort and the pulse aren't specified for anything v0 produces | Map the v0 kinds: decision and request → `pulse`; failure → `colour`; profile → `colour` (or `calm`). Put it in `Core` next to the needs-you ordering test (API:672) |
| N9 | LOW | Parity | API:768, API:617 | The P3 signal says `curl …/queue` byte-matches `$SINDRI queue --json`, but both bodies now carry `asOf.cursor`/`ledgerHead`, and `heartbeat` appends between the two calls. The check can fail for a reason unrelated to the feature (P2's signal already says "apart from `asOf`") | Pause the heartbeat service in the prover profile, or compare with `asOf` removed, as P2 does |

## Ratings
Anchors from `_shared/severity.md`.
| Dimension | Rating | Top gap |
|---|---|---|
| Information architecture | 4/5 | N3: a request can give two needs-you rows; N4: `ApplyCommand` is too coarse to be an action |
| Interaction states | 4/5 | N6: the CLI has no text for rebuilding or stalled, so it prints the empty sentence |
| User flow completeness | 3/5 | N1 (HIGH): the failure and profile needs-you sources have no ledger event; N2: failure rows never clear |
| Accessibility | 5/5 | Stable ids and escaped text are in place; urgency mapping (N8) is LOW |
| Brand/voice consistency | N/A (missing .impeccable.md) | n/a |

## Surface × state coverage
| Surface | Loading | Error | Empty | Success |
|---|---|---|---|---|
| Needs you (v0 sources) | API:153 `asOf.rebuilding` | API:162 `stalled`; API:371 `detail` | API:367–370 `empty` | API:344–365; failure and profile rows: GAP (N1); clearing: GAP (N2) |
| Queue / item list / item detail | API:153 | API:162, API:591 | API:370 (field missing from shapes, N6) | API:212–214; post-decision state: GAP (N5) |
| Timeline | API:215 `asOf` | API:591 | n/a | API:215 |
| Decision form | n/a | API:592, API:596 | API:218 `pending=` | API:218, API:237 |
| Request (filed by a session) | n/a | API:593–594 | n/a | API:236, API:311–312 `recordedOnly` |
| Services (Automatic work, v0 part) | API:210 `asOf` | failed-run outcome: GAP (N1) | n/a | API:210 |
| Job progress + cancel | API:221 `running` | API:221 `failed` + code | n/a | API:221, API:241 |
| Protected action (copy CLI) | n/a | API:413–415 | n/a | API:269–270, API:512–513 |
| Read-your-writes wait | API:172–173 | API:174, API:177–178 | n/a | API:176–178 |
| Live stream / stale | n/a | API:439, API:463–464, API:469 | n/a | API:465 ping; "updated Ns ago" clock: GAP (N7) |
| CLI text, rebuilding/stalled | GAP (N6) | GAP (N6) | API:370 | API:615–617 |
| Agents, Repos, rest of Automatic work, Today | out of v0 scope, stated (API:385, API:652–660) | n/a | n/a | n/a |

### Accessibility sub-table
API-side prerequisites only; rendering belongs to the UI design.

| Surface | Keyboard | Screen reader | Contrast | Focus mgmt |
|---|---|---|---|---|
| Needs you | n/a (UI) | `kind`, `title`, `reason` as text (API:350–352) | n/a (UI) | Stable `id` (API:349) |
| Item / decision lists | n/a (UI) | States as text enums (API:332–339) | n/a (UI) | Ids present |
| Timeline / agent text | n/a (UI) | Escaped plain text (API:373–376) | n/a (UI) | `sequence` stable (API:215) |
| Pulse vs calm | n/a | Urgency text present; v0 kind mapping: LOW (N8) | n/a (UI) | n/a |

## Verdict
Every pass-1 finding is resolved or partly resolved. v0's scope is now stated clearly (API:378–385, API:759), and the later views are named in §11. One new HIGH remains: two of the needs-you sources v0 promises (failed service runs, a changed profile) have no ledger event in S1 to fold (N1), so user flow rates 3. The fix is one §10 conflict row and two events.
verdict: NEEDS_WORK
