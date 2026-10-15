# Consolidated Plan Review — sindri-factory-addendum (round 2)

**Plan:** `docs/superpowers/specs/2026-10-09-sindri-factory-addendum.md` (rev 3 + §17.20 + §17.21, commit b1a9796)
**Reviewed:** 2026-10-09
**Lenses applied:** product · arch · design · devex · security
**Decided, not reviewed:** the C# port, stacked epics, the swappable host, ring order, and the core tenet.
**Round 1:** `round1/consolidated-review.md` (C1–C17). About 15 of the 17 hold. C6 (lease) is only partly fixed, and the C14 fix (micro-packets) created a new HIGH.

## Normalized findings
| id | severity | lens | location | finding | recommended fix |
|---|---|---|---|---|---|
| D1 | HIGH | product, arch, devex, security | §17.19 micro-packets | The write scope comes from model triage, and the packet freezes with no desk step. This breaks invariant 13 and the ring-1 policy, and the §8.3 trust check isn't stated | Write scope is computed by code (from the item's linked paths, code-index neighbours of named symbols, or "unknown" → no auto-packet). Freezing is a one-tap human decision (phone allowed for ring 0, desk only for ring 1). The trust gate applies first |
| D2 | HIGH | arch | §17.19 lease, §17.13 | The shared lease isn't specified. The epoch is still per host (`ledger/db.ts`), and `hosts.active` comes from the profile (`observe.ts:189`). The lease arrives at F7, but pushes start at F0.5. `handover --back` reads as restoring an old snapshot. Desk commands on a host without the lease write to a stale ledger | Specify the lease store, its TTL and fencing token now; the lease is required from **F0.5** (one host can hold it trivially). Every outward action and every desk command checks the lease and proxies to the holder otherwise. `--back` means a new handover in the other direction, never a snapshot restore |
| D3 | HIGH | design, security | §17.12, §17.19 phone | Walkthroughs "accept comments" but sit on an origin with no endpoints. The 10-second undo conflicts with a signed decision that triggers outward actions. The passkey's RP ID and cookies are shared with walkthroughs on the same tailnet name. Nonces chosen by the phone for the offline queue let a walkthrough script mint valid decisions | Comments go through the dashboard origin (the walkthrough links there). Undo means outward commands wait 10 s in the outbox before dispatch, so undo cancels the command, not a sent action. Walkthroughs use a **different hostname** (a separate tailnet name), so they share no RP ID and no cookies. Nonces are issued by the server per item version; offline decisions store the server nonce received while online, or are refused |
| D4 | HIGH | security | §17.20 P2 | Workers have no enrollment step and no host-class matching. It's unclear where the model-proxy token lives. Phase-1 verification could run on a worker and let it forge its own pass | Workers enroll at the desk with a key per worker and a class (`work`/`personal`, resources). The model proxy and the real tokens stay on the coordinator. Workers get proxy credentials per Step. **Verification never runs on the worker that produced the work** |
| D5 | HIGH | security | §17.20 P3 | Federation ships evidence providers (code) and workflow templates (capability fields) across the work/personal line | Federated packs are **prompt text only**. Code and capability fields never cross instances; each side re-implements or re-approves them. Direction defaults to home → work only |
| D6 | HIGH | security | §17.21 API | Routing the CLI through one HTTP API removes the §8.7 interactive-terminal and typed-confirmation check that stops the human's own agent sessions from approving | Approval-tier and protected decisions keep a **terminal-bound** path: a typed confirmation from an interactive TTY, or a passkey from an enrolled device. Never an API token alone |
| D7 | MEDIUM | product, arch | §17.5, §17.13 vs §17.19 | Text that §17.19 overrides is still in the earlier sections | Edit §17.5 and §17.13 in place to match §17.19, and drop the override notes |
| D8 | MEDIUM | product | §17.16, §17.19 | F-steps have no sizes or exit bars. "F3 holds" has no number. The ring-1 table misses rows (containers on the work host, auto-start, P3, workplace connectors). Human-minutes has no comparison to the baseline. The first factory-built PR is about a quarter away | Size every F-step with exit bars. "Holds" = N packets merged with rework ≤ baseline. Add the missing ring-1 rows. Compare human-minutes with the step-0 baseline. **Run the F0.5 thin slice on one of the R2 port packets** |
| D9 | MEDIUM | arch | §17.21 outbox | The outbox can't be exactly-once against GitHub without a check before each retry. The idempotency key `(kind, item, attempt, sha)` collides for repeated events. A requeued Step needs an attempt token. Ring vs tenant contradicts P1's per-ring WIP limits | Before retrying, check whether the effect already exists (does the PR, comment or label exist?). Keys include an event sequence number. Attempt tokens are fenced. Define rings as a dimension inside a tenant |
| D10 | MEDIUM | security | §17.21 | Outbox commands aren't re-checked at dispatch. Tenancy lives only in app code, with caches shared across tenants. The audit chain isn't anchored outside its store. An owner can approve their own request. There's no authentication between instances. Still open from round 1: ntfy authentication, trusting tailnet headers on loopback, adapter-pin tier, graph retention | Re-check the gate at dispatch. Use database-level tenant filters and cache keys per tenant. Anchor the chain head externally (a signed daily commit). Two-person rule for owner self-requests where the policy says so. Mutual TLS or signed requests between instances |
| D11 | MEDIUM | design | §17.9, §17.12 | Standing workflows have no status view or pause. There's no "blocked on you" alert. Screen-reader actions and focus aren't specified for the phone. Generated walkthroughs have no semantics requirement. In the sindri.html walkthrough, `--ink-3` contrast is 4.04:1 and the state chips look like buttons but do nothing | A standing-workflow panel with pause. A blocked-on-you alert inside the notification budget. Phone accessibility notes. A semantics checklist for walkthroughs. Fix the contrast and the chips |
| D12 | MEDIUM | devex | §17.20, §17.21 | No command shows why a Step is queued. Error areas aren't seeded. There's no docs list, and evidence publish is unattended | `sindri queue why <step>`. Seed the error-catalogue areas. A docs deliverables list. Evidence publish asks first by default |

