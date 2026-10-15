# Product Review (pass 2): Sindri API surface, rev 2

_Reviewed by `/productReview` on 2026-10-10 | Mode: mvp | slim_

Plan: `docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md` (commit 78d3c41). Cross-checked against the engine spec (`2026-10-10-sindri-workflow-engine-design.md`, rev 2) and the S1 worktree (head a7d2ada, read only). The owner decisions (CQRS, v0 with no tokens, webhooks cut, MCP proven by a subagent, the honest-mistake threat model) and the pass-1 LOW items answered in §13 C-28 are not reopened.

## Verdict: SHIP

All five pass-1 HIGH and MEDIUM findings are resolved (table below). Rev 2 introduces no HIGH issue. Three MEDIUM issues remain (#1–#3), and each is a text edit to the spec, not a redesign. #1 is what is left of pass-1 #1: the ownership split is right, but the action classes and callers that §3.5 assigns to engine commands contradict the engine spec in three places. #2 is new: removing rev 1's P7 left v0 items with no way out of the queue. #3 is new: the needs-you row for a filed request doesn't match P4's signal. By the rule (no CRITICAL, no HIGH), the verdict is SHIP. Fix #1–#3 inline before `/specToProvenPR` dispatches P3 and P4. P1, P2 and P5 can start now.

### Pass-1 resolution check

| Pass-1 | Sev | Status | Evidence |
|---|---|---|---|
| #1 P7/P9 duplicate the engine's park, takeover and handback | HIGH | **Resolved** (one follow-up, #1 below) | §3.2 notes and §3.5: the engine owns instance commands; P7 and P9 are gone; there's no item hold, park or handback; `sindri handback` is the engine's alias, and the engine's §6.2 agrees |
| #2 Scope ahead of consumers; `ILauncher` in the wrong place | MED | **Resolved** | v0 is P1–P5 (Phases). P10 is marked as feeding the engine, and engine W20's desk runtime means P10's late position doesn't block the engine |
| #3 Stale S1 baseline | MED | **Resolved** | §3.1 lists `GET /ping`, C1 is closed, and `profile validate` is class `local`. All match S1's `EndpointTable.cs` (`/status`, `/ping`, `/stop`, `/profile/approve`, `/events`) |
| #4 P4 decisions are inert before S2 | MED | **Partly resolved** (#3 below) | `recordedOnly` is on the decision row with honest copy (§3.4), but the needs-you row, the main UI surface, doesn't carry it |
| #5 Auto-start contradicts itself for sessions | MED | **Resolved** | §7: no verb run inside an agent session starts the process, which is the same rule as `sindri mcp`. It's in P1's scope |
| #8 (LOW) ledger read | LOW | Resolved | `ReadLedger`, `GET /ledger` and `sindri ledger` in P5 |

## Lens checklist (mvp)

| Lens item | Result |
|---|---|
| Scope check | It is now tight: v0 is P1–P5, P7 and P9 are cut, and the idempotency journal moved to Later. One gap that rev 2 opened (#2): v0 can create items but can't finish or cancel them, so the queue only ever grows |
| Persona clarity | Unchanged and clear: one owner at the desk. `session` and `service` are caller classes, not personas |
| Timeline reality | v0 is a serial chain of four M-sized PRs (P1 → P2 → P3 → P4), with P5 (S) beside P2. That's about 2 focused weeks with the 3-implementer, 2-build team. It's tight but credible. P6, P8 and P10 are correctly after the milestone |
| Riskiest assumption | It's now "the two specs agree on each engine command's action class and caller". §3.5 asserts classes that the engine spec contradicts (#1). Test it the same way as before: put the two verb tables side by side before P1 freezes the authorization table and catalog rows |
| Build vs. skip | It's fine as revised. The stream cap and the human slot reservation stayed rather than being deferred, and §13 C-28 answers that, so it isn't re-raised |

## Concerns

| # | Severity | Concern | Recommendation |
|---|----------|---------|----------------|
| 1 | MEDIUM | **§3.5's classes for engine commands contradict the engine spec and the API's own rules.** (a) §3.5 says takeover is "set by the hook's turn classification … through the `service` caller", but §4.2 says `service` "never arrives over HTTP", and the hook is an out-of-process bash script. Engine §6.2 makes `workflow takeover` a `human` verb, so this path can't exist as written. (b) §3.5 classes "answer" as `desk`, but engine §6.2 requires `human-terminal` for `answer --grant` and `--accept`, which is this spec's `protected` class. As `desk`, the UI could grant budget without a terminal. (c) Two specs change the same S1 code differently. Engine S1-D5 (engine P2) replaces `profile approve`'s `confirmed` body flag with a server-issued challenge, while this spec's §3.2 note and C2 keep `{hash, confirmed}`. Engine S1-D7 (engine P4) and this spec's C6 (P1) both rewrite `EventFactory`. §3.5 reconciles only S1-D6 | In §3.5: make takeover `desk`, run by the human (the hook's classification stays an engine-internal event, or say exactly how it reaches the process). Split answer into `desk` (plain text) and `protected` (`--grant`, `--accept`). Add S1-D5 and S1-D7 to the ownership table with one owner each. P1 is the natural owner of the `EventFactory` change, and the approve challenge needs a decision: either P1 adopts it, or engine P2 owns it and C2 says so. Ask the engine spec to link back to §3.5 |
| 2 | MEDIUM | **v0 items have no exit, and `item.state-changed` has no emitter.** With P7 gone, P1–P5 have `CreateItem` and no command that changes an item's state. Nothing reaches `done` or `cancelled` before S2, so every `sindri item add` sits in `queued` permanently. Yet P3 still adds `item.state-changed` with a version fact key, `SND-ITEM-409 "changed since you looked"`, and §5.3's example event (`new → parked` by `human:cli`), none of which any v0 command produces. The "UI renders live state" milestone renders a queue the owner can't clear | Either add one `desk` slice to P3, `CloseItem {done\|cancelled}`, that appends `item.state-changed` (it's the only item verb not owned by an engine instance, so it doesn't reopen pass-1 #1), or state in §3.6 that v0 items are closed only once S2 runs, and move `item.state-changed` and the item half of `SND-ITEM-409` to "emitted by the engine's item projection". Fix §5.3's example to use an event v0 actually emits |
| 3 | MEDIUM | **The needs-you row for a filed request is under-specified.** P4's signal asserts that the filed request appears in `GET /needs-you` "with `primaryAction` `Decide` and `recordedOnly`", but the `NeedsYouRow` table (§3.6) has no `recordedOnly` field. The v0 sources list both "pending decisions and requests", and one request derives both a request row and a decision row (§3.4), so it isn't said whether the owner sees one row or two. This is the leftover of pass-1 #4, and the prover will have to guess | Add `recordedOnly` (or a generic `note`) to `NeedsYouRow`, and say that a filed request produces exactly one needs-you row (`kind: decision`, `subject` = the decision, id `decision:DEC-…`), so `kind: request` is reserved for later sources or dropped |

verdict: PASS
