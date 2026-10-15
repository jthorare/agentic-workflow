# Product Review (pass 3, final): Sindri API surface, rev 3

_Reviewed by `/productReview` on 2026-10-10 | Mode: mvp | slim_

Plan: `docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md` (commit eca342e). I checked it against the engine spec (`2026-10-10-sindri-workflow-engine-design.md`, read only) and the S1 worktree (head a7d2ada, read only). I haven't reopened the owner decisions (CQRS, v0 with no tokens, webhooks cut, the MCP proof by subagent, the honest-mistake threat model) or the LOW items already answered in §13.

## Verdict: SHIP

All three pass-2 MEDIUM findings are fixed in rev 3 (table below). Rev 3 adds no HIGH issue. Two MEDIUM issues are left:
- **#1 is new.** A filed request doesn't record which repo it came from. So the per-repo request cap and session scoping are undefined for requests that aren't tied to an item, and P4's own signal files one of those.
- **#2 is the cross-spec part of pass-2 #1.** This spec now owns one consistent model, but the engine spec still says the opposite in the places its implementers will read.

Neither stops a v0 phase. #1 is a one-line field addition before P4 dispatches. #2 is an edit to the engine spec before engine P2, P4 or P5 dispatch. By the rule (no CRITICAL, no HIGH), the verdict is SHIP. P1–P3 and P5 can start now.

### Pass-2 resolution check

| Pass-2 | Sev | Status | Evidence |
|---|---|---|---|
| #1 §3.5's classes contradict the engine spec. (a) The takeover path couldn't be reached. (b) `answer` was `desk` even with `grant`/`accept`. (c) S1-D5 and S1-D7 each had two owners | MED | **Resolved in this spec**, with the cross-spec residue in #2 | (a) The new class `pause` is open to `session` (§4.1, §4.2), and the hook's out-of-process call is now a `session` caller by design. (b) `ApplyCommand:answer-grant` and `answer-accept` are `protected`, and plain `answer` is `desk` (§3.5, §4.1). (c) S1-D7 has one change "made by whichever phase lands first" (§3.5, C6). For S1-D5, the confirmation mechanism sits behind the class check, and the prover seeds approval through the real terminal flow, so it works with either (§3.2 notes, §12). I checked this against S1's `Verbs.Confirmation`, which prompts for the first 6 characters on stdin |
| #2 v0 items have no exit, and `item.state-changed` has no emitter | MED | **Resolved** | `CloseItem` is in P3 (desk, with `itemVersion` and `SND-ITEM-409`). §3.6 defines the item states before S2. §5.3's example is now `new → done`, which v0 actually emits |
| #3 The needs-you row for a filed request is under-specified | MED | **Resolved** | `NeedsYouRow.recordedOnly` was added. A filed request appears exactly once, as its `decision` row (§3.6). P4's signal asserts "exactly one `decision` row" |

## Lens checklist (mvp)

| Lens item | Result |
|---|---|
| Scope check | It's tight. v0 is P1–P5. `CloseItem` is the only addition, and it's the minimum the queue needs to be usable. Nothing should be cut from v0. P1 is the one phase that looks too big for "M" (#4) |
| Persona clarity | Unchanged and clear: one owner at the desk. `session`, `service` and `prover` are caller classes or actor labels, not personas |
| Timeline reality | The critical path is a serial chain: four M phases (P1 → P2 → P3 → P4), with P5 (S) running beside P2. That's about 2 focused weeks with 3 implementers and 2 builds, which is credible. The schedule risk is P1, which now carries the scaffold mode, the prove wrapper, the whole chain, both catalogs, three architecture tests and the CLI headers (#4) |
| Riskiest assumption | It's now "the engine spec adopts this spec's authorization and needs-you model". Only this spec says so, and the engine spec still says otherwise (#2). Test it the same way as before: put the two verb tables side by side and edit the engine spec before its P2 contract unit |
| Build vs. skip | Fine as revised. `pause` is a new class with exactly one user, but it's the cheapest honest way to let the hook take over without opening `desk` to sessions, so it should stay |

## Concerns

| # | Severity | Concern | Recommendation |
|---|----------|---------|----------------|
| 1 | MEDIUM | **A filed request has no repo.** `itemId` is optional on `FileRequest`, and neither the `request.filed` payload (§3.4) nor the request row (§3.1) has a repo. Session scoping (§3.1) is defined only for items. That leaves three things undefined: <ul><li>§4.2's "20 open requests per repo" (`SND-REQ-002`) for a request without an item;</li><li>whether a `session` in repo A sees repo B's item-less requests and decisions in `ListRequests` and `ListDecisions`;</li><li>what goes in the `repo?` field of the resulting needs-you row.</li></ul>P4's own signal files exactly this kind of request (`X-Sindri-Repo: demo`, no `itemId`), so the implementer will have to guess | Add `repo?` to `request.filed`, to the request row and to the derived decision. For a `session` caller, take it from `X-Sindri-Repo`; for a `human`, take it from the item, or leave it empty. Say that §3.1's repo scoping applies to requests and decisions by this field, the same way it applies to items. Add a case to P4's integration test |
| 2 | MEDIUM | **The engine spec still contradicts this spec's model, and only this spec records the fix.** The engine spec still has: <ul><li>W22 and §6.2 make `workflow takeover` a `human` verb, and keep the `human-terminal` actor and `SND-WF-007`. This spec makes takeover `pause` (open to sessions) and says to drop both;</li><li>the engine's `--needs-me` also lists idle taken-over instances and open desk tasks. §3.6's needs-you kinds (`decision`, `park`, `failure`, `profile`) cover parks and gates but have no kind for those two. So the plan to have `--needs-me` read `GET /needs-you` would quietly drop them.</li></ul>This doesn't affect v0, but an engine P4 or P5 implementer working from the engine spec will build the old model | Before engine P2 dispatches, edit the engine spec: change W22, the §6.2 actor column and the error table to point at this spec's §4, and add a link back to §3.5. In §3.6 here, either add a needs-you kind `task` (desk tasks and idle takeovers, urgency `colour`, cleared on `CompleteStep` or handback) or say that `park` covers them |
| 3 | LOW | **`CloseItem` has no rule once S2 runs.** §3.6 says an item's state comes from its engine instance once S2 runs, but `CloseItem` (desk) can still set `done` or `cancelled` on an item whose instance is running or parked | Add one line: once an item has an active instance, `CloseItem` refuses with `SND-ITEM-409` ("cancel the workflow first: `sindri workflow cancel …`"), or it maps to `ApplyCommand:cancel` |
| 4 | LOW | **P1 is bigger than M, and it's the root of the whole graph.** C7 (`--slice-only` mode, the scaffold test, the anchor restructuring) is repo tooling that doesn't depend on the CQRS types | Split C7 into its own small PR (or the first unit, landed alone) so P1's contract unit and P2/P5 aren't held up by scaffold review rounds |
| 5 | LOW | **P1's first signal depends on the order of binding and authorization.** The raw `curl` sends S1's `{hash, confirmed}` body. If engine P2's challenge (S1-D5) lands first and changes the body record, minimal-API body binding fails with a 400 before the chain's 403 | In §2.1 or the P1 signal, state that `AddCommand` authorizes before body binding (or that a binding failure from a non-human caller still answers `SND-AUTH-002`). Otherwise, tie the signal's body to whatever the request record is at P1's merge |

## Recommended Next Action

Make the #1 field edit in §3.1, §3.4 and §4.2 now (5 minutes), then start `/specToProvenPR` with P1 (C7 split out per #4). Raise #2 with the engine spec's owner as a prerequisite for engine P2's contract unit.

verdict: PASS
