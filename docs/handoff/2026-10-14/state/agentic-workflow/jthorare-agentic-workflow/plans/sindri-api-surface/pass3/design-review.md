# Plan Design Review: sindri-api-surface (pass 3, final, slim)

**Plan:** `docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md` rev 3 (cited as `API:<line>`)
**Checked against:** pass 2 (`pass2/design-review.md`), S1's contract `docs/csharp/sindri.md` on `feat/sindri-s1-app-shell` (`S1:<line>`)
**Reviewed:** 2026-10-10

These owner decisions are not reopened: CQRS, v0 with no tokens, webhooks cut, and dashboard views beyond v0 scoped to a later collector spec. This pass reports only (a) pass-2 findings that are still open and (b) new issues in rev 3.

## Inputs available
| Input | Present? |
|---|---|
| plan (single-file spec) | ✓ |
| design-brief.md | ✗ (the dashboard spec stands in) |
| product.md | ✗ |
| .impeccable.md | ✗ |
| design-tokens.json | ✗ (doesn't matter for an API spec) |

Brand/voice is N/A because `.impeccable.md` is missing.

## Pass-2 findings: status

| id | Status | Evidence |
|---|---|---|
| N1 (HIGH) needs-you sources with no events | Resolved | C8 (API:731); `service.run-failed` in P2 and `profile.drifted` in P4 (API:349, 352); the `profile-watch` service (API:926). See R1 and R2 for the fact keys |
| N2 failure rows never clear | Resolved | A clearing rule per kind (API:432–437). S1's `service.<name>` run event (S1:101) can carry "next successful run". See R2 for the profile case |
| N3 a request gives two rows | Resolved | "A filed request appears **once**, as its `decision` row" (API:430); P4 signal says "exactly one `decision` row" (API:926) |
| N4 `actions` can't name a variant | Resolved | Action ids `<Command>:<variant>`, one catalog row each (API:249, 315–316); parks use `ApplyCommand:answer` (API:374–375) |
| N5 item states before S2 | Resolved | API:407–413: `new` → `needs-decision` → back to `new`, or `done`/`cancelled`; `needs-answer` only from the engine |
| N6 CLI text for rebuilding and stalled | Resolved | `EmptyText` holds `rebuilding` and `stalled` (API:444–447); the CLI prints them (API:183–184); `empty` is on every list shape (API:243, 250–258). The CLI's exit code in these states is still unstated (R4, LOW) |
| N7 `asOf` has no clock | Resolved | `asOf.projectedAt` (API:153–155); 30 s staleness on the stream (API:209) |
| N8 urgency per kind | Resolved | Urgency column (API:432–437) |
| N9 byte-match is flaky | Resolved | `jq -S 'del(.asOf)'` on both sides (API:925) |

## New findings (rev 3)

| id | severity | lens | location | finding | recommended fix |
|---|---|---|---|---|---|
| R1 | MEDIUM | User flow | API:349, API:731, S1:101 | `service.run-failed` is keyed `service.run-failed:<name>:<slot>`, but S1 keys its own run events with the epoch, `service.heartbeat:<epoch>:<slot>`. That suggests slots restart with each process epoch. If they do, the first failure at slot N after a restart has the same key as an earlier one. The append is deduplicated and writes nothing, so the failure row never appears. That is the one signal the row exists to show | Key it like S1's run event: `service.run-failed:<name>:<epoch>:<slot>`. Or state that slots are unique across epochs, and add a projection test that restarts the process and checks the failure row |
| R2 | MEDIUM | User flow | API:352, API:437, API:731, S1:101 | The profile row can get stuck or go missing because of fact keys on both sides. (a) **Revert:** the owner edits the profile (A→B, so a row appears) and then reverts the file to A. `profile-watch` sees live = approved and appends nothing, and nobody will ever approve B. The row stays forever, and v0 has no acknowledge. (b) **Repeat drift:** `profile.drifted:<liveHash>` has no approved hash in it. After approving B and later C, a drift back to B reuses the key, writes nothing, and no row appears. (c) **Re-approval:** S1 keys `profile.approved:<hash>`, so approving a hash that was approved before writes nothing, and the clearing event never arrives | Have `profile-watch` also append `profile.drift-cleared {live}` (or a `profile.drifted` with live = approved) when the live hash returns to the approved one, and clear the row on it. Key drift as `profile.drifted:<approvedHash>:<liveHash>`. Flag S1's `profile.approved:<hash>` key as a conflict (C10): add the epoch or the command id, or clear the row by comparing hashes in the projection rather than waiting for the event. Add the A→B→A fold to the needs-you clearing tests (API:773) |
| R3 | LOW | User flow | API:427, API:432–437 | The per-kind table gives urgency and clearing, but no `primaryAction`. A `failure` row has nothing to act on in v0 (`RunService` is P6). A `profile` row's natural action, `ApproveProfile`, is `protected`, so in the UI it has to be `copy:sindri profile approve <hash>` | Add a `primaryAction` column: `decision` → `Decide`; `park` → the engine's action id; `failure` → `copy:sindri doctor` until P6, then `RunService`; `profile` → `copy:sindri profile approve <live hash>` |
| R4 | LOW | Interaction states | API:183–184, API:668–669 | The CLI prints the `rebuilding` or `stalled` sentence, but its exit code isn't given. A script reading `sindri queue` exit 0 can't tell "empty" from "rebuilding". The field is also named `empty` even when it holds the rebuilding or stalled text | Say the CLI exits 1 (attention) while `rebuilding` or `stalled`. Optionally rename the field `statusText`, or note in §3.1 that `empty` follows `asOf` |
| R5 | LOW | IA / phasing | API:251, API:924, API:926 | `GetProfile` ships in P2 with "live hash, as of the last drift check" and "whether it changed", but the drift check (`profile-watch`) only lands in P4. Between P2 and P4 those fields have no source | Move `profile-watch` into P2 next to `service.run-failed` (both are C8), or mark those `ProfileView` fields "from P4" |
| R6 | LOW | User flow | API:281, API:409–410 | `CloseItem` on an item that still has a pending decision leaves that decision's row in needs-you, pointing at a `done` item, and it can still be accepted | Either refuse `CloseItem` while a decision is pending (`SND-ITEM-409`), or have the projection close the decision's row as `withdrawn` when the item closes |

## Ratings
Anchors from `_shared/severity.md`.
| Dimension | Rating | Top gap |
|---|---|---|
| Information architecture | 5/5 | R5 (LOW): `ProfileView` fields come before their source |
| Interaction states | 5/5 | R4 (LOW): no exit code for rebuilding or stalled |
| User flow completeness | 4/5 | R1, R2 (MEDIUM): fact-key collisions can hide a failure row or leave a profile row stuck |
| Accessibility | 5/5 | No new gaps; stable ids, escaped text and urgency as text are in place |
| Brand/voice consistency | N/A (missing .impeccable.md) | n/a |

## Surface × state coverage
| Surface | Loading | Error | Empty | Success |
|---|---|---|---|---|
| Needs you (v0 sources) | API:153, 183 `rebuilding` | API:191 `stalled`; API:448 `detail` | API:444–447 | API:415–442; clearing API:432–437 (revert and repeat cases: R2) |
| Queue / item list / item detail | API:183 | API:191, API:682–683 | API:243, 252–253 | API:252–254; state path API:407–413 |
| Timeline | API:255 `asOf` | API:682 | n/a | API:255 |
| Decision form | n/a | API:683, 686–687 | API:258 `pending=` | API:283, 361–371 |
| Request (filed by a session) | n/a | API:684–685 | n/a | API:282, 370–371 `recordedOnly` |
| Services (Automatic work, v0 part) | API:250 `asOf` | `service.run-failed` API:349 (key: R1) | API:250 `empty` | API:250 |
| Profile | API:251 `asOf` | n/a | n/a | API:251 (source before P4: R5) |
| Job progress + cancel | API:261 `running` | API:261 `failed` + code | n/a | API:261, 287 |
| Protected action (copy CLI) | n/a | API:676 | n/a | API:324–325, 602–603 |
| Read-your-writes wait | API:199–200 | API:200, 680 | n/a | API:205–208 |
| Live stream / stale | n/a | API:526, 555, 688 | n/a | API:553 ping; API:155 `projectedAt`; API:209 30 s rule |
| CLI text, rebuilding/stalled | API:183–184 | API:446 | API:445 | exit code: R4 |
| Agents, Repos, rest of Automatic work, Today | out of v0 scope, stated (API:463, 751–760) | n/a | n/a | n/a |

### Accessibility sub-table
API-side prerequisites only; rendering belongs to the UI design.

| Surface | Keyboard | Screen reader | Contrast | Focus mgmt |
|---|---|---|---|---|
| Needs you | n/a (UI) | `kind`, `title`, `reason`, `urgency` as text (API:419–424) | n/a (UI) | Stable `id` (API:419) |
| Item / decision lists | n/a (UI) | States as text enums (API:397–404) | n/a (UI) | Ids present |
| Timeline / agent text | n/a (UI) | Escaped plain text (API:450–454) | n/a (UI) | `sequence` stable (API:255) |
| Pulse vs calm | n/a | Urgency per kind as text (API:432–437) | n/a (UI) | n/a |

## Verdict
Every pass-2 finding is resolved, including the HIGH (N1, through C8). What remains is two MEDIUM fact-key edge cases in the new needs-you events (R1, R2), so user flow rates 4, plus LOW items. No dimension is at 3 or below and there is no HIGH, so the spec passes. R1 and R2 are small fixes to key shapes and one clearing event, and can be folded into P2 and P4.
verdict: PASS
