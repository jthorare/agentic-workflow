# Consolidated Plan Review — 2026-10-11-sindri-app-epic

**Plan:** `plans/2026-10-11-sindri-app-epic/plan.md`, an index of PR #170: `docs/sindri/README.md` (X1–X18) and the API surface, workflow engine and context retrieval specs
**Reviewed:** 2026-10-11
**Lenses applied:** product · arch · design · devex · security

## Normalized findings

Deduped across lenses. Product called its own result BLOCKED (RETHINK) with 0 CRITICAL; its findings are mapped by severity here.

| id | severity | lens | location | finding | recommended fix |
|---|---|---|---|---|---|
| E1 | HIGH | product, arch, devex | API §1 "Fixed, from stage S1", §9 | `SND-API-004` and `SND-API-005` are reserved for new meanings, but S1 already uses both ("error without a code", "unexpected exception"). The S1 code list is stale | Renumber the API spec's new codes from `SND-API-006` up (keep 006 for tokens, so start at 007+); refresh the S1 table from `docs/csharp/sindri.md` |
| E2 | HIGH | product, arch | API P10, WF W20, §7.2 | Nobody owns launching an **agent** session for a Step and taking its result back. `ILauncher` in P10 is only the human handoff; a session can't call `CompleteStep` (`desk`); no token metering. The factory line can't run unattended | Split: P10 = human handoff (`OpenItem`). A new engine-owned "agent runtime" (session launch per Step, result report through a run-token-bound `step-report` class open to the launched session only, token metering via the provider port) is its own spec section or the next engine design, named in X15 |
| E3 | HIGH | devex, arch, product | README X17, API Phases, WF Phases, CTX stage table | Build order is stated four ways; CTX stages depend only on S1 though their routes and `/sindri/mcp` need API P1; WF's dev loop needs `sindri-prove.sh` (API P1). Stacked PRs #166/#167/#169 differ materially from the specs (routes, doc names, package placement, a ConnectAI source the owner declined) | One build-order table in the README that every spec cites: S1 → API P0–P2 → {WF P2+, CTX S-1A/S-1M} → … Each spec's dependency column points at it. X17 states the specs win over #166/#167/#169 and lists what each PR must change (not "rebase") |
| E4 | HIGH | devex, product | WF P4/P7 signals, API P3 | Engine P4 starts instances on items nothing has created yet (items come from API P3, not a dependency); signals use `T-1`/`--item ENG-482`, which don't resolve now that `ItemId` is `ITM-…` | WF P4 depends on API P3; signals create the item first (`sindri item add`) and use its `ITM-…` id. No phase links an item to its Linear record yet: give that to CTX Phase 6 (or intake) explicitly |
| E5 | HIGH | devex | API §12, WF Phases "Prover", CTX "Parallel delivery" | Three ways to act as the human in proofs: `sindri-prove.sh human`, an owner runbook under `script -q`, and `expect` with fixed ports plus `owner-check.sh`. WF's `alias sindri=…sindri-prove.sh` assumes a pass-through mode API never defines | One recipe: API §12's `sindri-prove.sh` (with a defined pass-through mode for agent-class calls), adopted by WF and CTX. Keep the owner runbook only for checks the owner must do by policy |
| E6 | HIGH | devex, security, design, arch | API §8, CTX §3 | MCP registration for Codex and Cursor is undefined: Codex only has user-level MCP config, so a fixed `X-Sindri-Repo` would leak work context into every Codex session; Cursor's `.cursor/mcp.json` is in-repo and could be committed; doctor checks only `~/.claude.json`. Claude's local scope may not reach worktree sessions (untested) | Define per provider: Claude local scope (verify worktrees), Codex through a launch-time config override (`-c mcp_servers…` in the `ILauncher` command line, never user config), Cursor through a gitignored project file or launch-time config. Doctor checks all three. Until defined, Codex/Cursor launch without Sindri MCP |
| E7 | HIGH | security, arch | CTX §3, §8.5; API §3.7 | Context reads bypass the egress switch and the repo scope: `context.mcp.enabled` gates only MCP, so a session can still read work intent through the CLI or curl; "repo-scoped" is undefined for Linear, Onyx and PRD records | `context.sessions.enabled` (renamed from `mcp.enabled`) gates every `session`-class context read on any transport. Define repo scope for context: records reachable from the profile repo's mapped products and Linear teams; else refuse with `SND-ITEM-404`-style code |
| E8 | HIGH | arch, devex, security | WF §4.2, §5.4, §6.3; API §2.5 | The engine's nonce and challenge `POST`s have no action class, catalog row or event, so `ARCH-CQRS-001` fails; defaulting them to `desk` would block a session's `takeover` (it needs a nonce) | Classify them as queries-with-side-effect carve-outs listed in `ARCH-CQRS-001` (nonce: open to any caller that may run the target command; challenge: `human` + terminal), or fold the nonce into the command and drop the route |
| E9 | HIGH | arch, product | README X18, CTX §4, `deps.yaml` | X18 as written breaks the tier rules: Judge and Context are both tier 1, so Context can't reference Judge's `IModelProvider` | Same pattern as the heavy lock: `IModelPort` in `Aw.Context.Core`, adapted in Sindri's Host over Judge's provider (Sindri declares the Judge edge). X18 names the pattern |
| E10 | HIGH | arch | CTX §8.5 classes, API P6 | `map build` and `sources sync` are `job`, but no CTX stage depends on API P6's job runner; inline they'd hold the 5 s `CommandGate` for minutes | CTX stages with jobs depend on API P6, or run as scheduled services whose manual trigger is `RunService` |
| E11 | HIGH | design, product, arch | README X2, API §4.2, CTX §12 | Owner wants the app to start scoping, but no spec owns a scoping workflow (CTX §12 defers it), and nothing describes a recipe's inputs, so the UI can't build a start form | Name the scoping recipe's owner and stage (factory step 5, on CTX). Add recipe `inputs` metadata to the catalog/`recipe show` so the UI can render a start form |
| E12 | HIGH | design, arch | API §3.6, P11 | Engine → item-state mapping is incomplete: escalated, desk tasks and waits have no item state; `parked` sits outside `waitingOnYou` though every park needs the human; `needs-answer` is both an item state and a park reason | A full mapping table in API §3.6 (every WF §3.8 state and wait → item state, queue group, needs-you row kind and primary action) |
| E13 | HIGH | design | README "How you interject", API P10 | Open vs takeover contradict: the README says opening takes the item over; P10 marks a takeover only for `attach`, once S2 runs | Decide: opening a session on an item with a live instance is a takeover (pause), and handback is offered in the app; P10 and the README say the same |
| E14 | HIGH | design | API P10, §11 | "Attach" has no data: nothing records whether a live session exists; `ItemView` has no session field; `GET /sessions` is deferred | Launched sessions are recorded (`session.launched`/`ended` events from `ILauncher`); `ItemView.session?`; attach is offered only when one exists. Collector-sourced sessions stay later |
| E15 | MEDIUM | security | API §2.1 IClassifiedCommand | `grant-tokens` isn't named by the "grant or accept is protected" rule | Name `answer-grant-tokens` explicitly as protected in API §2.1 |
| E16 | MEDIUM | security, devex, arch | CTX §8.7, `SND-CONTEXT-041` | Map review keeps its own terminal rule and code instead of the shared challenge | Map review apply uses the engine's challenge and `SND-AUTH-002`; retire `SND-CONTEXT-041` |
| E17 | MEDIUM | security | API §3.7 | `map import` and `promote` are `desk`, so the app can make unreviewed content current | Make `promote` and `import` (to current) `protected`, or import writes a draft |
| E18 | MEDIUM | security | API §8 | `sindri_*` tool text isn't fenced as untrusted, unlike `context_*` | Same trust/fence fields on `sindri_*` results |
| E19 | MEDIUM | security | API §6, §11 | With the app now a full desk client, the trusted `human` header is the whole guard | Move the per-start `human` token from §11 into the UI's first write phase |
| E20 | MEDIUM | security | S1, #166–#169 | Routes outside the chain before API P1, including `ApproveProfile` trusting `confirmed` | Covered by E3's order; no stacked PR merges before API P1 |
| E21 | MEDIUM | security | API P10 | `OpenItem` has no double-launch guard | Fact key per item and live session |
| E22 | MEDIUM | security | README X18 | Provider API keys have no pointer-only rule | Keys only as secret pointers (CTX §4's resolver), never in the profile |
| E23 | MEDIUM | devex, product | CTX §8.6 | Exit codes diverge: CTX exits 2 when not running (S1: 1) and 1 for no results (API: 0 on empty) | CTX adopts S1/API codes; keep 75 |
| E24 | MEDIUM | product | WF §6.3, P6 | No OS notification before engine P6 (`notify.desk` is the only notifier) | Accept for v0 or let the app notify until P6; state it |
| E25 | MEDIUM | arch | WF §5.2 `workflow.request` | Leftover event duplicates `FileRequest`; nothing acts on an accepted request | Drop `workflow.request`; name who acts on accepted requests (engine, a later phase) |
| E26 | MEDIUM | arch | CTX | Context commands write no ledger events, so the UI is never told to refresh | Context commands append events (map.built, map.promoted, sources.synced) |
| E27 | MEDIUM | devex, arch | API P2/C6, WF S1-D7 | The `EventFactory` change has two owners; WF says `/new-slice` doesn't fit Sindri while API P0 adds `--slice-only` for it; WF `--needs-me` needs API P11, which needs WF P4 | Name one owner (API P1); WF adopts `--slice-only`; WF P4's `--needs-me` reads `GET /needs-you` only after P11 (plain list until then) |
| E28 | MEDIUM | devex | docs | Contract docs spread over six files; `sindri mcp register` owner unclear | One index in `docs/csharp/sindri.md`; `register` ships with whoever creates `/sindri/mcp` |
| E29 | LOW | devex, arch | various | Stale README lines; `ENG-482` examples instead of UUID; BSD-only `script`; port baked into MCP registration; `mcp-servers.md` "global only" wording; `serve --headless` typo | Sweep in the next edit |

## Top tensions (cross-lens)

1. **Full desk client vs a trusted header** — design/product (X2) vs security (E19). The owner's decision puts every desk action in the app, but the `human` class rests on a header. Resolution: keep X2; bring the per-start `human` token forward to the UI's first write phase.
2. **One-click Codex/Cursor handoff vs never user-wide** — design/product (X15, E14) vs security/devex (E6). Resolution: launch-time MCP config in the `ILauncher` command line; Codex/Cursor launch without Sindri MCP until defined.
3. **Unattended factory vs sessions never decide** — product/arch (E2) vs security (the session class). Resolution: a narrow, run-token-bound `step-report` action that only the launched session for that run can use; it reports, the engine still decides through gates.
4. **Ship context early vs the API spine** — product (MCP as the growth lever) vs arch/devex (E3, E10). Resolution: CTX S-1A/S-1M wait for API P1; jobs wait for API P6 or run as services.
5. **Map review friction vs protected promotion** — product (fast map adoption) vs security (E17). Resolution: deterministic bulk accept stays; making a map `current` is `protected`.

No tension between devex and arch: their HIGH findings (E1, E3, E5, E8) agree on the fix. No tension between design and devex: both ask for one build order and the attach/launch contract (E3, E6, E14). No tension between arch and security on E8: both want the nonce/challenge classified. No tension between product and devex: both want E1 and E3. No tension between design and arch on E12: both want the full mapping table.

## Per-lens highlights

### Product
- Top wins: one permission model, one write path, one needs-you list, one MCP endpoint now agree across the specs.
- Top gaps: E2 (no unattended Step completion), E1 (code collision), E11 (scoping unowned).
- File: `plans/2026-10-11-sindri-app-epic/product-review.md`

### Architecture
- Top wins: X1–X18 removed every structural fork; remaining issues are spec edits, no redesign.
- Top gaps: E8 (nonce/challenge vs CQRS), E9 (tiers vs X18), E10 (jobs vs the gate), E2 (agent runtime).
- File: `plans/2026-10-11-sindri-app-epic/arch-review.md`

### Design
- Top wins: catalog-driven buttons, `EmptyText`, `asOf` freshness and needs-you rows give the UI a real contract.
- Top gaps: E11, E12, E13, E14, E6; nine items the UI design spec must settle.
- File: `plans/2026-10-11-sindri-app-epic/design-review.md`

### Devex
- Top wins: `sindri-prove.sh` isolation, `--slice-only` scaffolding, golden status layouts.
- Top gaps: E3, E4, E5, E1, E6; about 45 min to first success.
- File: `plans/2026-10-11-sindri-app-epic/devex-review.md`

### Security
- Top wins: sessions can't decide; `protected` stays terminal-bound; provider choice doesn't change trust.
- Top gaps: E7 (context egress and scope), E6 (registration leak), E15–E19.
- File: `plans/2026-10-11-sindri-app-epic/security-review.md`

## Recommended next actions

1. **Reconciliation batch X19+ on #170** for E1, E3–E5, E7–E10, E12, E13, E15–E17, E23, E27 — mechanical spec edits, no owner decision needed — raised by all five lenses.
2. **Decide the agent runtime (E2) and per-provider MCP registration (E6)** — the two items that need an owner call; they gate the factory line and the Codex/Cursor handoff — product, arch, security, devex.
3. **Seed the UI design spec with the lenses' "must settle" lists** (E11, E13, E14, E19 plus design-review's nine items) so the UI design agent starts from them — design, product.

## Lens status

| Lens | File | Status |
|---|---|---|
| Product | product-review.md | ✓ (own verdict BLOCKED, 0 CRITICAL) |
| Architecture | arch-review.md | ✓ |
| Design | design-review.md | ✓ |
| Devex | devex-review.md | ✓ |
| Security | security-review.md | ✓ |

## Overall verdict

No CRITICAL findings and fourteen HIGH (E1–E14), led by E1, E2, E3 and E6, so the verdict is NEEDS_WORK.
verdict: NEEDS_WORK
