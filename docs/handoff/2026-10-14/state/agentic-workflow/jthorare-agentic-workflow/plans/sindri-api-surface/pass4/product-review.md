# Product Review (pass 4): Sindri API surface, final edit

_Reviewed by `/productReview` on 2026-10-10 | Mode: mvp | slim_

Plan: `docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md` (final edit 0617aae). I checked it against the engine spec at rev 4 (f36f2c1, read only). I haven't reopened the owner decisions or the LOW items already answered in §13. This pass reports only pass-3 findings that are still open and new issues from the final edit.

## Verdict: SHIP

Four of the five pass-3 findings are resolved (#1, #3, #4, #5). #2 (the engine spec contradicts this spec's authorization and needs-you model) is still open. It now matters more, because the engine spec's rev 4 was written after this final edit and still uses the old model. Engine P2 also doesn't wait for this spec's P1. It stays MEDIUM because none of this spec's own phases fails or ships wrong behaviour; the cost is rework at the seam between the two specs. The final edit adds one new LOW. No CRITICAL or HIGH, so the verdict is SHIP.

### Pass-3 resolution check

| Pass-3 | Sev | Status | Evidence |
|---|---|---|---|
| #1 A filed request has no repo | MED | **Resolved** | `request.filed` now carries `repo?` (the item's repo, else the session's `X-Sindri-Repo`) (§3.4). §3.1 says session commands are repo-scoped, and that the request caps and the request and decision rows follow the recorded repo |
| #2 The engine spec contradicts this spec's model | MED | **Open**, see Concern 1 | §3.5 now lists the exact edit the engine spec needs, including the needs-you answer (idle takeovers and desk tasks become kind `park`). But the engine spec at rev 4 still has the old model: W22, §4.2's `human-terminal` challenge, `SND-WF-007` (line 669, and P2 and P4 signals that assert it), `takeover` as `human` (line 539), and `--needs-me` with its own Decisions and Tasks sections (line 535) |
| #3 `CloseItem` has no rule once S2 runs | LOW | **Resolved** | §3.2: refused with `SND-ITEM-409` while an instance is active, and it voids pending decisions |
| #4 P1 is bigger than M | LOW | **Resolved** | `--slice-only` ships first as P0 (wave W0) |
| #5 The binding order in P1's first signal | LOW | **Resolved in effect** | §2.1 now says outright that requests the host can't bind get 400 and never reach the chain. The P1 signal's `{hash, confirmed}` body matches S1, and the engine flags replacing `profile approve`'s body to the S1 owner instead of doing it in engine P2 (engine §S1-D5). So the signal holds unless the S1 owner changes the body first. That's acceptable |

## Lens checklist (mvp)

| Lens item | Result |
|---|---|
| Scope check | Unchanged and tight. The final edit added only what pass 3 asked for: `service:host`, `service.ran`, the `profile-watch` hosted service, `repo` on requests, and the P0 split. Nothing should be cut |
| Persona clarity | Still one owner at the desk. `service:host` is a caller label, not a persona |
| Timeline reality | Credible. Splitting out P0 takes the scaffold work off P1's critical path. The chain is still P1 → P2 → P3 → P4 (four M phases), with P5 beside P2: about 2 focused weeks with 3 implementers |
| Riskiest assumption | Still "the engine adopts this spec's authorization model", and it hasn't been tested. Engine rev 4 diverges, and its graph has no edge to this spec's P1 (Concern 1) |
| Build vs. skip | Fine. Making `profile-watch` a hosted service is the cheapest way to get drift rows that clear, and it needs no S1 change |

## Concerns

| # | Severity | Concern | Recommendation |
|---|----------|---------|----------------|
| 1 | MEDIUM | **The engine spec still has its own authorization model, and nothing orders the two builds** (pass-3 #2, still open). Engine P2's contract unit builds "the challenge shell, the actor table and `HarnessMarkers`" and starts once S1 merges. Its wave table has no edge to this spec's P1. So engine P2 can land a second caller model in parallel with this spec's P1 chain, with `SND-WF-007`, `human-terminal` and takeover limited to `human`. This spec's Phases section says the engine "needs P1: the chain, the callers and the catalogs", but neither graph enforces that. §3.5 also still refers to the engine's "rev 3", and the engine spec is now at rev 4 | <ul><li>Make the edit in §3.5 to the engine spec before engine P2's contract unit dispatches.</li><li>Or add a cross-spec gate to both wave tables: **engine P2's contract unit waits for API P1's contract unit** (the callers, the classes, `IClassifiedCommand` and the catalog rows).</li><li>Change "rev 3" in §3.5 to "rev 4".</li></ul> |
| 2 | LOW | **New in the final edit: the `park` kind contradicts itself in §3.6.** §3.5 now folds idle takeovers and desk tasks into needs-you kind `park`. But §3.6's row table says `park` comes from "Engine parks and gates (S2)" and is cleared when "the engine resumes the instance". An idle takeover clears on `handback`, a desk task clears on `CompleteStep`, and the engine's `takeoverIdle` notification is one-shot. So an engine implementer has no clearing rule for those rows | Extend §3.6's `park` row: the sources are parks, gates, idle takeovers and open desk tasks, and a row clears when its frame pops (resume, handback or `CompleteStep`). Alternatively, add the `task` kind suggested in pass 3 |
| 3 | LOW | **The `service:host` exception sits outside §4.2's table.** §2.1 allows `service:host` for `StopProcess` only, while §4.2's `desk` × `service` cell says "only the engine commands its recipe step owns". The catalog's allowed-callers column can carry the exception, but the table a reviewer will check against doesn't mention it | Add "and `service:host` for `StopProcess`" to that cell |

## Recommended Next Action

Start `/specToProvenPR` with P0, then P1. In the same step, send the engine spec's owner the §3.5 edit list together with the proposed gate (engine P2's contract unit waits for API P1's contract unit), so the two authorization models never both land.

verdict: PASS
