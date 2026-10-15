# Plan Design Review: sindri-api-surface (pass 4, slim)

**Plan:** `docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md`, final edit `0617aae` (cited as `API:<line>`)
**Checked against:** pass 3 (`pass3/design-review.md`), §13 "Pass 3 → final edit" (API:940–966), and S1's contract `docs/csharp/sindri.md` on `feat/sindri-s1-app-shell` (`S1:<line>`)
**Reviewed:** 2026-10-10

Owner decisions are not reopened: CQRS, v0 with no tokens, webhooks cut, and dashboard views beyond v0 scoped to a later collector spec. This pass reports only pass-3 findings that are still open and new issues from the final edit.

## Inputs available
| Input | Present? |
|---|---|
| plan (single-file spec) | ✓ |
| design-brief.md | ✗ (the dashboard spec stands in) |
| product.md | ✗ |
| .impeccable.md | ✗ |
| design-tokens.json | ✗ (doesn't matter for an API spec) |

Brand/voice is N/A because `.impeccable.md` is missing.

## Pass-3 findings: status

| id | Status | Evidence |
|---|---|---|
| R1 (MEDIUM) `service.run-failed` key collides across epochs | Resolved | It's replaced by `service.ran` for every run, keyed `service.ran:<name>:<epoch>:<slot>` (API:370), the same shape as S1's `service.heartbeat:<epoch>:<slot>` (S1:102). P2 signal: fail once, then succeed (API:981) |
| R2 (MEDIUM) profile row stuck on revert, repeat drift, re-approval | Resolved | `profile.checked` keyed by the previous check (API:371), appended whenever the (approved, live) pair changes, so revert and drift-back both write (API:754). Clearing is "next `drifted: false`" (API:458). Re-approval works because S1 already keys `profile.approved:<hash>:<previous>` (S1:101), so "S1 needs no change" holds. Revert is covered by P2 and P4 signals (API:981, 983) |
| R3 (LOW) no `primaryAction` per kind | Resolved, with one new gap | Spelled out per kind (API:448). But the pre-P6 `failure` action names a verb that doesn't exist yet: see F1 |
| R4 (LOW) CLI exit code on rebuilding/stalled | Resolved | Exit 1, versus 0 for empty (API:469–470). The field is still called `empty`; that was optional and isn't re-raised |
| R5 (LOW) `GetProfile` fields come before their source | Resolved | `profile-watch` is a hosted service in P2 (API:754, 981) |
| R6 (LOW) `CloseItem` leaves a live decision row | Resolved | `CloseItem` voids pending decisions, a later `Decide` gets `SND-ITEM-409`, and it's refused while an S2 instance is active (API:314–316) |

## New findings (final edit)

| id | severity | lens | location | finding | recommended fix |
|---|---|---|---|---|---|
| F1 | LOW | User flow | API:448, API:302, API:985 | Before P6, a `failure` row's `primaryAction` is `copy:sindri services run <name>`. That CLI verb only ships in P6 (API:302), and S1 has no `services` verb (S1:18–25). So from P4 to P6, the one action the row offers is a command that fails | Until P6, use `copy:sindri status` (it lists each service's last run, S1:114) or `copy:sindri doctor`. Switch to `RunService` in P6 |
| F2 | MEDIUM | User flow | API:457, API:462, API:754 | A `failure` row clears only on the service's next `service.ran` with `ok`. If the owner removes the failing service from the profile and approves the change (a natural response to a failing service), it never runs again, so the row stays forever. v0 has no acknowledge (API:462), and rebuilding views gives the same result. This is the same stuck-row class as pass-3 R2(a) | Also clear a `failure` row when a `profile.checked` or `profile.approved` shows an approved profile that no longer names the service. Add "fail, remove from the profile, approve → row clears" to the P4 needs-you clearing tests |

## Ratings
Anchors from `_shared/severity.md`.
| Dimension | Rating | Top gap |
|---|---|---|
| Information architecture | 5/5 | No open gaps |
| Interaction states | 5/5 | No open gaps; rebuilding and stalled now have exit codes |
| User flow completeness | 4/5 | F2 (MEDIUM): a removed service leaves its failure row stuck; F1 (LOW): the pre-P6 copy command doesn't exist |
| Accessibility | 5/5 | No new gaps; stable ids, escaped text and urgency as text are unchanged |
| Brand/voice consistency | N/A (missing .impeccable.md) | n/a |

## Surface × state coverage
| Surface | Loading | Error | Empty | Success |
|---|---|---|---|---|
| Needs you (v0 sources) | API:466 `rebuilding` | API:467 `stalled`; API:471 `detail` | API:465–468 | API:438–462; clearing API:455–458 (removed service: F2) |
| Queue / item list / item detail | API:466 | API:467, `SND-ITEM-409` API:314–316 | `EmptyText` API:465 | API:982; close voids decisions API:314 |
| Decision form | n/a | `SND-ITEM-409` after void API:315 | `pending=` | API:983 |
| Services (v0 part) | `asOf` | `service.ran failed` API:370 | `empty` | `service.ran ok` API:981 |
| Profile | `asOf` | invalid live profile: S1 `SND-PROFILE-002` | n/a | `profile.checked` API:371, revert clears API:981 |
| Failure row action | n/a | pre-P6 copy verb missing: F1 | n/a | `RunService` from P6 API:448 |
| CLI text, rebuilding/stalled | API:466 | API:467 | API:465 | exit 1 vs 0 API:469–470 |
| Agents, Repos, rest of Automatic work, Today | out of v0 scope, stated (collector spec, accepted) | n/a | n/a | n/a |

### Accessibility sub-table
API-side prerequisites only; rendering belongs to the UI design.

| Surface | Keyboard | Screen reader | Contrast | Focus mgmt |
|---|---|---|---|---|
| Needs you | n/a (UI) | `kind`, `title`, `reason`, `urgency` as text (API:440–446) | n/a (UI) | Stable `id`, e.g. `failure:service:heartbeat` (API:440) |
| Item / decision lists | n/a (UI) | States as text enums | n/a (UI) | Ids present |
| Timeline / agent text | n/a (UI) | Escaped plain text (API:473–475) | n/a (UI) | `sequence` stable |

## Verdict
Every pass-3 finding is resolved: R1 and R2 through `service.ran` and `profile.checked`, which match S1's keys. The final edit adds one MEDIUM (F2, a removed service's failure row never clears) and one LOW (F1, the pre-P6 copy action names a verb that doesn't exist yet). Both are small edits to the clearing table and the `primaryAction` column. User flow rates 4, and there is no HIGH or a dimension at 3 or below.
verdict: PASS
