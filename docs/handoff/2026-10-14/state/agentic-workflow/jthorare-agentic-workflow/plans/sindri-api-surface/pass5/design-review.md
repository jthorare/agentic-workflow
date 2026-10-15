# Plan Design Review: sindri-api-surface (pass 5 of 5, final, slim)

**Plan:** `docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md` at `11500b8`, the final text after pass 4 (cited as `API:<line>`)
**Checked against:** pass 4 (`pass4/design-review.md`), §13 "Pass 4 → final text" (API:981–1004), the diff `0617aae..11500b8`, and S1 on `feat/sindri-s1-app-shell` (`docs/csharp/sindri.md`, `src/Sindri/Aw.Sindri.Core/Events/ProfileApproved.cs`)
**Reviewed:** 2026-10-10

Owner decisions and accepted scoping are not reopened: CQRS, v0 with no tokens, webhooks cut, and views beyond v0 left to the collector spec. This pass reports only pass-4 findings that are still open and new issues from the pass-4 edit.

## Inputs available
| Input | Present? |
|---|---|
| plan (single-file spec) | ✓ |
| design-brief.md | ✗ (the dashboard spec stands in) |
| product.md | ✗ |
| .impeccable.md | ✗ |
| design-tokens.json | ✗ (not relevant to an API spec) |

Brand/voice is N/A because `.impeccable.md` is missing.

## Pass-4 findings: status

| id | Status | Evidence |
|---|---|---|
| F1 (LOW) The pre-P6 `failure` action names a verb that doesn't exist yet | Resolved | `failure` → `copy:sindri status` until P6, then `RunService` (API:457). `status` exists in S1 and lists each service's last run |
| F2 (MEDIUM) A removed service's `failure` row never clears | Partly resolved: see N1 | The clearing rule now reads "or a `profile.approved` whose profile no longer names the service" (API:469). The intent is right, but the event it relies on doesn't carry the data the fold needs. The P4 test I asked for (fail, remove from the profile, approve → the row clears) wasn't added: the P4 signals still cover only "its next good run clears it" (API:1021) |
| Related (architecture P4-1) A manual run never clears a failure row | Resolved, and it helps the design | `RunService` appends `service.ran` with slot `manual:<jobId>` (API:388). The clearing rule names "scheduled or manual" (API:469), and there's a P6 signal (API:1022) |

## New findings (pass-4 edit)

| id | severity | lens | location | finding | recommended fix |
|---|---|---|---|---|---|
| N1 | MEDIUM | User flow | API:469, API:198–200, S1 `ProfileApproved.cs:8` | The fix for F2 can't be built as written. Projections are pure folds over ledger events (`Apply(row?, event)`, API:198). S1's `profile.approved` payload is `(Hash, Changes)`, and `Changes` holds the key paths that changed. Neither field says which services the approved profile names, and a changed path can't tell "removed" from "edited". So the `failure` projection has no way to know that the service is gone, and the stuck row from F2 still happens. Without a test, nothing would catch this before P4 | Put the facts the fold needs into an event P2 owns. For example, `profile.checked` (or a new `profile.services`) carries the approved profile's service names, and the failure fold clears rows for services that aren't in that list. That keeps "S1 needs no change" true. Then add the P4 signal: a service fails, is removed from the profile, the profile is approved, and the row clears |
| N2 | LOW | Interaction states | API:188–189, API:91–94, API:715, API:229 | The fail-fast rule during a rebuild reuses `SND-API-004` with a second meaning ("views rebuilding"), but the error table still defines the code only as "didn't reach `X-Sindri-After` … within 2 s" (API:715). §2.1, the section that defines how view-reading commands wait, doesn't mention the rebuild case. The UI rule at API:229 handles `SND-API-004` only after an *accepted* command. A command refused because of a rebuild was never accepted, so the client needs different wording ("views are rebuilding, try again shortly", not "done, view stale") | Add the rebuild case to §2.1 and to the `SND-API-004` row at API:715. Either give it its own code or tell the cases apart in the message (or a `reason` field), so the CLI and UI can show "rebuilding: retry" and don't confuse it with a stale view after a successful write |

## Ratings
Anchors from `_shared/severity.md`.
| Dimension | Rating | Top gap |
|---|---|---|
| Information architecture | 5/5 | No open gaps |
| Interaction states | 5/5 | N2 (LOW): `SND-API-004` now has two meanings, and only one is written down |
| User flow completeness | 4/5 | N1 (MEDIUM): the removed-service clearing rule depends on data no event carries, and has no test |
| Accessibility | 5/5 | Unchanged: stable ids, escaped text, urgency as text |
| Brand/voice consistency | N/A (missing .impeccable.md) | n/a |

## Surface × state coverage
| Surface | Loading | Error | Empty | Success |
|---|---|---|---|---|
| Needs you (v0 sources) | API:205 `rebuilding` | API:208–213 `stalled`; API:471 `detail` | API:476–481 | API:450–470; clearing API:466–470 (removed service: N1) |
| Queue / item list / item detail | API:205 | `SND-ITEM-409`; repo scope `SND-ITEM-404` API:287 | `EmptyText` API:476 | P3 signals API:1020 |
| Decision form | n/a | `SND-ITEM-409` after a void; `SND-API-004` while rebuilding API:188 (N2: wording) | `pending=` | P4 signals API:1021 |
| Services (v0 part) | `asOf` | `service.ran failed` | `empty` | `service.ran ok`, scheduled or manual API:388, API:1022 |
| Profile | `asOf` | invalid live profile: S1 `SND-PROFILE-002` | n/a | `profile.checked`, revert clears it API:766 |
| Failure row action | n/a | n/a | n/a | `copy:sindri status` before P6, then `RunService` API:457 |
| CLI text for rebuilding and stalled | API:478 | API:478 | API:476 | exit 1 vs 0 API:481 |
| Agents, Repos, the rest of Automatic work, Today | out of v0 scope, stated (collector spec, accepted) | n/a | n/a | n/a |

### Accessibility sub-table
API-side prerequisites only; rendering belongs to the UI design.

| Surface | Keyboard | Screen reader | Contrast | Focus mgmt |
|---|---|---|---|---|
| Needs you | n/a (UI) | `kind`, `title`, `reason`, `urgency` as text | n/a (UI) | Stable `id`, e.g. `failure:service:heartbeat` |
| Item / decision lists | n/a (UI) | States as text enums | n/a (UI) | Ids present |
| Timeline / agent text | n/a (UI) | Escaped plain text | n/a (UI) | `sequence` stable |

## Verdict
F1 is resolved. F2's rule is now in the text, but it depends on profile contents that no ledger event carries (N1, MEDIUM), and the test that would catch this is still missing. The rebuild fail-fast rule adds a second, undocumented meaning to `SND-API-004` (N2, LOW). Both are small edits: one event field and one P4 signal, plus one error-table row. Neither is HIGH, and no dimension is at 3 or below.
verdict: PASS
