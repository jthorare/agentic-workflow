# Consolidated Plan Review (pass 3 of 3, final) — sindri-workflow-engine

**Plan:** `docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md` (revision 3)
**Reviewed:** 2026-10-10
**Lenses:** product · arch · design · devex · security (`--slim`)

## Normalized findings

| id | severity | lens | finding | fix in revision 4 |
|---|---|---|---|---|
| G1 | HIGH | arch N1 | Answering a park resumes a frame whose trigger was already consumed, so nothing re-runs | §5.8 resume actions, stored per suspended frame |
| G2 | HIGH | arch N2 | The approval check on every `Commit` blocks `cancel` and the other exits | Exempt triggers in §5.3 |
| G3 | HIGH | arch N3, L2 | Outbox ids collide inside one entry; UUIDv5 needs SHA-1 (CA5350) | UUIDv8 over SHA-256 of the trigger key and effect index; a separate stable effect identity |
| G4 | HIGH | arch N4, product M2 | `eventHold` expires child rows aimed at a suspended frame; `running`'s timeout is shorter than desk work | Rows aimed at a live frame never expire; `running` timeout 14d |
| G5 | HIGH | arch N5, devex 1 | The Prover is an agent session without a TTY, so it can't run human-class signal steps | Signal steps are marked agent or owner; the owner runs a terminal runbook under `script`, and the Prover checks the transcript and the ledger |
| G6 | HIGH | devex 2, arch N9 | Nonce, key and actor ordering contradict each other; a replayed refused command reports success | `Commit` order: actor, key (replays the recorded result), nonce, fence, approval, version |
| G7 | HIGH | product 1 | Revoke and drift recovery dead-ends; a new attempt's claim reads as `claimExists` | Re-approving the same hash for drift, then `--resume`; claim identity keyed by item |
| G8 | HIGH | design G1, G2 | A `timeout` park and a dispatch-refused `spec-conflict` park have no way out | §3.8 rows with `--resume` |
| G9 | MEDIUM | sec N-2 | The challenge doesn't bind the previewed subject | Bind the subject hash and version |
| G10 | MEDIUM | sec N-3 | `RCP-CAP-005` ignores a gate's `with:` and evidence's `at`/`expect` | Compare them |
| G11 | MEDIUM | sec N-4 | Recipe command names can collide with engine verbs | `RCP-TRIG-002`; a `workflow command` verb |
| G12 | MEDIUM | sec N-1, design G5, devex | The harness markers are unverified, and the refusal text misleads | A probe-pinned list plus a parent-process check; refusals name the marker |
| G13 | MEDIUM | arch N6, sec N-5 | Frame stack depth and pop-by-id; revoke over two frames | Stack rules in §5.1 |
| G14 | MEDIUM | arch N8 | A dispatch refusal has no terminal outbox state; engine notifications are dropped after a revoke | Kernel `MarkRefused` (owned by P6); engine-owned commands exempt |
| G15 | MEDIUM | arch N10, N11, N12 | A forwarded challenge; `retry` identity; the `dev-notify` shape | Forwarded proof; `retry` keeps its entry; `dev-notify` waits on `notify.delivered` |
| G16 | MEDIUM | devex (OutputSpec, P1 child resolution, dev profiles, route and repo shape, P2 needing actors, `/new-slice`, P3 wave) | Assorted | Fixed inline |
| G17 | MEDIUM | design G3, G4, G6; product M3 | Layout 1, escalation texts, nonce and challenge naming; `step.checks` still a desk task | Fixed inline; checks runtime answered |
| G18 | LOW | all remaining | Arch L1–L8, sec L-1/L-2, design G7/G8, product L4–L6, devex LOWs | Fixed inline, or answered in §13 |

## Lens status
| Lens | File | Verdict |
|---|---|---|
| Product | product-review-r3.md | NEEDS_WORK |
| Architecture | arch-review-r3.md | NEEDS_WORK |
| Design | design-review-r3.md | NEEDS_WORK |
| Devex | devex-review-r3.md | NEEDS_WORK |
| Security | security-review-r3.md | PASS |

## Overall verdict
There are no CRITICAL findings. HIGH findings G1–G8 make the verdict NEEDS_WORK. This was the last allowed pass:
revision 4 fixes G1–G17 in the doc and answers G18 in §13, with no fourth review.

verdict: NEEDS_WORK
