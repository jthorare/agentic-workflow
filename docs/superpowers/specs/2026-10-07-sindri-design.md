# Sindri — design

Status: draft for review, revision 7 · 2026-10-08
Revision 7:
- adds the self-evolution loop with self-adopt and approval tiers (§7.4)
- moves the scoping harness to the front (§7.5, rollout step 1)
- adds dogfooding (§7.6) and prior art and reuse (§16)
- fixes round-6 W3–W7
- turns W1, W2 and W8 into spike criteria (§13.1)
- amendment (audit feedback): labels come from outcomes or a model adjudicator (invariant 9); regex patterns are floor counts until calibrated (§2, §13 step 0); resumed and forked transcript copies are deduped; "wrong path" splits into design and process, with a step-0 decision rule for direction checks

*Amended 2026-10-11 by the factory addendum §17.29 (decision Q7): Sindri is one application the human starts
(`sindri`), not a set of launchd-ticked commands. It is built new in C# with three engines (a control plane, the
§17.28 workflow engine and a context retrieval engine), and agent work stays in Claude Code sessions in the human's
terminal. This document is kept as design history; where it describes the tick, launchd jobs or the TS package
layout, §17.29 wins. Overview: `docs/sindri/README.md`.*

Renamed from the working name "conductor" (it collides with Conductor.build) to **Sindri**, the dwarf smith who forged Draupnir, the ring that multiplies itself.

Revision 6 history:
Revision 6 addresses `/autoplan` round 5 (V1–V9, M1–M8): two-phase verification with a separate
reproduce sandbox, a write shim for bugFixOrchestrator, clone from the mirror, hardened spool ingestion,
subscription auth, a container debug path, image onboarding, effort sizing and a spike fallback.

Revision 5 history:
Revision 5 addresses `/autoplan` round 4: sessions run in containers (§8.2) with sindri-mediated
push (§8.5); the environment is an adapter; a dashboard and menu-bar badge (§10.5); calibration fixes.

Revision 4 history:
Revision 4 addresses `/autoplan` round 3 (T1–T6, M1–M10): hook semantics grounded in the Claude Code
docs, a sandboxed session user, enforced shape checks with defined outcomes, and an offline index.
Revision 2 addressed `/autoplan` round 1 (21 HIGH). Revision 3 addresses round 2 (7 HIGH, R1–R7, and M1–M15,
L1–L2 in `plans/sindri/consolidated-review.md`). It also adds hook-enforced checks (§5.3) and code-shape
checks with a code index (§6.2).

## 1. Why

A transcript audit of one month of work (406 sessions, 2,911 human turns; method in the appendix) found the
human's effort goes mostly to the least judgment-heavy parts of agent work:

| Where human turns go | Share |
|---|---|
| Shipping direction (draft PR → review loop → ready → watch reviewers → fix CI/conflicts → "push") | ~20% |
| Product decisions answering agent questions | ~20% |
| Keeping orchestrators on track (handoff prompts, "read this digest", "we crashed, pick up", role corrections) | ~11% |
| Research asks | ~10% |
| Defects the human found by hand | ~5.5% |
| Dispatch (pasting tickets). One turn per ticket, so turn counts undercount its real cost | ~5.5% |
| Mid-flight scope discovery | ~4% |
| QA direction | ~4% |
| Evidence and environment recipes (near-verbatim repeats) | ~3% |

The sindri is a local, always-on system that picks up work, sizes it, starts sessions with a cited
context pack, keeps them on track, ships them through a fixed recipe, and asks the human only when it has to.
It learns from its own steering so recurring corrections become rules.

## 2. Goals, metrics, kill criteria

### Goals
- Remove copy-paste dispatch (manual start in one command, then auto-start for small clear work).
- Remove retyped shipping direction (the ship recipe is data, executed by the sindri).
- Remove orchestration babysitting (cited pack injected at start and re-injected on compaction).
- Every judgment step proves it is complete before handing off.
- Evolve: recurring human steering becomes a proposed, backtested, typed rule change.
- **Scope projects at creation:** surfaces, implications and workstreams found up front, proven by
  backtest (§7.5).
- **Improve itself:** the whole toolkit repo (skills, hooks, judge, scorer, the sindri itself,
  installers, docs) plus adopted skills, prompts, recipes and thresholds are evaluated and evolved
  automatically. Human input is needed only for protected surfaces and, by default, merges (§7.4, §7.7).
- Keep the codebase simple as it grows: deterministic checks for reinvention, second-case
  generalization and size/complexity, judged only on a shortlist (§6.2).
- Generic core; workplace and repo specifics live in a separate private profile repo.

### Non-goals
- Merging. The human always merges.
- Answering product questions. The sindri batches and contextualizes them; it never answers them.
- Multi-host scheduling in v1.

### Success metrics (baselines and final targets set in rollout step 0; figures below are starting proposals)
| Metric | Source | Proposed target after 4 weeks of auto-start |
|---|---|---|
| Human turns per merged PR | transcripts + GitHub | −40% vs baseline |
| Shipping-direction turns per merged PR | adjudicator labeler calibrated in step 0 (regex patterns are floor counts; they feed a metric only when step-0 calibration marks them metric-grade) | −75% |
| Continuity turns (handoffs, "pick up", role corrections) per week | adjudicator labeler calibrated in step 0 (patterns only when metric-grade) | −50% |
| Copy-paste dispatches of auto-start-eligible items | ledger vs transcripts | 0 |
| Interrupting notifications per merged PR | ledger | ≤ 2 |
| Auto-started PRs reworked or reverted | GitHub + ledger | ≤ the rate for human-started PRs |
| Model tokens per merged PR (subscription) | proxy usage log | within `budget.perItem` |
| Human "wrong path" **design** corrections after code was written, per merged PR | adjudicator labeler calibrated in step 0 (`wrong_approach_design`, turns with `editsBefore`) | −50% (direction checks should catch these first). Applies only when the step-0 decision rule makes Approach and Drift checks a deliverable; otherwise tracked, no target |
| Human "wrong path" **process** corrections (CI vs local, which doc or tool, step order), per merged PR | adjudicator labeler (`wrong_approach_process`) | Tracked; no −50% target. Addressed by recipes, not direction checks |
| Clone count and duplication ratio on files sindri sessions touched | code index | flat or falling |
| Transcript-classifier accuracy | outcome-labeled turns and the step-0 adjudicator sample (a later human correction or rework marks a miss; no hand labels) | ≥ 0.85, reported in `shadow report` |

### Kill criteria
- **Auto-start:** if after 4 weeks human turns per merged PR is down less than 20%, or the rework rate of
  auto-started PRs exceeds that of human-started PRs **of the same size class** (minimum 20 auto-started
  PRs before this fires), auto-start is turned off (`mode: assist`). The
  sindri keeps running manual start, packs and the ship recipe.
- **Eval loop:** if fewer than half of approved rules hold their backtested gain over the next 20 items,
  proposals stop.
