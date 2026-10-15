# Product Review (pass 5, final): Sindri API surface

_Reviewed by `/productReview` on 2026-10-10 | Mode: mvp | slim_

Plan: `docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md` at `11500b8` (the pass-4 edit). I checked it against the engine spec at rev 4 (`f36f2c1`), which I only read. This pass reports only pass-4 findings that are still open and new issues from the pass-4 edit. It doesn't reopen owner decisions or the LOWs already answered in §13.

## Verdict: SHIP

Pass-4 #1 (the competing caller model, MEDIUM) and #3 (`service:host` missing from §4.2) are resolved. Pass-4 #2 (how `park` rows clear) is still open, but it was LOW and still is. The pass-4 edit introduced one new LOW: some text was left behind when `--slice-only` moved to P0 and the generator gained the `A.cs` and `IdsJsonContext` writes. No phase fails or ships wrong behaviour because of any of this. All three concerns are LOW, so the verdict is SHIP.

### Pass-4 resolution check

| Pass-4 | Sev | Status | Evidence |
|---|---|---|---|
| #1 The engine spec can build a second caller model, and nothing orders the builds | MED | **Resolved on this spec's side** | The wave table has a Gate row: the engine's P2 contract unit waits for API P1's contract unit, unless the engine's §6.2 and §6.3 edit lands first. §3.5 now says "rev 4, `f36f2c1`". What's left is the engine spec's own wave table. That's an action for the engine spec's owner, see Concern 2 |
| #2 `park` rows have no clearing rule for idle takeovers and desk tasks | LOW | **Open**, see Concern 1 | §3.6 still gives the `park` row's source as "Engine parks and gates (S2)" and its clearing rule as "The engine resumes the instance". §13's pass-4 list doesn't mention it |
| #3 `service:host` sits outside §4.2 | LOW | **Resolved** | The `desk` × `service` cell now reads "…and `service:host` → `StopProcess` (§2.1)". §2.1 also says only `ServeCommand` sets it, and a command with no caller context is refused |

## Lens checklist (mvp)

| Lens item | Result |
|---|---|
| Scope check | Still tight. The pass-4 edit only clarified existing behaviour: a manual `service.ran`, worktree repo resolution, failing fast during a rebuild, and the gate. It added no feature. Nothing to cut |
| Persona clarity | Still one owner at the desk. `service:host` and `service:<name>` are caller labels, not personas |
| Timeline reality | Unchanged and credible. P0 (S), then a chain of four M phases (P1 to P4), with P5 and P6 beside them. About 2 focused weeks with 3 implementers. The gate holds back only the engine, not this spec |
| Riskiest assumption | Still "the engine adopts this spec's authorization model". It's now gated from this side, but the engine spec at rev 4 hasn't changed (W22, §4.2's `human-terminal` challenge, `SND-WF-007`, `--needs-me`), so the gate holds only if the engine's lead reads this spec's wave table (Concern 2) |
| Build vs. skip | Fine. Giving `RunService` the slot `manual:<jobId>`, and taking the repo from `--git-common-dir`, both reuse what's already there and need no new parts |

## Concerns

| # | Severity | Concern | Recommendation |
|---|----------|---------|----------------|
| 1 | LOW | **The `park` row still has one source and one clearing rule** (pass-4 #2, not addressed). §3.5 folds idle takeovers and open desk tasks into kind `park`, but §3.6's table says `park` comes from "Engine parks and gates (S2)" and clears when "the engine resumes the instance". An idle takeover clears on `handback`, and a desk task clears on `CompleteStep`. Without a rule for those, an engine implementer could leave rows stuck. This is engine-era work (S2), so no phase in this spec breaks | Extend §3.6's `park` row. Sources: parks, gates, idle takeovers and open desk tasks. A row clears on resume, `handback` or `CompleteStep`, whichever closes its frame |
| 2 | LOW | **Only one of the two wave tables has the gate.** This spec says "Both wave tables must show this gate", but the engine spec at rev 4 has not changed: its wave 2 starts P2 once "S1 merged", with no edge to API P1. If a separate `/specToProvenPR` lead runs the engine spec, it never reads this spec's table | Put this in the handoff to the engine spec's owner: add the same Gate row to the engine's wave table, or make the §6.2 and §6.3 edit first. No change to this spec |
| 3 | LOW | **New in the pass-4 edit: text left behind after the P0 split.** <ul><li>C7 (§10) still says "**P1** adds a `--slice-only` mode", but the Phases table and "Units inside a phase" give it to P0.</li><li>P0's signal says "only slice files and its own PublicAPI lines change", but C7 and §12 now have the generator also write the slice's `A.cs` factory method and its `IdsJsonContext` entry. A prover who takes the signal literally would fail a correct P0.</li><li>§9's `SND-API-004` row covers only the `X-Sindri-After` and head timeouts, not the new "views rebuilding" fail-fast from §2.3.</li></ul> | Three small text fixes: <ul><li>C7: change "P1 adds" to "P0 adds", leaving the anchor restructuring in P1.</li><li>P0 signal: "only slice files plus its own PublicAPI, `A.cs` and `IdsJsonContext` lines change".</li><li>§9 `SND-API-004`: add "or the views are rebuilding".</li></ul> |

## Recommended Next Action

Make the three text fixes in Concerns 1 and 3, all in the spec, with no review pass. Then start `/specToProvenPR` with P0 and P1. Send the engine spec's owner the §3.5 edit list and the gate row (Concern 2) in the same handoff.

verdict: PASS
