# Consolidated Plan Review — sindri-factory-addendum

**Plan:** `docs/superpowers/specs/2026-10-09-sindri-factory-addendum.md` (revision 2, commit 027920e; §17.18 scouting and invariant 16 landed after the lenses started and aren't reviewed)
**Reviewed:** 2026-10-09
**Lenses applied:** product · arch · design · devex · security

## Normalized findings
| id | severity | lens | location | finding | recommended fix |
|---|---|---|---|---|---|
| C1 | HIGH | arch, security | §17.1 inv. 13, §17.5, §17.7 | The model pass can add lenses, and each lens brings its own command, MCP and skill allowlists. So a model proposal widens the sandbox. The compiled policy has no ceiling from the approved profile allowlist | Rewrite invariant 13 to list the fields a model may never propose. Make the session policy the intersection of the profile allowlist (ceiling) and the lens allowlists. A lens a model adds can only review, never grant capabilities. Every re-freeze is a desk action |
| C2 | HIGH | arch, security | §17.6, §17.7, §7.4 | Lens allowlists, workflow templates and security rules aren't protected. Self-adopt, overlay-before-merge and imported community packs (whose upstream upgrades auto-adopt) can all change them | Make lens capability fields, workflow templates and security rules approval-tier. Imported packs never self-adopt; prompt text only after review. A packet can't override them downward |
| C3 | HIGH | arch | §17.5 | The enforcement table overstates what's enforceable. Only network, budgets and writes (at bundle export) are real boundaries. Command allowlists checked on Bash strings, and read scoping, are patterns. The per-session settings overlay can be edited by the agent | Split the table into pattern and boundary columns. Put the overlay in read-only `/sindri`, and exempt the gate, Stop gate and safety hooks from it. Map dangerous capabilities to egress and credentials, not command strings |
| C4 | HIGH | arch | §17.4, §17.7 | Held-out acceptance examples leak to the implementer if they're committed to the repo | Keep holdouts in sindri-private state, mounted only into evaluator Steps, with a canary probe |
| C5 | HIGH | arch | §17.5, §17.6 | Rule checks such as eslint run repo-controlled code inside the phase-1 verifier, which must run none | Move repo-config rule checks to the phase-2 verifier |
| C6 | HIGH | arch, security, design | §17.13 | The fencing epoch is stored per host (`ledger/db.ts`), so a laptop that wakes with a copied ledger acts as active. Handover copies the ledger and approvals with no integrity check. Hosts aren't tagged work or personal. A sleeping laptop can't checkpoint. Passkeys bind to one hostname | A shared lease or remote epoch, checked before every push, merge and tracker write. Signed, verified handover. Tag hosts `work` or `personal` like accounts, and ring-1 data stays on work hosts. A stable tailnet name for the dashboard (or one passkey per host). Drop the "laptop closes" promise, or make the always-on box hold the lease |
| C7 | HIGH | arch, security | §17.3 | The context graph isn't separable (it depends on scrub, sanitize, the index, the registry and the Linear-only backtest). `Connector` duplicates `Source`. No store or single writer is defined. Consolidation ("dreaming") can turn untrusted chat and telemetry text into facts Sindri treats as its own. The graph's evals could grade the graph | Injected ports; a `Source`→`Connector` shim; one store per profile with sindri as the only writer; consolidation output keeps the lowest trust of its inputs; evals stay in sindri |
| C8 | HIGH | security, design, arch | §17.12, §17.9 | The phone path has no enrollment, revocation or binding to the item's state, so decisions can be replayed or applied after the item changes. Tier raises and approval-tier proposals appear on the phone, contradicting §17.9, §8.7 and Plan 6. Walkthrough HTML shares an origin with the decision actions. Plan 6's 127.0.0.1-and-Host-check binding conflicts with `tailscale serve` | Define enrollment at the desk and revocation. Each decision signs (item id, item version, nonce). Remove tier raises and approval-tier items from the phone. Serve walkthroughs from a separate origin with no ambient authority. Amend Plan 6's bind and Host rules for the tailnet name |
| C9 | HIGH | security | §17.10 | The epic auto-merge gate trusts agent-controlled inputs: threads the babysitter resolved count as resolved. It isn't bound to the verified SHA, it needs an undraft §8.1 forbids, and ring-1 policy isn't enforced | Count only threads resolved by a human or a trusted bot; merge with `--match-head-commit` on the verified SHA; allowlist the epic undraft explicitly; `epicMerge` is off by profile for ring 1 unless the workplace policy allows it |
| C10 | HIGH | product | §17.16, §17.17 Q5 | Ring 1's human-gated policy may block auto-merge, phone approvals, the home box and `act`-tier workflows, so "ring 2 after F3 holds on ring 1" may never trigger | A ring-1 table marking each F-step and standing workflow as allowed, allowed with a setting, or blocked. Redefine "holds" over the allowed rows only |
| C11 | HIGH | design | §17.12 | The phone surfaces have almost no states: swipe meaning, undo, offline, passkey failure, item already decided at the CLI, walkthrough comments, desk queue. Swipe has no button alternative (WCAG 2.5.1). Videos have no captions. Three queues don't say which item kinds each holds | A state table per surface, buttons alongside swipe, captions on recordings, and a map from item kind to queue |
| C12 | HIGH | devex | §17.4, §17.5, §17.7, §17.14 | Nothing creates or amends a packet; `--json` failures aren't defined; `SND-SCOPE` collides with `sindri scope`'s codes; there's no command to show a compiled session spec or explain a denial; the plan doesn't say how a skill detects sindri mode, or how existing state files stay compatible; the skill lint and pressure-scenario format aren't defined | `sindri packet init --from-scope` and `amend`; a new error area (for example `SND-SANDBOX`); `sindri session spec <step>` and `sindri why-denied <id>`; a `SINDRI_MODE` env var plus a state-path shim; a skill lint spec |
| C13 | MEDIUM | product | §17.16 | No thin end-to-end slice and no effort sizes. The starting catalogs are broad for one builder | Add F0.5: one packet, one task, one PR through `/specToProvenPR` in sindri mode. Size each step. Start with about 4 lenses, 4 evidence kinds and 3 standing workflows |
| C14 | MEDIUM | product | §17.4 | Every build needs a frozen packet, even XS bug tickets | An auto-frozen micro-packet for S-and-under items |
| C15 | MEDIUM | product | §17.2, §10.1 | Total human effort has no ceiling or kill condition, and the notification budget isn't revised for the inbox and standing workflows | A weekly human-minutes metric with a ceiling; a notification budget per surface |
| C16 | MEDIUM | devex | §17.6, §17.7 | Rule, lens and workflow files have no version field and no JSON Schema export. Lens `commands` don't bind to repo commands. No command explains why a rule was selected. "Lens", "factory" and "profile" already have other meanings in this repo | `schemaVersion` plus JSON Schema export; command aliases from the repo profile; `sindri rules explain`; a glossary |
| C17 | MEDIUM | devex | §17.13 | Host handover has no dry-run or rollback | `--dry-run` and `handover --back` |

## Top tensions (cross-lens)
1. **Phone convenience vs authority** — design (C11) and product want more done from the phone, while security and arch (C8) want fewer decision kinds there and stricter binding. *Resolution:* keep the phone to binary, non-protected kinds signed against the item version, and make it great at those. Everything else gets a "queue for desk" button.
2. **Self-evolution vs a protected sandbox** — the core tenet (invariant 16, scouting) wants every catalog to evolve, while arch and security (C1, C2) want lens capabilities to be approval-tier. *Resolution:* split each artifact into prompt text (self-adopt) and capability fields (approval-tier). Evolution still proposes capability changes; the human approves them.
3. **Swappable host vs one-host fencing** — the human's Q3 decision vs arch and security (C6). *Resolution:* keep it swappable, but put the lease on the always-on box (or another shared lease) and fence every outward action on it. The laptop factory works offline only in a degraded mode that can't push or merge.
4. **Ring-1-first vs factory autonomy** — product's C10 against C9 and the F-plan. *Resolution:* the ring-1 policy table decides what "holds" means. Ring-1 runs assist mode with epic auto-merge off unless the workplace allows it.
5. **Broad catalogs vs a thin first slice** — the addendum's catalogs vs product's C13. *Resolution:* the catalogs stay as the target; F0.5 builds the minimum, and scouting grows the rest.
- No tension between devex and security: devex's explain and debug commands (C12) help auditability and can be read-only.

## Per-lens highlights
### Product
- Top wins: the packet-miss feedback loop; lean session start; context-graph parts added only on a backtest gain; the phone surface starts as a web app
- Top gaps: ring-1 policy fit (C10); no thin slice (C13); micro-packets (C14); no ceiling on human effort (C15)
- File: `plans/sindri-factory-addendum/product-review.md`
### Architecture
- Top wins: an injection plan frozen once per deliverable; a deterministic first pass before the model pass; backtest arms
- Top gaps: model-widened sandbox (C1); enforceability (C3); holdout leak (C4); fencing (C6); context-graph boundaries (C7)
- File: `plans/sindri-factory-addendum/arch-review.md`
### Design
- Top wins: splitting decisions across phone, walkthrough, desk and live session by how much context each needs
- Top gaps: phone states (C11); phone reachability and passkey binding (C8, C6); queue mapping (C11)
- File: `plans/sindri-factory-addendum/design-review.md`
### Devex
- Top wins: deterministic validation of packets; reusing existing skills as Step implementations
- Top gaps: packet creation and amendment, denial debugging, sindri-mode detection, skill lint (C12)
- File: `plans/sindri-factory-addendum/devex-review.md`
### Security
- Top wins: the §8 container, proxy and verifier boundary still holds; tailnet-only and no funnel
- Top gaps: no sandbox ceiling (C1); supply chain (C2); phone path (C8); auto-merge gate (C9); context laundering (C7); handover and host tagging (C6)
- File: `plans/sindri-factory-addendum/security-review.md`

## Recommended next actions
1. Add a capability ceiling and split each artifact's protection tier: the session policy is the profile allowlist intersected with the lenses; capability fields, workflow templates and security rules are approval-tier; a model may never propose them. Fixes C1, C2 and tension 2 — arch, security
2. Rewrite §17.12 and §17.13: phone decisions signed against the item version with enrollment and revocation; no tier or approval-tier items on the phone; walkthroughs on a separate origin; Plan 6 binding amended; a shared lease with host tags and signed handover. Fixes C6, C8, C11 — security, design, arch
3. Add F0.5 (one packet → one PR in sindri mode), a ring-1 policy table, micro-packets, the devex commands (`packet init/amend`, `session spec`, `why-denied`, `SINDRI_MODE`), and C3–C5, C7, C9 as targeted edits. Fixes C3–C5, C7, C9, C10, C12–C14 — product, devex, arch, security

## Lens status
| Lens | File | Status |
|---|---|---|
| Product | product-review.md | ✓ NEEDS_WORK |
| Architecture | arch-review.md | ✓ NEEDS_WORK |
| Design | design-review.md | ✓ BLOCKED (its own rule: dimensions at 2/5; under severity.md, 2/5 = structural HIGH) |
| Devex | devex-review.md | ✓ NEEDS_WORK |
| Security | security-review.md | ✓ NEEDS_WORK (0 CRITICAL, 9 HIGH) |

## Overall verdict
No CRITICAL findings and twelve HIGH (C1–C12), so the rule gives NEEDS_WORK. The design lens's BLOCKED comes from 2/5 dimension scores, which severity.md anchors as structural HIGH.
verdict: NEEDS_WORK