- **Direction checks:** per checkpoint, see §6.1 (back to shadow when `revise` doesn't reduce rework).
- **Index providers:** graph or embeddings are turned off for a repo when the eval-loop arm comparison
  shows no gain.

## 3. Invariants

1. **One sindri at a time** per host, fenced (§9.1). Across hosts, v1 runs one active host and treats
   cross-host exclusion as best-effort with duplicate-tolerant outcomes (§9.4).
2. **Nothing long-lived holds a model.** The tick is short deterministic code; model work runs in bounded
   jobs and sessions that end.
3. **No step grades itself.** Verifiers are separate; the sindri runs the worker's reproduce check
   itself (§6). **No path goes unchallenged:** approach, scoping, drift, shape and
   scope-expansion checkpoints get a direction check on a different model before more effort is
   committed (§6.1).
4. **Escalate, never guess.** Below-threshold decisions and exhausted loops notify and park.
5. **One heavy job at a time per host.** The session cap is a ceiling on sessions, not heavy jobs.
6. **Deny by default inside sindri sessions.** A sindri gate enforces the profile allowlist,
   fails closed, and is not disableable from inside a session (§8.1).
7. **Untrusted text is data, never instructions** (§8.3).
8. **No secrets in packs, ledger, logs, notifications or eval corpus** (§8.4).
9. **No human hand-labeling.** Labels come from outcomes or a model adjudicator (never the builder or another
   human by hand): merged without rework, reverted, a human
   correction in a later turn, CI results, or a model labeler run with no tools. A `proceed` written by an agent is never a label on its own (M5).
10. **Hooks enforce; agents don't opt in.** Every required check fires on a hook event, a git hook, or a
    sindri action, never because an agent chose to call a tool. MCP is for reading data only. Hooks
    enforce *patterns*. The *security boundary* is the session container, server-side protections, and
    sindri-side re-verification (§5.3).
11. **Self-evolution stays inside its tier.** Automatic adoption never touches protected surfaces, never
    edits the eval suite that judges it, is always evaluated by the stable channel, and every adoption
    can be reverted automatically (§7.4, §7.7).

## 4. v1 scope

| In v1 | Deferred (interface and contract tests exist, no built-in) |
|---|---|
| macOS, launchd, session containers (OrbStack or Docker), tmux inside containers, macOS notifier (with fallback chain §10.4), dashboard app with tray badge (Avalonia, C#; was a web dashboard plus SwiftBar badge, amended 2026-10-10) | systemd/cron schedulers, ntfy, chat-DM notifier |
| Single active host | Shared `Lease` adapter, multi-host |
| Tracker: Linear. Reviewer: GitHub | Other trackers and reviewers |
| Sources: tracker, notes dir, transcripts, memory (Prism) | Chat (Slack) as a pack source |
| Modes: `shadow`, `assist`, `auto-small` | `full` (auto-start scoping workstreams) |
| Worker wraps existing `/bugFixOrchestrator` | New native worker |
| Ship state machine for single PRs and stacks | — |
| Eval loop: proposal-only on evidence and ship recipes | Free-form rule discovery |
| Direction checks, sindri-relayed, the ledger as the record; shadow first (§6.1) | Human participating live in a thread; non-Anthropic challengers by default |
| Code index: structure, clones, dependencies, local embeddings, graphify graph; shape checks at commit (§6.2) | Cross-repo index |

Linux is a design constraint in v1 (no OS assumptions in core, per-OS boot-id, hook portability fixes,
Linux container test script) but its built-in adapters ship later (§12).

## 5. Architecture

*Amended 2026-10-11 by addendum §17.29: the tick below becomes work inside one long-running control-plane process
(§17.29.1–§17.29.2). launchd (or systemd) only keeps that process alive, and the old launchd jobs (index builds,
observe, scorer, the weekly evolve job) are background services it schedules. Deadlines are evaluated by the
workflow engine (§17.28.1, Timers), and every machine below becomes a recipe it runs. §5.1's paths describe the TS
`sindri/` reference; the C# app is built new (§17.29.4).*

```
launchd ─► sindri tick  (short, deterministic; singleton lock + fencing epoch §9.1)
             ├─ reconcile: ledger ⇄ Launcher.list() ⇄ job/session liveness (§9.2)
             ├─ Tracker.scan(scope, cursor)        → changed work items
             ├─ spawn bounded jobs (detached, own lease, own budget):
             │     triage · context pack · scoping · verifier · eval
             ├─ router (trust gate + size + ambiguity + mode)
             ├─ Launcher.start(session, pack, overlay)   (≤ cap)
             ├─ ship state machine (per change request / stack)
             └─ Notifier (taxonomy + budget §10)
```

The tick never waits on a model. It reads job results from the ledger on the next tick.

### 5.1 Core packages (in this repo)
| Path | Responsibility |
|---|---|
| `sindri/` | Tick, queue, cap, fencing, job runner, Step runner, ship state machine, ledger, CLI |
| `sindri/adapters/` | Interfaces, built-ins (§11.2), `adapterContractTests()` |
| `sindri/profile/` | Zod schema, JSON Schema export, validator, migrator, example profile, scaffolder |
| `sindri/gate/` | Sindri-mode PreToolUse gate, outward-write filter, path guard (§8) |
| `sindri/scrub/` | Secret/PHI scrubber used at ingest, pack write, ledger write, eval corpus |
| `judge/src/questions/` | New questions (size, ambiguity, pack-probe, independence, step-complete, direction-verdict), called through the `judge` CLI |
| `sindri/index/` | Code index builders, `Index` adapter, graphify adapter, shape signals (§6.2) |
| `config/hooks/` | The sindri-mode hook set in §5.3: injection, gate, checkpoints, shape checks, turn classification, Stop gate, heartbeat |

### 5.2 Storage (M7)
- The sindri owns `$AW_STATE_DIR/sindri/ledger.db` (SQLite, WAL). It is the only writer.
- Hooks never write to the DB. They append events to `$AW_STATE_DIR/sindri/spool/<session>.jsonl`, and
  the tick ingests them. The pre-commit shape hook writes one file per run, `$AW_STATE_DIR/sindri/spool/shape-<runId>.json` (temp name, then rename), and the tick ingests it and deletes the file only after the ledger transaction commits.
- Judge decisions are referenced by judge's decision id. Score, threshold and reason code are **copied**
  into the ledger row, because judge prunes decision details after 30 days (L1, L5).
- The schema lives in `planning/ERD.md` with versioned migrations. `doctor` detects schema skew (M19).
- Ledger files and the state dir are mode 0700, outside any session's working tree. Sindri sessions run
  with a gate that denies writes under `$AW_STATE_DIR/sindri/` (STRIDE tampering).

### 5.3 Enforcement model: hooks force patterns, the sandbox enforces security
**Principle:** every check that must happen is fired by a hook event or by the sindri. No check
depends on an agent choosing to call a tool. MCP servers are for **reading data**.

There are two layers, with different jobs:
- **Hooks enforce behavior patterns** on a cooperative agent: checkpoints, shape checks, pack injection,
  turn classification. This is what removes the human steering.
- **The security boundary does not depend on hooks:**
  - a dedicated OS user for sessions
  - sindri state readable only by the sindri user
  - server-side branch protection
  - sindri-side re-verification of the pushed head (§6, §8.5)

  A hook that fails open costs a missed nudge. It never costs a breach.

**Grounded hook semantics** (Claude Code docs, checked 2026-10-07; rollout step 1 confirms them with a live
probe, and the probe suite becomes a `doctor` check):

| Fact | Consequence |
|---|---|
| Only exit 2 or `permissionDecision:"deny"` blocks a tool call. Other non-zero exits, timeouts (default 600 s) and a missing command **fail open** | Every sindri hook runs through `sindri-hook`, a small static wrapper that traps all errors and timeouts (its own budget is shorter than the hook timeout) and emits an explicit deny with `SND-HOOK-9xx`. `doctor` probes every hook. |
| `continue:false` with `stopReason` ends the turn, and Claude sees the reason | Checkpoints end the turn cleanly instead of looping on denials |
| A Stop block (`decision:"block"`) is capped at 8 consecutive continuations; `stop_hook_active` marks a re-entry | Stop is a **pattern** gate only. It blocks when the Step's required artifacts are missing (at most twice per chain). Known waits (`awaiting-direction`, `awaiting-human`) are always allowed to stop. Completion is verified **sindri-side after Stop**, never inside the hook. |
| `UserPromptSubmit` can add `additionalContext` and can block | Inbox delivery and turn classification |
| `Managed-settings` hooks merge with all other levels and can't be removed below `managed` | Sindri hooks ship in the **session image's root-owned `managed settings`**. The session runs as an unprivileged container user and can't change them. The host's own Claude sessions get no sindri hooks. |
| `-p` and SDK sessions skip the trust dialog; interactive sessions need the folder trusted | The image pre-accepts trust for `/workspace/src` in the container user's config at build time |

**Identifying a sindri session:** the container *is* the session. Sindri hooks exist only in the
session image, so there's no identity check to bypass, and no `env -u` path (T2).

**Hook map:**

| Event (matcher) | Enforces | Effect on violation |
|---|---|---|
| `SessionStart` (startup) | Inject the pack's Task, Acceptance and Evidence plan; register; heartbeat | — |
| `SessionStart` (compact, resume, fork) | Re-inject Task and Acceptance plus the pack pointer | — |
| `UserPromptSubmit` | Turn classification (`answer` / `takeover` / `sindri`). On a verified delivery token (§6.1), inject the inbox. A human correction raises a drift signal, but only when the calibrated labeler or a metric-grade pattern says so (§13 step 0). | Block a forged delivery token (`SND-DIR-030`) |
| `PreToolUse` (all tools) | Sindri tool gate (§8.1) | Deny `SND-GATE-1xx` |
| `PostToolUse` (all tools) | **Edit detection by working-tree state, not by tool name:** after any tool call, the hook compares `git status --porcelain --untracked-files=all` plus content hashes against the last snapshot, so untracked files count too (M6). The first change to a non-test source file without a `proceed` verdict fires the Approach checkpoint. This catches Edit, Write, `sed -i`, `tee`, scripts and codegen. | `continue:false`, `stopReason: SND-DIR-010 awaiting direction check <id>`. The changes stay in `/workspace/src`; the verdict decides keep or revert. |
| `PreToolUse` (`Bash`) plus a git `pre-commit` hook set via the image's system git config | **Shape checks** (§6.2), as **pattern** enforcement. The git hook catches the common commit forms; `--no-verify` doesn't matter, because the sindri-side verifier re-runs the same checks on the bundle before anything is pushed (§8.5). | Commit refused with evidence and `continue:false` (M6); Shape direction check opened |
| Session export (Stop and commit hooks write `/spool/out.bundle`) | Hand-off for sindri-mediated push (§8.5) | — |
| `PostToolUse` | Heartbeat spool; drift signals; scrub tool output going back into context | — |
| `Stop` | Pattern gate as described above | Block at most twice, then allow and record `SND-HOOK-210` |
| `PreCompact` | Checkpoint Step state to the spool | — |

Session-side git hooks are a convenience for fast feedback, not a control. The authority is the
sindri's verifier container (§8.5).

**Debuggability (T4):**
- Every hook decision is appended to `$AW_STATE_DIR/sindri/hooks/<session>.jsonl` with event, matcher,
  input hash, decision, code and duration.
- `sindri hook log <item>` shows the decisions for an item.
- `sindri hook replay <log-id>` re-runs a recorded decision.
- `sindri hook test <event> <fixture.json>` runs a hook against a fixture.
- Codes separate the cause: `1xx` policy, `2xx` pattern, `9xx` crash or timeout.
- The deny text always includes the code, a one-line reason and `sindri why <item>`.
- **Kill switches:** `hooks.<name>.enabled` per hook in the profile. Changing one requires
  `profile approve` and is shown in `status` as a degraded mode. The gate and the Stop pattern gate can't be
  switched off.

**Coexistence (T1d):**
- The session image includes the existing `config/hooks/*` safety hooks. Hooks merge, and the safety
  hooks only add denials.
- The image omits `done-gate.sh` and `scope-gate.sh`, because the sindri's Stop pattern gate and tool
  gate replace them.
- On the host, nothing changes.

## 6. The Step contract

```
Step = { inputs, output schema, deterministic checks, verifier, max rounds, budget, escalation }

produce → deterministic checks → verifier → pass ── yes ─► ledger + hand off
              ▲                             │ no (reasons)
              └──── revise with reasons ────┘   rounds or budget exhausted → park + notify (needs-answer)
```

- **Generation** runs in a bounded job (a fresh `claude -p` with a tool allowlist) or a session.
- **Decisions** are judge `QuestionModule`s run through the `judge` CLI, on the chain
  `jev → agent CLI → rules`. A below-threshold result escalates.
- **Verifiers** are judge questions first. When the chain escalates, a separate refutation job runs; the
  producer never verifies itself.
- **Budgets** are measured in **tokens and turns** (subscription auth). Each Step has `maxRounds` and a
  token cap. Each item has `budget.perItem` (tokens). The host has `budget.perDay` (tokens), plus a
  circuit breaker that pauses auto-start when the proxy sees subscription rate-limit responses (C8).

| Step | Runs as | Deterministic checks | Verifier |
|---|---|---|---|
| Triage | tool-less job | Schema-valid; every in-scope item has `{size, ambiguity, missingInfo[], sources[]}`; size in the profile vocabulary (L2) | Judge re-sizes **100% of auto-start candidates** and a sample of the rest (M14) |
| Context pack | tool-less job (fetching is done by sindri code through Source adapters) | Every claim cites a source ref; required sections present (§8.3); scrubber passes; size within limit | Judge pack-probe: a fresh job answers N probe questions from the pack alone |
| Scoping | job with read-only code tools | Every surface maps to a workstream; dependency graph acyclic; independence checked against predicted file sets | Adversarial "missing surface" job until no new surface or the round cap |
| Worker | tmux session (`/bugFixOrchestrator` in v1) | **The sindri re-runs the recorded reproduce check itself** in a fresh **verifier container** from the exported bundle (§8.5): it must fail on the base and pass on the head. Flaky checks: 3 runs, majority wins, and disagreement parks the item (M2). Path guard (§8.5) and shape checks (§6.2) pass. | Judge resolution-check against the original item text |
| Ship | sindri code | Reviewer threads resolved and CI green, read from the Reviewer adapter and CI, never from agent output | — |
| Eval proposal | job | Typed rule schema; forbidden fields untouched (§7.4) | Holdout backtest (directional), then a shadow A/B win before approval (§13 step 6) |

### 6.1 Direction checks ("is this the right path?")
Step verifiers ask *is this output complete?* Direction checks ask a different question: *is this the right
thing to be doing at all?* In the audit only the human asked it: "the ticket description suggests the
fix, why are we not doing it", "that's a messy workaround, not the fix we want", "wait, why are you adding
that branch?". A direction check is a short, structured dialogue between models before more effort is
committed.

**Checkpoints:** each one is fired by a hook (§5.3) or by the sindri. None is fired by the agent.

| Checkpoint | Fired by | Question |
|---|---|---|
| Approach | `PreToolUse` on the first non-test source edit | Does this solve the item as written? Does it match the stated fix? Is there a simpler or more correct path? |
| Scoping | Sindri, before a scoping proposal goes to the human | Are these the right workstreams? Is anything here solving the wrong problem? |
| Drift | Sindri, on any drift signal: 2 failed fix rounds, the same verifier reason twice, a diff outside the predicted file set, a human correction (calibrated labeler or metric-grade pattern only), elapsed time more than 2× estimate | Keep going, change approach, or stop and ask? |
| Shape | `PreToolUse` on `git commit` when a shape signal fires (§6.2) | Reuse, generalize, or keep the new code (with a reason)? |
| Scope expansion | Sindri, when a session's diff or notes propose new items or a widened change | Is this necessary for this item, or a separate item? |

XS-clear items skip Approach unless a drift signal fires.

**Transport: the sindri relays every turn (R1, R2, R4).**
- Participants never talk to each other directly. The sindri runs each turn as a bounded job:
  - **proposer turn:** from the session's own checkpoint output, which the hook spools as `position.json`
  - **challenger turn:** a stdin/stdout job, read-only code tools, no network
- The sindri writes every turn itself, with a fixed role (`proposer | challenger | arbiter`), the check
  id, the epoch, and an HMAC keyed by a sindri secret. Each MAC covers the previous turn's MAC, so the
  turns form a chain.
- **Key custody (M3):** the key lives in the human's keychain (macOS) or a 0400 file in the
  sindri-private zone (Linux). It is never mounted into any container. It rotates on every epoch
  change, and the previous key stays valid until every check opened under it has closed (M6).
- **Position authorship (M2):** the proposer's `position.json` is captured by the PostToolUse hook from the
  session's checkpoint output, stamped with the session id and turn id, and signed by the sindri on
  ingest. The session never signs anything.
- **Leg timeouts (M2):** position 10 min, challenge 5 min, reply 10 min, verdict 2 min. A timeout escalates.
- **The ledger is the record of each dialogue,** and `sindri why` reads it. (This section first planned an MCP bridge
  mirror of each turn; the bridge was retired on 2026-10-10 without ever carrying a message, so no mirror is built.
  A later audit view reads the ledger's events, addendum §17 "Event log and bus".)
- No session can read or post to a direction-check thread.

**State machine** (durable in the ledger):
```
requested → position → challenge(n) → reply(n) → verdict → delivered
        └──────────── any state → cancelled | escalated
```
- n ≤ `direction.maxExchanges` (default 2).
- While a check is open, the session is in the known-wait state `awaiting-direction`. The reaper never
  treats it as a stall (§9.2).

**Delivery into the session (R2):**
1. The verdict (or the human's decision) is written to the session's inbox file. The file is owned by the
   sindri and is read-only to the session.
2. The sindri sends `continue snd-tok:<nonce>` through `send-keys`. The nonce is single-use, bound to
   the check id and the session, and signed into the inbox file.
3. The `UserPromptSubmit` hook verifies the nonce against the inbox (match, unused, unexpired), injects
   the inbox contents as additional context, marks the nonce used, and writes a delivery ack to the spool.
   A prompt that carries a bad or reused nonce is blocked (`SND-DIR-030`). A prompt with no nonce is
   classified as human (§9.3).
4. For a `revise` verdict, the text is synthesized by sindri code from the verdict's agreed objections,
   as a structured list. Raw thread text never becomes instructions.

**Decision:** a judge question `direction-verdict` (`proceed | revise | escalate`) reads the HMAC-verified
turns only. Below threshold, the result is `escalate`. Escalation creates a `decide` item (§10.1). The
human runs `sindri decide <item>` to see both positions and choose.

**Providers and egress (R3):**
- The profile's `providers.allowed` defaults to Anthropic only.
- Diversity comes from a **different Claude model** than the proposer's (e.g. proposer Sonnet, challenger
  Opus), plus **Jev** as the arbiter's first classifier (already vendor-reviewed for code in judge).
- The arbiter is restricted as well. The sindri calls `judge --providers <profile list>`, a new judge
  CLI flag that limits the chain (M1). Without the flag judge would fall through to any installed agent
  CLI.
- Other providers (Codex, Cursor CLI, others) are opt-in per provider. They receive a bounded bundle:
  scrubbed, sensitive paths denied, non-agentic mode. Every provider call writes an audit row. `sindri scope` writes one `model_calls` ledger row per call (run id, role, model, tokens).
- If no distinct challenger model is available, the check is skipped and recorded as `skipped:no-diverse-model`.
  It never silently uses the proposer's model.

**Proof before trust (R5):**
- Checks start in `direction.mode: shadow`: they run and record verdicts but don't block. The ledger
  compares them against later human "wrong path" corrections and rework.
- **Exception:** the **Shape** checkpoint enforces from rollout step 3b, because enforced shape checks
  (§6.2) depend on it to resolve a refused commit.
- Other checkpoints move to `enforce` once shadow shows precision of at least `direction.minPrecision`
  (default 0.6) over at least 20 checks.
- **Caps:** per checkpoint, `direction.maxPerItem.<checkpoint>` (default 3 each; Shape 5), `direction.maxPerDay` (default 40), a per-check budget
  (`direction.maxTokensPerCheck` default 150k, `direction.maxMinutesPerCheck` default 30,
  `direction.deliveryTimeout` default 15 min → escalate), plus `budget.perItem` (M9).
- **Kill criteria** need at least 30 checks per checkpoint before they can fire (M9).
- **Kill criterion:** a checkpoint whose `revise` verdicts don't reduce rework compared to shadow over 30
  items goes back to shadow.

### 6.2 Code-shape checks and the code index
Four questions keep a codebase healthy as it grows:
- Can this be simpler?
- Can this be more elegant?
- Now that there are two cases, should this be generalized?
- Have we reinvented something that already exists?

At scale they must be **deterministically evaluable**: deterministic signals produce evidence, and models
judge only the shortlist. **All index layers enforce from the first enforcing rollout step (3b).** That's
an explicit decision, taken over a shadow-first alternative. Every enforced outcome is defined, so nothing
can deadlock.

**Code index** (per repo, behind an `Index` adapter, stored in `$AW_STATE_DIR/sindri/index/<repo>.db`,
owned by the sindri user):

| Layer | Content | Built with |
|---|---|---|
| Structure | Symbols (name, signature, kind, file, exported, callers) | TypeScript compiler API for TS/JS (v1); tree-sitter grammars for other languages in a later plan; LSP (Serena) when available |
| Clones | Normalized AST hashes per function and block (identifiers and literals abstracted), plus MinHash/LSH over token shingles (no pairwise comparison) | core |
| Dependencies | Package manifests, plus internal utility modules marked by profile globs | core |
| Embeddings | Function-level vectors from a **local** model | Ollama on loopback (`index.embeddings.url` must be the IP literal `127.0.0.1` or `[::1]`; redirects and cloud models are refused). A Prism route with `cloud_fallback:false` and `route_guard:local` is not built (a later plan) |
| Graph | Module and call graph, cross-file relationships, surface clusters | **graphify** adapter (`index.graph: graphify`) |

**Offline guarantee (T5):**
- Indexing never sends code off the machine. Embedding calls go only to a loopback endpoint.
- The graphify adapter runs with network access denied (sandbox profile, or a network-less namespace on
  Linux) and fails closed if it attempts egress.
- `doctor` verifies all three: the loopback endpoint, the graphify sandbox, and (once that route is built) the Prism flags.
- **Inputs:** tracked files only (respecting `.gitignore`), excluding the profile's `index.denyPaths`
  (secrets, fixtures containing PHI, generated code). Symlinks aren't followed. Per-file and total size
  caps apply.
- graphify runs on a snapshot of the tracked, non-denied source and docs files, never on the working tree; network denied, LaunchServices opens and Apple events denied on macOS (an `open URL` would start a browser outside the sandbox), writes confined to the snapshot, credential directories hidden (by their real paths), environment cleaned. Everything else stays readable, and other macOS mach services (securityd and the like) stay reachable (residual risk). graphify is installed at an exact, 14-day-old pin with `uv --exclude-newer`.

**Freshness and branches (M5):**
- The main index is rebuilt incrementally on merges to main. Builds take the heavy-job lock.
- Each worktree gets a **per-worktree overlay** built from its own diff, so checks see in-flight changes.
- An hourly quick build (structure, clones, deps) and a nightly full build; both read the files as checked out, so keep the default branch checked out in the indexed checkout.
- Every derived artifact carries a version stamp (indexer version, model id, graphify version, commit
  SHA). A stamp mismatch triggers a rebuild.

**Graphify as a provider:** it is one source of candidates for the Reinvention and Scoping checks.
- A prior trial found a code graph did not beat plain search for agent *navigation*. Here it is used for
  *candidate recall* and enforces from day one by decision.
- The eval loop still measures it arm-vs-arm and reports whether it earns its cost.
- **Kill criterion:** a layer whose candidates are overturned more than 80% of the time over 30 signals
  is demoted through a rule proposal.
- **Fast circuit breaker (M1):** a single signal type overturned 5 times in a row is paused for that
  repo until reviewed.
- **Single-layer failure:** only that layer's checks take the `index-unavailable` outcome. The other
  layers still enforce.

**Signals** (computed at commit by the git `pre-commit` hook, and re-computed sindri-side on the pushed
range) use profile thresholds, seeded by a historical replay over past merged PRs in rollout step 2, then
calibrated **per layer** on real commits in step 3a (Q1, M1). Until rollout step 3b, signals are recorded
only: the pre-commit hook opens the ledger read-only, writes them to the spool and never blocks. Each run
records the staged tree hash; once its commit is 14 days old each signal is labeled kept, acted-on, dropped
or n/a, and per-layer precision (acted-on over acted-on plus kept) is the 3b bar. This is an outcome proxy,
not a human label.

| Question | Signal | Default threshold |
|---|---|---|
| Reinvented? | A new symbol's AST hash matches an existing one; signature and name similarity ≥ t; an embedding neighbor ≥ e **and** an AST similarity ≥ a; a graph neighbor with an overlapping call set; a new dependency overlapping an existing one by purpose tag | t 0.85, e 0.9, a 0.6 |
| Generalize at the second case? | A near-clone between new code and existing code (≥ N tokens, MinHash Jaccard ≥ s) | N 60, s 0.8 |
| Simpler? | Changed lines over the size budget (XS 80, S 250, M 600); cyclomatic complexity delta above the limit; new files, exports, interfaces, flags or config keys above the size class's allowance | per size class |
| More elegant? | No direct signal. Covered by the three rows above plus the Shape direction check | — |

**Enforced outcomes (T3), every one of which ends:**

| Situation | Commit | Then |
|---|---|---|
| Signal fires, check budget available | Refused with evidence | Shape direction check opens; the session ends its turn (`awaiting-direction`); the verdict is delivered: `revise` (reuse or generalize), `proceed` (keep, reason recorded), or `escalate` (`decide` item) |
| Signal fires, `direction.maxPerItem` exhausted or no diverse model available | Allowed, with a `shape-unresolved` trailer | PR gets the `shape-unresolved` label; needs-approval to the human; the ship state machine won't undraft until it's resolved |
| Index missing, stale beyond `index.maxAgeHours`, or failing | Allowed, with an `index-unavailable` trailer | Sindri rebuilds; the sindri-side re-check runs on the push range once the rebuild finishes; a failure there → needs-approval |
| Check exceeds the 2 s commit budget | Allowed, with a `shape-deferred` trailer | The sindri-side check on push is authoritative and refuses the push if it signals |

The Shape direction check ships in the **same** rollout step as shape enforcement (§13, step 3b).

**Evidence format (M8):**
- Each signal names its type, value and threshold, both locations as `path:line`, the index age and the
  providers involved.
- Repo-derived strings (symbol names, comments, snippets) are shown inside `<untrusted>` fences (M4).
- A `proceed` reason is labeled `agent-written` with the session id, and the daily digest lists every
  `proceed` that let code through.

**Exemptions expire (M4):** a recorded `proceed` exempts the pair `(new symbol, existing symbol)` until
either file changes, or for 30 days, whichever comes first.

**Repo trend:** the eval loop tracks duplication ratio, clone count, average complexity and dependency
count over time on the files sindri sessions touched (M9). A rising trend becomes a typed `reuse-hint`
rule proposal (§7.4).

## 7. Flows

### 7.1 Intake and routing
1. The tick scans the profile scope with an `updatedSince` cursor, pagination and rate-limit backoff (M6).
2. New or changed items get a triage job.
3. The router decides:

| Condition | Route |
|---|---|
| `mode: shadow` | Record only. No session starts. |
| `mode: assist` | The item is listed in `status` as startable. The human runs `sindri start <item>`. |
| `mode: auto-small`, size ≤ `autoStartMaxSize` (default `XS`), ambiguity `none`, all authors trusted (§8.3), budget available | Auto-start a worker session. |
| Otherwise | Scoping queue. In v1 the scoping output is a proposal the human approves before workstreams start. |

4. Unassigned items are assigned to the human only when a session starts (allowlisted as `assign: self,
   unassigned only`, M15).
5. Questions are batched per item and sent as one needs-answer notification (§10).

### 7.2 Sessions
- `sindri start <item>` (manual) and auto-start both go through the Launcher. A session gets:
  - a sindri-generated name `snd-<ulid>` (§8.6)
  - its own container (§8.2) with `/workspace/src` cloned from the read-only base, on the local branch
    for the claim ref `sindri/<user>/<item>` (§9.4); trust pre-accepted in the image (M5)
  - an environment from the repo's `Environment` adapter (§11.2), for example a PR preview, shared host
    services or a per-container stack
  - a settings overlay: normal permission mode (never bypass), the profile tool allowlist, and the
    sindri gate registered (§8.1)
  - a scrubbed environment and scoped tokens (§8.2)
  - its pack path, injected at SessionStart
- **Re-injection (M9):** on SessionStart for `compact` and `resume`, a hook injects the pack's Task and
  Acceptance sections plus a pointer to the full pack. This replaces the context guard's digest nudge for
  sindri sessions.
- Sessions end when their Step completes. A session past `session.maxAgeHours` (default 8) or
  `session.maxContextTokens` is checkpointed and relaunched from pack plus checkpoint.

### 7.3 Ship state machine
The profile names the reviewers (by account id), labels and CI checks.

```
build:   each level opened as a draft; stack complete when the last level's worker step passes
release: for level = bottom … top:
           undraft (needs-approval unless the allowlist includes it; default: needs-approval)
           → wait for reviewer bots → fix rounds (worker session) until threads resolved ∧ CI green
           → re-draft → next level
done:    stack fully reviewed; the human merges
```

### 7.4 Self-evolution loop (skills, prompts, recipes, thresholds)
The harness improves its own parts with little to no human input. That includes every skill it adopts,
native or ported (§16). Human approval is reserved for **protected surfaces** and for **merging code**.

**Registered artifacts.** Everything that shapes agent behavior is registered in an artifact registry in the
ledger. Each entry has an id, a version, a source (`native`, `vendored:<pack>@<rev>` or
`overlay:<profile>`), an owner Step, and an **eval suite**. Registered artifacts:
- skills (including ported pstack, continual-learning, advisor and cursor-team-kit skills)
- Step prompts and verifier rubrics
- evidence, ship and environment recipes
- the size rubric and shape thresholds
- direction-check prompts
- hook denial and shim messages

**Proposal sources** (all automatic). Wherever reflect, correct and steering telemetry read transcripts,
copied turns from resume and fork are deduped by (timestamp, text), and a "human correction" signal comes from
the calibrated labeler or metric-grade patterns only (§13 step 0):

| Source | Produces |
|---|---|
| Recurring human steering (≥ `eval.minSessions` sessions on ≥ 2 days, human-origin turns only) | A typed rule or recipe change, as before |
| Per-item **reflect** (pstack `/reflect`, ported), run after every completed item | A skill or prompt edit that captures what worked |
| History **correct** (pstack `/correct`, ported), weekly | A fix at the highest level that works, for repeated mistakes |
| Verifier and direction-check overturn patterns | A rubric or threshold edit |
| A new upstream revision of a vendored pack | An upgrade proposal |
| The reuse map's gaps (§16) | A port proposal |

**Evaluation** (no hand labels, invariant 9). A proposal is a variant of an artifact. It must win both
evaluations:
1. **Offline blinded comparison** on a frozen replay corpus drawn from the ledger.
   - The current version and the variant each run on the same past inputs.
   - A judge question compares the two outputs **blind**. The judge runs on a different model from the
     generator.
   - Outcome proxies score the results: the deterministic Step checks pass, and the later human
     correction or rework on that item would have been avoided.
   - Pass bar: win rate ≥ 0.6 over ≥ 20 comparisons with a lower confidence bound > 0.5.
   - 30% of the corpus is a **sealed holdout** that is never used to generate proposals.
2. **Online shadow A/B:** the variant runs in shadow next to the current version on ≥ 10 live items, and
   no outcome metric regresses.

**Adoption tiers:**

| Tier | Covers | Adoption |
|---|---|---|
| **Self-adopt** | Skills, prompts, rubrics, recipes, thresholds, messages, rules of the §7.4 typed kinds, vendored-pack upgrades | **Automatic** on passing both evaluations: a canary on 25% of items for one week, then 100%. Lands as a signed commit to the profile overlay (`overlay/skills/…`), effective immediately. Shown in the daily digest as `fyi`. |
| **Approval** | Protected surfaces: tool allowlist, `trustedAuthors`/`trustedBots`, hosts, secret pointers, egress allowlist, hook configuration and safety hooks, budgets and quota reserve, notifier/scheduler config, scrubber patterns (they may only be *added* to automatically) | needs-approval with the diff, source turns and evaluation results |
| **Code** | Anything in the core repo (generic improvements to native skills, sindri code) | The sindri opens a PR to the core repo **as a normal work item** (dogfooding, §7.6). The human merges. Until the merge, the overlay version is in effect. |

**Guardrails:**
- At most 3 adoptions per artifact per week, and at most 10 per week in total.
- **Auto-revert:** any outcome metric regressing over the next 20 items after adoption reverts the change
  and records it as a failed variant.
- **Tier brake:** if more than half of the self-adopted changes in a 30-day window are reverted, the
  self-adopt tier pauses (everything goes to approval) until the human resumes it.
- Variants can't modify their own eval suite. Eval-suite changes are approval-tier.
- `sindri evolve status | history | revert <id> | pause | resume` exposes all of it, and the
  dashboard's Today view lists adoptions and reverts.

**Rollout step 1 (Plan 5) narrows this section as follows:**
- **Adoption is manual.** A prompt variant that wins its offline comparison is marked `won`; `sindri evolve adopt <id>` (a terminal, the line diff shown first, a typed confirmation bound to the variant's sha256) installs it into the overlay and records its hash in the ledger. The loader uses an overlay only when its hash matches the latest adoption and it keeps the injection-resistance clause. The self-adopt tier, canary and auto-revert start at rollout step 6.
- **Hook false-positive rates are adjudicated.** A fire is recognised only from the harness's own hook-feedback shapes; the adjudicator decides whether each sampled fire was warranted (by default `opus`, the same model as the challenger that drafts reflect and correct proposals, but hook-fix proposals are drafted from a template, so no proposal is judged by its own drafter); a `hook-fix` proposal needs at least 10 labelled samples and a Wilson lower bound above 0.2. The rate counts blocks only.
- **Only the scoping draft prompt gets an offline comparison.** Other prompts and skills are evaluated through their module tests and telemetry until a corpus of PRs exists, and interactive skills need replayed agent sessions (step 3a).
- **Evolve reads only this repo's sessions** (working directory under the toolkit repo), whatever `sources.transcripts.enabled` says. A line copied into a resumed or forked session counts once (deduped on timestamp and text).
- **Corrections are model-labeled.** `correct` has a model (never the builder, never a hand label; invariant 9) label each human turn as a design correction, a process correction, a restated instruction, a missed surface, or none of those, instead of matching keywords: a regex caught 5 of 45 wrong-approach corrections, and about half of real corrections are about process. Process classes lean toward rule, doc or skill proposals and design classes toward prompt or skill proposals; the proposal prompt decides. The labeler is not calibrated against outcome labels yet (it replaces the "calibrated labeler" of the proposal-source preamble above).
- **"Already ran" is the complete-run marker only.** `reflect` records `Already reflected on PR #n`, and `correct` records `Already ran for <week>`, only when a run finished within budget (and, for `correct`, without label errors). A partial run is retried next time; saving a proposal is deduplicated, so a rerun can't double it. This replaces the earlier rule that any saved proposal for the source meant "already ran".
- **Not built in step 1:** the "at most 3 adoptions per artifact per week" limit, `sindri evolve history`, `pause` and `resume`, and automatic regression rollback. Adoptions are manual and rare, `sindri evolve revert <prompt-id>` and `sindri channel rollback` are the manual equivalents, and the tier brake arrives with the self-adopt tier.

### 7.5 Scoping harness (front of the rollout)
The problem that started this design: projects like a multi-surface "new shift times" rollout sprawl
because surfaces and implications are found during implementation, not at creation. The scoping harness
runs **host-side**, early (rollout step 1). It needs no containers, because it reads no session-authored
code.

- **Command:** `sindri scope <project | brief file | tracker project URL> [--backtest]`
- **Inputs** (through Source adapters, read-only):
  - the project brief and docs
  - existing tracker issues and comments
  - (not in v1: linked chat threads are deferred by §4, and memory (Prism) is read inside agent sessions through MCP, not by the CLI)
  - the code index (structure, call graph via graphify, embeddings) for the affected modules
  - prior steered transcripts on the same area (opt-in: sources.transcripts.enabled, default false)
  - the notes dir (vault)
- **Output:** a cited **scope map**.
  - **Surfaces:** every UI, API, job, data and integration touchpoint, each with an evidence citation
    (code symbol, doc or ticket).
  - **Implications:** data migration, permissions, reporting, notifications, mobile, feature flags.
  - **Workstreams** with dependencies.
  - **Acceptance checks** per surface.
  - **Open product questions,** batched for the human.
- **Verification loop (Step contract):**
  1. Deterministic checks: every surface cites a source; the dependency graph is acyclic; the map has at least one surface and every surface is in a workstream.
  2. An adversarial **missing-surface** pass using graph neighbors and embedding recall over the index,
     repeated until no new surface appears (round cap).
  3. A Scoping direction check (§6.1), from rollout step 5. Until then the missing-surface loop on a different model is the verifier.
- **Delivery:** the scope map is written to `--out` or the notes dir (0600) and, in v1, not attached to the tracker project (that write needs the step-3a path, §8.1 and §8.5); `sindri scope` refuses to write inside a git worktree unless `--sources` is `file,code`. Creating issues from it is needs-approval in v1, then self-adopt once the backtest bar is met
  for that project type.
- **Backtest** (the proof): `--backtest` runs scoping on a project's **original brief** as of its creation
  date, then measures **recall** of the surfaces behind the issues filed later.
  - The first backtest target is the project that motivated this design.
  - Sources are cut to the project's creation date: the notes dir is left out, and so is the code index unless `--with-index` (the run is then labeled leaky). Linear text is fetched as it is today, so recall is optimistic.
  - Labels come from an adjudicator model, never a person; every batch is judged twice with the order reversed and only agreeing answers count. The report gives recall, precision (the share of map surfaces some project issue supports), the same numbers for a brief-only baseline, and a pass bar of recall >= 0.60, precision >= 0.60 and recall above the baseline's.
  - Recall becomes the scoping harness's eval-suite metric, and the self-evolution loop improves it
    (§7.4).

*Amendments (Plan 4):*
- *(12) The Scoping job in v1 has no read-only code tools (`--tools ""`); code context comes from the code-index source in the pack, and "independence checked against predicted file sets" (§6.1) is deferred.*
- *(13) Acceptance checks are per workstream in v1, not per surface.*
- *(14) The challenger sees the same evidence pack as the drafter; graph-neighbour and embedding recall for the challenger are deferred (cross-source retrieval is issue #75).*
- *(15) Scoping v2 (factory addendum §17.26) replaces (12) and (14): the challenger writes typed expansion queries that Sindri runs against the index and tracker every round, a prune pass drops unsupported surfaces, tracker intake can start scoping, and the design studio edits the packet with the same pipeline.*

### 7.6 Dogfooding
The harness's own backlog goes through the harness as ordinary work items in the core repo's tracker:
- reuse ports (§16)
- eval findings
- hook fixes (for example the done-gate false positives)
- code-tier proposals from §7.4
- code- and approval-tier proposals are staged privately by the weekly job and published, on request and on a branch, as tasks in `docs/superpowers/plans/<week-monday>-sindri-plan-proposals.md` after a scrubber and privacy check (`privacy.denyTerms`); sindri never commits them

They are scoped, built in assist mode, verified, and opened as PRs. The human merges.

### 7.7 The toolkit repo as a registered artifact
The **entire toolkit repo** (this repo) is registered in the artifact registry, at module granularity. That
covers:
- skills and the shared skill text
- hooks and adapters
- `judge`, `scorer`, `sindri` itself
- provider installers and `setup.sh`
- rules, planning docs, mods
- the external-pack pins

Every module gets an eval suite and telemetry, and its improvements come from the same proposal sources as
§7.4.

| Module class | Eval suite (outcome-labeled, no hand labels) | Telemetry that drives proposals |
|---|---|---|
| Skills (native, vendored, ported) | Blinded comparison on replayed items; the skill's own tests | Steering turns while the skill is active; reverts; verifier overturns |
| Hooks | Recorded-payload suites; **false-positive and false-negative rates** computed from all sessions' transcripts (e.g. a gate firing on a turn that made no claim) | Hook decision logs across host and container sessions |
| `judge` questions | judge's existing eval harness; escalation and override rates | Decisions followed by human reversal |
| `scorer` | Report accuracy against recomputed ledgers | Discrepancies, failed parses |
| `sindri` | Unit, contract and heavy suites; ledger invariants; replayed ticks | Stalls, parks, relaunches, budget breaches |
| Installers / `setup.sh` | `setup.sh --dry-run` on all providers; bash tests | Install failures in `doctor` reports |
| Docs and rules | Link and freshness checks; `sync-rules.sh --check`; agent answer accuracy on doc-derived probe questions | Questions agents ask that the docs should have answered |

**How self-changes are applied safely:**
- **Two channels.** The harness always runs from a pinned **stable** release of itself. Proposed changes
  to the toolkit land on a **next** channel, built and evaluated *by the stable harness*. They are
  promoted only after passing the merge gate, their module's eval suite, and a soak period (default 3
  days of shadow use on `next` alongside `stable`).
  - Channels are install locations, not branches: `scripts/install-sindri.sh --channel next --ref <sha>` builds a merged commit into an immutable directory; `sindri channel promote <sha>` needs a passing `package:sindri` suite run at that sha, the soak, a smoke start and a typed confirmation; `rollback` needs the previous build to still exist.
  - `sindri evolve check --at <sha>` runs the suite in a temporary detached worktree of the repo at that sha, removed afterwards.
  - `--channel stable` only bootstraps the first stable build; every later stable change goes through `promote`, which refuses a sha that is already stable. `rollback` clears `previous`, so a second rollback refuses.
- **No self-certification.** A change can never modify the eval suite or telemetry that judges it in the
  same proposal (invariant 11). The evaluator for a proposal is always the stable channel.
- **Regression rollback.** If promotion regresses any module metric over the following 20 uses, `stable`
  rolls back to the previous release automatically, and the change becomes a recorded failed variant.
- **Protected modules** need human approval whatever the merge policy: the safety hooks, the tool gate and
  allowlist code, the scrubber's removal paths, the self-evolution tier rules, eval suites, and installers'
  permission and settings writes.
- Every self-change is a work item processed by the harness itself (dogfooding, §7.6). It is scoped,
  built, verified, and opened as a PR in this repo.

**Merge policy for this repo:** the profile's `selfMerge` key decides who merges promoted toolkit PRs that
touch no protected module.
- `human` (default): every PR waits for the human, batched into one daily review digest.
- `auto`: the sindri merges once all gates pass.

Protected modules always wait for the human.

## 8. Safety boundary
Required before `mode: auto-small` can be enabled. `doctor` refuses `auto-small` until every check in
§8.1–8.7 passes.

### 8.1 Sindri tool gate (H1, H6, M12)
- A PreToolUse hook that exists only in the session image (§5.3). It reads the session's policy from
  `/sindri/policy.json` (read-only mount), never from env.
- **Default deny.** It allows only the profile allowlist, with argument constraints. Examples:
  - comment only on the claimed item, at most one per milestone
  - status/labels only on the claimed item
  - push only to the claimed branch
  - `gh pr create --draft` only
- **MCP tools are allowed by exact name only.** Unknown or new tools are denied (M4).
- **Real controls live server-side:** branch protection and rulesets on the remote (no push to protected
  branches, no force push). The gate is the in-session layer. Because tokens can't express "draft only",
  the sindri watches PR state and re-drafts plus alerts on any undraft it didn't approve.
- **Always denied**, with no allowlist entry possible in v1: undraft, merge, close/cancel items, create
  items, chat posts, `gh api` writes, workflow triggers, prod-mutating MCP tools, `kubectl`/exec tools.
- **Fails closed:** a parse error or missing policy means deny. `AW_JUDGE_CHILD` and other env flags are
  ignored in sindri mode.
- A denied call returns a typed reason. The sindri parks the item with that reason and sends a
  needs-approval notification. The session never sits on a permission prompt.
- `send-keys` sends only the fixed `continue` token, and only when the pane is not showing a permission
  dialog.

### 8.2 Session isolation, credentials and identity (I1–I5)
**Every session runs in its own container.** The container runtime sits behind a `Sandbox` adapter:
- macOS: OrbStack or Docker Desktop, both in one Linux VM.
- Linux: Docker or Podman, natively. This is the same model on a cloud box.

The host never executes code a session wrote.

**Resource ceiling** (this also fixes the host crashes):
- Each container has CPU and memory limits (`session.cpus`, `session.memory`, default 2 CPU / 3 GB).
- On macOS the whole fleet also lives inside one VM with a fixed size (`sandbox.vmMemory`, default 12 GB).
- A runaway session hits its own limit. It can't take down the host, the terminal, or the other sessions.

**Mounts:** reads follow the Serena pattern (read-only mounts, as in `scripts/serena-docker`); writes stay in
the container.

| Host path | Container path | Mode | Purpose |
|---|---|---|---|
| Sindri-owned bare mirror, `$AW_STATE_DIR/sindri/mirrors/<repo>.git` (fetched every tick) | `/mirror` | ro | Object store |
| Sindri-created checkout of the base ref (a `git worktree` of the mirror) | `/workspace/base` | ro | Exact starting tree |
| Package and toolchain caches | `/cache` | ro | Installs become cache copies |
| Code index snapshot | `/index` | ro | Shape checks |
| `$AW_STATE_DIR/sindri/sessions/<id>/ro/` (pack, policy, inbox, delivery nonces) | `/sindri` | ro | The sindri writes it; the session reads it |
| `$AW_STATE_DIR/sindri/sessions/<id>/spool/` | `/spool` | rw | Heartbeats, positions, hook decision log, outgoing `git bundle` (§8.5). The host treats all of it as untrusted data. |
| *(container volume)* | `/workspace/src` | rw | `git clone --reference /mirror --dissociate /mirror` then `checkout <base SHA>` (V3). The mount of `/workspace/base` is for reading only. |
| *(container volume)* | `node_modules`, build output | rw | Fast IO; never bind-mounted |
| *(per-item persistent volume)* | `/state` (`$AW_DIR`, candidate branches) | rw | Survives relaunch (W5); deleted when the item completes or is abandoned |

- Directory zones (I4):
  - **sindri-private:** HMAC key, ledger, mirrors' config. Never mounted.
  - **shared read-only:** `/sindri`, `/mirror`, `/workspace/base`, `/cache`, `/index`.
  - **session-writable:** `/spool` and the volumes.
- **No shared gitdir (I1):** the session's `.git` lives in its own volume. Nothing the session writes can
  end up in a git dir the human's shell or editor reads.
- **Live view, off by default (V4):** `view.enabled: false`. When enabled:
  - The sindri's spool reader copies only an allowlist of source file types (no dotfiles, no `.vscode`,
    `.idea`, `.envrc`, `.git*`, no executables) to `$AW_STATE_DIR/sindri/view/<item>/`.
  - The copy uses `O_NOFOLLOW`, regular files only, and per-file and total size caps.
  - `sindri view ITEM` opens it in the editor's restricted mode.
- **Spool ingestion (V4, W7):** the host never walks session paths. The sindri copies the spool out
  with `tar` streamed from a short-lived helper container inside the VM, which archives regular files
  only and refuses symlinks. The archive is then read by the sindri's spool reader:
  - a separate low-privilege process
  - archive entries validated (no absolute paths, no `..`, regular files only; FIFOs, sockets, devices
    and symlinks rejected)
  - every transcript and WIP export passes the scrubber (W3)
  - per-file and per-session size quotas
  - JSON-schema validation

  Anything else is quarantined and logged as `SND-SANDBOX-2xx`.

**Credentials (I3):**
- **No write tokens in the container.** The session commits locally. Pushing is sindri-mediated (§8.5).
- **Model access uses subscription login (V6):**
  - `claude setup-token` is run once on the host. The long-lived token is stored sindri-private.
  - **The real token never enters a container (W1).** Containers get `ANTHROPIC_BASE_URL` pointing at
    the model proxy and a per-session dummy credential. The proxy swaps in the subscription bearer per
    request, so `docker inspect`, transcripts and pane previews contain no real token. This is validated
    by the spike (§13.1). The fallback is API-key mode, with the key also injected only by the proxy.
  - **Rotation and revocation:** `sindri auth rotate` re-runs `setup-token` and swaps the proxy's
    bearer with no container restart. `sindri auth revoke` stops the proxy forwarding immediately.
  - **Quota (W2):**
    - The proxy tracks subscription usage. `quota.reserveForHuman` (default 30% of the observed window
      limit) is held back for the human's own sessions.
    - Rate-limit responses put sessions into `quota-exhausted` (a known wait, not a stall) and pause
      new starts.
  - The **model proxy** is the only egress to Anthropic and is the control point (M3):
    - per-session proxy credentials, so each request is attributed to a session
    - a fixed upstream (`api.anthropic.com`), with model and endpoint allowlists from `providers.allowed`
    - token and turn budgets enforced from response usage
    - full request logs
  - The outward-write scrubber treats the OAuth token pattern as a secret, so it can't leave through
    allowed writes.
  - API-key mode (key injected by the proxy, never in the container) is a profile option for cloud boxes
    and teammates.
- **Data access:** read-only tracker and docs tokens, scoped per profile. MCP servers are configured
  inside the image with read scopes only.
- **Egress mechanism (M3):** session containers sit on an internal network with **no default route**.
  - The only reachable host is the sindri's egress proxy. It forwards to: the model upstream, the
    read-only data endpoints, package registries through the cache, and the profile's environment
    endpoints.
  - Everything else is denied, including the host's loopback services (the Prism dashboard).
  - Every denial is logged as `SND-PROXY-1xx`.
- **Secrets for recipes** (e.g. a preview login): `sindri cred run <recipe>` runs host-side and passes a
  short-lived session cookie or token into `/sindri` for that recipe only. Long-lived secrets never
  enter the container.

**Identity:**
- The container is the sindri session. Sindri hooks ship only in the session image's
  **root-owned `managed settings`** (`/etc/claude-code/managed-settings.json`), and Claude runs as an
  unprivileged user that can't modify them.
- The host's own Claude sessions get no sindri hooks at all, so there's nothing to no-op.
- Writes the sindri makes to trackers and GitHub use bot identities when the profile provides them, or
  carry a "via sindri" footer.

**tmux and attach (M3):**
- tmux runs **inside** each container, with its own socket. Sessions can't reach each other's panes, and
  the host doesn't need tmux.
- `sindri attach ITEM` runs `docker exec -it <ctr> tmux attach` and records the attach.
- **In-pane input is advisory (W6).** tmux can't attribute input to a client, so in-pane input is never
  treated as an authenticated human decision.
  - Input typed while a sindri-recorded attach is open is classified `takeover`. That is the safe
    direction: it pauses automation.
  - Approvals, answers, decisions and handbacks come **only** from host-side verbs (`answer`, `decide`,
    `approve`, `handback`), which the host OS user authenticates.
  - The eval corpus counts only host-verified human input (M5).
- Answers outside a session go through `sindri answer` / `decide`, which the host OS user
  authenticates.

**Heavy-job lock across containers:**
- The sindri brokers the lock over a narrow unix socket mounted into each container. The socket offers
  `acquire(kind, ttl)` and `release` only.
- Leases expire, so a dead container never holds the lock.
- Index builds and image builds take the same lock.
- Host side: a `mkdir` lock at `${AW_HEAVY_JOB_LOCK:-$AW_STATE_DIR/locks/heavy-job.lock}`, the same `mkdir` primitive as `config/lib/locks.sh` and the same default path as ui-evidence's heavy-job lock (`skills/ui-evidence/scripts/lib/locks.sh`), with the holder record (`<lockdir>.holder.json`) beside it so the lock dir stays empty for `rmdir`; a holder that is dead on this host (its pid is gone, or its record names another boot id) is reclaimed. Only the waiter that creates the `<lockdir>.reclaim` mutex may reclaim; it re-checks the holder inside the mutex, moves the dir aside by rename, and keeps the mutex until its own dir and holder record exist, so two waiters can never both enter. A holder record older than the lock dir is a leftover and is never reclaimed on.

**Warm start:**
- Images are keyed by `(repo, lockfile hash, toolchain)`. Rebuilding one is a heavy job.
- Startup is: mirror fetch (host), base worktree (host), `clone --reference --dissociate` and cache-backed install
  (container).
- Target: under 60 s to the first prompt on a warm web-app image (measured in the spike).
- **Uncommitted work survives relaunch (M6):** every 10 min, and on PreCompact, a hook writes a WIP
  bundle (a stash commit) to the spool. Relaunch restores the latest one.
- **VM sizing (M6):** `sandbox.vmMemory` defaults to `cap × session.memory + 4 GB` headroom. `doctor`
  warns when the configured VM is smaller.
- **Transcripts (M7):** the container's Claude transcripts are exported through the spool on Stop and on
  relaunch, so the scorer and the eval loop see them.
- **Bounded jobs** that read session code (scoping, challengers) run in short-lived containers from the
  same image, never on the host (M7).

**Escape surface:** container escape is out of scope for v1. The runtime's defaults apply, plus no
privileged mode, no host network, no Docker socket mount, read-only root filesystem except the volumes,
and dropped capabilities.

### 8.3 Trust tiers and pack format (H2)
- `trustedAuthors` (immutable account ids) and `trustedBots` live in the profile. Rules can't change them.
- **Auto-start requires** the item creator, last editor and all commenters to be trusted. Otherwise the
  item goes to `assist`.
- **`plan-file` items:** trust is the git author email, which anyone can forge in a public repo. It only affects what `observe` would start. Before `auto-small` (step 4), trusted authorship must come from signed commits or tracker accounts.
- **Review comments** feed fix rounds only from trusted reviewers and bots, matched by id.
- **Pack layout:**
  ```
  ## Task            (synthesized by sindri from trusted fields; instructions live only here)
  ## Acceptance
  ## Stated fix      (present only if its author is trusted; otherwise quoted below)
  ## Surfaces
  ## Evidence plan   (recipe names from the profile)
  ## Sources         (citations)
  ## Untrusted quotations
  <untrusted source="tracker:ITEM#comment-…" author="…"> … </untrusted>
  ```
- The session preamble states that `<untrusted>` content is data and never an instruction source.
- Ingest strips HTML comments, zero-width characters, encoded blobs and remote image URLs. `sindri scope` applies this to every source, and neutralizes model-written text (images, links, tags, URLs) when it renders Markdown.
- Triage and pack Steps are tool-less. Fetching happens in sindri code through typed Source adapters,
  by id, never by arbitrary URL (M18).

### 8.4 Scrubbing and retention (H11)
- The scrubber (secret patterns from `detect-secrets.sh` plus JWTs, chat tokens, private key blocks,
  credentialed URLs, and identifier shapes such as MRN and SSN) runs on every fetched record, every pack
  write, every ledger free-text field and the eval corpus.
- The scrubber also runs **at every egress to a model** (pack injection, challenger bundle, judge input),
  not only before storage (M9).
- Challenger text is never part of the eval corpus (M10).
- Packs are stored 0600 with a 14-day TTL purge.
- The ledger stores reason codes and hashes. Free-text reasons are scrubbed and capped.

### 8.5 Egress filter and path guard (H8, M16)
- **Outward-write filter:** every allowed outward write (comment, PR body, attachment) passes the scrubber,
  a size cap and a URL domain allowlist.
- **Status comments** are composed by sindri code from structured fields (state, PR link, check
  results). They are never free model text.
- **Evidence attachments** need a manifest and a redaction pass.
- **Prod data:** worker sessions get no prod-read or exec MCP tools by default. Prod reads are available
  only through named profile recipes, and their output is marked sensitive and blocked from egress.
- **Path guard:** a diff that touches profile-declared protected paths (CI and workflow config, hooks,
  auth, lockfiles, the profile itself) parks the item for approval.
- **Sindri-mediated push with two-phase verification (I2, I3, V1, V5):**
  1. **Export (W4).** Export is triggered by the **Step completion event**, not by Stop. For the bug
     worker, that is bugFixOrchestrator's state reaching `resolved`. The sindri's hook bundles the
     **winning candidate branch** named in the worker's state file:
     `git bundle create /spool/out.bundle <base>..<winning-branch>`. Opening the PR, the review loop and
     undraft belong to the separate **Ship** Step (§7.3), matching the skill's own "do not open a PR;
     suggest /shipRelease" contract.
  2. **Snapshot.** The sindri's spool reader (§8.2) copies the bundle **once** into sindri-private
     storage. It accepts regular files only, uses `O_NOFOLLOW`, and enforces a size cap
     (`verify.maxBundleMB`, default 200). It records the bundle's SHA-256, and every later step uses only
     that copy and that hash.
  3. **Phase 1, trusted checks (before push).** These run in a **verifier container** that executes no
     session code: unprivileged, no network, no credentials, read-only root.
     - It fetches the bundle copy into a clean clone of the mirror with `transfer.fsckObjects=true`,
       `core.hooksPath=/dev/null`, and no attributes or filters.
     - It enforces pack size and object-count limits and a timeout.
     - It runs only sindri-owned, deterministic checks: `git fsck --strict`, path guard, diff caps,
       shape checks (index snapshot, read-only), protected-path rules, and a **secret/PHI scrubber pass
       over the diff and every new blob**. A hit parks the item with needs-approval (W3).
     - Its verdict is a signed JSON written by the verifier binary, which comes from the sindri image,
       not the repo.
  4. **Transfer.** On a phase-1 pass, the sindri runs `git fetch <bundle copy>` into its mirror, with
     the same hardening and the hash verified. It then pushes that exact SHA to `sindri/<user>/<item>` as a
     **draft** PR.
  5. **Phase 2, environment checks (after push, PR stays draft).**
     - The repo's `Environment` adapter provisions an environment for the pushed SHA (for example a PR
       preview).
     - A separate **reproduce sandbox** runs the recorded reproduce check against that environment. It's a
       container with network access only to the environment's endpoints, no credentials beyond the
       environment's short-lived low-privilege test identity, and no access to the verifier or the ledger.
       This is the only place session-authored test code runs.
     - The sindri reads **only the sandbox's exit status and stdout cap**. Nothing the sandbox writes
       can change a verdict file.
     - The check must fail on the base (recorded once per item) and pass on the head. Flaky checks run 3
       times; disagreement parks the item.
  6. **Undraft gate.** The ship state machine may request undraft (needs-approval by default) only after
     phase 1 passed, phase 2 passed, and the head SHA is unchanged since both.
  - Repos with `Environment: none` run phase 2 in the reproduce sandbox with no network.
  - A **forged-verdict probe** in `doctor` and the tests confirms that a bundle whose tests write a fake
    "pass" file cannot produce a passing verdict.
- **Server side:** rulesets protect `sindri/*` refs (no force push, no direct push to protected branches).
  Configuring them requires **repo-admin** rights, a per-repo prerequisite that `sindri repo check`
  detects (M8). `doctor` runs a **bypass probe**: from inside a session container, a direct push and a
  direct API write must both fail.
- **CI secrets:** environment-protected so `sindri/*` refs can't use them without approval.
- **Undraft:** the undraft watch re-drafts within one tick interval. The remaining window is documented.

### 8.6 Notifier action contract (H7)
- Session names are sindri-generated `snd-<ulid>`, validated against `^snd-[0-9a-z]{26}$` at both the
  Launcher and the Notifier.
- Notification actions carry only `{kind, session_id | item_id}`. A local resolver turns that into an argv
  array at click time, with no shell: `docker exec -it <container> tmux attach`. Remote: `ssh -- <host>
  sindri attach <item>`, with the host taken from the profile allowlist.
- Titles and bodies carry the item id plus a scrubbed, length-limited title. No other item text goes into
  actions.

### 8.7 Audit log and profile integrity (M13, M17)
- **Audit log:** an append-only log under `$AW_STATE_DIR/sindri/audit/` records every allowed and
  denied gated call (tool, args hash, item, session, profile hash). There are per-item rate limits, and a
  spike in denied calls raises an alert.
- **Profile integrity:** each session pins the profile commit hash in the ledger. A profile change takes
  effect only after `sindri profile approve <hash>`. Runtime commands load the last approved snapshot; approving needs an interactive terminal and a typed confirmation. Recipes are validated against a command schema.

## 9. Runtime correctness

### 9.1 Singleton tick with fencing (H3)
The sindri implements its own lock in TypeScript. The bash helpers in `config/lib/locks.sh` and
`skills/ui-evidence/scripts/lib/locks.sh` are unchanged, and the heavy-job lock stays canonical there (L1).

- **Acquire:**
  1. Create `lock.tmp-<pid>/owner.json` holding `{pid, pidStartTime, host, bootId, startedAt, epoch}`.
  2. Rename it to `sindri.lock`.
  - The rename is atomic, and it fails if `sindri.lock` exists, because that directory is never empty.
- **Stale takeover:** if the owner is provably dead (same host, same `bootId`, pid not alive, or a
  different `bootId`, or a live pid whose start time differs from `pidStartTime`, meaning the pid was
  reused), rename `sindri.lock` → `sindri.lock.stale-<ulid>`. Only one taker wins that rename. The
  winner re-reads the renamed owner file and confirms it is still the dead owner before acquiring. A
  mismatch puts it back and exits (M2).
- **Fencing:** acquiring increments `epoch` in the ledger in a `BEGIN IMMEDIATE` transaction. Every ledger
  write, job result and Launcher action carries the epoch, and stale epochs are rejected. A paused or
  zombie tick can't act after another tick takes over.
- **Boot id:**
  - macOS: `sysctl -n kern.bootsessionuuid`
  - Linux: `/proc/sys/kernel/random/boot_id`
  - The state dir must be on local disk; `doctor` checks this.
  - Containers must not share a state dir.
- A tick that can't acquire exits 0 and writes `no-op: locked by <host>/<pid> since <t>` to stderr.

### 9.2 Job and session liveness (H4)
- **Alive:** the container is running, and its tmux pane exists with `pane_pid` alive.
- **Progressing:** the transcript mtime changed or a spooled heartbeat arrived within
  `liveness.stallAfter` (default 20 min). Pane content changes are ignored, because spinners fool them (L2).
- **Known waits are not stalls:** a permission dialog, a question to the human, `awaiting-direction`
  (§6.1), or a wait on the heavy-job lock.
- **Relaunch is kill-before-relaunch:**
  1. Kill the pane.
  2. Confirm it's gone.
  3. Relaunch a container on the same claim ref, reattaching the item's **persistent volume** (W5),
     which holds `$AW_DIR` (the worker's state such as `bugfix/<slug>/state.json`) and its candidate
     branches. WIP bundles include every candidate branch. Restored WIP passes the phase-1 checks
     before the session resumes (M6).
  - After 2 relaunches the item is parked.
- **Sleep:** when the tick sees a wall-clock jump larger than its interval, it extends every lease by the
  gap before evaluating them.

### 9.3 Answers, takeover and handback (R6)
The `UserPromptSubmit` hook classifies every human turn in a sindri session:

| Turn | Condition | Effect |
|---|---|---|
| `answer` | A question is pending for this session | Recorded as the answer. The session continues. No takeover. |
| `takeover` | No question is pending (unsolicited input) | Session → `taken-over`: no reaping, no `send-keys`, lease paused, `status` shows `with you`. Also raises a drift signal. |
| `sindri` | The fixed `continue` token from the sindri | Inbox delivery (§6.1) |

- `sindri handback <item>` ends a takeover.
- A takeover during an open direction check pauses that check (its leg timers stop). Handback resumes it.
- **Timeout:** after `takeover.idleTimeout` (default 2 h) with no human input, the item gets a needs-answer
  notification: "hand back ITEM? (`sindri handback ITEM`)". It is never handed back silently.

### 9.4 Claims (M1)
- v1 has one active host (`hosts.active`). Other hosts refuse to tick.
- **The atomic claim is one deterministic ref**, `sindri/<user>/<item>`. It is created with a create-only ref
  update that fails if the ref exists. A relaunch reuses the same ref, and nothing else creates it. The
  same naming is used everywhere (§7.2, §15).
- Tracker markers are advisory, and are trusted only when written by the sindri's identity.
- Cross-host exclusion is best-effort. The ref makes duplicates fail fast.

## 10. Human-facing surfaces

### 10.1 Notification taxonomy and budget
| Kind | Interrupts | Title pattern | Action (as text) |
|---|---|---|---|
| needs-answer | yes; **never overflows** (queued with a count) | `[repo] ITEM: question (n)` | `sindri attach ITEM` (in-session) or `sindri answer ITEM` (job) |
| decide | yes; never overflows | `[repo] ITEM: direction check needs you` | `sindri decide ITEM` |
| needs-approval | yes; **never overflows** | `[repo] ITEM: approve <action>` | `sindri approve call:<id>` / `rule:<id>` / `profile:<hash>` |
| parked | batched hourly | `[repo] ITEM: parked: <code>` | `sindri why ITEM` |
| fyi | no | `[repo] ITEM: <milestone>` | — |
| digest | no; at `notify.digestTimes` | `sindri: N waiting on you` | `sindri status` |

- Notifications carry the item id, a scrubbed short title and the command as text (§8.6). They never carry
  item body text or debate content.
- `notify.maxPerHour` (default 4) caps parked and fyi; anything over goes to the digest.
- needs-answer, decide and needs-approval never overflow. They're bounded by a **global interrupt ceiling**
  `notify.maxInterruptsPerHour` (default 6). Above it, interrupts collapse into one "N items need you"
  notification that lists ids, and the ATTENTION file gets every entry (M7).
- Repeats of the same error code are deduplicated with a cool-down.
- Direction-check escalations count toward `direction.maxPerItem`, not the hourly cap.
- Voice: plain, first person ("I parked FRN-123 because the reproduce check passed on the base"), states
  the reason and the next step, never blames.

### 10.2 Question lifecycle
1. `asked`
2. Reminder after `questions.remindAfter` (default 4 h).
3. Auto-park after `questions.parkAfter` (default 24 h).

- `sindri snooze ITEM <dur>` delays a question.
- Questions inside a session are answered in the session. The hook records them as `answer`, not takeover.
- Questions from jobs are answered with `sindri answer ITEM`, which opens `$EDITOR` with the template:
  item, what's already decided, the question, options with a recommended default, sources.

### 10.3 CLI
| Command | Purpose | Empty / error text examples |
|---|---|---|
| `status` | Grouped: waiting on you, with you, parked, running, queued. Columns: item, title, step, round/cap, age, next action | "Nothing is waiting on you. 3 running, 1 queued." |
| `why ITEM` | Triage verdict, router inputs, Step rounds, direction-check threads and verdicts, shape signals with evidence, judge scores and thresholds, denials | `SND-ITEM-404 no such item: ITEM` |
| `ledger [--item --step --since]` | Raw ledger query | "No ledger rows match." |
| `start ITEM` | Manual dispatch | "Started ITEM in snd-…; attach with `sindri attach ITEM`" |
| `attach ITEM\|snd-id [--print]` | Attach, or print the command; prints the tmux detach key | `SND-SESS-404 session ended; see sindri why ITEM` |
| `answer` / `approve` / `reject` / `snooze` | Human responses. Ids are typed (`call:`, `rule:`, `profile:`, `dir:`) | "Recorded. ITEM resumes on the next tick." / `SND-ITEM-409 nothing pending for ITEM` |
| `decide ITEM [--pick a\|b\|other --note "..."]` | Shows the item's Task, the proposer position, the challenger objections and replies (untrusted strings fenced), the arbiter score and threshold, and a recommendation; records the pick | "No direction check is waiting on you for ITEM." |
| `hook log\|replay\|test` | Hook decisions per item; re-run a recorded decision; run a hook on a fixture (§5.3) | "No hook decisions recorded for ITEM." |
| `setup-host [--dry-run]` / `teardown-host` / `repo add <path>` / `repo onboard [<path>] [--template --no-build]` / `repo status [<path>] [--nudge]` / `index setup` / `image build <repo>` | Host, repo, index and session-image setup (§11.3) | dry-run prints every change; `teardown-host` reverses the manifest; `repo onboard`: "approval pending: sindri profile approve …" (exit 1) |
| `dashboard [--url]` | Start the dashboard, or print its tokened URL (§10.5) | — |
| `open ITEM` / `view ITEM` | Fetch the pushed branch into a host worktree / open the read-only live working files | — |
| `park` / `unpark ITEM [--hint]` | Park or resume with a hint | "Parked ITEM." / `SND-ITEM-409 ITEM is not parked` |
| `handback ITEM` | End a takeover | "ITEM handed back; resuming on the next tick." / `SND-SESS-409 ITEM is not taken over` |
| `pause` / `resume` | Global kill switch (also `AW_SINDRI_DISABLE=1`) | "Paused. Running sessions finish their current Step; nothing new starts." |
| `ack [ITEM]` | Clear one item's ATTENTION entries, or all of them | "Cleared 3 attention entries." / "Nothing to acknowledge." |
| `notify --test` | Send a test notification through every configured notifier | "Sent via macOS; wrote ATTENTION entry. (macOS can't confirm you saw it.)" |
| `observe` | Zero-config read-only run on the example profile (M14); records to the ledger when the profile is approved (exit 1 when it can't for a fixable reason) | "Observed N items (M open); would start K. … Nothing outside the ledger changed." |
| `doctor` | Health checks (§11.5), one line per check: `ok` / `warn` / `fail` + fix command | exit 1 on any warn, 2 on any fail |
| `tick [--dry-run]` | One tick, or print every action | "no-op: <reason>" on stderr when nothing to do |
| `shadow report` | Routing, direction-check and shape agreement per class, plus classifier accuracy | "Not enough shadow data yet (n/30)." |
| `rules show\|approve\|reject` | Eval-loop proposals | "No proposals pending." |
| `index build\|status\|query` | Code index (§6.2). `index status` lists every repo; a missing index is a row and exit 1, not an `SND-INDEX-404` abort. `index query` still uses `SND-INDEX-404` | `SND-INDEX-404 no index for <repo>; run sindri index build --repo <repo>` |
| `evolve init\|status\|check\|telemetry\|reflect\|correct\|proposals\|show\|reject\|tier\|compare\|stage\|publish\|adopt\|revert\|weekly` | Artifact registry, module eval suites, proposals and offline comparisons (§7.4, §7.7). `adopt`, `revert`, `channel promote` and `channel rollback` need a terminal | "No artifacts registered yet." / "Nothing to stage." / `SND-EVOLVE-006 adopting a prompt needs an interactive terminal` |
| `channel status\|promote\|rollback` | Stable and next installs of sindri itself (§7.7) | "No channels recorded." / `SND-EVOLVE-005 next has soaked 1 of 3 days` |
| `scope <brief.md \| linear:<project>> [--backtest --dry-run --sources]` / `scope runs` | Scoping and its backtest (§7.5); past runs | "No scope runs recorded." / `SND-SCOPE-025 refusing to write … inside a git worktree` |
| `repo add <path>` | Edits the profile only (then `profile approve`); each full `index build` creates or refreshes the mirror | — |
| `profile init\|validate\|explain\|migrate\|approve` | Profile tooling. `approve` shows the diff first | validate: "Profile valid." / errors with file, key path, fix |
| `scheduler install\|uninstall\|status [--dry-run]` | Scheduler | dry-run prints the unit/plist |

**Output contract:**
- Plain-text state words; state is never shown by color or glyph alone.
- `--json` on every read command.
- `NO_COLOR` and non-TTY output are honored.
- Exit codes: `0` ok, `1` attention needed, `2` error.
- Errors carry stable codes `SND-<AREA>-<NNN>`, with the areas seeded in `docs/sindri/errors.md`: PROFILE,
  LOCK, SESS, ITEM, GATE, DIR, SHAPE, INDEX, NOTIFY, BUDGET, CLI, LEDGER, SCRUB, TRACKER, SCOPE, SECRET, EVOLVE.

### 10.4 Silent-failure floor (R7)
- **Fallback chain:** macOS notification, then the `$AW_STATE_DIR/ATTENTION` file, then a banner in
  `status` output.
- **ATTENTION file lifecycle:**
  - Every interrupting notification also appends a line (timestamp, kind, item, command).
  - An item's entries clear automatically when that item's pending question, decision or approval is
    resolved. `sindri ack [ITEM]` clears the rest manually.
  - `status` and `doctor` show the count of unacknowledged entries.
- **Delivery limit:** macOS can't confirm a notification was seen. `doctor` therefore reports the time of
  the last `notify --test` and the oldest unacknowledged ATTENTION entry, and states this limit plainly. It
  doesn't claim delivery.

### 10.5 Dashboard and menu-bar badge
**Web dashboard** (`sindri dashboard`):
- Served by the sindri on `127.0.0.1` with a random token in the URL (rotated on restart, printed by
  `sindri dashboard --url`).
- No CORS, Host-header check, CSRF token on actions.
- Works over an ssh tunnel to a cloud box unchanged.
- It's a thin layer over the CLI's `--json` outputs. Actions call the same verbs, so there's no second
  source of truth.

| View | Shows | Actions |
|---|---|---|
| **Sessions** | Per item: title, step, state (`running` / `awaiting-direction` / `with you` / `verifying` / `parked`), round/cap, age, container CPU and memory against its limit, and a **live read-only pane preview** (`tmux capture-pane` snapshot every 5 s, scrubbed, untrusted strings escaped) | Attach (opens Warp via the §8.6 resolver), park, unpark, pause |
| **Waiting on you** | needs-answer, decide and needs-approval items, oldest first | answer, decide (both positions side by side), approve, reject |
| **Item** | The `sindri why` view: triage, pack sources, direction-check threads, shape evidence, verifier rounds, hook decisions, ledger timeline | open the pushed branch, view the live working files (`view/<item>/`) |
| **Fleet** | VM ceiling and use, heavy-job lock holder and queue, index age per repo, environment status per repo | pause / resume all |
| **Today** | Cost against budget, notifications sent and suppressed, ATTENTION entries, direction-check and shape-check counts | ack |

- **Live updates:** the dashboard polls the CLI JSON every 5 s. No websocket server is needed in v1.
- **Accessibility:** states are shown as text, not color only. The previews are text, not images.

**States and accessibility (M1, M4):**
- **Landing view:** Waiting on you when it isn't empty, otherwise Sessions. Every item has a deep link
  `/item/<id>`. An **All items** list (filterable by state) sits alongside the five views.
- **Per view:**

  | State | Behavior |
  |---|---|
  | Loading | Skeleton rows on the first load only |
  | Empty | The same sentence as the CLI ("Nothing is waiting on you. 3 running, 1 queued.") |
  | Error | Banner with the code and the CLI command that shows more |
  | Stale | "updated Ns ago". After 3 failed polls: "sindri not responding: run `sindri doctor`" |
  | Pane preview | Stale previews are greyed out with their age; `crashed`/`oom` show the last capture and the reason |
- **Action races:** an action that hits `SND-ITEM-409` (already resolved elsewhere) shows "Already
  resolved" and refreshes.
- **Token rotation:** the token is exchanged for an HttpOnly, SameSite=Strict cookie on first load and
  removed from the URL. A 401 after rotation shows "run `sindri dashboard --url`".
- **Headers:** a CSP of `default-src 'self'` (no inline scripts), and Origin plus Host checks on every
  action.
- **Pane previews:**
  - rendered as plain text, after ANSI escape stripping, bidi and control-character stripping, and HTML
    escaping
  - never rendered as HTML
  - kept inside `<untrusted>`-styled regions
- **Accessibility:**
  - Polling never moves focus, scroll position, or form input.
  - Updates are announced through one polite `aria-live` region ("2 items now waiting on you").
  - Every action is reachable by keyboard, in a visible tab order.
  - State is shown as text (not color alone).
  - Previews are labeled regions that can be skipped.
- **Approve parity:** approve shows the same diff the CLI shows before confirming.
- **Answer form:** the job-question answer form uses the §10.2 template.
- **Attach over an ssh tunnel:** shows the exact command to run locally instead of opening Warp.
- **Badge text form:** "3 running, 1 waiting" as the accessible title. Its states are `ok`, `paused`,
  `stopped` and `stale (no update > 2 min)`.

**Menu-bar badge** (macOS, `Badge` adapter, built-in: SwiftBar plugin):
- Shows `▶ running · ⚑ waiting on you` counts, refreshed every 30 s from `sindri status --json`.
- The dropdown lists waiting items. Clicking one opens the dashboard at that item.
- No actions run from the menu bar itself.
- Linux has no built-in badge; the dashboard and notifications cover it.

## 11. Developer contracts

### 11.1 Profile (H16, M20)
- **Format:**
  - `profile.yaml` (workplace) and `repos/<repo>.yaml` (per repo), both carrying `schemaVersion`.
  - Defined in Zod, exported as JSON Schema for editor autocomplete.
  - Example profile at `sindri/profile/examples/generic/`.
- **Tooling:**
  - `sindri profile init` scaffolds a profile in `mode: shadow`.
  - `sindri profile validate [--json]` runs by hand and in the profile repo's pre-commit. Errors give
    file, key path, expected type and a fix hint.
  - `sindri profile migrate --dry-run` covers schema upgrades. Deprecated keys warn for one minor
    version before they halt.
- **Growth:** the schema is strict (unknown keys are errors). New optional keys join `schemaVersion: 1`; renames and removals bump it and add a migration (Plan 2 amendment).
- **Precedence:** repo file > profile file > core default. `sindri profile explain <key>` shows the
  effective value and where it came from.
- **Data only:** the profile contains no code in v1. Adapters live in core.
- **Secret pointers:** schemes are `env:`, `file:`, `keychain:`, `op:`. The validator rejects values that
  look like secrets.
- **Size vocabulary (L2):** `XS | S | M | L | XL`, with the default rubric in core and overrides in the
  profile.

### 11.2 Adapters (H17)
```ts
type AdapterError = { kind: "retryable" | "rate-limited" | "fatal" | "not-found"; code: string; retryAfterMs?: number };
type Result<T> = { ok: true; value: T } | { ok: false; error: AdapterError };

interface Tracker {
  scan(scope: ScopeQuery, cursor?: string): Promise<Result<{ items: WorkItemRef[]; cursor: string }>>;
  read(id: string): Promise<Result<WorkItem>>;                 // includes authors for the trust gate
  comment(id: string, body: TemplatedBody, key: string): Promise<Result<void>>;  // idempotent on key
  setStatus(id: string, status: StatusKind): Promise<Result<void>>;              // idempotent
  assign(id: string, who: "self"): Promise<Result<void>>;                        // idempotent
  attach(id: string, url: string, title: string, key: string): Promise<Result<void>>; // idempotent on key
}
interface Source   { find(item: WorkItem): Promise<Result<SourceRef[]>>; fetch(ref: SourceRef): Promise<Result<SourceRecord>> }
interface Launcher { start(spec: SessionSpec): Promise<Result<SessionId>>; list(): Promise<Result<SessionState[]>>;
                     attachArgv(id: SessionId): string[]; sendContinue(id: SessionId): Promise<Result<void>>; kill(id: SessionId): Promise<Result<void>> }
interface Notifier { notify(n: Notification): Promise<Result<void>>; test(): Promise<Result<void>> }
interface Reviewer { threads(change: ChangeRef): Promise<Result<ReviewThread[]>> }   // author ids for trusted-bot matching
interface Scheduler{ install(dry: boolean): Promise<Result<string>>; uninstall(dry: boolean): Promise<Result<string>>; status(): Promise<Result<string>> }
interface Sandbox  { buildImage(repo: RepoRef, dry: boolean): Promise<Result<ImageRef>>; run(spec: ContainerSpec): Promise<Result<ContainerId>>;  // limits, mounts (§8.2), egress allowlist
                     exec(id: ContainerId, argv: string[], tty: boolean): Promise<Result<number>>; stats(id: ContainerId): Promise<Result<ResourceUse>>; remove(id: ContainerId): Promise<Result<void>> }
interface Environment { provision(item: WorkItem, change: ChangeRef | null): Promise<Result<EnvHandle>>;  // urls + short-lived creds via the broker
                        status(h: EnvHandle): Promise<Result<"pending" | "ready" | "failed">>; teardown(h: EnvHandle): Promise<Result<void>> }
interface Badge    { publish(summary: StatusSummary): Promise<Result<void>> }
```
`Source` in v1 is query-based: `find({keywords, asOf, limit})` returns scrubbed records with stable references; `fetch(ref)` arrives when a Step needs a single record.
- **`plan-file` Tracker (built-in):** reads `docs/superpowers/plans/*.md` task headings and checkboxes as work
  items, so a repo can be built from its own plans with no tracker account (§13.3 ring 0).
- **Selection:** the profile picks an adapter by `type:` (for example `tracker: { type: linear, ... }`).
  **`Environment` is chosen per repo** in `repos/<repo>.yaml`. Built-ins:
  - `none`
  - `preview`: request a preview through profile-named labels or CI, then wait for the deploy URL
  - `shared-host`: host services, with a database and key prefix per session
  - `per-container`: a full stack in the container

  Labels, hosts and wait conditions are profile data; the core contains no workplace specifics.
- **Registration:** a static registry in core.
- **Contract tests:** `adapterContractTests(name, factory)` is a reusable Vitest helper. Every built-in and
  fake adapter must pass it. It lives under `sindri/tests/contract/` as test code (`trackerContractTests` for the Tracker), not in `src/`.

### 11.3 Setup (H18)
- **Scheduler:** `sindri scheduler install [--dry-run]` writes the launchd plist; `--dry-run` prints it.
  Re-running is idempotent.
- **setup.sh:** `./setup.sh --with-sindri` is opt-in. It builds the package and runs the scheduler
  install, and it respects `--dry-run`, so the merge gate's setup dry-run stays clean.
- **Profile root resolution:** `--profile` > `$AW_PROFILE_DIR` > the `$AW_STATE_DIR/profile` symlink.
- **Host:** `sindri setup-host [--dry-run]`:
  - checks or installs the container runtime
  - sizes the VM (`sandbox.vmMemory`)
  - creates the sindri state zones
  - starts the key proxy and the heavy-lock broker
  - installs the dashboard and badge
  - writes a **manifest** of every change; `sindri teardown-host` reverses it
  - needs no admin rights and no new OS users (I5)
- **Session images:** `sindri image build <repo>` builds the repo's image. It contains the toolkit,
  skills, the session hook set, the existing safety hooks, Claude Code, tmux and the repo toolchain, and
  it is keyed by lockfile hash. Rebuilding one is a heavy job.
- **Repos:** `sindri repo add <path>` records the repo in the profile (it does not create the mirror). Every full `sindri index build` creates or refreshes the bare mirror at `$AW_STATE_DIR/sindri/mirrors/<repo>.git`; the first image build is queued later. `sindri repo onboard [<path>]` chains `repo add`, an approval check, the pre-commit hook and a first `index build --repo` (one try at the heavy-job lock). It never approves: until a human runs `sindri profile approve` at a terminal, it prints that command and exits 1. A SessionStart nudge (all providers, read-only, bounded) names a repo that isn't onboarded. `sindri repo onboard --template` sets `init.templateDir` (only when unset; never `core.hooksPath`) so new clones get a pre-commit hook that does nothing until the repo is approved.
- **Index dependencies (M6):** structure uses the TypeScript compiler API, not bundled tree-sitter grammars (other languages come later). Ollama and the embedding model
  (`index.embeddings.{enabled,url,model}`) are checked and pulled by `sindri index setup`; `uv` is installed by `./setup.sh --with-sindri`. graphify is installed and pinned by
  the same command and run in its network-less sandbox. `doctor` verifies each.
  *Amendment (2026-10-09): the graphify `graph.json` size cap is the profile key `index.graphMaxMB` (default 256, max 512), not a fixed 32 MB.*

### 11.4 Migration from existing orchestrators (H19, V2)
- The v1 Worker session runs `/bugFixOrchestrator <item>` inside the container, unchanged, through a
  **write shim** that is enforced by hooks, not by changing the skill.
  - Its outward writes are intercepted by the session image's PreToolUse hooks: `git push`,
    `gh pr create|edit|comment|ready`, tracker MCP write tools.
  - Each intercepted call is **denied with a structured request** written to `/spool/requests/<n>.json`,
    and the agent is told "queued as request R<n>; the sindri performs it after verification".
  - The request records kind, target, body or ref, and evidence paths.
  - The sindri executes requests through §8.1's allowlist and §8.5's verified push, in order. It
    writes each result back to the inbox, delivered with the next `continue`.
  - Requests outside the allowlist become needs-approval items.
- bugFixOrchestrator's own state (`$AW_DIR/bugfix/<slug>/`) and `ui-evidence` output live in the
  container volume.
  - The sindri exports them through the spool on Stop, so `sindri why` and the ledger can read
    them. They're reconciled read-only.
  - Items that already have a host-side bugfix state dir from a manual run are skipped.
- Profile evidence recipes are passed to the session as pack content. Adding multi-source evidence to
  bugFixOrchestrator itself is a follow-up spec.
- Cut-over to a native worker is a later spec. `/bugFixOrchestrator` is not deprecated, and manual host
  runs stay supported.
- A "When to use which" table (`/bugFixOrchestrator`, `/specToProvenPR`, `/autoplan`, sindri) goes in
  `docs/sindri/README.md` (L3).

### 11.5 Debug surface (H20)
- **Ids:** every ledger row, job and session carries `tickId` and `epoch`.
- **Logs:** `$AW_STATE_DIR/sindri/logs/tick-YYYYMMDD.jsonl`, rotated after 14 days.
- **`doctor` checks:**
  - lock holder and stale locks
  - epoch sanity
  - duplicate scheduler installs
  - profile valid and approved
  - each adapter reachable
  - container runtime reachable, VM headroom, key proxy and lock broker alive
  - the bypass probe (§8.5) fails as it should
  - secret pointers resolve (without printing them)
  - notifier test send
  - sindri gate registered and failing closed (a deliberate denied probe)
  - state dir on local disk
  - boot-id readable
  - ledger schema version
  - budget headroom (`budget.perItem` and `budget.perDay` are optional before rollout step 3a; an unset budget is reported `ok` with "not enforced before step 3a")
  - every §5.3 hook registered, each answering a recorded probe payload with the expected decision
  - code index age per repo, and which providers are enabled (structure, embeddings, graphify)
  - local embedding model reachable
  - unacknowledged ATTENTION entries and the last `notify --test` time
  - direction-check mode per checkpoint, shadow precision, caps used today

### 11.6 Session images and repo onboarding (V8)
`repos/<repo>.yaml` declares the image:

```yaml
image:
  base: node:22-bookworm            # or a team base image
  toolchain: [bun@1.2, node@22]     # pinned
  install: "bun install --frozen-lockfile"
  lockfiles: [bun.lock]
  extraPackages: [postgresql-client]
session:
  memory: 6g                        # per-repo override (M6); monorepo type-checks need more than 3 GB
  cpus: 3
environment:
  type: preview                     # none | preview | shared-host | per-container
  preview:                          # profile data, never core
    requestLabel: "<label name>"
    urlFrom: "pr-comment:<bot login>"   # or ci-output, deploy API
    readyTimeoutMin: 20
    testIdentity: "op:<vault>/<item>"   # low-privilege, short-lived via the broker
```

- **Image key** = hash of the base, toolchain, lockfiles, install command, sindri version, hook-set
  version, Claude Code version and toolkit version. Any change rebuilds the image, as a heavy job.
- **The image contains:** Claude Code, tmux, the toolkit and skills, the session hook set and the existing
  safety hooks, Serena, the repo toolchain, and the preinstalled dependencies.
- **`sindri repo check <repo>`** builds the image (or reuses it) and starts a throwaway container. It
  verifies, in order:
  1. Claude Code starts with the subscription token.
  2. The hooks are registered.
  3. The install is cached.
  4. The Environment adapter can provision once (dry-run where the adapter supports it).
  5. Rulesets are present on the remote (M8).
  6. The bypass and forged-verdict probes fail as they should.

  It prints one line per check with a fix command.
- **`per-container` environments** declare services (`services: [postgres:16, redis:7]`) and need
  `session.memory` of at least 8 g. `repo check` refuses a smaller setting with `SND-ENV-020`.
- Worked examples live in `sindri/profile/examples/`: `preview` (web app with PR previews), `none`
  (library), and `shared-host`.

### 11.7 Container debugging (V7)
| Command | Does |
|---|---|
| `sindri logs ITEM [--follow] [--hooks] [--proxy]` | Container stdout and stderr, hook decision log, proxy log for the session |
| `sindri shell ITEM` | Opens a root-less debug shell in the container as the session user. Recorded as a takeover (§9.3), and blocked while the Stop gate is in `verifying`. |
| `sindri inspect ITEM` | Container state, limits, OOM events, mounts, image key, environment reference, last bundle hash |
| `sindri retain ITEM` / `release ITEM` | Keep a stopped container for debugging, or let it be reaped |

- **States** (in `status`, the dashboard and `why`), in addition to §10.3's: `starting`, `crashed`
  (non-zero exit), `oom` (killed for memory), `image-building`, `env-pending`, `env-failed`.
- A `crashed` or `oom` container is kept for 24 h (`sandbox.retainFailedHours`) before reaping. The item
  is parked with the code and `sindri logs ITEM` as its next action.
- **Error areas added:** `SANDBOX`, `IMAGE`, `ENV`, `PROXY`, `HOOK`, `VERIFY`.

## 12. Portability
| Concern | v1 (macOS) | Linux (seams in v1, built-ins later) |
|---|---|---|
| Scheduler | launchd | systemd user timer with `loginctl enable-linger`; cron fallback |
| Launcher | containers (OrbStack or Docker) with tmux inside | Docker or Podman with tmux inside (no VM) |
| Notifier | macOS notification → ATTENTION file | ntfy (authenticated, high-entropy topic, M11), chat DM |
| Boot id | `kern.bootsessionuuid` | `/proc/sys/kernel/random/boot_id` |
| Claude auth | subscription token (`claude setup-token`) injected per container (§8.2) | same; API-key mode optional |
| Hooks | — | Fix `context-guard.sh` to prefer `stat -c%s` on GNU (detect, don't guess); add hooks to the Linux test script |
| Tests | local | `scripts/test-linux.sh` runs core and hook tests in an Ubuntu container (M22) |

## 13. Rollout
0. **Measure and decide** (M3, M7, M14):
   - Turn the audit extraction into `scripts/transcript-audit/` (L4).
   - **Dedupe resumed and forked copies.** Resume and fork copy earlier human turns into the new
     transcript with the same timestamp; the audit keys turns on (timestamp, text) and counts each once.
   - **Calibrate patterns against adjudicator labels** on a 400-turn sample (`scorer audit --label 400`).
     A pattern is `metric-grade` only when its precision and recall both have a Wilson lower bound of at
     least 0.6 and the label has at least 10 positives. Others stay floor counts.
   - **Measure wrong-approach corrections** from the builder's own transcripts: design vs process, after
     code was written (`editsBefore`), per 30 days.
   - **Decision rule:** if design wrong-approach corrections after code average under 8 per 30 days
     (point estimate; the audit also prints a 95% interval),
     Approach and Drift direction checks are not a step-3a deliverable: they are not built in 3a, even in
     shadow, and step 3a effort goes to the largest process-correction and shipping-direction sources
     instead. If the interval straddles 8, the decision is provisional and the audit is re-run with a larger
     `--label` (a value above the turn count labels every typed turn). Re-check monthly with a manual
     `scorer audit --since 30d --label 400` (the weekly job stays unlabeled, so it is free and offline). If
     a later check reaches 8 or more, the checks are added in shadow first (§6.1).
   - Record baselines and **set the §2 targets from them**.
   - Measure the share of the last 60 days of in-scope items that would have been XS, clear and trusted.
     - If it is at least 10%, keep `autoStartMaxSize: XS`.
     - If it is under 10%, pilot `S` with 100% verification, or skip auto-small and stay in `assist`.
   - Measure **subscription quota per item** on a sample of manual sessions (W2).
1. **Host foundation, scoping first:**
   - Profile schema and tooling, lock and fencing, scrubber, judge `--providers` flag, done-gate fix
     (dogfooded, §7.6).
   - **Reuse ports, phase 1** (§16): the skills scoping and evidence need first.
   - Code index on the host: structure, clones, dependencies, local embeddings, graphify.
   - **Scoping harness** (§7.5), with `--backtest` on the motivating project.
     **First value: scope maps, plus a measured recall number.**
   - Self-evolution loop (§7.4) for the scoping harness and the ported skills, offline blinded eval only.
   - **Artifact registry over the whole toolkit repo** (§7.7): module eval suites wired to existing tests,
     plus hook false-positive telemetry from every session's transcripts, and stable/next channels. The
     first self-proposals target known defects, for example done-gate's bare-word false positives.
2. **Shadow, plus the container spike in parallel:**
   - `sindri observe`, shadow triage and packs, historical replay, dashboard (Sessions, Waiting) and
     badge.
   - **Container spike (go/no-go),** with criteria and fallback in §13.1. It runs as one heavy job at a
     time.
   - Exit when per-class routing agreement is at least 90% over at least 30 items **and** the spike's
     verdict is in.
3a. **Assist, patterns:** `sindri start`, packs and re-injection, turn classification, the tool gate,
   the Stop pattern gate, the worker → ship Steps, the write shim, two-phase verify, notifications.
   - Approach and Drift direction checks run in shadow, and are a deliverable only if the step-0 decision rule
     says design corrections are frequent enough. Otherwise they are not built in 3a, and the Approach and
     Drift rows of §13.2 read "—" in the 3a column until a monthly re-check passes the rule.
   - Shape signals are recorded only.
   - It runs in containers if the spike passed, otherwise on the host with no auto-start (§13.1).
   - The self-evolution loop adds the online shadow A/B.
3b. **Assist, shape enforcement:** once each index layer reaches a precision of at least 0.7 over at
   least 30 signals on real commits:
   - All index layers enforce.
   - The Shape direction check enforces.
   - The sindri-side verifier is authoritative.
4. **Auto-small:** requires a container isolation pass, `doctor` passing §8, the step-2 threshold, and
   confirmed plan terms for unattended subscription use (or API-key mode) (W2).
5. **Scoping in the loop:** Scoping and Scope-expansion direction checks enforce. Issue creation from scope
   maps moves to self-adopt once a project type meets its backtest bar.
6. **Self-evolution, full:** the self-adopt tier runs across all artifacts (§7.4).

### 13.1 Effort sizing and spike fallback (V9)
These are rough, for one builder with agent help. Every step ends with a usable increment.

| Step | Contents | Size | First value delivered |
|---|---|---|---|
| 0 | Audit scripts, baselines, XS share, quota per item | S (2–3 days) | Measured targets |
| 1 | Profile tooling, lock and fencing, scrubber, judge flag, done-gate fix; reuse ports phase 1; host code index; **scoping harness + backtest**; offline self-evolution | L (2–3 weeks) | **Scope maps and a recall number on the motivating project** |
| 2 | Shadow triage and packs, replay, dashboard and badge; **container spike** in parallel | M (1–2 weeks) | Dashboard of what the sindri would do; spike verdict |
| 3a | Start, packs, worker → ship, write shim, two-phase verify, notifications, shadow direction checks (only if the step-0 rule makes them a deliverable) | L (2–3 weeks) | **Copy-paste dispatch, retyped ship direction and handoffs gone** |
| 3b | Shape enforcement | M (1 week) | Enforced code-shape checks |
| 4 | Auto-small | S | Unattended XS items |
| 5 | Scoping direction checks enforce; issue creation | S | Self-scoped projects |
| 6 | Full self-evolution | M | The harness improves itself |

**Container spike: pass/fail criteria (W1, W2, W8 added).** Every one must pass:
1. Interactive Claude Code runs in a container with **no real token inside it**. The container has
   `ANTHROPIC_BASE_URL` pointing at the model proxy and a per-session dummy credential. The proxy swaps in
   the subscription bearer per request (W1). If subscription auth can't work through a base-URL proxy,
   the fallback is API-key mode for containers.
2. The model/egress proxy and the lock broker run as **sidecar containers** on the internal network, and
   the host sindri reaches them over one authenticated TCP channel. No host unix socket is
   bind-mounted (W8).
3. Attach from Warp works.
4. Bundle export and two-phase verify work on one real item.
5. Warm start is under 60 s on the web-app image.
6. A 4-session fleet stays stable for 24 h within the VM limit.
7. Quota use per item is measured, along with the effect on the human's own sessions under the
   `quota.reserveForHuman` share (W2).

**If the spike fails:**
- Steps 3a and earlier proceed **on the host** with no auto-start. Shadow and assist are cleared without
  isolation, provided the live view stays off and the sindri never fetches bundles on the host.
- Step 4 waits for a working isolation mechanism (another runtime or a Linux cloud box).

### 13.2 Checkpoints by step
| Checkpoint | 2 | 3a | 3b | 4 | 5 |
|---|---|---|---|---|---|
| Approach | — | shadow | shadow | enforce when §6.1 bar met | enforce |
| Drift | — | shadow | shadow | enforce when bar met | enforce |
| Shape | replay only | record only | **enforce** | enforce | enforce |
| Scoping | — | — | — | — | shadow → enforce |
| Scope expansion | — | — | — | — | shadow → enforce |

### 13.3 Bootstrapping ladder: when each piece switches on and starts building the rest
Sindri builds Sindri. Every piece is switched on **as soon as its plan merges**, first on this toolkit repo
(**ring 0**: the backlog of Sindri plans), then on the profile's workplace repos (**ring 1**). From the
moment a piece is on, it does its job on the rest of the build, so later plans are built with progressively
less human direction. Every plan ends with a **"Turn it on" task**:
1. Run the switch-on command.
2. Record the evidence in the PR.
3. Use the piece on the next plan's work.

**Ring 0 backlog:** the remaining plan files and their task checkboxes, read through a `plan-file`
`Tracker` adapter. It is generic: it reads `docs/superpowers/plans/*.md` tasks as work items, so the
toolkit needs no tracker account to build itself. The adapter ships in Plan 2.

| Piece (plan) | Switch on when | Switch-on command (ring 0) | Evidence it's on | Starts doing for the rest of the build | Ring 1 |
|---|---|---|---|---|---|
| done-gate claim detection (P1 T1) | P1 merges | `scripts/install-done-gate.sh --provider claude` (reinstalls the hook copy in `~/.claude/hooks/`) | The `done-gate` false-positive rate in the next weekly audit drops to ~0 | Stops false "Claiming done" blocks in every builder session from then on | Same hook, immediately |
| `judge --providers` (P1 T2) | P1 merges | none; used by callers | `judge --providers jev why <id>` works | Lets P2+ direction checks and verifiers pin providers (Anthropic + Jev) | Same |
| `scorer audit` (P1 T3–T7) | P1 merges | `scorer audit --since 60d --label 400` once (baselines, calibration and the step-0 decision); then weekly and unlabeled via the scorer launchd job (`--since 7d --label 0`) | `~/.agentic-workflow/audit/baseline.md`; weekly `summary.json` | **Measures the build itself:** steering turns per merged Sindri PR are the first metric the ladder must move down | Baselines for workplace repos |
| Profile + ledger + lock + CLI skeleton (P2) | P2 merges | `sindri profile init --ring0` (toolkit profile, `mode: shadow`); `sindri doctor` | `doctor` all `ok`; ledger file exists | Every plan task and each change to it is recorded in the ledger on each `observe` run; sessions are linked to items from rollout step 2 | `sindri profile init` in the private profile repo |
| Scrubber (P2) | P2 merges | `sindri scrub --install-pre-commit` in this repo | A committed fixture secret is refused | **Guards this public repo:** no secret- or identifier-shaped strings land in commits from any build session (names and other workplace prose still need review) | Pre-commit in workplace repos where wanted |
| `plan-file` tracker + `sindri observe` (P2) | P2 merges | `sindri profile init --ring0 --plans '*-sindri-plan-*'`, then `sindri observe` (hourly via launchd) | Lists the remaining plan tasks with rule-based sizes (model triage from step 2) | Gives a live, ordered backlog of the rest of Sindri | `observe` on the real tracker |
| Code index, record-only shape signals (P3) | P3 merges | `sindri index setup && sindri index build`, `sindri repo add .`, `sindri scrub --install-pre-commit` (upgrades the hook to v2, which records shape signals), an hourly `sindri index build --quick` and a nightly full build (launchd); a degraded switch-on (embeddings or graph unavailable) is allowed and the PR evidence lists the layers that are down | `index status` fresh; shape signals in the ledger for builder commits | **Calibrates shape thresholds on Sindri's own commits** from P4 onward; flags reinvention while P4/P5 are built | `repo add` for workplace repos (record-only) |
| Scoping harness (P4) | P4 merges | `sindri scope docs/superpowers/specs/2026-10-07-sindri-design.md --section 13 --sources file,code --out docs/superpowers/scopes/` | Scope map file plus `--backtest` recall on the motivating project | **Scopes every later Sindri plan before it's written:** the plan writer starts from the scope map | Scope new workplace projects at creation |
| Ported `reflect` / `correct` / `eval` + artifact registry (P5) | P5 merges | `sindri evolve init`; `sindri evolve check --changed`; `sindri evolve reflect --pr <n>` on each merged Sindri PR (the weekly job does it for the last 14 days); `sindri evolve weekly` (Mondays 07:30, launchd); then `sindri evolve publish` on a branch | Registry lists every module; first reflect run recorded; staged proposals published as plan tasks that `sindri observe` lists; `evolve status` reports the merge rate | **The build improves its own tools:** proposals for the skills and hooks used to build Sindri arrive as PRs (human merges) | Same loop over ring-1 artifacts |
| Shadow triage + dashboard/badge (step 2) | Step-2 plan merges | `sindri dashboard`; SwiftBar badge install | Dashboard shows ring-0 items and what Sindri would do | Visible queue for the remaining build | Shadow on the real tracker |
| `sindri start`, packs, worker → ship, notifications (step 3a) | Step-3a plan merges | Remaining Sindri tasks are started with `sindri start <plan-task>` instead of pasted prompts | Next Sindri PR opened by the ship Step | **Sindri dispatches and ships its own remaining plans** (human starts, human merges) | Assist mode for workplace tickets |
| Shape enforcement (3b) | Per-layer precision bar met on ring-0 commits | `profile: shape.enforce: true` for ring 0 | A refused commit with evidence | Enforces code shape on Sindri's own code first | After ring 0 holds for 2 weeks |
| Auto-small (4) | Isolation pass + bars met | `mode: auto-small` for ring 0 | An XS ring-0 task goes from triage to draft PR unattended | Small Sindri fixes build themselves | After ring 0 holds for 2 weeks |
| Self-evolution, full (6) | Step-6 bars met | `sindri evolve resume --tier self-adopt` | An adoption plus an auto-revert observed | The toolkit improves continuously | Same |

**Rules:**
- A piece that isn't switched on within one working day of its plan merging is a ring-0 work item of its own.
- No later plan is started by hand if the pieces before it could start it.
- Every ring-1 switch-on waits until the same piece has run on ring 0 for at least one plan's worth of work, with no open defects.

## 14. Testing
- **Unit tests** in Vitest with in-memory SQLite and fake adapters: tick, router, Step runner (pass,
  fail-then-pass, exhausted rounds, budget breach, escalation), ship state machine, question lifecycle,
  notification budget and dedup.
- **Singleton and fencing:**
  - two concurrent ticks: one is a no-op
  - stale lock, dead pid: taken over
  - stale lock, other boot id: taken over
  - live owner: respected
  - stale epoch write: rejected
  - non-active host: refuses to tick
- **Liveness:**
  - a stalled pane is relaunched exactly once (kill-before-relaunch)
  - a permission dialog is not a stall
  - a sleep gap extends leases
  - a taken-over session is never reaped
- **Gate:**
  - every "always denied" row is denied
  - malformed policy denies
  - `AW_JUDGE_CHILD` is ignored
  - argument constraints hold (wrong item id, non-draft PR)
- **Injection fixtures:**
  - an untrusted-author item never auto-starts
  - `<untrusted>` fences survive pack synthesis
  - a stated fix from an untrusted author is quoted, not placed in Task
  - hidden content is stripped
- **Notifier:** an item title containing shell metacharacters never reaches argv; session-name validation
  rejects bad names.
- **Scrubber:** fixtures for each secret and identifier pattern, at ingest, pack, ledger and egress.
- **Hooks (§5.3):**
  - every row is driven with a recorded payload and its decision asserted
  - the `sindri-hook` wrapper turns a crash, a timeout, a non-2 exit, and a missing inner script into an
    explicit deny with a `9xx` code
  - `continue:false` ends the turn
  - the Stop pattern gate blocks at most twice, then allows and records
  - host Claude sessions have no sindri hooks
  - the container user can't modify the `managed settings`
  - edit detection fires on `sed -i`, `tee` and script writes, not only on Edit/Write
  - the git pre-commit hook catches `commit -a`, `-C` and aliases; `--no-verify` is caught by the verifier
- **Live hook probe** (heavy, also in `doctor`): the §5.3 semantics table against the installed Claude
  Code version.
- **Isolation:**
  - from a session container, all of these fail: reading the HMAC key or the ledger; reaching any host
    other than the egress proxy;
    writing to `/sindri`, `/mirror` or `/workspace/base`; pushing to the remote; writing to the
    tracker; reaching host loopback services; attaching another session's tmux
  - a tampered spool entry is ignored
  - a bundle that fails verifier checks is never pushed, even when session-side checks passed or were
    skipped with `--no-verify`
  - the verifier runs no git hooks, filters or attributes from the bundle
- **Two-phase verify:**
  - a bundle modified after the snapshot is rejected (hash mismatch)
  - a bundle whose tests write a fake pass file never yields a pass (forged-verdict probe)
  - a pack bomb and an oversized bundle are refused within limits
  - phase 2 runs only against the provisioned environment
  - undraft is blocked unless both phases passed on the unchanged head
- **Write shim:** each intercepted write (`git push`, `gh pr create/comment/ready`, tracker writes)
  becomes exactly one spool request, is executed only through the allowlist, and has its result
  delivered on the next `continue`.
- **Spool reader:** symlinks, FIFOs, sockets, oversized files and schema-invalid JSON are quarantined;
  the tick never blocks on a FIFO.
- **Live view:** off by default; when on, dotfiles, `.vscode` and executables are never copied.
- **Auth:** the subscription token reaches only the model upstream through the proxy; the scrubber
  blocks the token pattern in outward writes.
- **Containers:** CPU and memory limits hold; the heavy-lock lease expires for a killed container; the
  warm-start target is met on a warm image; `teardown-host` restores the manifest.
- **Direction checks:**
  - each checkpoint fires from its hook or sindri trigger, never from an agent tool call
  - forged, unsigned or wrong-role turns are rejected
  - the arbiter reads only HMAC-verified turns
  - verdict delivery through the inbox and `continue`
  - `awaiting-direction` is never reaped
  - same-model fallback is recorded as `skipped:no-diverse-model`
  - providers outside `providers.allowed` are never called
  - per-item and per-day caps
  - shadow mode never blocks
- **Shape checks:**
  - fixture repos with an exact clone, a renamed clone, a second-case near-clone, a duplicate dependency, a
    graph-neighbor reinvention and an over-budget diff each produce the right signal
  - each enforced-outcome row in §6.2 ends (cap exhausted, index unavailable, budget exceeded): no
    deadlock
  - an exemption expires on file change and after 30 days
  - evidence fences untrusted strings
- **Offline index:** with the network blocked, embeddings and graphify still build; with an egress attempt
  injected, the graphify adapter fails closed; Prism flags are verified; denied paths and symlinks are
  never read; the per-worktree overlay matches a full rebuild.
- **Index:** `adapterContractTests` for structure, embeddings and the graphify adapter; incremental rebuild
  matches a full rebuild.
- **Turn classification:** an answer to a pending question isn't takeover; unsolicited input is; the
  takeover timeout notifies.
- **Unattended e2e** (heavy suite): a fixture item goes from shadow through assist start, approach check,
  commit with shape check, PR draft, to Stop gate, with no prompt appearing (M5).
- **Eval loop:** non-human-origin turns are excluded, forbidden-field rules are rejected, the holdout split
  is enforced.
- **Adapters:** `adapterContractTests` for every built-in and fake.
- **Real-process tests** (tmux, macOS notifier, the unattended e2e) are tagged `heavy` and excluded from
  the default `npm test`. They are a merge-gate item, run serially (`npm run test:heavy`), per the
  one-heavy-job rule. `scripts/test-linux.sh` is also a merge-gate item for changes under `sindri/`
  and `config/hooks/` (M15).
- **Merge gate:** add `sindri` to the AGENTS.md typecheck and test lists.

### Docs deliverables (M19)
- `docs/sindri/README.md` (including "when to use which")
- `docs/sindri/profile.md` (reference generated from the schema)
- `docs/sindri/adapters.md`
- `docs/sindri/errors.md`, generated from the code registry. A test fails when a code is used but not
  documented, or documented but unused (M8).
- `docs/sindri/hooks.md`, `docs/sindri/index.md`, `docs/sindri/host-setup.md` (containers,
  model/egress proxy, teardown)
- `docs/sindri/linux.md` (with the Linux adapters)
- ledger schema in `planning/ERD.md`
- CLI in `planning/API_CONTRACT.md`
- updates to `planning/ARCHITECTURE.md` and `planning/TESTING.md`

## 15. Second user (M4)
- A teammate runs `sindri profile init` from the generic example and gets their own state dir and
  ledger.
- Claims are namespaced by user (`sindri/<user>/<item>`, §9.4), so two humans on the same tracker
  never claim each other's items. Their scopes come from their own profiles.
- Shared recipes can be copied between profile repos. A shared team profile layer is out of scope for v1.

## 16. Prior art and reuse
The harness reuses existing work wherever it can. Every adopted piece becomes a **registered artifact** in the
self-evolution loop (§7.4, §7.7), pinned by commit and content hash and evaluated like native code. The full
port map is in `plans/sindri/reuse-map.md` (reuse spike, 2026-10-08).

| Priority | Source (license) | Port as | Spec part |
|---|---|---|---|
| 1 | pstack `eval` plus `arena` blinding rules (MIT) | Native: leak linter, shuffled labels, a single pairwise judge with skill names hidden | §7.4 offline blinded evaluation |
| 1 | pstack `correct` (MIT) | Native: its "fix at the highest level that works" ladder becomes a proposal source | §7.4 |
| 1 | pstack `reflect` (MIT) | Native: three reviewers plus a synthesizer; its approval gate is replaced by the §7.4 tiers | §7.4 |
| 2 | pstack `interrogate` (MIT) | Prompts become the challenger template | §6.1 |
| 2 | pstack babysit/shipping playbooks (MIT) | Rules ported (frontier-only, flake classification, patch-id re-verify); the merge and landing steps are dropped because the human merges | §7.3 |
| 2 | pstack `create-verification-skill`, `maintain-verification-skill` (MIT) | Native, feeding repo onboarding and evidence recipes | §11.6, evidence |
| 2 | pstack `why` (MIT) | Native: multi-source evidence queries | evidence recipes (H6) |
| 3 | pstack `tdd`, principles, `unslop`; `cli-for-agent`; cursor-team-kit `verify-this` and similar (MIT) | Pinned external pack, via the existing fetch pattern | skills |
| 3 | thermos (MIT) | Prompts become judge questions and review rubrics | review lens |
| 3 | agent-compatibility (license to be confirmed) | Advisory repo-readiness signal, run sandboxed and pinned | §11.6 |
| ref | Cyrus (license inconsistent: the LICENSE file says Apache-2.0 boilerplate, package.json says MIT; ask the maintainers before copying any code) | Reference only: Linear tracker, fake-tracker test pattern, trust gate, egress proxy | `Tracker` adapter, §8.3 |
| ref | orchestrate (MIT, needs the Cursor SDK) | Schemas as reference | §6 |
| ref | Conductor (conductor.build) | UX reference | §10.5 |
| ref | Superplane (license to be confirmed) | Reference only: workflows configured as recipes (stages, events, approvals), not code | §17.28 recipes |
| skip | ralph-loop | A self-declared completion loop; breaks invariant 3 | — |
| skip | swarm / arena runners / poteto-mode | Depend on Cursor `Task` subagents and on the agent choosing in-session; breaks invariant 10. Only the blinding rules are kept | — |
| skip | container-use (Apache-2.0) | Its agent-facing MCP tools break invariant 10 and the interactive-session model | — |

**Mapping Cursor constructs to Claude Code:**

| Cursor construct | Becomes |
|---|---|
| `pstack-models.mdc` model map | Profile `models.roles`, Anthropic-only by default (Grok defaults removed) |
| Cursor `Task` subagents | Bounded sindri jobs |
| `AskQuestion` | needs-answer notification |
| `agent-transcripts/` | `~/.claude/projects`, human-origin turns only |

**Open items:**
- Cyrus license.
- A pin policy for fetched packs (commit plus content hash; upgrades go through §7.4 as `pack-upgrade` proposals).
- agent-compatibility's license and its `npx @latest` network use (pin, don't float).

## Appendix: audit method
- Extract every non-machine human turn from `~/.claude/projects/**/*.jsonl` (excluding sidechains, tool
  results, and injected text) with context: active skills, context-guard state, token count, and the
  preceding agent message.
- Drop duplicate turns: resumed and forked sessions copy earlier human turns into the new transcript with
  the same timestamp, so turns are keyed on (timestamp, text) and counted once.
- A model adjudicator (run with no tools; its output is schema-validated) classifies a sample of turns. The
  first pass was a stratified sample of 450 turns.
- Floor counts across all turns using exact-phrase patterns. A later run on a second user's transcripts,
  labeled by a model, showed low recall (a correction pattern caught 5 of 45 wrong-approach corrections) and
  low precision for image attachments as defects (22 real of 80). Patterns are therefore calibrated against
  adjudicator labels before any metric depends on them (§13 step 0), and the image pattern is named
  `image_turn`.
- Rollout step 0 moves the scripts into `scripts/transcript-audit/` so the audit can be reproduced.
