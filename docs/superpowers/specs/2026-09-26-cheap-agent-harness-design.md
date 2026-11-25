# Cheap agent harness — design

- **Date:** 2026-09-26
- **Owner:** Joi
- **Status:** design approved in brainstorm. `/archReview` rounds 1 (F1–F16), 2 (N1–N8) and 3 (R1–R3, SOUND) fixes are folded in below. Pending spec review.
- **Arch reviews:** `~/.agentic-workflow/joi-fairshare-agentic-workflow/plans/20260926-152232-arch-review-cheap-agent-harness.md`, `…/20260926-153319-arch-review-cheap-agent-harness-r2.md`, `…/20260926-154500-arch-review-cheap-agent-harness-r3.md`
- **Lives in:** `~/personal/agentic-workflow` (portable across dev boxes via `setup.sh`)
- **Vault pointer:** `~/personal/work-vault/10-knowledge/toolset/cheap-agent-harness.md`

## Why

Claude Code agent teams are expensive, and the owner is pulled in too often. Measured from 7 days of transcripts (`~/.claude/projects/<work-repo-dir>`, 78 sessions, 586 agents, to 2026-09-25):

| Signal | Value |
|---|---|
| Context tokens re-sent to the model (cache reads) | ~23.7B |
| Share in calls whose context was already > 200k / > 400k | 76% / 44% |
| Share held by the top 10% of agents by call count | 69% |
| Startup prefix (the first call's context, re-sent on every call) | **23.7%** of all context tokens |
| First-call context, median | main 78k · subagent 59k |
| All file reads / re-reads of an already-read file / re-read by another agent | 7.1M / 2.1M / 1.2M |
| Teammate messages delivered to orchestrators | ~1,280/week: 707 `idle_notification`, 558 text, 19 terminate |
| Joi's messages / plain "continue, yes, ok" / corrections / interrupts | 801 / 101 / 32 / 74 |
| Browser QA via Playwright MCP (45 days) | 447 clicks; 0.7M tokens of browser output; **195M** tokens of context re-sent (~437k per click) |

Conclusions:

- **Duplicate file reads are not the cost.** The cost is **large contexts × many calls**, plus a heavy **fixed startup prefix**.
- **Idle pings wake orchestrators whose context is huge.**
- **Browser QA was expensive because a ~437k-context model drove every click.**

Billing is both per-token and usage-limited, so this optimizes total tokens and peak usage.

## Goals and non-goals

**Goals**

1. Fewer tokens per merged PR.
2. Fewer orchestrator wakes that nothing acts on.
3. Fewer interruptions for Joi.
4. Fewer "done" claims that fail review.
5. UI evidence on every UI PR, without an expensive model driving a browser.

**Non-goals**

- Replacing the coding model.
- Cloud services.
- Changes to the web-app repo's code. Only its `.claude/` skill set changes, through profiles.

## Architecture

```
agentic-workflow/
├── judge/            TS package + CLI: typed cheap decisions (rules fast path -> per-content-class model chain)
├── scorer/           TS CLI: reads ~/.claude/projects/**, daily cost/involvement/quality report
├── config/hooks/     wake-gate.sh · outbox-flush.sh · context-guard.sh · done-gate.sh · scope-gate.sh ·
│                     external-write-guard.sh · turn-origin.sh · judge-health.sh · prism-context.sh
├── config/agents/    lean-coder · lean-researcher · lean-reviewer · qa-runner (tools allowlist, pinned model)
├── config/profiles/  web-app · ios · personal (which skills + MCP servers a repo gets)
├── skills/ui-evidence/ + skills/judge/
└── setup.sh          builds judge/scorer, installs hooks/agents/profiles, merges settings with jq via
                      merge_hook(event, id, entry): entries tagged "id":"aw:<name>", replaced by id (F5).
                      It never removes entries it doesn't own (e.g. prism-route), and prints a diff of hooks by owner (N7)
```

Per-box state lives outside git, under `~/.agentic-workflow/`:

- `judge/config.json`: questions on or off, thresholds. Re-read on every call.
- `judge/decisions.sqlite`: every decision, its model, content class, confidence, latency and undo; approved briefs keyed by the `Agent` call's `tool_use_id` (F4, N3); the outbox (F1). WAL mode, `busy_timeout=2000`. A failed write fails open and counts as a judge failure.
- `judge/sessions/<session_id>.json`: per-session turn state (`auto_continued_at`). Deleted when older than 24 h (N2).
- `digests/`: file digests shared across agents.
- `scorer/reports/`: daily reports.

Keys (`TYPESAFE_API_KEY`, `ANTHROPIC_API_KEY`) come from the environment or keychain, never the repo.

Stack follows the repo: Node ≥ 20, TypeScript strict, Zod, Vitest with 100% coverage, better-sqlite3.

### Foundation A — `judge`

- **CLI:** `judge <question> < input.json` returns `{decision, confidence, model, reason_code, id}`. It is also exposed as an MCP tool.
- **Questions** are typed modules. Each owns:
  - input schema
  - allowed outputs (enum)
  - prompt
  - per-question threshold
  - deterministic pre-rules
- **Order of evaluation:**
  1. Deterministic rules and fast paths. These decide alone when they can.
  2. The model chain for the input's content class (below). Each class lists its models in order, then falls back to rules.
  3. Models available:
     - Jev (`jev-1.13.0`; text only; ≤ 255 options per Choice; 32k tokens of state; 1,200 req/min; $0.042/MTok input, output free)
     - Haiku (anything with images, and code-bearing classes)
     - Optionally a local Ollama model (Prism repair 5a). It is allowed only for questions with a ≥ 5 s budget, such as the scorer and rule check, and only after a warm-check passes. It is never used on hot-path hooks (N6).
- **Inputs are always small and focused:** a message, a brief, a diff hunk, a screenshot. Never an agent's context.
- **Content-class routing (arch review F2).** Each input declares a class, and the chain is chosen per class. The class is recorded on every decision.
  - `code`, `diff`, `brief`, `transcript`, `message-meta` -> Jev first, falling back to Haiku (`claude-cli`). TypeSafe has passed vendor review for company code (2026-09-28), so Jev is no longer restricted to `message-meta`.
  - `image` -> Haiku only. Jev is text-only, so this exclusion is a capability limit, not a policy gate.
- **Untrusted input (F6).** Messages and diffs are data, not instructions. Outputs are enums only. Deterministic pre-rules decide every safety-relevant outcome, and the model can never override them.
- **Fast paths (F7).** Obvious cases skip the model entirely: JSON acks, short messages, `Explore`/`claude-code-guide` dispatches. p95 latency is tracked per question. The status line warns above 1.5 s.
- **Below threshold:** escalate, never guess. `PreToolUse` hooks can only allow or deny; there is no "ask". So escalation means one of:
  - **deny + reason + `judge approve <id>`**, when blocking is safe;
  - **pass through + notice**, when blocking would cost more than the mistake.

### Foundation B — `scorer`

A daily report, plus `scorer --since`. Every lever must move its number or be removed.

- **Cost:** tokens per merged PR, startup prefix per agent type, wakes, turns over 200k.
- **Involvement:** Joi's messages per PR, "continue"s, corrections, interrupts.
- **Quality:** review rounds, Bugbot/CI failures after "done".
- **Judge:** decisions, undos (labeled mistakes), error rate per question, failures and fallbacks.
- **Startup accounting:** tokens injected per session and per prompt, by source: skills, MCP names, `CLAUDE.md`, SessionStart hooks, and the per-prompt `prism-route/on_prompt.py` hook (N8).

A later version adds an observer pass that reads the worst runs and proposes changes to briefs or settings as a PR.

### Visibility and tuning (applies to every lever)

- **Hooks fail open,** but failures are loud. `judge` never blocks work by breaking. It surfaces in four places:
  1. a status line segment (`judge ✓ n` / `⚠ n failures` / `✗ down`)
  2. a `systemMessage` notice on every fallback
  3. a SessionStart health check
  4. the top of the scorer report
- **Enforcement starts on day one. There is no shadow mode.** Every action shows a notice with `judge undo <id>`.
- **Live tuning:** config is re-read per call, and a `/judge` skill tunes it in-session. `judge why <id>` explains a decision. Undos feed the error rate.
- **Notices can later drop to a daily summary,** per question, once its undo rate is near zero. Joi decides.

## Levers

### 1 — Wake gating

**A. Sender-side gate.** A `PreToolUse` hook on `SendMessage` calls `judge wake-gate` and returns one of:

- `send`: result, failure, blocker, question, or plan change. Delivered immediately.
- `batch`: progress with nothing to act on. The hook blocks the send and queues the message in the outbox, keyed by the orchestrator's session id. **There is no relay process or socket (F1).** A message posted to the orchestrator's socket by a process that isn't its own child is held for approval in bypass mode. Instead, queued items ride on the next outgoing `send` from any teammate to that orchestrator: the hook rewrites that message via `updatedInput` and appends a digest. Once the oldest queued item is 10 minutes old, the next `SendMessage` to that orchestrator is upgraded to `send` with the digest attached.
  - **Flush before idle (N1, R1).** `outbox-flush.sh` is a **`TeammateIdle`** hook. Teammates are separate sessions, and `TeammateIdle` exit 2 "sends feedback and keeps the teammate working" (agent-teams docs). If this teammate still has queued items, it exits 2 with "send your queued progress as one message now", and that message is classified `send`. The hook exits 0 once the teammate has no queued items, so it can't loop. For in-process subagents, the same check runs from their `Stop` hook, if one fires; the probe in rollout step 0.5 confirms whether it does. Without this, a teammate that batches and then goes idle would strand its items, while the orchestrator gets an `idle_notification` without them.
  - Outbox items expire after 2 hours (the orchestrator is probably gone). Expiries are counted by the scorer (F12).
  - If digests go stale in practice, v2 adds a relay launched by the orchestrator's own `SessionStart` hook. It runs as that session's child and authenticates with `CLAUDE_CODE_MESSAGING_TOKEN`.
- `drop`: a pure ack. Blocked, and the sender is told not to ack.

**B. Idle pings.** `idle_notification` is emitted by Claude Code itself, and no hook can suppress it. `TeammateIdle` exit 2 only keeps the teammate working; it doesn't remove the ping. The fix is in how work is dispatched:

- One-shot work goes to background subagents, which report back once.
- Finished teammates are shut down, unless the brief marks them `keep_alive`. Keep-alive teammates hold context for feedback from Joi or the orchestrator.
- The scorer tracks idle pings per session.

### 2 — Context engineering

**A. Startup diet.** Target: main startup 78k → < 45k, subagent 59k → < 30k.

1. Dedupe the 14 skills installed twice: camelCase in `agentic-workflow`, kebab-case in web-app `.claude/skills`. The kebab-case copy wins inside web-app.
2. Skill profiles per repo, using native settings: `skillOverrides` (hides user-level skills) and `enabledPlugins` (toggles plugins), written by `setup.sh --profile <name>` into the project's `.claude/settings.local.json`. No symlink sets. The web-app profile excludes the iOS and design packs.
3. Lean agent types, each with a `tools:` allowlist and a pinned sonnet or haiku model. They are the default in dispatch briefs.
4. MCP servers per project. Turn off `xcodebuildmcp` in web-app.
5. Trim SessionStart injections (banners, repeated text).

**B. Short contexts.**

- The context guard is a `PostToolUse` hook. It measures the size of `transcript_path`. Past ~200k tokens it injects `additionalContext`: "write a handoff to `~/.agentic-workflow/digests/<task>.md` and return your result". The orchestrator re-dispatches a fresh agent with that handoff.
  - A hook cannot start a new agent itself.
  - Main sessions and `keep_alive` teammates only get a notice.
- Briefs point to shared digests in `~/.agentic-workflow/digests/` rather than lists of raw files to read.

### 3 — Evaluator gates

1. **Scope gate.** `PreToolUse` on `Agent`, calling `judge brief-scope`. It returns `ready`, `missing[]` or `needs_design`.
   - It checks for a goal and acceptance criteria, the location of the work, how to prove it, and the limits (no DB tests, one heavy job at a time, keep-alive).
   - `missing` blocks the dispatch.
   - `needs_design` goes to Joi.
   - **An approved brief is saved** in `decisions.sqlite`, keyed by the `Agent` call's `tool_use_id`: goal, acceptance criteria, proof command. This is what the done gate checks against (F4).
     - `agent_id` doesn't exist yet when `PreToolUse` runs. `PostToolUse` on `Agent` sees the same `tool_use_id`. A `SubagentStart` hook records the `tool_use_id` -> `agent_id` mapping for teammates (N3).
   - **Exempt:** read-only agent types (`Explore`, `claude-code-guide`, `lean-researcher`) and skill-internal dispatches (F13).
2. **Done gate.**
   - Main agent: a `Stop` hook. Exit 2 when a "done" claim is missing evidence required by the saved acceptance criteria (tests, PR, screenshots).
     - A main session has no dispatch brief. There the check uses a task file written by the `/judge brief` skill, if one exists. Otherwise it only checks "claims done with no evidence at all".
     - When `stop_hook_active` is true, the hook exits 0, so it never loops.
   - Subagents: `SubagentStop` can't block, so a `PostToolUse` hook on `Agent` annotates the result before the parent reads it.
3. **Rule check.**
   - Deterministic first: lint, type-check, the docstring-length linter, and `rpc-schema:check` on migration diffs.
   - Then the model: a checked-in rule list drawn from `AGENTS.md` and `best-practices`, answered `violated / fine / n/a` per rule and per hunk. Content class `diff`, so Haiku until TypeSafe is approved.
     - Rules are pre-filtered by file glob.
     - One request per hunk carries every applicable rule as parallel questions (F9).
   - Then intended behavior: do the tests and the change match the acceptance criteria?
4. **Auto-continue.** The `Stop` hook calls `judge ask-check`. If the next step was already authorized by the brief or plan, exit 2 with "continue".
   - Deterministic rules forbid auto-continue for merge, delete, force-push (and force push), external writes (Linear, Slack, PR comments) and anything irreversible. A bare "push" is not on the list: the model decides whether a branch push was authorized, and pushes to the base branch stay blocked by `block-push-main.sh`.
   - **External-write guard (F3).** `external-write-guard.sh` is a `PreToolUse` hook on `gh pr merge|comment|review`, Linear `save_*`/`create_*`/`delete_*`, and Slack `send_*`. If the current turn began with an auto-continue, it denies with "needs Joi". Branch pushes and `gh pr create` were removed from the matched set so auto-continued runs don't stop to ask the user (pushes to the base branch stay blocked by `block-push-main.sh`). The guard is needed because `ask-check` sees only the agent's question, not its next tool calls.
     - **Turn-state lifecycle (N2).** The `done-gate` sets `auto_continued_at` in `judge/sessions/<session_id>.json` when it auto-continues. `turn-origin.sh`, a `UserPromptSubmit` hook, clears it on Joi's next real prompt.
       - **It ignores machine-delivered prompts (R2).** A prompt whose text starts with a teammate or cross-session wrapper (`<teammate-message`, `Another Claude session sent a message`) never clears the flag. The docs don't say whether `UserPromptSubmit` fires for delivered messages, so this filter makes the behavior correct either way. The guard keys on the parent `session_id`, so subagents spawned during that turn inherit it. That assumes a subagent's hooks see the parent's `session_id`; the probe in rollout step 0.5 confirms it. Teammates are separate sessions and track their own flag. Files older than 24 h are deleted.

### 4 — UI evidence

Trigger: before pushing a branch whose diff touches UI files, or on request.

1. **Plan.** A `qa-runner` (sonnet, lean) gets the diff, the route map and the `verify-web-app` skill. It writes a short Playwright script: route, role, steps, the expected visible state after each step, desktop and phone.
2. **Run with no model.** Headless via Bash against the local stack. Records video, a screenshot per step, and a trace. Returns only a JSON summary.
3. **Repair.** `judge` picks the element from enumerated candidates, at most 2 tries. After that the step is reported broken.
4. **Visual check.**
   - **With a reference** (Figma via the Figma MCP, or a Claude mockup): a per-step comparison.
   - **Without a reference:**
     1. Before/after against main, same script and viewports.
     2. Sloppiness lint from the page:
        - overlapping or clipped elements, truncated labels
        - phone overflow
        - values not in `packages/tokens`
        - misaligned edges
        - missing hover/focus states
        - unresolved empty or loading states
     3. Haiku rubric critique: alignment, spacing, typography hierarchy, visual weight, and consistency with the main-branch screenshot. Returns `looks right / looks off / sloppy`, with crops.
5. **Publish.**
   - Evidence goes to the PR's Linear issue as attachments.
   - A PR comment carries the per-step table, with `unclear` / `looks off` / `sloppy` first, and links to the attachments.

Guardrails:

- It takes two locks: the per-session heavy-job lock, and a **box-wide** `~/.agentic-workflow/locks/qa-stack.lock`, because `:3000` is shared by every session (F14). It checks that the stack is healthy before planning a script.
- It checks the local DB's provenance: seeded or scrubbed data only. Otherwise evidence stays local, and only file paths go in the PR comment (F15).
- A failed Linear upload still posts the PR comment, with local paths and "upload failed", and retries on the next push (F16).
- If Haiku is unavailable, visual checks are marked `unchecked`, never `looks right` (F8).
- It runs against the local stack only, never dev or prod.
- The done gate treats a UI PR without evidence as not done.

## Prism repair

Joi chose to repair Prism rather than remove it (2026-09-26). Measured over 14 days:

- 163 `session_bootstrap` calls loaded **no project and no prior context**.
- `session_save_ledger` failed 9 of 10 times with `context_not_loaded`.
- `prism_infer` was used 0 times.

Findings:

1. **False startup warning.** `config/hooks/prism-context.sh` probes `http://localhost:7180/health`, which returns 404. The dashboard itself is up (`/` returns 200). Every session starts with a spurious "unreachable" warning. Fix: probe `/`, or whatever endpoint the dashboard documents.
2. **No Auto-Load Projects.** `~/.prism-mcp/prism-config.db` has no auto-load project set, so bootstrap loads nothing. Saves then fail, and the generated `CLAUDE.md` block forbids the only other way to load context (`session_load_context`). Fix: configure Auto-Load Projects in the dashboard, with developer name and context depth. **Project names must equal each repo's `REPO_SLUG` as the skills derive it (N4):** `<work-repo>` and `joi-fairshare-agentic-workflow`, not `agentic-workflow`. `setup.sh` ends with a check that lists the dashboard's projects next to each tracked repo's slug and flags any mismatch.
3. **Wrong repo path.** `repo_path:<work-repo>` points at `~/.agentic-workflow/<work-repo>/reviews`, not `~/repos/<work-repo>`. Fix in the dashboard.
4. **Three versions at once.**
   - The CLI is npm `prism-coder` 20.14.0 (`/opt/homebrew/bin/prism`).
   - The MCP server runs 20.18.0 from an **ephemeral npx cache path** (`~/.npm/_npx/ecf8cc3fafff496b/...`) registered in `~/.claude.json`. It breaks whenever npm cleans its cache.
   - The latest is 20.21.15.

   Fix: `prism update` for both, then `prism connect` with Claude Code closed (Prism requires it) so the registration points at a stable install. Then `prism autoupdate enable`.
5. **Local-first worker has no model.** Ollama has only `nomic-embed-text`, and no text model, so `prism_infer` / `session_task_route` -> `claw` can never run. The "Prism local-first orchestration" block in `~/.claude/CLAUDE.md` is instructions for a worker that doesn't exist. Fix, pick one:
   - (a) pull a small local text model and test it as a free tier inside `judge`, for questions with a ≥ 5 s budget only (see Foundation A, N6);
   - (b) switch the policy off via `prism connect` options so the block stops loading.

   Decide after the (a) test.
6. **The skill preambles contradict `CLAUDE.md`.** 43 `agentic-workflow` skills call `session_load_context` (forbidden by the generated block) and `session_save_ledger`. Fix: the preambles drop the `session_load_context` step. They reuse the `conversation_id` **and the project** from bootstrap's `<prism_session conversation_id="…" projects="…">` line, rather than re-deriving the slug (N4). If a save fails, the skill says so once and continues.
7. **Visibility.** It follows the same rule as `judge`: a `prism` status line segment, the SessionStart check (fixed per finding 1), and a Prism section in the scorer report (bootstrap loaded a project? saves succeeded?).

Done means:

- A fresh session in **both** web-app and `agentic-workflow` shows its project and last summary at bootstrap.
- A skill's closing `session_save_ledger` succeeds in both.
- The scorer shows 0 Prism save failures for a week.

**Installer order (N7).** Run `prism connect` first, then `setup.sh`. `prism connect` owns `~/.claude.json`, its `CLAUDE.md` generated blocks and its `prism-route` hook. `setup.sh` touches only `aw:*` hook ids.

## Rollout order

0. Prism repair findings 1-4 (config and upgrades, mostly Joi at the dashboard, plus one `prism connect` with Claude Code closed).
0.5. **Hook-input probe (R3).** A logging-only hook on `UserPromptSubmit`, `Stop`, `SubagentStop`, `TeammateIdle`, `SubagentStart` and `PreToolUse`/`PostToolUse` (`Agent`, `SendMessage`) records raw inputs to `~/.agentic-workflow/probe/` for one working day. It confirms:
   - whether `UserPromptSubmit` fires for delivered messages, and what it sees;
   - `session_id` / `agent_id` for subagents vs teammates;
   - whether a subagent's `Stop` fires;
   - that `tool_use_id` matches between `PreToolUse` and `PostToolUse`.

   Levers 1 and 3 aren't built until the probe confirms their assumptions.
1. `scorer`, to capture the baseline.
2. `judge` core + health visibility.
3. Lever 2A, the startup diet. Setup-only, and the biggest guaranteed saving.
4. Lever 1.
5. Lever 3.
6. Lever 4.
7. Lever 2B.

Each step ships with its scorer metric, and is kept only if the metric moves.

## Risks

- **Hook latency.** A `SendMessage`, `Agent` or `Stop` call that no fast path settles runs a model call. Budget 1.5 s per hook; fail open beyond it.
- **Jev availability.** Early access. Haiku covers it.
- **False blocks erode trust.** Visible notices, `undo`, error rate per question.
- **Claude Code changes hook semantics.** Pin tested versions. The SessionStart health check detects a hook that stops firing.
- **`setup.sh` merge gaps (F5).** `Stop` is added only when absent (`setup.sh:253`), and existing installs already have the `WINCH` Stop hook. Only the `Bash` matcher gets idempotent replace (`setup.sh:290-295`). `merge_hook` replaces both, with unit tests against fixtures of real `settings.json` files.
- **The scorer depends on undocumented transcript JSONL (F10).** Parsing is incremental, with per-file byte offsets in sqlite. A schema probe reports "unknown format" loudly instead of emitting zeros. Tests use golden fixtures from anonymized real lines.
- **Evidence is private data.** Screenshots come from the local stack. Evidence goes to Linear only when the local DB passes the provenance check. Otherwise it stays local (F15).

## Open questions

- Does TypeSafe support several questions in one request (the `parallel_questions` cookbook)? This matters for F9.
- When should TypeSafe be approved for company code (content classes `code` / `diff` / `brief`)?
