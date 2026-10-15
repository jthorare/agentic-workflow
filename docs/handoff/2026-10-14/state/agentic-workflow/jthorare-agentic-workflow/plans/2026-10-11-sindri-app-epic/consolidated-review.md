# Consolidated Plan Review — 2026-10-11-sindri-app-epic (pass 2)

**Plan:** `plans/2026-10-11-sindri-app-epic/plan.md`, the index of PR #170 at `1c328de9` (README X1–X34 plus the API surface, workflow engine and context retrieval specs)
**Reviewed:** 2026-10-11
**Lenses applied:** product · arch · design · devex · security
**Pass 1:** `pass1/consolidated-review.md` (E1–E29, 14 HIGH)

## Pass-1 outcome

Across the lenses, roughly a third of pass-1 findings are resolved, a third partly and a third not; none regressed.
- **Resolved:** E1 (code collision), E8 (nonce and challenge carve-outs, mostly), E10 (jobs), E15, E16, E23 (exit codes), E7 (session reads and scope, in CTX), and engine P4 waiting on API P3.
- **Partly resolved:** most of the rest. The usual reason is that an X answer reached one spec but not its sibling.
- **Not resolved:** chiefly the E29 LOW sweep, the doc map, and the UI's must-settle checklist.

## Normalized findings