## Top tensions (cross-lens)
1. **Fewer desk stops vs a human gate** (product's speed vs D1). *Resolution:* deterministic scope plus a one-tap freeze keeps the speed and restores the gate.
2. **Phone convenience vs authority** (design D3 vs security D3 and D6). *Resolution:* a separate walkthrough hostname, server nonces, undo via outbox delay, and terminal-bound approval-tier decisions.
3. **Federation for sharing vs isolation** (the goal of P3 vs D5). *Resolution:* share prompt text only; capabilities never cross.
4. **Swappable host vs one writer** (Q3 decided vs D2). *Resolution:* the lease from F0.5, with every command proxied to the holder.
- No tension between devex and architecture: D12's queue and why commands read the same scheduler state that D9 defines.

## Per-lens highlights
- **Product (NEEDS_WORK):** 11 of 17 fully resolved. D1 and D8; the thin slice on a port packet is the biggest schedule win. File: `product-review.md`
- **Architecture (NEEDS_WORK, 3/3/4):** 15 of 17 hold. D1, D2, D9; 16 edge cases tabled. File: `arch-review.md`
- **Design (NEEDS_WORK, 4/3/3/4):** the round-1 BLOCK is cleared. D3, D11. File: `design-review.md`
- **Devex (NEEDS_WORK):** all round-1 HIGHs closed; time-to-first-success went from about 75 to 30 minutes. D1, D12, and the seam/cutover conflict (readiness B1). File: `devex-review.md`
- **Security (NEEDS_WORK):** 5 of 9 round-1 HIGHs closed, and 4 dropped to MEDIUM. New: D3, D4, D5, D6. File: `security-review.md`

## Recommended next actions
1. Fix the gates: deterministic micro-packet scope with a one-tap freeze (D1), and a specified lease from F0.5 with command proxying (D2) — every lens
2. Fix the phone and API authority model: a separate walkthrough hostname, server nonces, undo via outbox delay (D3), terminal-bound approvals (D6) — security, design
3. Constrain distribution before it's built: worker enrollment, verification never on the producing worker (D4), federation as prompt text only and home → work (D5). Then clean up D7 and D8, and run F0.5 on a port packet — security, product

## Lens status
| Lens | File | Status |
|---|---|---|
| Product | product-review.md | ✓ NEEDS_WORK |
| Architecture | arch-review.md | ✓ NEEDS_WORK |
| Design | design-review.md | ✓ NEEDS_WORK |
| Devex | devex-review.md | ✓ NEEDS_WORK |
| Security | security-review.md | ✓ NEEDS_WORK |

## Overall verdict
There are no CRITICAL findings and six HIGH ones (D1–D6), so the rule gives NEEDS_WORK.
verdict: NEEDS_WORK
