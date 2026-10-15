# Consolidated Plan Review (pass 2 of 3) — sindri-workflow-engine

**Plan:** `docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md` (revision 2)
**Reviewed:** 2026-10-10
**Lenses applied:** product · arch · design · devex · security (`--slim`)

## Normalized findings

| id | severity | lens | finding | recommended fix |
|---|---|---|---|---|
| E1 | HIGH | arch R1 | Popping the resume stack and the `entry` counter conflict (inbox rows expire, run tokens go stale, children are recreated, rounds are counted, deadline ids are reused) | Frames: push and pop suspend and resume `{entry, run, child, deadlines}`; deadline ids carry an arm sequence |
| E2 | HIGH | sec H-1, arch R2 | Actor class derivation is undefined; the server can't see the TTY; a session can complete its own Steps; grant and accept rest on a client claim | A derivation table; harness markers mean `session`; terminal-class commands need a server challenge; MCP is stamped `session` |
| E3 | HIGH | devex, arch R6 | P2 needs P3's evals; P4 and P5 need P2's approvals; P4's nonce re-send and P6's revoke order are unrunnable | Evals ship in P1; P4 depends on P2; `--nonce`; a fixed signal order with a dispatch delay |
| E4 | HIGH | devex, arch R15 | `workflow.item` fails its own validator (child outcomes, `item.routed` facts) | Fix the recipe and the event descriptor |
| E5 | HIGH | devex, arch R7 | Reserved migration ranges are impossible; S1 appends the outbox migration as count + 1, so the next migration breaks ledgers | Sequential numbers, renumbered at rebase; pin the outbox migration at 3 (owned S1 change) |
| E6 | HIGH | devex | Profile keys the rules read (effect allowlist, mode) don't exist in S1's closed schema | Owned profile additions (`workflow.effects` in P6, `workflow.route` in P7); `validate` reports `skipped` until then |
| E7 | HIGH | devex | Hotspot rules stop units from building (PublicAPI `RS0016`, the single `SindriErrors` class, wiring) | Units append to hotspots; one partial errors file per phase; the integrator resolves unions |
| E8 | HIGH | devex, arch R13 | Exit codes and error format don't match S1 | Checker verbs (`validate`, `eval`) use 0/1/2 like `aw-packets`; every other verb uses S1's `Output.Error` (1 not running, 2 error; `CODE message (fix: …)`) |
| E9 | HIGH | design N1 | `escalated` is also entered by timeouts and children, with no text and no defined `--accept` | One row per way in |
| E10 | HIGH | design N2 | A `budget` park can't be answered usefully | `--grant-tokens`, terminal-only and capped |
| E11 | HIGH | product 1, arch R10 | Desk runtime: timeouts sized for agents park the human's task; desk notifications crowd out alerts; reports with missing outputs use up rounds; `retry` makes run tokens stale; Start ordering is unstated | Desk floor timeout with `then: park`; non-interrupting digest notifications; reports validated synchronously; Start runs after the commit |
| E12 | MEDIUM | arch R3 | Outbox ids are Guids; duplicate ids conflict on enqueue | Name-based UUIDv5 ids plus a `workflow_commands` map; a `retry` gets a new attempt number |
| E13 | MEDIUM | arch R4 | Refusals, shifts, idle notices, expiries and child starts have no event or version bump | Every row change goes through `Commit` with an event and a version bump |
| E14 | MEDIUM | arch R5 | Forwarded commands lose their actor; accept and reject aren't forwarded; a taken-over child can't be cancelled | Forwarding keeps the actor and challenge; accept and reject forward; cancel works in every state |
| E15 | MEDIUM | arch R8, sec M-D | A revoke doesn't park running instances; a dispatch refusal doesn't reach the instance | A revoke enqueues inbox triggers; a dispatch refusal returns as an event |
| E16 | MEDIUM | arch R9 | The nonce model is undefined | Random, stored, short-lived, consumed in the commit |
| E17 | MEDIUM | sec M-A, design N11 | `--accept` scope (later gates, evidence, stale head) and preview | Override only the escalated gate; status shows what accept fires |
| E18 | MEDIUM | sec M-B | `complete` isn't bound to a run; can set undeclared facts; isn't labeled | `--run` required; declared outputs only; `via: desk` |
| E19 | MEDIUM | sec M-C | CAP-005/006 compare by name | Compare by path; loops by state; a missing budget counts as larger |
| E20 | MEDIUM | sec M-E, M-F | Scrubbing is a field list; the architecture test covers only approve; session requests are undefined | Redact the whole payload in the writer; the test covers every terminal route; a `workflow.request` event |
| E21 | MEDIUM | design N3–N8 | Answer text destination, layouts for every state, write-verb output, eval and challenge codes, revoke preview, child address | Specified in §6 |
| E22 | MEDIUM | product 2–6 | `ILauncher` unowned, desk fallback weakens fail-closed, pin drift recovery, eval bootstrap, bugfix mode B | `deskCapable`, `recipe list --stale`, `eval --scaffold`, written answers |
| E23 | MEDIUM | devex | `/new-slice` claim false; stacked phases touching S1; start version; claim repo; dev-loop profile | Corrected |
| E24 | LOW | all | R11, R12, R14, R16; sec L-a–L-e; design N10–N15; product L7–L9; devex LOW | Fixed inline, or answered in §13 |

## Top tensions
1. **Desk ergonomics versus fail-closed** (product E11/E22 ∥ arch W5). Resolution: only Steps whose descriptor declares `deskCapable` fall back to the desk; any other Step without a runtime still parks with `SND-WF-010`.
2. **Human completion versus session safety** (product E11 ∥ security E2/E18). Resolution: `complete` refuses `session`, is bound to `--run`, and its outcome is labeled `via: desk`.
3. **No tension between arch and devex** on E3, E5 and E8: both found the same facts on the S1 branch.

## Lens status
| Lens | File | Status |
|---|---|---|
| Product | product-review-r2.md | ✓ |
| Architecture | arch-review-r2.md | ✓ |
| Design | design-review-r2.md | ✓ |
| Devex | devex-review-r2.md | ✓ |
| Security | security-review-r2.md | ✓ |

## Overall verdict
There are no CRITICAL findings. HIGH findings E1–E11 make the verdict NEEDS_WORK.

verdict: NEEDS_WORK