| id | severity | lens | location | finding | recommended fix |
|---|---|---|---|---|---|
| P2-1 | HIGH | product, arch, design, devex | API P10, WF §6.5 | Nothing writes `session.ended`: Warp tabs aren't Sindri's children and the watcher is deferred. Attach and the double-launch guard never clear (an item can't be reopened), `ItemView.session` goes stale, a dead agent Step waits its full timeout. Both API P10 and WF P8 claim the events; the guard key "per item and session" doesn't stop a second launch | One owner (API P10) and a liveness rule: the launched command runs under a tiny `sindri session wrap` that appends `session.ended` on exit, plus a heartbeat with expiry for crashes; dedupe key per item while live; catalog rows for both events |
| P2-2 | HIGH | arch, security, devex, product | API §4.1, §6, WF §6.5 | `step-report` is undefined end to end: the "run-scoped token" reads like the public `--run 1.1` counter that `status` prints; the CLI sends `session` and the MCP endpoint drops every header but the repo; the inherited launcher writes one fixed launch file, so concurrent launches can cross; token transport, header, storage and lifetime (takeover, handback) unnamed | A separate random run secret per launch, passed by environment to the wrapped command (never argv or a shared file), sent as `X-Sindri-Run-Token`; per-launch launch-config file; valid only while the run is open and not taken over; its own error code |
| P2-3 | HIGH | arch, design, product | WF §6, API §3 | The engine has no route table: `GET /workflow/recipes`, nonce, challenge, start, answer, complete and instance reads are unrouted; instance addresses contain `/`; `GetInstance` doesn't return a Step's outcomes and outputs, so the app's forms can't be built | An engine route table in WF §6.3 (addresses URL-encoded or by instance id), request bodies, file encoding, and `GetInstance` returning the open Step's declared outcomes and outputs |
| P2-4 | HIGH | arch, design | API §3.6 | X28's state map contradicts the group table above it: `parked` maps to both `waitingOnYou` and the `parked` group; `escalated` covers only gates; `needs-answer` still has two meanings; park rows carry one primary action, so the UI can't tell text vs resume vs grant | Rewrite the group table from the map; add `answerKind` (text, resume, grant, grantTokens, accept/reject) to park rows; cover escalation by timeout and by child |
| P2-5 | HIGH | devex, arch, product | README build order; each spec's phase and agent-team tables | Still several build orders: the README table misorders API P3 vs P2 and holds API P6, P5 and context stages later than their needs; the engine, API and context agent-team graphs weren't updated (no WF P8, no API P11, no API edges in CTX); "contract unit" vs "merged" gates differ | Make the README table the only order, derived from each phase's needs; each spec's waves table is replaced by a pointer plus its in-spec graph with the cross-spec edges added |
| P2-6 | HIGH | devex, security, design, arch | API §6, §12 | The `human` token (X31) has no phase, error code or header row; D4 and §6 still say "no tokens"; `sindri-prove.sh human`/`curl` don't send it, and the parent-process check would class the wrapper as a session anyway; the prover recipe, if copied, reaches `protected` actions on the real home | API P1 builds the token (header `X-Sindri-Token`, `SND-AUTH-001`); `sindri-prove.sh` reads the token from the guarded temp home only and is excluded from the parent check by an explicit marker valid only under that home; D4 and §6 updated |
| P2-7 | HIGH | design, arch | API P10, WF §6.5 | `OpenItem` breaks the chain's own rules: several events from one command, a `pause` inside a `desk` command, and a terminal spawned inside the 5 s gate; attach still means three things (focus, takeover, typing) | `OpenItem` decides and appends one event (`item.open-requested`); the takeover is a separate `pause` command it issues; the launch runs after commit like a job; one definition of attach (focus the live session; the takeover happened when it opened) |
| P2-8 | HIGH | devex | README X17 | The stacked-PR change lists can't be carried out: #167 (+9.7k) and #166 span many one-PR phases with no split, rebuild or keep decision; missed divergences (#167 ships built-in recipes approved, #166 reads `LINEAR_API_KEY`, #169's doc and `collect` verb); #166 is already out of draft; no phase owns #169's Judge and Evidence blocks | Decide per PR: keep as a reference branch and rebuild phase by phase from the specs (recommended), or split. List the missed divergences; put #166 back in draft; give the blocks to WF P8 or a blocks phase |
| P2-9 | MEDIUM | product, security, arch, devex | CTX §3, API §8 | X22 never reached CTX §3 (Codex and Cursor "get the equivalent entry"; doctor reads only `~/.claude.json`); API §8 still says `context.mcp.enabled`; no phase owns the three-provider doctor check; worktree check deferred to API P8 though S-1M lands first | Sweep CTX §3 and API §8; the doctor check and the worktree probe move to whoever creates `/sindri/mcp` (S-1M) |
| P2-10 | MEDIUM | product, arch | WF §6.3, README X5 | The app-notifies-until-P6 rule (X32) isn't in WF §6.3 or X5; "desk runtime gives way to `ILauncher`" contradicts X21; API §3.5 still says the `EventFactory` change goes to "whichever lands first" | Sweep the stale lines |
| P2-11 | MEDIUM | arch, product, security | WF §6.5, X18 | Token metering assigned to `IModelProvider`, which makes model calls and doesn't read session usage; usage is self-reported by the session | A per-target usage reader in the launcher's provider adapter (reads the session's own usage log), not the session's report |
| P2-12 | MEDIUM | arch, product | `deps.yaml`, X18 | Sindri → Judge has no owner and pushes Sindri past fan-out 4 (its exception is spent on Context) | Name the edge's phase and a recorded exception, or route the provider port through Kernel |
| P2-13 | MEDIUM | arch, security, devex | CTX §8.7, Phase 3 | An empty-queue map build makes the map `current`, bypassing protected promote (X30); Phase 3 HTTP import contradicts API §3.7 | Builds always write a draft; promotion is always the protected command |
| P2-14 | MEDIUM | security, arch | API §4, WF §4.2 | Kind `issue` isn't in §4.1/§4.2; the nonce route has no repo scope; challenges aren't bound to their subject for profile, map import, promote or review; map review has no one-confirmation-per-batch rule | Add `issue` rows; bind every challenge to a typed subject; one challenge per review batch |
| P2-15 | MEDIUM | arch, devex, product | CTX Phase 6, API §3 | `item.linked` has no catalog row, alias projection or matching rule; `CreateItem` takes no link; Phase 5 signals use `ENG-482` before Phase 6 | Catalog row, alias projection, `CreateItem` `link?`; Phase 5 signals use UUID keys |
| P2-16 | MEDIUM | security, product | WF §6.5 | The agent runtime sends the injection plan at launch (argv, launch file), against P10's pull model; unattended runs can't work on `--headless` (Warp tabs); per-run worktree and permission mode unspecified | Launch passes only ids; the session pulls context through MCP; a headless launcher target (plain process) for the always-on box; per-run worktree |
| P2-17 | LOW | all | various | The E29 sweep: README "API P0–P10", `ENG-482` UUID examples, BSD `script -q`, `serve --headless`, W27 missing 75, port-0 registration, `mcp-servers.md` wording, `context.repoTeams` missing from the profile example, doc map, UI must-settle checklist | One sweep commit |

## Top tensions (cross-lens)

1. **Unattended factory vs headless box** — product (P2-16) vs design/arch (P2-7): Warp tabs are for humans; the always-on box has no Warp. Resolution: `ILauncher` has two kinds of target: interactive (Warp + agent) for handoff, and process for the agent runtime.
2. **Session liveness vs simplicity** — product/design (P2-1) want reliable attach state; arch/devex resist a session collector. Resolution: the `session wrap` exit hook plus a heartbeat, no collector.
3. **Prover convenience vs the protected tier** — devex (P2-6) wants owner steps scripted; security says the copied recipe reaches the real home. Resolution: the token lives only in the guarded temp home.
4. **Keep built PRs vs specs win** — devex (P2-8) vs the owner's X17. Resolution: keep the branches as references and rebuild phase by phase.

No tension between arch and security on P2-2: both want a separate run secret. No tension between design and devex on P2-3: both want the engine route table. No tension between product and design on P2-4: both want one state map. No tension between arch and devex on P2-5: both want one derived order. No tension between product and security on P2-11: both want metering read, not self-reported.

## Per-lens highlights

### Product
- Top wins: 15 of 30 pass-1 findings resolved; the authorization model and MCP decisions now hold.
- Top gaps: P2-1, the cross-spec sweep (P2-9, P2-10), metering (P2-11).
- File: `plans/2026-10-11-sindri-app-epic/product-review.md`

### Architecture
- Top wins: F1, F4, F5, F10, F15 and F16 resolved.
- Top gaps: P2-2, P2-1, P2-3, P2-4.
- File: `plans/2026-10-11-sindri-app-epic/arch-review.md`

### Design
- Top wins: the state map and recipe inputs exist; open-takes-over agrees.
- Top gaps: P2-4, P2-7, P2-6, P2-3 (forms).
- File: `plans/2026-10-11-sindri-app-epic/design-review.md`

### Devex
- Top wins: codes renumbered, `--slice-only`, exit codes aligned; ~40 min to first success.
- Top gaps: P2-5, P2-6, P2-8, P2-9.
- File: `plans/2026-10-11-sindri-app-epic/devex-review.md`

### Security
- Top wins: pass-1 H1 and H2 resolved (session reads gated and scoped; no Codex/Cursor registration).
- Top gaps: P2-2, P2-6, P2-16, P2-14.
- File: `plans/2026-10-11-sindri-app-epic/security-review.md`

## Recommended next actions

1. **Define the launch contract once (P2-1, P2-2, P2-7, P2-16)** in API P10: `session wrap`, run secret, per-launch files, two target kinds, `OpenItem` as one event — arch, security, design, product.
2. **Add the engine route table and fix the state map (P2-3, P2-4)** — what the UI design revision needs most — design, arch.
3. **One derived build order and the PR decisions (P2-5, P2-8), then the sweep (P2-6, P2-9–P2-17)** — devex, all.

## Lens status

| Lens | File | Status |
|---|---|---|
| Product | product-review.md | ✓ NEEDS_WORK |
| Architecture | arch-review.md | ✓ NEEDS_WORK |
| Design | design-review.md | ✓ NEEDS_WORK |
| Devex | devex-review.md | ✓ NEEDS_WORK |
| Security | security-review.md | ✓ NEEDS_WORK |

## Overall verdict

No CRITICAL findings and eight HIGH (P2-1 to P2-8), down from fourteen, led by P2-1 (session liveness), P2-2 (the step-report secret) and P2-3 (the engine's routes), so the verdict is NEEDS_WORK.
verdict: NEEDS_WORK
