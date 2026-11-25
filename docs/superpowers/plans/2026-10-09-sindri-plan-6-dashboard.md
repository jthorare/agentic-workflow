# Sindri Plan 6: The Dashboard (fleet collector, `aw:fleet` hook spool, local dashboard, attach, menu-bar badge) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> **Amendment 2026-10-10 (MCP bridge retired):** the toolkit's MCP bridge was retired before this plan ran
> (`mcp-bridge/` and its `bridge.db` are gone). When executing, drop the bridge fleet source everywhere it
> appears: Task 7's `sindri/src/fleet/bridge.ts` and `tests/fleet-bridge.test.ts`, the `bridge` switch in
> `fleet.sources`, the `bridge` need kind and its needs-you category, `AW_BRIDGE_DB`, and every test row, fixture or
> doc line that names it; and leave `mcp-bridge` out of the merge-gate package loop. The spec's needs-you list no
> longer has the bridge item.

**Goal:** Give the builder one place to see every agent on the machine and what needs them (spec §1). This plan builds:
- **`sindri fleet`**, a read-only collector. It covers Claude Code, Codex and Cursor sessions (including ones Sindri didn't start), Sindri's own jobs, the onboarded repos, gh pull requests and bridge messages. It gives exactly one state per session and one sorted "needs you" list.
- **The `aw:fleet` hook**, installed for every provider. It appends one short line per event to a spool, which is the authoritative "waiting on you" signal.
- **`sindri dashboard`**, a loopback-only HTTP server over `sindri … --json`. It has five views, an attention signal you can notice without reading, and a fixed action list in which terminal-gated commands are only ever shown.
- **Attach**, which brings a live session's host to the front and resumes only sessions that have ended. It never resumes a live one.
- **A SwiftBar menu-bar badge.**

**Architecture:**
- **`sindri/src/fleet/`** (collector):
  - **Adapters** (`adapters/claude.ts`, `codex.ts`, `cursor.ts`, `sindri.ts`): each discovers sessions or jobs from the provider stores. They read only bounded file tails, never write, and each runs inside its own error and time boundary (`collect.ts` `runSource`).
  - **`spool.ts`**: turns hook payloads into one-line events, appends them, rotates by size and summarises them per session.
  - **`state.ts`**: pure rules from signals to one state, plus the subagent roll-up.
  - **`needs.ts`**: the needs-you aggregator, fed by `decisions.ts` (ledger, profile, channels), `gh.ts` (PRs, cached 2 min), `bridge.ts` (unread count, read-only) and `acks.ts`.
  - **`command.ts`**: `sindri fleet [--json] [--provider P] [--state S] [--badge]`, `sindri fleet hook <provider>` and `sindri fleet ack <key>`.
- **`sindri/src/dashboard/`** (server):
  - `tokens.ts` (one-time tokens, sessions, CSRF), `security.ts` (headers, Host and Origin checks), `actions.ts` (read map, safe-action allowlist, terminal-only commands), `server.ts` (`node:http` handler and loopback listener), `attach.ts` (resolver and launcher) and `command.ts` (`sindri dashboard [--url] [--port N] [--open PATH]`).
  - The server holds no state of its own: every view is a cached `sindri … --json` run, and every action is a checked argv run with no shell.
- **`sindri/ui/`** (browser): static HTML and CSS plus TypeScript compiled by `tsc`. `model.ts` holds the pure view logic and is unit-tested with 100% coverage; `app.ts` is the DOM glue, tested in real Chromium. No framework, no CDN, no inline script or style.
- **`config/hooks/fleet.sh`** (the `aw:fleet` hook, run directly for Claude Code and through the Codex and Cursor adapters) and **`config/swiftbar/sindri-fleet.30s.sh`**. `scripts/install-sindri.sh` installs both.

**Tech Stack:** TypeScript 5.7 strict, ESM, Node >= 20.11, Vitest 2 (v8, 100%), Zod 3, better-sqlite3 13 (read-only opens), `node:http` (no web framework), browser code compiled by `tsc` (DOM lib, ES2022 modules), Playwright `^1.48.0` (the dependency `skills/ui-evidence` already uses for its real-Chromium tests) for UI behaviour, bash 3.2 + jq for the hook and the installers, and the SwiftBar plugin format.

**Spec:** `docs/superpowers/specs/2026-10-09-sindri-dashboard-design.md` (approved, binding: every section). Background: `docs/superpowers/specs/2026-10-07-sindri-design.md` §10.5, whose security and accessibility rules stay unless the new spec replaces them. Where the two disagree, the new spec wins.

**Depends on:** Plans 2–5 merged. This plan uses these existing pieces:
- **Plan 2:** `Deps`, `SystemProbe.pidStartTime`, `GitRunner`, `success`/`failure`/`fromError`, `SindriError`, `parseFlags`, `makeScrubber`, `openLedgerReadOnly`, `approvedProfile`/`approvalState`, `loadProfile`/`resolveProfileRoot` and `ulid`. `pidStartTime` runs `ps -o lstart=` under `TZ=UTC LC_ALL=C`, which is the exact string Claude Code writes as `procStart` (checked on this machine: `Fri Oct  9 19:23:16 2026` for both).
- **Plan 3:** `ProcessRunner`, `realProcessRunner`, `heavyLockState`/`heavyLockDir`/`withHeavyLock`, `openIndexReadOnly`/`indexPath`/`meta`.
- **Plan 5:** `escapeInvisible` (`evolve/invisible.ts`, the shared sanitizer from #82), `textBlocks`/`toolNames` (`evolve/transcripts.ts`), `readChannels`/`canPromote`, `ghRepoOf`/`ghJson`, `ringZeroRepo`, `STATUSES`.

The source-based privacy work runs in a parallel plan. This plan doesn't touch `privacy.*`, `src/evolve/privacy.ts` or their docs. Every collector string goes through `src/fleet/text.ts` (`cleanLine`/`cleanText`), which is the single point where source-based rules plug in when they land.

## Task list

| # | Task | Commands it adds |
|---|---|---|
| 1 | Fleet types, limits, the text sanitizer, the bounded tail reader, profile keys `dashboard.*` and `fleet.*` | |
| 2 | The process table and the Claude Code adapter | |
| 3 | The Codex and Cursor adapters | |
| 4 | Sindri jobs: job records from `runCli`, the heavy-lock queue, repo status | |
| 5 | The `aw:fleet` hook, the spool, per-provider install | (`fleet.sh`, `install-sindri.sh --fleet-hook`) |
| 6 | State rules (pure, table-driven) and the subagent roll-up | |
| 7 | The needs-you aggregator: decisions, failures, gh PRs (cached), bridge unread, acks | |
| 8 | The `sindri fleet` command and the collector | `sindri fleet`, `fleet hook`, `fleet ack` |
| 9 | Dashboard server security, the read map, the action allowlist | `sindri dashboard [--url] [--port N] [--open PATH]` |
| 10 | The dashboard UI: five views, deep links, polling, states, the attention signal | |
| 11 | Attach: resolver and launcher, never resuming a live session | (`POST /api/attach`) |
| 12 | The SwiftBar badge and its installer step | `sindri fleet --badge` |
| 13 | Docs, the end-to-end evidence run, and the merge gate | |

## Spec decisions in this plan

These resolve the spec's open points; each one is restated in `docs/sindri/dashboard.md` in Task 13.

1. **The Claude result record.** The JSONL transcripts on disk have no `result` record: that's an SDK stream message, and T3 reads it from the SDK. So the Claude adapter derives `terminalReason` from the transcript:
   - `[Request interrupted by user for tool use]` gives `aborted_tools`, and `[Request interrupted by user]` gives `aborted_streaming`;
   - a `system/turn_duration` or `system/stop_hook_summary` line gives `completed`;
   - a synthetic assistant line with `isApiErrorMessage: true` gives `error`.

   If a `{"type":"result", "terminal_reason": …}` line ever appears, it is honoured the same way.
2. **429 versus 529.** These give `limited`: 429, "usage limit" or "rate limit" text, or `error: "rate_limit"`. A 529, an overload or any other API error gives `failed`. `error: "authentication_failed"` (or "Login expired") gives `auth` while the process lives, and `failed` with "login expired" once it's gone.
3. **The status file's `waiting`.** `~/.claude/sessions/<pid>.json` carries `status: busy | idle | waiting`. With no spool event, `waiting` shows as `approval`, with the reason "the session says it is waiting on you". A spooled question turns it into `input`.
4. **When a pending event is resolved.** A spooled permission, question or plan event stays pending until one of these is newer than it:
   - a later spool event (`Stop`, `UserPromptSubmit`, `SessionEnd`, `PreCompact`, or another pending kind);
   - a transcript output line (an assistant line or a tool result).

   There is no `PostToolUse` hook: it would fire on every tool call.
5. **The Codex app-server.** An observer can't reach the app-server of a session it didn't start: the app-server speaks JSON-RPC over the owning client's stdio. So the adapter takes `{waitingOnApproval, waitingOnUserInput}` through a `CodexStatusProbe` port, and the v1 probe returns `null`. Codex approvals come from the `PermissionRequest` hook in the spool, and from `*_approval_request` rollout events when present. The state rules honour the flags (tested), so a later probe plugs in without any rule change.
6. **Tokens and cost.** `scorer live` reads no prices (`scorer/src/live-snapshot.ts`). For live Claude sessions:
   - tokens come from `scorer live --json` (`usage.contextTokens + usage.outputTokens`), cached 60 s, with at most 20 refreshes per pass;
   - dollars come from the newest `cost-state` line in the transcript (`totalCostUSD`).

   Codex tokens come from the rollout's newest `token_count.info.total_token_usage.total_tokens`. Cursor shows neither. All figures are per session ("this session"), and Today sums the sessions updated since local midnight.
7. **Bridge unread.** `GET /messages/unread` marks messages as read, so the collector never calls it. Instead it counts `messages WHERE recipient = <profile user> AND read_at IS NULL` in the bridge's SQLite file, opened query-only (`AW_BRIDGE_DB`, default `<toolkit repo>/mcp-bridge/bridge.db`).
8. **`fleet.sources` has six switches:** `claude`, `codex`, `cursor`, `sindri`, `gh` and `bridge`. The spec names the provider adapters; the needs-you sources get switches too, so a builder without gh or the bridge can turn them off.
9. **The heavy-lock queue.** Waiters weren't recorded anywhere. Now `withHeavyLock` writes `<lock>.queue/<pid>-<ulid>.json` while it waits and removes it when it stops waiting. Waiters that use `config/lib/locks.sh` directly don't appear, and the docs say so.
10. **Job results.** Index builds, `observe`, the weekly job, `evolve check`, scope runs, `shape reconcile` and `doctor` didn't keep their outcomes. Now `runCli` records each run of those commands under `$AW_STATE_DIR/sindri/fleet/jobs/`: start, end, exit code and one scrubbed summary line (the weekly job keeps up to 8 lines, one per step). A record with no end whose process is gone (pid or start time) is `crashed`.
11. **Acknowledge and "seen".** `sindri fleet ack <key>` is a new safe action that writes `$AW_STATE_DIR/sindri/fleet/acks.json`.
    - It hides a failure item until that failure happens again.
    - It marks a `ready` session's "done" marker as seen (key `done:<provider>:<id>`). The dashboard sends it when the builder opens that agent.

    Park and unpark aren't in the action list: no CLI verb exists for them until the Sindri runner (rollout step 3a).
12. **Tokens for the menu bar.** SwiftBar can run a command but can't mint a token, so a new `sindri dashboard --open <path>` mints a one-time URL for a deep link and opens it. How tokens work:
    - `sindri dashboard` writes `$AW_STATE_DIR/sindri/dashboard/server.json` (mode 0600), holding the pid, its start time, the port and an HMAC key made fresh on every restart.
    - `--url` and `--open` mint `nonce.expiry.hmac` tokens with that key.
    - The server accepts each nonce once, within 10 minutes.
    - A restart means a new key, so every old token and session cookie stops working.
13. **Over an ssh tunnel.** The Host check needs the same port number on both ends (`ssh -L 7190:127.0.0.1:7190 box`). The server knows it's running remotely from `SSH_CONNECTION` in its environment, and attach then shows the local command instead of launching anything.
14. **"Notifications sent" (Today).** Sindri sends no notifications of its own yet, so the Today view counts the OS notifications this dashboard tab has fired.
15. **A new Warp tab running a command.** Warp's URL scheme can't run a command in a new tab directly. So the launcher writes one launch configuration, `~/.warp/launch_configurations/sindri-attach.yaml`, and opens `warp://launch/sindri-attach.yaml`. Warp's folder isn't a provider store, and adapters still never write into provider folders.
16. **Needs-you order.** Items sort by category first, in the spec's list order (waiting sessions, decisions, failures, PRs, bridge), then by state priority, then oldest first.
17. **Sessions outside onboarded repos.** They're grouped under "Other", by their git top-level, or by their cwd when there's no git.

## Global Constraints

- **Language and checks.** Node >= 20.11, TypeScript 5.7 strict, ESM (Node16), no `any`, no `/* v8 ignore */`. New sindri code has 100% coverage. The coverage excludes this plan adds, each a thin wiring file exercised elsewhere:
  - `src/fleet/proc-real.ts` (ps and lsof; smoke-tested in `tests/real.test.ts`);
  - `src/dashboard/io-real.ts` (the signal wait and the browser opener);
  - `ui/src/app.ts` (DOM glue, run by real Chromium in `tests/browser/`).

  `ui/src/model.ts` is inside the coverage gate. If `test:coverage` names a line or branch that a task's listed tests miss (for example, a fallback for a status record with an empty `name`), add a focused test to that task's test file before committing. Never add an ignore comment.
- **Read-only toward providers.** No code writes into `~/.claude`, `~/.codex` or `~/.cursor`. `sindri fleet` opens the ledger query-only and never writes it. It writes only under `$AW_STATE_DIR/sindri/fleet/`, and `sindri dashboard` only under `$AW_STATE_DIR/sindri/dashboard/`.
- **Bounded.** Each pass reads:
  - at most 1 MiB from the end of each transcript;
  - at most 200 sessions per source, newest first;
  - for at most 2 s per source;
  - only files changed in the last 24 h, except for sessions whose process is alive.
- **Contained.** Every source runs inside `runSource`. A throw becomes an `SND-FLEET-001` entry for that source alone, and a source that runs out of budget is marked `truncated` with `SND-FLEET-002`.
- **Scrubbed and escaped.** Every agent-written string is scrubbed, capped and escaped in the collector, through `cleanLine` or `cleanText` (profile scrubber, then `escapeInvisible`). The browser renders it with `textContent` only, and escapes again with `visible()` as defence in depth.
- **Private data.** Code, fixtures and docs hold no workplace names, paths or text. Fixtures are synthetic (`/Users/dev/acme.web`, "Fix the login form"). The end-to-end screenshots of real sessions stay under `$AW_STATE_DIR/sindri/evidence/`: they are never committed, never attached to the PR, and never posted.
- **Security** follows spec §6 exactly:
  - it binds `127.0.0.1` only;
  - a taken port is an error that names the port and `dashboard.port`;
  - one-time tokens are exchanged for an `HttpOnly`, `SameSite=Strict` cookie;
  - every request needs a `Host` of `127.0.0.1:<port>` or `localhost:<port>`, and every action also needs a matching `Origin` and a CSRF token;
  - no CORS headers are sent;
  - the CSP is `default-src 'self'`, with no inline script or style and `frame-ancestors 'none'`, plus `Referrer-Policy: no-referrer`;
  - actions are a fixed argv list with checked arguments, run with no shell;
  - terminal-gated commands are shown, never run.
- **The hook** never blocks, never prints to the host and always exits 0.
- **The CLI output contract** (main spec §10.3): state words first, no colour, `--json` on every subcommand, exit codes 0/1/2, and a closing `Next: <command>` line whenever there is one. `sindri fleet` exits 1 when something needs you.
- **Error codes** are registered in the task that first uses them, because `tests/errors.test.ts` checks both directions. After every change to `errors.ts` or `schema.ts`, run `cd sindri && npm run gen` and commit `docs/sindri/errors.md`, `docs/sindri/profile.md` and `sindri/schema/*.json`.
- **One heavy job at a time on the box.** Another session may hold the heavy lock.
  - While you iterate, run only the task's own vitest files.
  - Run `npm test && npm run typecheck && npm run test:coverage` once per task, right before its commit.
  - The Playwright files are part of `npm test` (Task 10 onward).
  - Never run two of these at once, and skip `npm install` unless a dependency is missing. The one new dependency is `playwright` in Task 10.
- **Tracking and commits.** Tick each step's checkbox in this plan file in the same commit that completes it. Commit format `type: short description`, ending with the session's attribution trailer (written below as `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`).

## Review Focus

1. **A huge or half-written transcript.** Take a 50 MB Claude transcript whose last line is still being written (no newline yet). The adapter must:
   - read only the last 1 MiB;
   - drop the partial first and last lines;
   - never throw;
   - still give the session a state.

   Pinned in Task 1 (`readTail`) and Task 2 (`discoverClaude` on a 5 MiB file with a torn last line).
2. **A recycled pid.** `sessions/4242.json` names pid 4242, but 4242 now belongs to another program.
   - The record's `procStart` doesn't match `pidStartTime(4242)`, so the session isn't live. It shows as `crashed` or `ended` from its transcript, and attach never focuses the wrong window.
   - Codex liveness comes from whichever process holds the rollout file open, not from a pid file.
   - Cursor liveness is labelled `estimated`.

   Pinned in Tasks 2 and 3, with the attach side in Task 11.
3. **Many hundreds of sessions, or a provider directory that doesn't exist.** With 900 transcripts across 60 project dirs, one pass reads at most 200 per source, newest first. It stops at the 2 s budget and reports `truncated` with `SND-FLEET-002` in that source's entry. Also:
   - a missing `~/.codex` is an empty source, not an error;
   - a corrupt `sessions/*.json` or an unreadable Cursor `meta.json` affects only its own row;
   - a throwing source leaves the others intact.

   Pinned in Task 8 (`collectFleet`), with the adapter halves in Tasks 2 and 3.
4. **A burst of hook events.** Thirty hooks fire within a second (a fan-out of subagents).
   - Every hook returns at once, because the CLI runs detached.
   - Each event becomes exactly one whole spool line of at most 4 KiB.
   - Rotation at 1 MiB never leaves a line the reader can't skip.
   - A 10 MB `PermissionRequest` payload (a big Write) still records one short line.

   Pinned in Task 5.
5. **The tab left open for days, the server restarted, the clock moved back.**
   - Polling never stacks timers or grows the DOM.
   - After a server restart every request gets a 401. The page says "run `sindri dashboard --url`" once and stops polling.
   - Three failed polls in a row show "sindri not responding: run `sindri doctor`".
   - A transcript timestamp in the future (because the clock moved back) never counts as a recent write and never makes a session `stuck`, and ages never go negative.

   Pinned in Task 6 (rules) and Task 10 (Playwright: hundreds of fast polls, then a real restart).

---

## File Structure

| File | Responsibility |
|---|---|
| `sindri/src/profile/schema.ts` (modify) | `FleetSchema` (`stuckMinutes`, `sources`), `DashboardSchema` (`port`, `notify`) |
| `sindri/src/fleet/types.ts` | `Provider`, `Host`, `FleetState`/`STATES`, `Session`, `SessionSignals`, `SpoolEvent`, `SpoolSummary`, `ProcessTable`, `FleetIo`, `FleetCtx` |
| `sindri/src/fleet/limits.ts`, `paths.ts` | Every bound in one place; state and provider paths, `isUnder` |
| `sindri/src/fleet/text.ts` | `cleanLine`, `cleanText`: scrub, cap, escape (the one place privacy rules plug in) |
| `sindri/src/fleet/tail.ts` | `readTail` (last N bytes, whole lines only), `readHead`, `parseJsonLines` |
| `sindri/src/fleet/proc.ts`, `proc-real.ts` | `parsePs`, `sameStart`, `ancestorsOf`, `hostFromAncestors`; the real `ps`/`lsof` table |
| `sindri/src/fleet/adapters/claude.ts`, `codex.ts`, `cursor.ts` | Provider adapters: discovery, liveness, signals, timeline |
| `sindri/src/fleet/jobs.ts` | Job records written by `runCli`; job state |
| `sindri/src/fleet/adapters/sindri.ts` | Jobs, heavy-lock holder and queue, repo status (index, observe, scope runs) |
| `sindri/src/index/heavy-lock.ts` (modify) | Waiter records and `heavyLockQueue` |
| `sindri/src/fleet/spool.ts` | Hook payload → `SpoolEvent`, append with rotation, read, summarise; `runHook` |
| `config/hooks/fleet.sh` | The `aw:fleet` hook: capture stdin, run `sindri fleet hook` detached, exit 0 |
| `sindri/src/fleet/state.ts` | `deriveState`, `pendingOf`, `ageMs`, `rollUp` |
| `sindri/src/fleet/decisions.ts`, `gh.ts`, `bridge.ts`, `acks.ts`, `needs.ts` | Needs-you inputs and the aggregator |
| `sindri/src/fleet/place.ts`, `usage.ts`, `today.ts`, `sentences.ts`, `collect.ts`, `render.ts`, `command.ts` | Repo and worktree placement, `scorer live` tokens, the Today numbers, the empty-state sentences shared with the UI, the collector, CLI text output, the command |
| `sindri/src/fleet/badge.ts` | The SwiftBar output of `sindri fleet --badge` |
| `sindri/scripts/dashboard-evidence.mjs` | Screenshots of every view for PR evidence (outside the repo, never committed) |
| `sindri/src/dashboard/tokens.ts`, `security.ts`, `actions.ts`, `server.ts`, `command.ts`, `io-real.ts` | The dashboard server and command |
| `sindri/src/dashboard/attach.ts` | Attach resolver and launcher |
| `sindri/ui/index.html`, `app.css`, `favicon.svg`, `favicon-attn.svg`, `tsconfig.json`, `src/model.ts`, `src/app.ts` | The static UI |
| `config/swiftbar/sindri-fleet.30s.sh` | The SwiftBar plugin |
| `scripts/install-sindri.sh` (modify) | `aw:fleet` for every installed provider, `--fleet-hook --provider X`, the SwiftBar plugin |
| `docs/sindri/dashboard.md` | How it works, security model, attach table, troubleshooting |

---
### Task 1: Fleet types, limits, the text sanitizer, the bounded tail reader and the profile keys

**Files:**
- Create: `sindri/src/fleet/types.ts`, `sindri/src/fleet/limits.ts`, `sindri/src/fleet/paths.ts`, `sindri/src/fleet/text.ts`, `sindri/src/fleet/tail.ts`
- Modify: `sindri/src/profile/schema.ts`
- Generated: `docs/sindri/profile.md`, `sindri/schema/profile.schema.json` (`npm run gen`)
- Test: `sindri/tests/fleet-fixtures.ts` (shared), `sindri/tests/fleet-text.test.ts`, `sindri/tests/fleet-tail.test.ts`, `sindri/tests/fleet-profile.test.ts`

**Interfaces:**
- Consumes: `escapeInvisible` (`src/evolve/invisible.ts`), `Scrubber`/`makeScrubber` (`src/scrub/scrub.ts`), `stateDir` (`src/deps.ts`), `ProcessRunner` (`src/index/io.ts`), `Ledger`, `LoadedProfile`.
- Produces:
  - `FleetSchema` and `DashboardSchema` (exported from `schema.ts`). They add the profile keys `fleet.stuckMinutes` (10, an integer from 1 to 1440), `fleet.sources.{claude,codex,cursor,sindri,gh,bridge}` (all `true`), `dashboard.port` (7190, from 1024 to 65535) and `dashboard.notify` (`true`).
  - `types.ts`:
    - the constants and their types: `PROVIDERS`/`Provider`, `isProvider`, `HOSTS`/`Host`, `STATES`/`FleetState`, `stateRank`, `SOURCES`/`SourceName`;
    - the spool types `SpoolKind`, `SpoolEvent`, `SpoolSummary`;
    - the session types `TerminalReason`, `ApiError`, `SessionSignals`, `emptySignals()`, `TimelineEntry`, `Session`;
    - the IO types `ProcEntry`, `ProcessTable { list(); cwds(pids); holders(files) }`, `CodexStatusProbe`, `FleetIo`;
    - the context types `FleetConfig`, `FleetCtx`, `Discovery`, and the helper `spoolKey(provider, id)`.
  - `limits.ts`: `MAX_TAIL_BYTES` (1 MiB), `MAX_SESSIONS_PER_SOURCE` (200), `MAX_CHILDREN` (50), `RECENT_MS` (24 h), `SOURCE_BUDGET_MS` (2000), `RECENT_WRITE_MS` (60 s), `FUTURE_SLACK_MS` (2 min), `SPOOL_MAX_BYTES` (1 MiB), `SPOOL_LINE_MAX` (4096), `MAX_HOOK_PAYLOAD` (4 MiB), `USAGE_TTL_MS` (60 s), `MAX_USAGE_CALLS` (20), `GH_TTL_MS` (120 s), `JUDGE_TTL_MS` (300 s).
  - `paths.ts`: `fleetDir(deps)`, `dashboardDir(deps)`, `providerHomes(deps)` (`CLAUDE_CONFIG_DIR` and `CODEX_HOME` honoured), `isUnder(p, root)`, `iso(ms)`, `latestIso(...ts)`.
  - `text.ts`: `cleanLine(raw, max, scrubber)` (one line) and `cleanText(raw, max, scrubber)` (newlines kept). Both scrub, cap by code points and escape every control or invisible character as `\u{XXXX}`. Caps: `MAX_TITLE` 120, `MAX_ACTIVITY` 160, `MAX_DETAIL` 240, `MAX_TIMELINE` 400.
  - `tail.ts`: `readTail(file, maxBytes?)` returns `{ lines, truncated, size, mtimeMs, partialLast } | null`, with whole lines only. Also `readHead(file, maxBytes?)` (the first line or `null`) and `parseJsonLines(lines)` (`{ entries, bad }`).
  - Test helpers in `fleet-fixtures.ts`:
    - the clock and ids: `NOW`, `at(secondsAgo)`, `CWD`, `SID`, `SID2`;
    - file writers: `writeFile`, `jsonl`, `claudeSlug`;
    - `L`, which builds real-shaped Claude transcript lines;
    - `claudeStatusFile` and `claudeTranscript`;
    - fakes and contexts: `fakeProcTable`, `fleetIo`, `fleetCtx`.

- [ ] **Step 1: Write the failing tests**

`sindri/tests/fleet-fixtures.ts` (shared by every fleet test; later tasks append builders to it):

```ts
import fs from "node:fs";
import path from "node:path";

import type { Deps } from "../src/deps.js";
import type { FleetCtx, FleetIo, ProcEntry, ProcessTable, SpoolSummary } from "../src/fleet/types.js";
import { FleetSchema } from "../src/profile/schema.js";
import { makeScrubber } from "../src/scrub/scrub.js";
import { makeDeps } from "./helpers.js";

export const NOW = new Date("2026-10-08T12:00:00.000Z"); // makeDeps' clock
export const at = (secondsAgo: number): string => new Date(NOW.getTime() - secondsAgo * 1000).toISOString();
export const CWD = "/Users/dev/acme.web"; // synthetic; adapters never stat a session's cwd
export const SID = "11111111-1111-4111-8111-111111111111";
export const SID2 = "22222222-2222-4222-8222-222222222222";

export function writeFile(file: string, text: string, mtime?: Date): string {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, text);
  if (mtime !== undefined) fs.utimesSync(file, mtime, mtime);
  return file;
}

export const jsonl = (objs: readonly unknown[]): string => objs.map((o) => JSON.stringify(o)).join("\n") + (objs.length > 0 ? "\n" : "");
// Claude Code names a project dir after the cwd with "/" and "." turned into "-".
export const claudeSlug = (cwd: string): string => cwd.replace(/[/.]/g, "-");

const base = (ts: string, cwd = CWD) => ({ sessionId: SID, timestamp: ts, cwd, entrypoint: "cli", gitBranch: "main", isSidechain: false, userType: "external", version: "2.1.296" });

// Claude Code transcript lines: the key sets seen on disk (2026-10-09), synthetic content.
export const L = {
  user: (text: string, ts: string, cwd = CWD) => ({ ...base(ts, cwd), type: "user", uuid: `u-${ts}`, message: { role: "user", content: text } }),
  meta: (text: string, ts: string) => ({ ...base(ts), type: "user", isMeta: true, message: { role: "user", content: text } }),
  assistantText: (text: string, ts: string, stop = "end_turn") => ({
    ...base(ts), type: "assistant", requestId: "req_1",
    message: { id: `m-${ts}`, role: "assistant", model: "claude-test", stop_reason: stop, content: [{ type: "text", text }], usage: { input_tokens: 10, output_tokens: 5 } },
  }),
  toolUse: (name: string, input: Record<string, unknown>, id: string, ts: string) => ({
    ...base(ts), type: "assistant",
    message: { id: `m-${id}`, role: "assistant", model: "claude-test", stop_reason: "tool_use", content: [{ type: "tool_use", id, name, input }], usage: { input_tokens: 10, output_tokens: 5 } },
  }),
  toolResult: (id: string, ts: string, text = "ok") => ({
    ...base(ts), type: "user", sourceToolUseID: id, toolUseResult: {},
    message: { role: "user", content: [{ type: "tool_result", tool_use_id: id, content: text }] },
  }),
  interrupted: (forTool: boolean, ts: string) => ({
    ...base(ts), type: "user",
    message: { role: "user", content: [{ type: "text", text: forTool ? "[Request interrupted by user for tool use]" : "[Request interrupted by user]" }] },
  }),
  turnDuration: (ts: string, pending?: number) => ({
    ...base(ts), type: "system", subtype: "turn_duration", durationMs: 1000, messageCount: 4, isMeta: false,
    ...(pending === undefined ? {} : { pendingBackgroundAgentCount: pending }),
  }),
  stopSummary: (ts: string) => ({ ...base(ts), type: "system", subtype: "stop_hook_summary", hookCount: 1, hookErrors: [], hookInfos: [], preventedContinuation: false, stopReason: "", hasOutput: false, level: "info" }),
  apiErrorMessage: (text: string, error: string, ts: string) => ({
    ...base(ts), type: "assistant", isApiErrorMessage: true, error,
    message: { id: `e-${ts}`, role: "assistant", model: "<synthetic>", stop_reason: "stop_sequence", content: [{ type: "text", text }], usage: { input_tokens: 0, output_tokens: 0 } },
  }),
  apiRetry: (ts: string) => ({ ...base(ts), type: "system", subtype: "api_error", level: "error", retryAttempt: 1, maxRetries: 10, retryInMs: 500, source: "request_retry", error: { message: "Connection error", isNetworkDown: false } }),
  compactBoundary: (ts: string) => ({ ...base(ts), type: "system", subtype: "compact_boundary", content: "Conversation compacted", isMeta: false, level: "info", compactMetadata: { trigger: "auto", preTokens: 100 } }),
  aiTitle: (title: string) => ({ type: "ai-title", aiTitle: title, sessionId: SID }),
  costState: (usd: number) => ({ type: "cost-state", sessionId: SID, totalCostUSD: usd, totalDuration: 1, totalAPIDuration: 1 }),
  result: (reason: string, ts: string) => ({ type: "result", subtype: "success", terminal_reason: reason, timestamp: ts, sessionId: SID }),
};

// ~/.claude/sessions/<pid>.json with the key set Claude Code 2.1 writes.
export function claudeStatusFile(home: string, rec: { pid: number; sessionId?: string; cwd?: string; procStart?: string; status?: "busy" | "idle" | "waiting"; entrypoint?: string; name?: string; startedAt?: number; updatedAt?: number }): string {
  const full = {
    pid: rec.pid, sessionId: rec.sessionId ?? SID, cwd: rec.cwd ?? CWD, startedAt: rec.startedAt ?? NOW.getTime() - 3_600_000,
    procStart: rec.procStart ?? `start-${rec.pid}`, version: "2.1.296", peerProtocol: 1, peerFeatures: ["notify_idle"], kind: "interactive",
    entrypoint: rec.entrypoint ?? "cli", pidDomain: "darwin", messagingSocketPath: "/tmp/x.sock", name: rec.name ?? "Fix the login form",
    nameSource: "derived", nameSince: NOW.getTime() - 3_000_000, status: rec.status ?? "idle", updatedAt: rec.updatedAt ?? NOW.getTime() - 5_000, statusUpdatedAt: NOW.getTime() - 5_000,
  };
  return writeFile(path.join(home, ".claude", "sessions", `${rec.pid}.json`), JSON.stringify(full));
}

export function claudeTranscript(home: string, id: string, lines: readonly unknown[], o: { cwd?: string; mtime?: Date; subagents?: Record<string, readonly unknown[]> } = {}): string {
  const dir = path.join(home, ".claude", "projects", claudeSlug(o.cwd ?? CWD));
  const file = writeFile(path.join(dir, `${id}.jsonl`), jsonl(lines), o.mtime ?? NOW);
  for (const [agent, sub] of Object.entries(o.subagents ?? {})) writeFile(path.join(dir, id, "subagents", `agent-${agent}.jsonl`), jsonl(sub), o.mtime ?? NOW);
  return file;
}

export function fakeProcTable(entries: ProcEntry[] = [], cwds: Record<number, string> = {}, holders: Record<string, number[]> = {}): ProcessTable & { listed: number } {
  const t = {
    listed: 0,
    list: () => {
      t.listed += 1;
      return entries;
    },
    cwds: (pids: readonly number[]) => new Map(pids.flatMap((p) => (cwds[p] === undefined ? [] : [[p, cwds[p]] as [number, string]]))),
    holders: (files: readonly string[]) => new Map(files.flatMap((f) => (holders[f] === undefined ? [] : [[f, holders[f]] as [string, number[]]]))),
  };
  return t;
}

export function fleetIo(over: Partial<FleetIo> = {}): FleetIo {
  return {
    proc: fakeProcTable(),
    run: { run: async () => ({ code: 127, stdout: "", stderr: "not found" }) },
    codex: { status: () => null },
    monotonic: () => 0,
    ...over,
  };
}

export function fleetCtx(over: Partial<FleetCtx> = {}): FleetCtx {
  const deps: Deps = over.deps ?? makeDeps();
  return {
    deps, io: fleetIo(), loaded: null, db: null, cfg: FleetSchema.parse(undefined), scrubber: makeScrubber(),
    procs: [], spool: new Map<string, SpoolSummary>(), overBudget: () => false,
    ...over,
  };
}
```

`sindri/tests/fleet-text.test.ts`:

```ts
import { describe, expect, it } from "vitest";

import { cleanLine, cleanText, MAX_TITLE } from "../src/fleet/text.js";
import { makeScrubber } from "../src/scrub/scrub.js";

const s = makeScrubber();
const SECRET = "AKIA" + "ABCDEFGHIJKLMNOP";

describe("cleanLine", () => {
  it("flattens whitespace, scrubs secrets and shows control and invisible characters escaped", () => {
    expect(cleanLine("  fix\nthe\t\tform  ", 50, s)).toBe("fix the form");
    expect(cleanLine(`key ${SECRET} here`, 80, s)).toBe("key [REDACTED:aws-access-key] here");
    // ANSI colour, an OSC 52 clipboard write, a zero-width space and a bidi override stay visible and inert.
    expect(cleanLine("\u001b[31mred\u001b]52;c;aGk=\u0007 a​b ‮evil", 80, s))
      .toBe("\\u{001B}[31mred\\u{001B}]52;c;aGk=\\u{0007} a\\u{200B}b \\u{202E}evil");
    expect(cleanLine("line sep", 80, s)).toBe("line sep");
  });

  it("caps by code points, never splitting a surrogate pair", () => {
    const long = "\u{1D49C}".repeat(200); // an astral character: two UTF-16 units each
    const out = cleanLine(long, MAX_TITLE, s);
    expect(Array.from(out)).toHaveLength(MAX_TITLE);
    expect(out.endsWith("…")).toBe(true);
    expect(cleanLine("short", 10, s)).toBe("short");
  });
});

describe("cleanText", () => {
  it("keeps newlines, normalises CRLF, escapes the rest and caps", () => {
    expect(cleanText("a\r\nb\rc\u0000d", 100, s)).toBe("a\nb\nc\\u{0000}d");
    expect(cleanText("x".repeat(10), 4, s)).toBe("xxx…");
  });
});
```

`sindri/tests/fleet-tail.test.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { parseJsonLines, readHead, readTail } from "../src/fleet/tail.js";
import { writeFile } from "./fleet-fixtures.js";
import { tempDir } from "./helpers.js";

describe("readTail", () => {
  it("returns every whole line of a small file and flags nothing", () => {
    const f = writeFile(path.join(tempDir(), "a.jsonl"), '{"a":1}\n{"b":2}\n');
    expect(readTail(f)).toMatchObject({ lines: ['{"a":1}', '{"b":2}'], truncated: false, partialLast: false, size: 16 });
  });

  it("reads only the last maxBytes, dropping the cut first line and a half-written last line", () => {
    const line = JSON.stringify({ pad: "x".repeat(90) }); // 101 bytes + newline
    const body = `${line}\n`.repeat(50_000) + '{"type":"assistant","times'; // ~5 MiB plus a torn line
    const f = writeFile(path.join(tempDir(), "big.jsonl"), body);
    const t = readTail(f, 1_048_576);
    expect(t?.truncated).toBe(true);
    expect(t?.partialLast).toBe(true);
    expect(t?.size).toBe(Buffer.byteLength(body));
    expect(t?.lines.length).toBeGreaterThan(10_000);
    expect(t?.lines.length).toBeLessThan(10_500); // 1 MiB / 102 bytes, never the whole file
    expect(t?.lines.every((l) => l === line)).toBe(true);
  });

  it("returns no lines for one giant line, and null for a missing file or a directory", () => {
    const dir = tempDir();
    const f = writeFile(path.join(dir, "one.jsonl"), "y".repeat(3000));
    expect(readTail(f, 1000)).toMatchObject({ lines: [], truncated: true, partialLast: true });
    expect(readTail(path.join(dir, "missing.jsonl"))).toBeNull();
    fs.mkdirSync(path.join(dir, "d.jsonl"));
    expect(readTail(path.join(dir, "d.jsonl"))).toBeNull();
  });
});

describe("readHead and parseJsonLines", () => {
  it("reads the first line only, and counts lines that aren't JSON objects", () => {
    const dir = tempDir();
    const f = writeFile(path.join(dir, "h.jsonl"), '{"type":"session_meta"}\n{"x":1}\n');
    expect(readHead(f)).toBe('{"type":"session_meta"}');
    expect(readHead(writeFile(path.join(dir, "n.jsonl"), "no newline"))).toBe("no newline");
    expect(readHead(path.join(dir, "missing"))).toBeNull();
    expect(parseJsonLines(['{"a":1}', "[1]", "nope", "null", '{"b":2}'])).toEqual({ entries: [{ a: 1 }, { b: 2 }], bad: 3 });
  });
});
```

`sindri/tests/fleet-profile.test.ts`:

```ts
import { describe, expect, it } from "vitest";

import { renderProfileDoc } from "../src/docs/profile-doc.js";
import { emptySignals, isProvider, spoolKey, stateRank, STATES } from "../src/fleet/types.js";
import { isUnder, latestIso, providerHomes } from "../src/fleet/paths.js";
import { DashboardSchema, FleetSchema, ProfileSchema } from "../src/profile/schema.js";
import { makeDeps } from "./helpers.js";

const BASE = { schemaVersion: 1, user: "tester", hosts: { active: "test-host" }, tracker: { type: "plan-file", repo: "toolkit" }, repos: ["toolkit"] };

describe("fleet and dashboard profile keys", () => {
  it("default to port 7190, notifications on, 10 stuck minutes and every source on", () => {
    const p = ProfileSchema.parse(BASE);
    expect(p.dashboard).toEqual({ port: 7190, notify: true });
    expect(p.fleet).toEqual({ stuckMinutes: 10, sources: { claude: true, codex: true, cursor: true, sindri: true, gh: true, bridge: true } });
    expect(FleetSchema.parse(undefined)).toEqual(p.fleet);
    expect(DashboardSchema.parse(undefined)).toEqual(p.dashboard);
  });

  it("rejects privileged or out-of-range ports, a bind address, odd stuck minutes and unknown sources", () => {
    for (const bad of [{ dashboard: { port: 80 } }, { dashboard: { port: 70000 } }, { dashboard: { host: "0.0.0.0" } }, { fleet: { stuckMinutes: 0 } }, { fleet: { sources: { slack: true } } }]) {
      expect(ProfileSchema.safeParse({ ...BASE, ...bad }).success).toBe(false);
    }
  });

  it("documents both keys in the generated profile reference", () => {
    const doc = renderProfileDoc();
    expect(doc).toContain("| `dashboard` |");
    expect(doc).toContain("| `fleet` |");
  });
});

describe("fleet type helpers", () => {
  it("ranks states in spec order and recognises providers", () => {
    expect(stateRank("approval")).toBe(0);
    expect(stateRank("ended")).toBe(STATES.length - 1);
    expect(stateRank("input")).toBeLessThan(stateRank("working"));
    expect(isProvider("codex")).toBe(true);
    expect(isProvider("sindri")).toBe(false);
    expect(spoolKey("claude", "x")).toBe("claude:x");
    expect(emptySignals()).toMatchObject({ busy: null, turnOpen: false, backgroundTasks: 0, spool: null });
  });

  it("resolves provider homes, honouring CLAUDE_CONFIG_DIR and CODEX_HOME", () => {
    const d = makeDeps();
    expect(providerHomes(d)).toEqual({ claude: `${d.home}/.claude`, codex: `${d.home}/.codex`, cursor: `${d.home}/.cursor` });
    const o = makeDeps({ env: { CLAUDE_CONFIG_DIR: "/c", CODEX_HOME: "/x" } });
    expect(providerHomes(o)).toMatchObject({ claude: "/c", codex: "/x" });
  });

  it("isUnder needs an absolute path inside the root; latestIso skips nulls and junk", () => {
    expect(isUnder("/a/b/c", "/a/b")).toBe(true);
    expect(isUnder("/a/b", "/a/b/")).toBe(true);
    expect(isUnder("/a/bc", "/a/b")).toBe(false);
    expect(isUnder("/a/b/../x", "/a/b")).toBe(false);
    expect(isUnder("rel", "/a")).toBe(false);
    expect(latestIso(null, "2026-10-08T11:00:00.000Z", "junk", "2026-10-08T11:30:00.000Z")).toBe("2026-10-08T11:30:00.000Z");
    expect(latestIso(null)).toBeNull();
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd sindri && npx vitest run tests/fleet-text.test.ts tests/fleet-tail.test.ts tests/fleet-profile.test.ts`
Expected: FAIL with `Failed to load url ../src/fleet/text.js` (and the same for `tail.js` and `types.js`).

- [ ] **Step 3: Implement**

`sindri/src/profile/schema.ts`: add these two schemas above `ProfileSchema`, then the keys `fleet: FleetSchema, dashboard: DashboardSchema,` after `privacy: PrivacySchema,`:

```ts
export const FleetSchema = z
  .object({
    stuckMinutes: z.number().int().min(1).max(1440).default(10)
      .describe("A busy session with no output for this many minutes shows as stuck (never while it waits on an approval or runs background tasks)"),
    sources: z
      .object({
        claude: z.boolean().default(true),
        codex: z.boolean().default(true),
        cursor: z.boolean().default(true),
        sindri: z.boolean().default(true),
        gh: z.boolean().default(true),
        bridge: z.boolean().default(true),
      })
      .strict()
      .default({})
      .describe("Turn each fleet source on or off: the Claude Code, Codex and Cursor adapters, sindri's own jobs, gh pull requests and bridge messages"),
  })
  .strict()
  .default({})
  .describe("The agent fleet collector (sindri fleet)");

export const DashboardSchema = z
  .object({
    port: z.number().int().min(1024).max(65535).default(7190)
      .describe("Port sindri dashboard listens on, on 127.0.0.1 only; a taken port is an error, never a silent switch"),
    notify: z.boolean().default(true).describe("OS notifications from the dashboard tab, for approval and input only"),
  })
  .strict()
  .default({})
  .describe("The local dashboard (sindri dashboard)");
```

`sindri/src/fleet/types.ts`:

```ts
import type { Deps } from "../deps.js";
import type { ProcessRunner } from "../index/io.js";
import type { Ledger } from "../ledger/db.js";
import type { LoadedProfile } from "../profile/load.js";
import type { Scrubber } from "../scrub/scrub.js";

export const PROVIDERS = ["claude", "codex", "cursor"] as const;
export type Provider = (typeof PROVIDERS)[number];
export const isProvider = (v: string): v is Provider => (PROVIDERS as readonly string[]).includes(v);

export const HOSTS = ["t3", "warp", "terminal", "cursor", "tmux", "unknown"] as const;
export type Host = (typeof HOSTS)[number];

// Spec §4.3, highest priority first. The order ranks needs-you rows and the subagent roll-up.
export const STATES = ["approval", "input", "plan ready", "auth", "working", "background", "ready", "stuck", "interrupted", "failed", "limited", "crashed", "ended"] as const;
export type FleetState = (typeof STATES)[number];
export const stateRank = (s: FleetState): number => STATES.indexOf(s);

export const SOURCES = ["claude", "codex", "cursor", "sindri", "gh", "bridge"] as const;
export type SourceName = (typeof SOURCES)[number];

export type SpoolKind = "permission" | "question" | "plan" | "compact" | "stop" | "prompt" | "session-end" | "subagent-start" | "subagent-stop" | "notification";
// One spool line (spec §4.2): provider, session, event, time and a short scrubbed detail. Never the tool input.
export interface SpoolEvent {
  v: 1;
  provider: Provider;
  session: string;
  event: SpoolKind;
  ts: string;
  detail: string;
  bg?: number; // Stop: background tasks still running
  agent?: string; // subagent events
  transcript?: string; // the host's transcript path, when it sends one
}
export interface SpoolSummary {
  last: SpoolEvent | null;
  pending: SpoolEvent | null; // the newest permission, question or plan event not followed by a clearing event
  compacting: boolean;
  bg: number | null;
  openAgents: string[];
  transcript: string | null;
  stopStatus: string | null; // Cursor's stop status: completed, aborted or error
}

export type TerminalReason = "completed" | "aborted_tools" | "aborted_streaming" | "error";
export interface ApiError {
  kind: "limit" | "auth" | "error";
  status: number | null;
  resetAt: string | null;
}
export interface SessionSignals {
  busy: boolean | null; // the status file's word, null when the provider has none
  statusWaiting: boolean;
  lastWriteAt: string | null;
  lastOutputAt: string | null; // the newest assistant line or tool result
  turnOpen: boolean;
  openToolCall: boolean;
  terminalReason: TerminalReason | null;
  apiError: ApiError | null;
  pendingQuestion: string | null;
  planPending: boolean;
  compacting: boolean;
  backgroundTasks: number;
  codex: { waitingOnApproval: boolean; waitingOnUserInput: boolean; originator: string | null } | null;
  spool: SpoolSummary | null;
}
export const emptySignals = (): SessionSignals => ({
  busy: null, statusWaiting: false, lastWriteAt: null, lastOutputAt: null, turnOpen: false, openToolCall: false, terminalReason: null,
  apiError: null, pendingQuestion: null, planPending: false, compacting: false, backgroundTasks: 0, codex: null, spool: null,
});

export interface TimelineEntry {
  ts: string | null;
  kind: "prompt" | "tool" | "result" | "text" | "compaction" | "turn-end" | "error";
  text: string; // scrubbed and escaped
}

// Spec §4.1's Session, plus what the views need (timeline, turn age, tokens, cost).
export interface Session {
  provider: Provider;
  id: string;
  parentId: string | null;
  cwd: string;
  repo: string | null;
  worktree: string | null;
  host: Host;
  pid: number | null;
  tty: string | null;
  alive: boolean;
  startedAt: string;
  updatedAt: string;
  title: string;
  activity: string;
  signals: SessionSignals;
  estimated: boolean;
  transcript: string | null;
  turnStartedAt: string | null;
  promptTimes: string[];
  costUsd: number | null;
  tokens: number | null;
  timeline: TimelineEntry[];
}

export interface ProcEntry {
  pid: number;
  ppid: number;
  tty: string | null;
  start: string; // `ps -o lstart=` under TZ=UTC LC_ALL=C, spaces collapsed
  command: string;
}
// One `ps` listing per pass, and batched lsof calls: one per pass, never one per session.
export interface ProcessTable {
  list(): ProcEntry[];
  cwds(pids: readonly number[]): Map<number, string>;
  holders(files: readonly string[]): Map<string, number[]>;
}
export interface CodexStatusProbe {
  status(threadId: string): { waitingOnApproval: boolean; waitingOnUserInput: boolean } | null;
}
export interface FleetIo {
  proc: ProcessTable;
  run: ProcessRunner;
  codex: CodexStatusProbe;
  monotonic: () => number; // ms; budgets never use the wall clock
}

export interface FleetConfig {
  stuckMinutes: number;
  sources: Record<SourceName, boolean>;
}
export interface FleetCtx {
  deps: Deps;
  io: FleetIo;
  loaded: LoadedProfile | null;
  db: Ledger | null;
  cfg: FleetConfig;
  scrubber: Scrubber;
  procs: ProcEntry[];
  spool: Map<string, SpoolSummary>; // keyed by spoolKey(provider, session)
  overBudget: () => boolean;
}
export interface Discovery {
  sessions: Session[];
  truncated: boolean;
}
export const spoolKey = (provider: Provider, id: string): string => `${provider}:${id}`;
```

`sindri/src/fleet/limits.ts`:

```ts
// Every bound the collector, the spool and the caches keep (spec §4.1 "Bounded").
export const MAX_TAIL_BYTES = 1_048_576;
export const MAX_SESSIONS_PER_SOURCE = 200;
export const MAX_CHILDREN = 50;
export const RECENT_MS = 24 * 3_600_000;
export const SOURCE_BUDGET_MS = 2_000;
export const RECENT_WRITE_MS = 60_000;
export const FUTURE_SLACK_MS = 120_000;
export const SPOOL_MAX_BYTES = 1_048_576;
export const SPOOL_LINE_MAX = 4_096;
export const MAX_HOOK_PAYLOAD = 4 * 1_048_576;
export const USAGE_TTL_MS = 60_000;
export const MAX_USAGE_CALLS = 20;
export const GH_TTL_MS = 120_000;
export const JUDGE_TTL_MS = 300_000;
```

`sindri/src/fleet/paths.ts`:

```ts
import path from "node:path";

import { stateDir, type Deps } from "../deps.js";
import type { Provider } from "./types.js";

export const fleetDir = (deps: Deps): string => path.join(stateDir(deps), "fleet");
export const dashboardDir = (deps: Deps): string => path.join(stateDir(deps), "dashboard");

export function providerHomes(deps: Deps): Record<Provider, string> {
  return {
    claude: deps.env.CLAUDE_CONFIG_DIR ?? path.join(deps.home, ".claude"),
    codex: deps.env.CODEX_HOME ?? path.join(deps.home, ".codex"),
    cursor: path.join(deps.home, ".cursor"),
  };
}

// Both sides resolved: `/a/b/../x` is not under `/a/b`. A relative path is never under anything.
export function isUnder(p: string, root: string): boolean {
  if (!path.isAbsolute(p)) return false;
  const [a, r] = [path.resolve(p), path.resolve(root)];
  return a === r || a.startsWith(`${r}${path.sep}`);
}

export const iso = (ms: number): string => new Date(ms).toISOString();

export function latestIso(...ts: (string | null)[]): string | null {
  let best: string | null = null;
  for (const t of ts) {
    if (t === null || Number.isNaN(Date.parse(t))) continue;
    if (best === null || Date.parse(t) > Date.parse(best)) best = t;
  }
  return best;
}
```

`sindri/src/fleet/text.ts`:

```ts
import { escapeInvisible } from "../evolve/invisible.js";
import type { Scrubber } from "../scrub/scrub.js";

export const MAX_TITLE = 120;
export const MAX_ACTIVITY = 160;
export const MAX_DETAIL = 240;
export const MAX_TIMELINE = 400;

const KEEP_NEWLINE: ReadonlySet<string> = new Set(["\n"]);

function cap(s: string, max: number): string {
  const cps = Array.from(s);
  return cps.length <= max ? s : `${cps.slice(0, max - 1).join("")}…`;
}

// Every agent-written string reaches a view through one of these two (spec §6 "Untrusted text"). The
// order matters: scrub first (a secret split by the cap would survive), cap, then escape what's left.
// This is also where source-based privacy rules plug in when that plan lands.
export function cleanLine(raw: string, max: number, scrubber: Scrubber): string {
  const flat = raw.replace(/[\r\n\t  ]+/g, " ").replace(/ {2,}/g, " ").trim();
  return escapeInvisible(cap(scrubber.scrub(flat).text, max));
}

export function cleanText(raw: string, max: number, scrubber: Scrubber): string {
  return escapeInvisible(cap(scrubber.scrub(raw.replace(/\r\n?/g, "\n")).text, max), KEEP_NEWLINE);
}
```

`sindri/src/fleet/tail.ts`:

```ts
import fs from "node:fs";

import { MAX_TAIL_BYTES } from "./limits.js";

export interface Tail {
  lines: string[];
  truncated: boolean; // the file is bigger than maxBytes; the cut first line was dropped
  size: number;
  mtimeMs: number;
  partialLast: boolean; // the last line had no newline yet (still being written) and was dropped
}

// The last maxBytes of a transcript, whole lines only (spec §4.1 "Bounded"). Never reads more, never
// throws: a missing or unreadable file, or a directory, is null.
export function readTail(file: string, maxBytes: number = MAX_TAIL_BYTES): Tail | null {
  let fd: number;
  try {
    fd = fs.openSync(file, "r");
  } catch {
    return null;
  }
  try {
    const st = fs.fstatSync(fd);
    if (!st.isFile()) return null;
    const start = Math.max(0, st.size - maxBytes);
    const buf = Buffer.alloc(st.size - start);
    fs.readSync(fd, buf, 0, buf.length, start);
    const text = buf.toString("utf8");
    let lines = text.split("\n");
    const truncated = start > 0;
    const partialLast = lines.at(-1) !== "";
    lines = lines.slice(truncated ? 1 : 0, -1); // the cut first line, and the empty or partial last one
    return { lines: lines.filter((l) => l !== ""), truncated, size: st.size, mtimeMs: st.mtimeMs, partialLast };
  } finally {
    fs.closeSync(fd);
  }
}

export function readHead(file: string, maxBytes = 65_536): string | null {
  let fd: number;
  try {
    fd = fs.openSync(file, "r");
  } catch {
    return null;
  }
  try {
    const buf = Buffer.alloc(maxBytes);
    const n = fs.readSync(fd, buf, 0, maxBytes, 0);
    return buf.subarray(0, n).toString("utf8").split("\n")[0];
  } finally {
    fs.closeSync(fd);
  }
}

export function parseJsonLines(lines: readonly string[]): { entries: Record<string, unknown>[]; bad: number } {
  const entries: Record<string, unknown>[] = [];
  let bad = 0;
  for (const l of lines) {
    try {
      const v: unknown = JSON.parse(l);
      if (typeof v === "object" && v !== null && !Array.isArray(v)) entries.push(v as Record<string, unknown>);
      else bad += 1;
    } catch {
      bad += 1;
    }
  }
  return { entries, bad };
}
```

Trace for the giant-line case: the 3000-byte file has no newline, `start = 2000`, so `split` gives one element. `truncated` drops index 0 and `slice(…, -1)` drops the last one: `lines = []`, and `partialLast` is true because the last element is non-empty.

Regenerate the docs: `cd sindri && npm run gen` (writes `docs/sindri/profile.md`, `sindri/schema/profile.schema.json` and `repo.schema.json`; `errors.md` is unchanged).

- [ ] **Step 4: Run the tests**

Run: `cd sindri && npx vitest run tests/fleet-text.test.ts tests/fleet-tail.test.ts tests/fleet-profile.test.ts tests/profile-doc.test.ts`
Expected: PASS. Then, once for the task: `npm test && npm run typecheck && npm run test:coverage`. Expected: all PASS, and coverage stays at 100%.

- [ ] **Step 5: Commit**

```bash
git add sindri/src/fleet sindri/src/profile/schema.ts sindri/tests docs/sindri/profile.md sindri/schema
git commit -m "feat: sindri fleet types, text sanitizer, bounded tail reader and profile keys" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 2: The process table and the Claude Code adapter

**Files:**
- Create: `sindri/src/fleet/proc.ts`, `sindri/src/fleet/proc-real.ts`, `sindri/src/fleet/adapters/claude.ts`
- Modify: `sindri/vitest.config.ts` (exclude `src/fleet/proc-real.ts`), `sindri/tests/real.test.ts` (smoke test)
- Test: `sindri/tests/fleet-proc.test.ts`, `sindri/tests/fleet-claude.test.ts`

**Interfaces:**
- Consumes:
  - from Task 1: `FleetCtx`, `Session`, `SessionSignals`, `emptySignals`, `Discovery`, `readTail`, `parseJsonLines`, `cleanLine`, `cleanText`, `providerHomes`, `isUnder`, `iso`, `latestIso`, limits;
  - existing code: `textBlocks`, `toolNames` (`src/evolve/transcripts.ts`), `SystemProbe.pidAlive`/`pidStartTime`.
- Produces:
  - `proc.ts`:
    - `parsePs(out): ProcEntry[]`, for `ps -axo pid=,ppid=,tty=,lstart=,command=`;
    - `normStart(s)` and `sameStart(a, b)`, which compare with whitespace collapsed;
    - `ancestorsOf(procs, pid, depth?)`, the chain starting at `pid` itself;
    - `hostFromAncestors(chain): Host | null`, which recognises T3 Code, Warp, tmux, Cursor, Terminal and iTerm;
    - `parseLsofCwds(out)` and `parseLsofHolders(out)`.
  - `proc-real.ts`: `realProcessTable(): ProcessTable`, built from one `ps` call and batched `lsof` calls under `LC_ALL=C TZ=UTC`, with a 3 s timeout each.
  - `adapters/claude.ts`:
    - `UUID`;
    - `readStatusFiles(ctx): Map<sessionId, StatusRecord>`. A record is live only when the pid is alive **and** `pidStartTime(pid)` matches `procStart`.
    - `claudeSignals(entries, scrubber): ParsedClaude`, which returns the signals, title, first prompt, activity, cost, turn start, prompt times, timeline, cwd, entrypoint and last stop reason;
    - `toolLabel(name, input)`, `apiErrorOf(text, error)` and `hostOf(entrypoint, cwd, home, chain)`;
    - `discoverClaude(ctx): Discovery`, which covers live sessions and transcripts changed in the last 24 h, newest first, capped at 200, under the budget, with subagents as children (`parentId`).

- [ ] **Step 1: Write the failing tests**

`sindri/tests/fleet-proc.test.ts`:

```ts
import { describe, expect, it } from "vitest";

import { ancestorsOf, hostFromAncestors, normStart, parseLsofCwds, parseLsofHolders, parsePs, sameStart } from "../src/fleet/proc.js";
import type { ProcEntry } from "../src/fleet/types.js";

const PS = [
  "    1     0 ??       Thu Oct  1 08:00:00 2026     /sbin/launchd",
  "  100     1 ??       Fri Oct  9 07:00:00 2026     /Applications/Warp.app/Contents/MacOS/stable",
  " 4242   100 ttys003  Fri Oct  9 19:23:16 2026     claude --resume x",
  "garbage line",
].join("\n");

describe("parsePs", () => {
  it("reads pid, ppid, tty, the UTC start time and the full command", () => {
    expect(parsePs(PS)).toEqual([
      { pid: 1, ppid: 0, tty: null, start: "Thu Oct 1 08:00:00 2026", command: "/sbin/launchd" },
      { pid: 100, ppid: 1, tty: null, start: "Fri Oct 9 07:00:00 2026", command: "/Applications/Warp.app/Contents/MacOS/stable" },
      { pid: 4242, ppid: 100, tty: "ttys003", start: "Fri Oct 9 19:23:16 2026", command: "claude --resume x" },
    ]);
  });

  it("compares start times with whitespace collapsed, and never matches an unknown one", () => {
    expect(normStart("  Fri Oct  9 19:23:16 2026  ")).toBe("Fri Oct 9 19:23:16 2026");
    expect(sameStart("Fri Oct  9 19:23:16 2026", "Fri Oct 9 19:23:16 2026")).toBe(true);
    expect(sameStart("Fri Oct  9 19:23:16 2026", "Fri Oct  9 19:23:17 2026")).toBe(false);
    expect(sameStart(null, "x")).toBe(false);
    expect(sameStart("x", null)).toBe(false);
  });
});

describe("ancestorsOf and hostFromAncestors", () => {
  const procs = parsePs(PS);
  it("walks parents from the pid up, stopping at launchd and at loops", () => {
    expect(ancestorsOf(procs, 4242).map((p) => p.pid)).toEqual([4242, 100]);
    const loop: ProcEntry[] = [{ pid: 5, ppid: 6, tty: null, start: "s", command: "a" }, { pid: 6, ppid: 5, tty: null, start: "s", command: "b" }];
    expect(ancestorsOf(loop, 5).map((p) => p.pid)).toEqual([5, 6]);
    expect(ancestorsOf(procs, 999)).toEqual([]);
  });

  it("names the host from the first recognised ancestor", () => {
    const one = (command: string): ProcEntry[] => [{ pid: 2, ppid: 1, tty: null, start: "s", command }];
    expect(hostFromAncestors(ancestorsOf(procs, 4242))).toBe("warp");
    expect(hostFromAncestors(one("/Applications/T3 Code (Alpha).app/Contents/MacOS/T3"))).toBe("t3");
    expect(hostFromAncestors(one("tmux: server"))).toBe("tmux");
    expect(hostFromAncestors(one("/Applications/Cursor.app/Contents/MacOS/Cursor"))).toBe("cursor");
    expect(hostFromAncestors(one("/System/Applications/Utilities/Terminal.app/Contents/MacOS/Terminal"))).toBe("terminal");
    expect(hostFromAncestors(one("/usr/bin/login"))).toBeNull();
  });
});

describe("lsof parsers", () => {
  it("reads -Fpn output for cwds and for file holders", () => {
    expect(parseLsofCwds("p4242\nfcwd\nn/Users/dev/acme.web\np77\nfcwd\nn/tmp\n")).toEqual(new Map([[4242, "/Users/dev/acme.web"], [77, "/tmp"]]));
    expect(parseLsofHolders("p10\nf5\nn/a.jsonl\np11\nf9\nn/a.jsonl\nn/b.jsonl\n")).toEqual(new Map([["/a.jsonl", [10, 11]], ["/b.jsonl", [11]]]));
    expect(parseLsofHolders("")).toEqual(new Map());
  });
});
```

`sindri/tests/fleet-claude.test.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { apiErrorOf, claudeSignals, discoverClaude, hostOf, toolLabel } from "../src/fleet/adapters/claude.js";
import { makeScrubber } from "../src/scrub/scrub.js";
import { at, claudeStatusFile, claudeTranscript, CWD, fleetCtx, jsonl, L, NOW, SID, SID2, writeFile, claudeSlug } from "./fleet-fixtures.js";
import { fakeSystem, makeDeps } from "./helpers.js";

const s = makeScrubber();

describe("discoverClaude", () => {
  it("treats a status record as live only when the pid's start time matches procStart", () => {
    const deps = makeDeps();
    claudeStatusFile(deps.home, { pid: 4242, status: "busy", entrypoint: "sdk-ts", cwd: `${deps.home}/.t3/worktrees/acme/t1` });
    claudeTranscript(deps.home, SID, [L.user("fix the form", at(120)), L.toolUse("Bash", { command: "npm test" }, "t1", at(60))], { cwd: `${deps.home}/.t3/worktrees/acme/t1` });
    const [main] = discoverClaude(fleetCtx({ deps })).sessions;
    expect(main).toMatchObject({ provider: "claude", id: SID, alive: true, pid: 4242, host: "t3", title: "Fix the login form", activity: "Bash: npm test", estimated: false });
    expect(main.signals).toMatchObject({ busy: true, turnOpen: true, openToolCall: true });
  });

  it("never takes a recycled pid for the session (Review Focus 2)", () => {
    const deps = makeDeps(); // fakeSystem: every pid is alive and started at `start-<pid>`
    claudeStatusFile(deps.home, { pid: 4242, procStart: "Thu Oct  1 08:00:00 2026", status: "busy" });
    claudeTranscript(deps.home, SID, [L.user("go", at(300)), L.toolUse("Edit", { file_path: "/x/a.ts" }, "t1", at(290))]);
    const [main] = discoverClaude(fleetCtx({ deps })).sessions;
    expect(main).toMatchObject({ alive: false, pid: null, host: "terminal" });
    expect(main.signals).toMatchObject({ busy: null, openToolCall: true });
    const dead = makeDeps({ system: fakeSystem({ pidAlive: () => false }) });
    claudeStatusFile(dead.home, { pid: 7, status: "busy" });
    expect(discoverClaude(fleetCtx({ deps: dead })).sessions[0]).toMatchObject({ alive: false, title: "Fix the login form" });
  });

  it("reads only the last MiB of a huge transcript and survives a half-written last line (Review Focus 1)", () => {
    const deps = makeDeps();
    const filler = JSON.stringify(L.assistantText("x".repeat(200), at(400)));
    const file = path.join(deps.home, ".claude", "projects", claudeSlug(CWD), `${SID}.jsonl`);
    writeFile(file, `${JSON.stringify(L.aiTitle("Title from the start of the file"))}\n${`${filler}\n`.repeat(20_000)}${JSON.stringify(L.toolUse("Grep", { pattern: "needle" }, "t9", at(30)))}\n{"type":"assistant","timest`);
    expect(fs.statSync(file).size).toBeGreaterThan(5_000_000);
    const [main] = discoverClaude(fleetCtx({ deps })).sessions;
    expect(main.title).not.toContain("Title from the start"); // only the tail was read
    expect(main.activity).toBe("Grep: needle");
    expect(main.signals.lastWriteAt).toBe(at(30));
  });

  it("adds subagents as children and ignores workflow journals", () => {
    const deps = makeDeps();
    claudeStatusFile(deps.home, { pid: 4242, status: "idle" });
    const file = claudeTranscript(deps.home, SID, [L.user("fan out", at(200)), L.turnDuration(at(100), 1)], {
      subagents: { abc123: [L.user("Explore the auth module", at(90)), L.toolUse("Read", { file_path: "/x/auth.ts" }, "s1", at(20))] },
    });
    writeFile(path.join(file.slice(0, -6), "subagents", "workflows", "wf_1", "journal.jsonl"), jsonl([L.user("x", at(10))]));
    const ss = discoverClaude(fleetCtx({ deps })).sessions;
    expect(ss.map((x) => [x.id, x.parentId, x.alive])).toEqual([[SID, null, true], ["abc123", SID, true]]);
    expect(ss[1]).toMatchObject({ title: "Explore the auth module", activity: "Read: auth.ts", estimated: true });
    expect(ss[0].signals.backgroundTasks).toBe(1);
  });

  it("shows a live session with no transcript yet, skips corrupt files and lines, and is empty without ~/.claude", () => {
    const deps = makeDeps();
    claudeStatusFile(deps.home, { pid: 4242, sessionId: SID2 });
    writeFile(path.join(deps.home, ".claude", "sessions", "99.json"), "{not json");
    writeFile(path.join(deps.home, ".claude", "sessions", "98.json"), JSON.stringify({ pid: 98 }));
    writeFile(path.join(deps.home, ".claude", "sessions", "29302.abc.key"), "secret-ish");
    claudeTranscript(deps.home, SID, [L.user("hello", at(50))]);
    fs.appendFileSync(path.join(deps.home, ".claude", "projects", claudeSlug(CWD), `${SID}.jsonl`), "not json\n");
    writeFile(path.join(deps.home, ".claude", "projects", claudeSlug(CWD), "notes.txt"), "x");
    const ids = discoverClaude(fleetCtx({ deps })).sessions.map((x) => [x.id, x.alive]);
    expect(ids).toEqual([[SID, false], [SID2, true]]);
    expect(discoverClaude(fleetCtx({ deps: makeDeps() }))).toEqual({ sessions: [], truncated: false });
  });

  it("keeps the newest 200 changed in the last 24 h, plus old transcripts of live sessions, and stops at the budget", () => {
    const deps = makeDeps();
    for (let i = 0; i < 205; i++) {
      const id = `00000000-0000-4000-8000-${String(i).padStart(12, "0")}`;
      claudeTranscript(deps.home, id, [L.user(`task ${i}`, at(1000 + i))], { cwd: `/Users/dev/p${i % 60}`, mtime: new Date(NOW.getTime() - (1000 + i) * 1000) });
    }
    claudeTranscript(deps.home, SID, [L.user("old but live", at(90_000))], { mtime: new Date(NOW.getTime() - 90_000_000) });
    claudeStatusFile(deps.home, { pid: 4242 });
    claudeTranscript(deps.home, SID2, [L.user("old and dead", at(90_000))], { mtime: new Date(NOW.getTime() - 90_000_000) });
    const r = discoverClaude(fleetCtx({ deps }));
    expect(r.truncated).toBe(true);
    expect(r.sessions).toHaveLength(201); // the newest 200 transcripts, then the live session past the cap
    expect(r.sessions[0].title).toBe("task 0");
    expect(r.sessions.at(-1)).toMatchObject({ id: SID, alive: true });
    expect(r.sessions.some((x) => x.id === SID2)).toBe(false);
    let calls = 0;
    const budgeted = discoverClaude(fleetCtx({ deps, overBudget: () => ++calls > 3 }));
    expect(budgeted.sessions.length).toBe(4); // three transcripts, then the live session with no transcript read
    expect(budgeted.truncated).toBe(true);
  });
});

describe("claudeSignals", () => {
  it("derives the turn outcome from interruption markers, turn ends and a result record", () => {
    const sig = (lines: object[]) => claudeSignals(lines as Record<string, unknown>[], s).signals;
    expect(sig([L.user("a", at(10)), L.interrupted(true, at(5))])).toMatchObject({ terminalReason: "aborted_tools", turnOpen: false, openToolCall: false });
    expect(sig([L.user("a", at(10)), L.interrupted(false, at(5))]).terminalReason).toBe("aborted_streaming");
    expect(sig([L.user("a", at(10)), L.stopSummary(at(5))])).toMatchObject({ terminalReason: "completed", turnOpen: false });
    expect(sig([L.user("a", at(10)), L.result("aborted_tools", at(5))]).terminalReason).toBe("aborted_tools");
    expect(sig([L.user("a", at(10)), L.result("completed", at(5))]).terminalReason).toBe("completed");
    expect(sig([L.user("a", at(10)), L.result("max_turns", at(5))]).terminalReason).toBe("error");
    expect(sig([L.user("a", at(10)), L.result("", at(5))]).terminalReason).toBe("completed");
  });

  it("classifies API errors, questions, plans, compaction and background tasks", () => {
    const p = (lines: object[]) => claudeSignals(lines as Record<string, unknown>[], s);
    expect(p([L.user("a", at(9)), L.apiErrorMessage("API Error: 429 rate limited, resets 5pm", "rate_limit", at(5))]).signals.apiError).toEqual({ kind: "limit", status: 429, resetAt: "5pm" });
    expect(p([L.apiErrorMessage("API Error: 529 Overloaded", "server_error", at(5))]).signals).toMatchObject({ apiError: { kind: "error", status: 529 }, terminalReason: "error" });
    expect(p([L.apiErrorMessage("Login expired · Please run /login", "authentication_failed", at(5))]).signals.apiError?.kind).toBe("auth");
    const q = p([L.user("a", at(9)), L.toolUse("AskUserQuestion", { questions: [{ question: "Which database?" }] }, "q1", at(5))]);
    expect(q.signals.pendingQuestion).toBe("Which database?");
    expect(p([L.toolUse("ExitPlanMode", { plan: "x" }, "p1", at(5))]).signals.planPending).toBe(true);
    const done = p([L.user("a", at(9)), L.toolUse("Bash", { command: "ls" }, "b1", at(8)), L.toolResult("b1", at(7)), L.compactBoundary(at(6)), L.turnDuration(at(5), 2), L.costState(1.25), L.aiTitle("Login form fix"), L.apiRetry(at(4))]);
    expect(done.signals).toMatchObject({ openToolCall: false, backgroundTasks: 2, lastOutputAt: at(7) });
    expect(done).toMatchObject({ costUsd: 1.25, title: "Login form fix", firstPrompt: "a", turnStartedAt: at(9), promptTimes: [at(9)] });
    expect(done.timeline.map((t) => t.kind)).toEqual(["prompt", "tool", "result", "compaction", "turn-end"]);
  });

  it("skips meta, command and compact-summary lines as prompts, and a new prompt clears the last outcome", () => {
    const p = claudeSignals([
      L.apiErrorMessage("API Error: 500", "server_error", at(20)),
      L.meta("Caveat: hidden", at(19)),
      L.user("<command-name>/clear</command-name>", at(18)),
      { ...L.user("This session is being continued", at(17)), isCompactSummary: true },
      L.user("next task", at(10)),
      L.assistantText("Working on it\nsecond line", at(5), "tool_use"),
    ] as Record<string, unknown>[], s);
    expect(p.signals).toMatchObject({ apiError: null, terminalReason: null, turnOpen: true });
    expect(p.firstPrompt).toBe("next task");
    expect(p.activity).toBe("Working on it");
    expect(p.entrypoint).toBe("cli");
  });

  it("labels tools and parses error text", () => {
    expect(toolLabel("Bash", { command: "npm test" })).toBe("Bash: npm test");
    expect(toolLabel("Read", { file_path: "/a/b/c.ts" })).toBe("Read: c.ts");
    expect(toolLabel("Grep", { pattern: "x" })).toBe("Grep: x");
    expect(toolLabel("Agent", { description: "explore" })).toBe("Agent: explore");
    expect(toolLabel("WebFetch", { url: "https://example.com" })).toBe("WebFetch: https://example.com");
    expect(toolLabel("TodoWrite", undefined)).toBe("TodoWrite");
    expect(apiErrorOf("Claude usage limit reached", null)).toEqual({ kind: "limit", status: null, resetAt: null });
    expect(apiErrorOf("boom", "rate_limit").kind).toBe("limit");
  });
});

describe("hostOf", () => {
  it("prefers T3 for sdk-ts under ~/.t3, then the process chain, then the entrypoint", () => {
    const home = "/Users/dev";
    const warp = [{ pid: 2, ppid: 1, tty: null, start: "s", command: "/Applications/Warp.app/Contents/MacOS/stable" }];
    expect(hostOf("sdk-ts", "/Users/dev/.t3/worktrees/a/b", home, [])).toBe("t3");
    expect(hostOf("cli", CWD, home, warp)).toBe("warp");
    expect(hostOf("cli", CWD, home, [])).toBe("terminal");
    expect(hostOf("sdk-ts", CWD, home, [])).toBe("unknown");
    expect(hostOf(null, CWD, home, [])).toBe("unknown");
  });
});
```

Add to `sindri/tests/real.test.ts`:

```ts
import { realProcessTable } from "../src/fleet/proc-real.js";

describe("realProcessTable (smoke)", () => {
  it.runIf(process.platform === "darwin" || process.platform === "linux")("lists this process and finds its cwd", () => {
    const t = realProcessTable();
    const me = t.list().find((p) => p.pid === process.pid);
    expect(me?.start).toMatch(/\d{2}:\d{2}:\d{2} \d{4}$/);
    expect(t.cwds([process.pid]).get(process.pid)).toBe(fs.realpathSync(process.cwd()));
    expect(t.holders([])).toEqual(new Map());
    expect(t.cwds([])).toEqual(new Map());
  });
});
```

(`real.test.ts` already imports `fs`, `describe`, `expect` and `it`; add only what's missing.)

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd sindri && npx vitest run tests/fleet-proc.test.ts tests/fleet-claude.test.ts`
Expected: FAIL with `Failed to load url ../src/fleet/proc.js` and `../src/fleet/adapters/claude.js`.

- [ ] **Step 3: Implement**

`sindri/src/fleet/proc.ts`:

```ts
import type { Host, ProcEntry } from "./types.js";

const PS_LINE = /^\s*(\d+)\s+(\d+)\s+(\S+)\s+([A-Z][a-z]{2}\s+[A-Z][a-z]{2}\s+\d{1,2}\s+\d{2}:\d{2}:\d{2}\s+\d{4})\s+(.+)$/;

export const normStart = (s: string): string => s.trim().replace(/\s+/g, " ");
// An unknown start time is never a match: an unknown is not proof that a pid still names a session.
export const sameStart = (a: string | null, b: string | null): boolean => a !== null && b !== null && normStart(a) === normStart(b);

// `ps -axo pid=,ppid=,tty=,lstart=,command=` under LC_ALL=C TZ=UTC: lstart is then the same string
// Claude Code writes as procStart in ~/.claude/sessions/<pid>.json.
export function parsePs(out: string): ProcEntry[] {
  return out.split("\n").flatMap((l) => {
    const m = PS_LINE.exec(l);
    if (m === null) return [];
    return [{ pid: Number(m[1]), ppid: Number(m[2]), tty: m[3] === "??" || m[3] === "?" ? null : m[3], start: normStart(m[4]), command: m[5].trim() }];
  });
}

export function ancestorsOf(procs: readonly ProcEntry[], pid: number, depth = 20): ProcEntry[] {
  const byPid = new Map(procs.map((p) => [p.pid, p]));
  const out: ProcEntry[] = [];
  const seen = new Set<number>();
  let cur = byPid.get(pid);
  while (cur !== undefined && out.length < depth && !seen.has(cur.pid)) {
    seen.add(cur.pid);
    out.push(cur);
    cur = cur.ppid > 1 ? byPid.get(cur.ppid) : undefined;
  }
  return out;
}

const HOST_PATTERNS: readonly [RegExp, Host][] = [
  [/\/T3 Code[^/]*\.app\//, "t3"],
  [/\/Warp\.app\//, "warp"],
  [/(^|\/)tmux(:|\s|$)/, "tmux"],
  [/\/Cursor\.app\/|(^|\/)cursor-agent(\s|$)/, "cursor"],
  [/\/(Terminal|iTerm2?)\.app\//, "terminal"],
];

export function hostFromAncestors(chain: readonly ProcEntry[]): Host | null {
  for (const p of chain) for (const [re, host] of HOST_PATTERNS) if (re.test(p.command)) return host;
  return null;
}

// `lsof -a -d cwd -p <pids> -Fpn`: "p<pid>" then "n<path>" lines.
export function parseLsofCwds(out: string): Map<number, string> {
  const m = new Map<number, string>();
  let pid = -1;
  for (const l of out.split("\n")) {
    if (l.startsWith("p")) pid = Number(l.slice(1));
    else if (l.startsWith("n") && pid > 0) m.set(pid, l.slice(1));
  }
  return m;
}

// `lsof -Fpn -- <files>`: every pid that holds one of the files open.
export function parseLsofHolders(out: string): Map<string, number[]> {
  const m = new Map<string, number[]>();
  let pid = -1;
  for (const l of out.split("\n")) {
    if (l.startsWith("p")) pid = Number(l.slice(1));
    else if (l.startsWith("n") && pid > 0) m.set(l.slice(1), [...(m.get(l.slice(1)) ?? []), pid]);
  }
  return m;
}
```

`sindri/src/fleet/proc-real.ts` (excluded from coverage; smoke-tested above):

```ts
import { execFileSync } from "node:child_process";

import { parseLsofCwds, parseLsofHolders, parsePs } from "./proc.js";
import type { ProcessTable } from "./types.js";

function run(cmd: string, args: string[]): string {
  try {
    return execFileSync(cmd, args, { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"], timeout: 3000, maxBuffer: 16 * 1024 * 1024, env: { ...process.env, LC_ALL: "C", TZ: "UTC" } });
  } catch (e) {
    // lsof exits 1 when some of the files aren't open; its stdout still lists the ones that are.
    const out = (e as { stdout?: unknown }).stdout;
    return typeof out === "string" ? out : "";
  }
}

export function realProcessTable(): ProcessTable {
  return {
    list: () => parsePs(run("ps", ["-axo", "pid=,ppid=,tty=,lstart=,command="])),
    cwds: (pids) => (pids.length === 0 ? new Map() : parseLsofCwds(run("lsof", ["-a", "-d", "cwd", "-p", pids.join(","), "-Fpn"]))),
    holders: (files) => (files.length === 0 ? new Map() : parseLsofHolders(run("lsof", ["-Fpn", "--", ...files]))),
  };
}
```

`sindri/src/fleet/adapters/claude.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import { z } from "zod";

import { textBlocks } from "../../evolve/transcripts.js";
import type { Scrubber } from "../../scrub/scrub.js";
import { MAX_CHILDREN, MAX_SESSIONS_PER_SOURCE, RECENT_MS } from "../limits.js";
import { iso, isUnder, latestIso, providerHomes } from "../paths.js";
import { ancestorsOf, hostFromAncestors, sameStart } from "../proc.js";
import { parseJsonLines, readTail } from "../tail.js";
import { cleanLine, cleanText, MAX_ACTIVITY, MAX_TIMELINE, MAX_TITLE } from "../text.js";
import { emptySignals, spoolKey, type ApiError, type Discovery, type FleetCtx, type Host, type ProcEntry, type Session, type SessionSignals, type TimelineEntry } from "../types.js";

export const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
const INTERRUPT_TOOL = "[Request interrupted by user for tool use]";
const INTERRUPT = "[Request interrupted by user]";

const StatusFile = z
  .object({
    pid: z.number().int().positive(),
    sessionId: z.string().regex(UUID),
    cwd: z.string(),
    startedAt: z.number(),
    procStart: z.string().optional(),
    entrypoint: z.string().optional(),
    name: z.string().optional(),
    status: z.string().optional(),
    updatedAt: z.number().optional(),
  })
  .passthrough();
export type StatusRecord = z.infer<typeof StatusFile> & { live: boolean };

const str = (v: unknown): string | null => (typeof v === "string" ? v : null);

// ~/.claude/sessions/<pid>.json (the *.key files beside them are skipped). A record names a live session
// only while its pid is alive AND that pid started when procStart says: pids are reused (Review Focus 2).
export function readStatusFiles(ctx: FleetCtx): Map<string, StatusRecord> {
  const dir = path.join(providerHomes(ctx.deps).claude, "sessions");
  const out = new Map<string, StatusRecord>();
  let names: string[];
  try {
    names = fs.readdirSync(dir);
  } catch {
    return out;
  }
  const sys = ctx.deps.system;
  for (const name of names.filter((n) => /^\d+\.json$/.test(n))) {
    let raw: unknown;
    try {
      raw = JSON.parse(fs.readFileSync(path.join(dir, name), "utf8"));
    } catch {
      continue;
    }
    const parsed = StatusFile.safeParse(raw);
    if (!parsed.success) continue;
    const r = parsed.data;
    const live = r.procStart !== undefined && sys.pidAlive(r.pid) && sameStart(sys.pidStartTime(r.pid), r.procStart);
    const prev = out.get(r.sessionId);
    if (prev === undefined || (live && !prev.live) || (live === prev.live && (r.updatedAt ?? 0) > (prev.updatedAt ?? 0))) out.set(r.sessionId, { ...r, live });
  }
  return out;
}

interface Block {
  type: string;
  id?: string;
  toolUseId?: string;
  name?: string;
  input?: Record<string, unknown>;
}

function blocks(content: unknown): Block[] {
  if (!Array.isArray(content)) return [];
  return content.flatMap((b: unknown): Block[] => {
    if (typeof b !== "object" || b === null) return [];
    const x = b as Record<string, unknown>;
    const input = typeof x.input === "object" && x.input !== null ? (x.input as Record<string, unknown>) : undefined;
    return [{ type: String(x.type), id: str(x.id) ?? undefined, toolUseId: str(x.tool_use_id) ?? undefined, name: str(x.name) ?? undefined, input }];
  });
}

export function toolLabel(name: string, input: Record<string, unknown> | undefined): string {
  const file = str(input?.file_path);
  const arg = str(input?.command) ?? (file === null ? null : path.basename(file)) ?? str(input?.pattern) ?? str(input?.description) ?? str(input?.url) ?? "";
  return arg === "" ? name : `${name}: ${arg}`;
}

// Spec decision 2: 429 and usage-limit text are `limit`; 529 and the rest are `error`; a login problem is `auth`.
export function apiErrorOf(text: string, error: string | null): ApiError {
  const status = /\b([45]\d\d)\b/.exec(text);
  const kind = error === "authentication_failed" || /login expired|please run \/login/i.test(text)
    ? "auth"
    : error === "rate_limit" || /usage limit|limit reached|rate.?limit|\b429\b/i.test(text) ? "limit" : "error";
  const reset = /resets? (?:at )?([0-9][^.·|\n]{0,30})/i.exec(text);
  return { kind, status: status === null ? null : Number(status[1]), resetAt: reset === null ? null : reset[1].trim() };
}

function questionText(input: Record<string, unknown> | undefined): string {
  const qs = input?.questions;
  if (Array.isArray(qs) && typeof qs[0] === "object" && qs[0] !== null) {
    const q = str((qs[0] as Record<string, unknown>).question);
    if (q !== null) return q;
  }
  return str(input?.question) ?? "asked you a question";
}

export interface ParsedClaude {
  signals: SessionSignals;
  title: string | null;
  firstPrompt: string | null;
  activity: string;
  costUsd: number | null;
  turnStartedAt: string | null;
  promptTimes: string[];
  timeline: TimelineEntry[];
  cwd: string | null;
  entrypoint: string | null;
  firstTs: string | null;
  lastStopReason: string | null;
}

// One pass over a transcript tail (spec decision 1: the outcome comes from the transcript's own markers).
export function claudeSignals(entries: readonly Record<string, unknown>[], scrubber: Scrubber): ParsedClaude {
  const s = emptySignals();
  const open = new Map<string, { name: string; input?: Record<string, unknown> }>();
  const timeline: TimelineEntry[] = [];
  const promptTimes: string[] = [];
  let title: string | null = null;
  let firstPrompt: string | null = null;
  let activity = "";
  let costUsd: number | null = null;
  let turnStartedAt: string | null = null;
  let cwd: string | null = null;
  let entrypoint: string | null = null;
  let firstTs: string | null = null;
  let lastStopReason: string | null = null;
  const push = (ts: string | null, kind: TimelineEntry["kind"], text: string): void => {
    timeline.push({ ts, kind, text: cleanText(text, MAX_TIMELINE, scrubber) });
    if (timeline.length > 40) timeline.shift();
  };
  for (const e of entries) {
    const type = str(e.type);
    const ts = str(e.timestamp);
    if (ts !== null) {
      s.lastWriteAt = ts;
      firstTs ??= ts;
    }
    cwd = str(e.cwd) ?? cwd;
    entrypoint = str(e.entrypoint) ?? entrypoint;
    const msg = (typeof e.message === "object" && e.message !== null ? e.message : {}) as { content?: unknown; stop_reason?: unknown };
    if (type === "ai-title") title = str(e.aiTitle) ?? title;
    else if (type === "cost-state") costUsd = typeof e.totalCostUSD === "number" ? e.totalCostUSD : costUsd;
    else if (type === "result") {
      const r = str(e.terminal_reason);
      s.terminalReason = r === "aborted_tools" || r === "aborted_streaming" ? r : r === null || r === "" || r === "completed" ? "completed" : "error";
      s.turnOpen = false;
    } else if (type === "system") {
      const sub = str(e.subtype);
      if (sub === "turn_duration" || sub === "stop_hook_summary") {
        s.turnOpen = false;
        s.terminalReason ??= "completed";
        if (typeof e.pendingBackgroundAgentCount === "number") s.backgroundTasks = e.pendingBackgroundAgentCount;
        if (sub === "turn_duration") push(ts, "turn-end", "turn finished");
      } else if (sub === "compact_boundary") {
        s.compacting = false;
        push(ts, "compaction", "context compacted");
      }
    } else if (type === "assistant") {
      s.lastOutputAt = ts ?? s.lastOutputAt;
      const text = textBlocks(msg.content).map((b) => b.text).join(" ").trim();
      if (e.isApiErrorMessage === true) {
        s.apiError = apiErrorOf(text, str(e.error));
        s.terminalReason = "error";
        s.turnOpen = false;
        push(ts, "error", text);
        continue;
      }
      lastStopReason = str(msg.stop_reason);
      for (const b of blocks(msg.content)) {
        if (b.type !== "tool_use" || b.id === undefined || b.name === undefined) continue;
        open.set(b.id, { name: b.name, input: b.input });
        activity = toolLabel(b.name, b.input);
        push(ts, "tool", activity);
      }
      if (text !== "") {
        if (open.size === 0) activity = text.split("\n")[0];
        push(ts, "text", text);
      }
    } else if (type === "user") {
      const results = blocks(msg.content).filter((b) => b.type === "tool_result");
      if (results.length > 0) {
        s.lastOutputAt = ts ?? s.lastOutputAt;
        for (const r of results) if (r.toolUseId !== undefined) open.delete(r.toolUseId);
        push(ts, "result", `${results.length} tool result(s)`);
        continue;
      }
      const text = textBlocks(msg.content).map((b) => b.text).join("\n").trim();
      if (text === INTERRUPT_TOOL || text === INTERRUPT) {
        s.terminalReason = text === INTERRUPT_TOOL ? "aborted_tools" : "aborted_streaming";
        s.turnOpen = false;
        open.clear();
        push(ts, "turn-end", "interrupted");
        continue;
      }
      if (e.isMeta === true || e.isCompactSummary === true || text === "" || text.startsWith("<")) continue;
      // A human prompt opens a turn and clears the last turn's outcome.
      s.turnOpen = true;
      s.terminalReason = null;
      s.apiError = null;
      s.backgroundTasks = 0;
      turnStartedAt = ts;
      firstPrompt ??= text;
      if (ts !== null) promptTimes.push(ts);
      push(ts, "prompt", text);
    }
  }
  s.openToolCall = open.size > 0;
  const ask = [...open.values()].find((t) => t.name === "AskUserQuestion");
  s.pendingQuestion = ask === undefined ? null : cleanLine(questionText(ask.input), MAX_TITLE, scrubber);
  s.planPending = [...open.values()].some((t) => t.name === "ExitPlanMode");
  return {
    signals: s,
    title: title === null ? null : cleanLine(title, MAX_TITLE, scrubber),
    firstPrompt: firstPrompt === null ? null : cleanLine(firstPrompt, MAX_TITLE, scrubber),
    activity: cleanLine(activity, MAX_ACTIVITY, scrubber),
    costUsd, turnStartedAt, promptTimes, timeline, cwd, entrypoint, firstTs, lastStopReason,
  };
}

export function hostOf(entrypoint: string | null | undefined, cwd: string, home: string, chain: readonly ProcEntry[]): Host {
  if (entrypoint === "sdk-ts" && isUnder(cwd, path.join(home, ".t3"))) return "t3";
  const fromChain = hostFromAncestors(chain);
  if (fromChain !== null) return fromChain;
  return entrypoint === "cli" ? "terminal" : "unknown";
}

interface Found {
  id: string;
  file: string;
  mtimeMs: number;
}

function listTranscripts(projects: string, cutoffMs: number, wanted: ReadonlySet<string>): Found[] {
  let dirs: string[];
  try {
    dirs = fs.readdirSync(projects);
  } catch {
    return [];
  }
  const byId = new Map<string, Found>();
  for (const d of dirs) {
    let names: string[];
    try {
      names = fs.readdirSync(path.join(projects, d));
    } catch {
      continue;
    }
    for (const n of names) {
      const id = n.endsWith(".jsonl") ? n.slice(0, -6) : "";
      if (!UUID.test(id)) continue;
      const file = path.join(projects, d, n);
      const st = fs.statSync(file, { throwIfNoEntry: false });
      if (st === undefined || !st.isFile() || (st.mtimeMs < cutoffMs && !wanted.has(id))) continue;
      const prev = byId.get(id);
      if (prev === undefined || st.mtimeMs > prev.mtimeMs) byId.set(id, { id, file, mtimeMs: st.mtimeMs });
    }
  }
  return [...byId.values()].sort((a, b) => b.mtimeMs - a.mtimeMs);
}

function sessionFrom(ctx: FleetCtx, id: string, f: Found | null, rec: StatusRecord | null): Session {
  const tail = f === null ? null : readTail(f.file);
  const p = claudeSignals(tail === null ? [] : parseJsonLines(tail.lines).entries, ctx.scrubber);
  const s = p.signals;
  const alive = rec?.live === true;
  if (rec !== null) {
    s.busy = alive ? rec.status === "busy" : null;
    s.statusWaiting = alive && rec.status === "waiting";
  }
  s.spool = ctx.spool.get(spoolKey("claude", id)) ?? null;
  if (s.spool?.compacting === true) s.compacting = true;
  const pid = alive ? (rec as StatusRecord).pid : null;
  const chain = pid === null ? [] : ancestorsOf(ctx.procs, pid);
  const cwd = rec?.cwd ?? p.cwd ?? "";
  const mtime = f === null ? null : iso(f.mtimeMs);
  const updatedAt = latestIso(s.lastWriteAt, mtime, rec?.updatedAt === undefined ? null : iso(rec.updatedAt)) ?? ctx.deps.now().toISOString();
  return {
    provider: "claude", id, parentId: null, cwd, repo: null, worktree: null,
    host: hostOf(rec?.entrypoint ?? p.entrypoint, cwd, ctx.deps.home, chain),
    pid, tty: chain[0]?.tty ?? null, alive,
    startedAt: rec === null ? (p.firstTs ?? updatedAt) : iso(rec.startedAt),
    updatedAt,
    title: rec?.name !== undefined && rec.name !== "" ? cleanLine(rec.name, MAX_TITLE, ctx.scrubber) : (p.title ?? p.firstPrompt ?? "(untitled)"),
    activity: s.compacting ? "compacting context" : p.activity,
    signals: s, estimated: false, transcript: f?.file ?? null, turnStartedAt: p.turnStartedAt, promptTimes: p.promptTimes,
    costUsd: p.costUsd, tokens: null, timeline: p.timeline,
  };
}

// Subagents live beside the transcript: <session>/subagents/agent-<id>.jsonl (workflow journals deeper down
// are skipped). A child counts as alive while its parent lives, it hasn't ended its turn, and it wrote
// within the stuck window; its state is estimated (no status file).
function childrenOf(ctx: FleetCtx, f: Found, parent: Session): Session[] {
  const dir = path.join(f.file.slice(0, -6), "subagents");
  let names: string[];
  try {
    names = fs.readdirSync(dir).filter((n) => /^agent-[A-Za-z0-9_-]{1,80}\.jsonl$/.test(n));
  } catch {
    return [];
  }
  const nowMs = ctx.deps.now().getTime();
  const files = names
    .map((n) => ({ n, st: fs.statSync(path.join(dir, n), { throwIfNoEntry: false }) }))
    .filter((x): x is { n: string; st: fs.Stats } => x.st !== undefined && x.st.mtimeMs >= nowMs - RECENT_MS)
    .sort((a, b) => b.st.mtimeMs - a.st.mtimeMs)
    .slice(0, MAX_CHILDREN);
  return files.map(({ n, st }) => {
    const file = path.join(dir, n);
    const tail = readTail(file);
    const p = claudeSignals(tail === null ? [] : parseJsonLines(tail.lines).entries, ctx.scrubber);
    const alive = parent.alive && p.lastStopReason !== "end_turn" && nowMs - st.mtimeMs <= ctx.cfg.stuckMinutes * 60_000;
    p.signals.busy = alive;
    return {
      ...parent, id: n.slice(6, -6), parentId: parent.id, pid: null, alive, title: p.firstPrompt ?? "(subagent)", activity: p.activity,
      signals: p.signals, estimated: true, transcript: file, startedAt: p.firstTs ?? iso(st.mtimeMs), updatedAt: latestIso(p.signals.lastWriteAt, iso(st.mtimeMs)) as string,
      turnStartedAt: p.turnStartedAt, promptTimes: [], costUsd: null, tokens: null, timeline: p.timeline,
    };
  });
}

export function discoverClaude(ctx: FleetCtx): Discovery {
  const status = readStatusFiles(ctx);
  const liveIds = new Set([...status.values()].filter((r) => r.live).map((r) => r.sessionId));
  const nowMs = ctx.deps.now().getTime();
  const found = listTranscripts(path.join(providerHomes(ctx.deps).claude, "projects"), nowMs - RECENT_MS, liveIds);
  const sessions: Session[] = [];
  let truncated = found.length > MAX_SESSIONS_PER_SOURCE;
  for (const f of found.slice(0, MAX_SESSIONS_PER_SOURCE)) {
    if (ctx.overBudget()) {
      truncated = true;
      break;
    }
    const main = sessionFrom(ctx, f.id, f, status.get(f.id) ?? null);
    sessions.push(main, ...childrenOf(ctx, f, main));
  }
  // A status record with no transcript read (none written yet, or past the cap or budget) still shows: a
  // live one always, a dead one only when nothing else names it.
  for (const rec of status.values()) {
    if (!sessions.some((x) => x.id === rec.sessionId) && (rec.live || !found.some((f) => f.id === rec.sessionId))) sessions.push(sessionFrom(ctx, rec.sessionId, null, rec));
  }
  return { sessions, truncated };
}
```

Traces for the tests:
- **The budget test.** `overBudget` returns false three times, so three transcripts are read, and on the fourth check it stops. The live status record (SID) has a transcript that wasn't read in this pass, so it's added by the trailing loop: four sessions, `truncated: true`.
- **The cap test.** 206 transcripts qualify (205 recent, plus SID because it is live); SID2 is old and dead, so it's dropped. The newest 200 are kept, and SID (90 000 s old) falls outside them. The trailing loop then adds SID with no transcript, because it's live, which makes 201. SID2 has no status record, so it never appears.
- **The dead-pid test.** There's no transcript and the record is dead, so the trailing loop adds it because no transcript names it.

In `sindri/vitest.config.ts`, add `"src/fleet/proc-real.ts"` to `coverage.exclude`, with the comment `// ps/lsof wiring; smoke-tested in tests/real.test.ts`.

- [ ] **Step 4: Run the tests**

Run: `cd sindri && npx vitest run tests/fleet-proc.test.ts tests/fleet-claude.test.ts tests/real.test.ts`
Expected: PASS. Then, once for the task: `npm test && npm run typecheck && npm run test:coverage`. Expected: all PASS, coverage 100%.

- [ ] **Step 5: Commit**

```bash
git add sindri/src/fleet sindri/tests sindri/vitest.config.ts
git commit -m "feat: sindri fleet process table and Claude Code adapter" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 3: The Codex and Cursor adapters

**Files:**
- Create: `sindri/src/fleet/adapters/codex.ts`, `sindri/src/fleet/adapters/cursor.ts`
- Modify: `sindri/tests/fleet-fixtures.ts` (Codex and Cursor builders)
- Test: `sindri/tests/fleet-codex.test.ts`, `sindri/tests/fleet-cursor.test.ts`

**Interfaces:**
- Consumes:
  - from Task 1: `FleetCtx`, `Session`, `emptySignals`, `readTail`, `readHead`, `parseJsonLines`, `cleanLine`, `cleanText`, `providerHomes`, `iso`, `latestIso`, `isUnder`, limits;
  - from Task 2: `ancestorsOf`, `hostFromAncestors`, `apiErrorOf`, `toolLabel`, `UUID`;
  - `FleetIo.proc.holders`/`cwds` and `FleetIo.codex`.
- Produces:
  - `codex.ts`:
    - `codexMeta(head)` returns `{ id, parent, guardian, originator, cwd }`;
    - `codexSignals(entries, scrubber)` returns `ParsedCodex`: the signals, title, activity, tokens, turn start, prompt times, timeline, cwd, first timestamp, `waitingOnApproval` and `waitingOnUserInput`;
    - `discoverCodex(ctx): Discovery`. A session is alive when a process holds its rollout file open (one batched `lsof` per pass). Flags from `FleetIo.codex.status(id)` win when the probe answers (spec decision 5). Subagent rollouts get `parentId`, and the approvals "guardian" is skipped. A row is `estimated` unless the spool or the probe has spoken for it.
  - `cursor.ts`:
    - `cursorSignals(entries, mtimeIso, scrubber)`;
    - `discoverCursor(ctx): Discovery`, reading `~/.cursor/chats/*/<id>/meta.json`, the matching `~/.cursor/projects/*/agent-transcripts/<id>/<id>.jsonl` (or a spool transcript path, but only one inside `~/.cursor`) and `prompt_history.json`. Liveness comes from a `cursor-agent` process whose cwd matches; when several chats share that cwd, only the newest counts as alive. Every row is `estimated` unless the spool has events for it.
  - Fixture builders: `CX`, `codexRollout`, `CU`, `cursorChat`, `cursorTranscript`.

- [ ] **Step 1: Write the failing tests**

Append to `sindri/tests/fleet-fixtures.ts`:

```ts
// Codex rollouts: { timestamp, type, payload } lines, as scorer/src/transcript/codex.ts learned them (codex 0.158).
export const CX = {
  meta: (id: string, cwd: string, ts: string, o: { originator?: string; parent?: string; guardian?: boolean } = {}) => ({
    timestamp: ts, type: "session_meta",
    payload: {
      id, session_id: id, timestamp: ts, cwd, originator: o.originator ?? "codex_cli_rs", cli_version: "0.158.0",
      source: o.parent !== undefined ? { subagent: { thread_spawn: { parent_thread_id: o.parent, agent_path: "/root/explorer" } } } : o.guardian === true ? { subagent: { other: "guardian" } } : "cli",
    },
  }),
  turnContext: (cwd: string, ts: string) => ({ timestamp: ts, type: "turn_context", payload: { turn_id: "t1", cwd, model: "gpt-test", approval_policy: "on-request" } }),
  taskStarted: (ts: string) => ({ timestamp: ts, type: "event_msg", payload: { type: "task_started", turn_id: "t1" } }),
  taskComplete: (ts: string) => ({ timestamp: ts, type: "event_msg", payload: { type: "task_complete", turn_id: "t1", last_agent_message: "Done." } }),
  aborted: (ts: string, reason = "interrupted") => ({ timestamp: ts, type: "event_msg", payload: { type: "turn_aborted", turn_id: "t1", reason } }),
  error: (message: string, ts: string) => ({ timestamp: ts, type: "event_msg", payload: { type: "error", message } }),
  approval: (ts: string) => ({ timestamp: ts, type: "event_msg", payload: { type: "exec_approval_request", call_id: "c9", command: ["make", "deploy"] } }),
  askInput: (ts: string) => ({ timestamp: ts, type: "event_msg", payload: { type: "request_user_input", call_id: "c8" } }),
  tokens: (total: number, ts: string) => ({ timestamp: ts, type: "event_msg", payload: { type: "token_count", info: { total_token_usage: { input_tokens: total - 10, output_tokens: 10, total_tokens: total } } } }),
  user: (text: string, ts: string) => ({ timestamp: ts, type: "response_item", payload: { type: "message", role: "user", content: [{ type: "input_text", text }] } }),
  assistant: (text: string, ts: string) => ({ timestamp: ts, type: "response_item", payload: { type: "message", role: "assistant", content: [{ type: "output_text", text }] } }),
  call: (name: string, input: string, callId: string, ts: string) => ({ timestamp: ts, type: "response_item", payload: { type: "custom_tool_call", call_id: callId, name, input, status: "completed" } }),
  fnCall: (name: string, args: Record<string, unknown>, callId: string, ts: string) => ({ timestamp: ts, type: "response_item", payload: { type: "function_call", call_id: callId, name, arguments: JSON.stringify(args) } }),
  output: (callId: string, ts: string) => ({ timestamp: ts, type: "response_item", payload: { type: "custom_tool_call_output", call_id: callId, output: "ok" } }),
};

export function codexRollout(home: string, id: string, lines: readonly unknown[], o: { day?: string; mtime?: Date } = {}): string {
  const day = o.day ?? "2026/10/08";
  return writeFile(path.join(home, ".codex", "sessions", day, `rollout-${day.replace(/\//g, "-")}T11-00-00-${id}.jsonl`), jsonl(lines), o.mtime ?? NOW);
}

// Cursor agent transcripts: { role, message } lines and { type: "turn_ended", status } markers, no timestamps.
export const CU = {
  user: (text: string) => ({ role: "user", message: { content: [{ type: "text", text }] } }),
  tool: (name: string, input: Record<string, unknown>) => ({ role: "assistant", message: { content: [{ type: "tool_use", name, input }] } }),
  text: (text: string) => ({ role: "assistant", message: { content: [{ type: "text", text }] } }),
  turnEnded: (status: string) => ({ type: "turn_ended", status }),
};

export function cursorChat(home: string, id: string, m: { cwd?: string; updatedAtMs?: number; hasConversation?: boolean; prompts?: string[]; hash?: string } = {}): string {
  const dir = path.join(home, ".cursor", "chats", m.hash ?? "aeba0000aeba0000aeba0000aeba0000", id);
  writeFile(path.join(dir, "prompt_history.json"), JSON.stringify(m.prompts ?? ["Fix the login form"]));
  return writeFile(path.join(dir, "meta.json"), JSON.stringify({ schemaVersion: 1, createdAtMs: NOW.getTime() - 3_600_000, hasConversation: m.hasConversation ?? true, updatedAtMs: m.updatedAtMs ?? NOW.getTime() - 30_000, cwd: m.cwd ?? CWD }));
}

export function cursorTranscript(home: string, id: string, lines: readonly unknown[], mtime: Date = NOW): string {
  return writeFile(path.join(home, ".cursor", "projects", "Users-dev-acme-web", "agent-transcripts", id, `${id}.jsonl`), jsonl(lines), mtime);
}
```

`sindri/tests/fleet-codex.test.ts`:

```ts
import { describe, expect, it } from "vitest";

import { codexMeta, codexSignals, discoverCodex } from "../src/fleet/adapters/codex.js";
import { makeScrubber } from "../src/scrub/scrub.js";
import { at, codexRollout, CWD, CX, fakeProcTable, fleetCtx, fleetIo, NOW, SID, SID2 } from "./fleet-fixtures.js";
import { makeDeps } from "./helpers.js";

const s = makeScrubber();
const WARP = { pid: 100, ppid: 1, tty: null, start: "s", command: "/Applications/Warp.app/Contents/MacOS/stable" };
const CODEX = { pid: 900, ppid: 100, tty: "ttys004", start: "s", command: "codex" };

describe("discoverCodex", () => {
  it("is alive when a process holds the rollout open, and reads title, activity and tokens", () => {
    const deps = makeDeps();
    const file = codexRollout(deps.home, SID, [
      CX.meta(SID, CWD, at(300)), CX.turnContext(CWD, at(299)), CX.taskStarted(at(299)),
      CX.user("# AGENTS.md instructions for /Users/dev/acme.web\n\nUse pnpm.", at(298)),
      CX.user("<ide_opened_file>src/app.ts</ide_opened_file>\nAdd a health endpoint", at(297)),
      CX.call("exec", "npm test", "c1", at(200)), CX.tokens(1050, at(199)),
    ]);
    const ctx = fleetCtx({ deps, io: fleetIo({ proc: fakeProcTable([], {}, { [file]: [900] }) }), procs: [WARP, CODEX] });
    const [x] = discoverCodex(ctx).sessions;
    expect(x).toMatchObject({ provider: "codex", id: SID, alive: true, pid: 900, tty: "ttys004", host: "warp", title: "Add a health endpoint", activity: "exec: npm test", tokens: 1050, estimated: true, cwd: CWD });
    expect(x.signals).toMatchObject({ busy: true, turnOpen: true, openToolCall: true, codex: { waitingOnApproval: false, waitingOnUserInput: false, originator: "codex_cli_rs" } });
  });

  it("links subagent rollouts to their parent and skips the approvals guardian", () => {
    const deps = makeDeps();
    codexRollout(deps.home, SID, [CX.meta(SID, CWD, at(100)), CX.user("parent task", at(99))]);
    codexRollout(deps.home, SID2, [CX.meta(SID2, CWD, at(90), { parent: SID }), CX.user("explore", at(89))], { mtime: new Date(NOW.getTime() - 1000) });
    const G = "33333333-3333-4333-8333-333333333333";
    codexRollout(deps.home, G, [CX.meta(G, CWD, at(80), { guardian: true })]);
    expect(discoverCodex(fleetCtx({ deps })).sessions.map((x) => [x.id, x.parentId, x.alive])).toEqual([[SID, null, false], [SID2, SID, false]]);
  });

  it("reads interruptions, errors, approvals and questions, and lets the probe's flags win", () => {
    const sig = (lines: object[]) => codexSignals(lines as Record<string, unknown>[], s);
    expect(sig([CX.taskStarted(at(9)), CX.aborted(at(5))]).signals).toMatchObject({ terminalReason: "aborted_streaming", turnOpen: false });
    expect(sig([CX.taskStarted(at(9)), CX.error("stream error: 429 Too Many Requests", at(5))]).signals.apiError).toMatchObject({ kind: "limit", status: 429 });
    expect(sig([CX.taskStarted(at(9)), CX.taskComplete(at(5))]).signals.terminalReason).toBe("completed");
    const waiting = sig([CX.taskStarted(at(9)), CX.fnCall("shell", { command: ["make", "deploy"] }, "c9", at(8)), CX.approval(at(7))]);
    expect(waiting).toMatchObject({ waitingOnApproval: true, activity: "shell: make deploy" });
    expect(sig([CX.taskStarted(at(9)), CX.approval(at(7)), CX.output("c9", at(3))]).waitingOnApproval).toBe(false);
    expect(sig([CX.taskStarted(at(9)), CX.askInput(at(7))]).waitingOnUserInput).toBe(true);
    expect(sig([CX.taskStarted(at(9)), CX.assistant("All done\nmore", at(4))]).activity).toBe("All done");

    const deps = makeDeps();
    codexRollout(deps.home, SID, [CX.meta(SID, CWD, at(100)), CX.taskStarted(at(99))]);
    const probed = discoverCodex(fleetCtx({ deps, io: fleetIo({ codex: { status: () => ({ waitingOnApproval: true, waitingOnUserInput: false }) } }) })).sessions[0];
    expect(probed.signals.codex?.waitingOnApproval).toBe(true);
    expect(probed.estimated).toBe(false);
  });

  it("is not estimated once the spool has spoken, and survives a corrupt first line and a missing dir", () => {
    const deps = makeDeps();
    codexRollout(deps.home, SID, ["not json", CX.taskStarted(at(10))]);
    const spool = new Map([[`codex:${SID}`, { last: null, pending: null, compacting: true, bg: null, openAgents: [], transcript: null, stopStatus: null }]]);
    const [x] = discoverCodex(fleetCtx({ deps, spool })).sessions;
    expect(x).toMatchObject({ id: SID, estimated: false, activity: "compacting context", title: "(untitled)" });
    expect(codexMeta(null)).toEqual({ id: null, parent: null, guardian: false, originator: null, cwd: null });
    expect(codexMeta('{"type":"session_meta","payload":"odd"}')).toMatchObject({ id: null });
    expect(discoverCodex(fleetCtx({ deps: makeDeps() }))).toEqual({ sessions: [], truncated: false });
  });

  it("scans only the newest 60 day directories and stops at the budget", () => {
    const deps = makeDeps();
    for (let d = 1; d <= 61; d++) {
      const day = `2026/${String(Math.ceil(d / 28)).padStart(2, "0")}/${String(((d - 1) % 28) + 1).padStart(2, "0")}`;
      const id = `00000000-0000-4000-8000-${String(d).padStart(12, "0")}`;
      codexRollout(deps.home, id, [CX.meta(id, CWD, at(50))], { day });
    }
    const ids = discoverCodex(fleetCtx({ deps })).sessions.map((x) => x.id);
    expect(ids).toHaveLength(60);
    expect(ids).not.toContain("00000000-0000-4000-8000-000000000001"); // the oldest day dir isn't opened
    let n = 0;
    expect(discoverCodex(fleetCtx({ deps, overBudget: () => ++n > 2 }))).toMatchObject({ truncated: true, sessions: [expect.anything(), expect.anything()] });
  });
});
```

`sindri/tests/fleet-cursor.test.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { cursorSignals, discoverCursor } from "../src/fleet/adapters/cursor.js";
import { makeScrubber } from "../src/scrub/scrub.js";
import type { SpoolSummary } from "../src/fleet/types.js";
import { CU, CWD, cursorChat, cursorTranscript, fakeProcTable, fleetCtx, fleetIo, NOW, SID, SID2, writeFile } from "./fleet-fixtures.js";
import { makeDeps } from "./helpers.js";

const s = makeScrubber();
const AGENT = { pid: 700, ppid: 1, tty: "ttys002", start: "s", command: "/Users/dev/.local/share/cursor-agent/versions/1/cursor-agent" };
const summary = (o: Partial<SpoolSummary>): SpoolSummary => ({ last: null, pending: null, compacting: false, bg: null, openAgents: [], transcript: null, stopStatus: null, ...o });

describe("discoverCursor", () => {
  it("marks only the newest chat in a cwd with a running cursor-agent as alive, all estimated", () => {
    const deps = makeDeps();
    cursorChat(deps.home, SID, { updatedAtMs: NOW.getTime() - 10_000, prompts: ["first", "Add dark mode"] });
    cursorChat(deps.home, SID2, { updatedAtMs: NOW.getTime() - 50_000 });
    cursorTranscript(deps.home, SID, [CU.user("Add dark mode"), CU.tool("Shell", { command: "pnpm test" })]);
    const ctx = fleetCtx({ deps, procs: [AGENT], io: fleetIo({ proc: fakeProcTable([AGENT], { 700: CWD }) }) });
    const ss = discoverCursor(ctx).sessions;
    expect(ss.map((x) => [x.id, x.alive, x.estimated])).toEqual([[SID, true, true], [SID2, false, true]]);
    expect(ss[0]).toMatchObject({ provider: "cursor", pid: 700, tty: "ttys002", title: "Add dark mode", activity: "Shell: pnpm test", host: "terminal" });
    expect(ss[0].signals).toMatchObject({ busy: true, turnOpen: true, lastWriteAt: NOW.toISOString() });
    expect(ss[1]).toMatchObject({ host: "cursor", pid: null });
  });

  it("reads turn outcomes from turn_ended markers", () => {
    const sig = (lines: object[]) => cursorSignals(lines as Record<string, unknown>[], NOW.toISOString(), s).signals;
    expect(sig([CU.user("a"), CU.text("ok"), CU.turnEnded("success")])).toMatchObject({ turnOpen: false, terminalReason: "completed" });
    expect(sig([CU.user("a"), CU.turnEnded("error")])).toMatchObject({ terminalReason: "error", apiError: { kind: "error" } });
    expect(sig([CU.user("a"), CU.turnEnded("aborted")]).terminalReason).toBe("aborted_streaming");
    expect(sig([CU.user("a"), CU.turnEnded("cancelled")]).terminalReason).toBe("aborted_streaming");
    expect(sig([CU.user("<system>x</system>"), CU.text("Hello\nthere")])).toMatchObject({ turnOpen: false });
    expect(cursorSignals([CU.text("Hello\nthere")] as Record<string, unknown>[], null, s).activity).toBe("Hello");
  });

  it("uses a spool transcript path inside ~/.cursor and the spool's stop status, and is then not estimated", () => {
    const deps = makeDeps();
    cursorChat(deps.home, SID);
    const t = writeFile(path.join(deps.home, ".cursor", "projects", "other", "agent-transcripts", "x", "x.jsonl"), `${JSON.stringify(CU.user("from spool"))}\n`);
    const [x] = discoverCursor(fleetCtx({ deps, spool: new Map([[`cursor:${SID}`, summary({ transcript: t, stopStatus: "aborted" })]]) })).sessions;
    expect(x).toMatchObject({ estimated: false, transcript: t });
    expect(x.signals.terminalReason).toBe("aborted_streaming");
    const outside = discoverCursor(fleetCtx({ deps, spool: new Map([[`cursor:${SID}`, summary({ transcript: "/etc/hosts" })]]) })).sessions[0];
    expect(outside.transcript).toBeNull();
  });

  it("skips bad and empty chats, old chats, non-uuid dirs and a missing dir; stops at the budget", () => {
    const deps = makeDeps();
    cursorChat(deps.home, SID);
    cursorChat(deps.home, SID2, { hasConversation: false });
    writeFile(path.join(deps.home, ".cursor", "chats", "h2", "33333333-3333-4333-8333-333333333333", "meta.json"), "{bad");
    cursorChat(deps.home, "44444444-4444-4444-8444-444444444444", { updatedAtMs: NOW.getTime() - 2 * 86_400_000 });
    fs.mkdirSync(path.join(deps.home, ".cursor", "chats", "h3", "not-a-uuid"), { recursive: true });
    expect(discoverCursor(fleetCtx({ deps })).sessions.map((x) => x.id)).toEqual([SID]);
    expect(discoverCursor(fleetCtx({ deps, overBudget: () => true }))).toEqual({ sessions: [], truncated: true });
    expect(discoverCursor(fleetCtx({ deps: makeDeps() }))).toEqual({ sessions: [], truncated: false });
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd sindri && npx vitest run tests/fleet-codex.test.ts tests/fleet-cursor.test.ts`
Expected: FAIL with `Failed to load url ../src/fleet/adapters/codex.js` and `../src/fleet/adapters/cursor.js`.

- [ ] **Step 3: Implement**

`sindri/src/fleet/adapters/codex.ts`:

```ts
import fs from "node:fs";
import path from "node:path";

import type { Scrubber } from "../../scrub/scrub.js";
import { MAX_SESSIONS_PER_SOURCE, RECENT_MS } from "../limits.js";
import { iso, latestIso, providerHomes } from "../paths.js";
import { ancestorsOf, hostFromAncestors } from "../proc.js";
import { parseJsonLines, readHead, readTail } from "../tail.js";
import { cleanLine, cleanText, MAX_ACTIVITY, MAX_TIMELINE, MAX_TITLE } from "../text.js";
import { emptySignals, spoolKey, type Discovery, type FleetCtx, type Session, type SessionSignals, type TimelineEntry } from "../types.js";
import { apiErrorOf } from "./claude.js";

const ROLLOUT = /^rollout-.+-([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\.jsonl$/;
// A rollout stays in the directory of the day its session started, so a long session can be appended to
// in an old day dir. The newest 60 day dirs bound the walk; a session started before that and still
// running doesn't show (docs/sindri/dashboard.md says so).
const MAX_DAY_DIRS = 60;

const obj = (v: unknown): Record<string, unknown> => (typeof v === "object" && v !== null && !Array.isArray(v) ? (v as Record<string, unknown>) : {});
const str = (v: unknown): string | null => (typeof v === "string" ? v : null);
const ls = (d: string): string[] => {
  try {
    return fs.readdirSync(d);
  } catch {
    return [];
  }
};

interface Rollout {
  id: string;
  file: string;
  mtimeMs: number;
}

function listRollouts(root: string, cutoffMs: number): Rollout[] {
  const days: string[] = [];
  for (const y of ls(root).filter((n) => /^\d{4}$/.test(n))) {
    for (const m of ls(path.join(root, y)).filter((n) => /^\d{2}$/.test(n))) {
      for (const d of ls(path.join(root, y, m)).filter((n) => /^\d{2}$/.test(n))) days.push(path.join(y, m, d));
    }
  }
  const out: Rollout[] = [];
  for (const day of days.sort().reverse().slice(0, MAX_DAY_DIRS)) {
    for (const n of ls(path.join(root, day))) {
      const m = ROLLOUT.exec(n);
      const file = path.join(root, day, n);
      const st = m === null ? undefined : fs.statSync(file, { throwIfNoEntry: false });
      if (m !== null && st !== undefined && st.isFile() && st.mtimeMs >= cutoffMs) out.push({ id: m[1], file, mtimeMs: st.mtimeMs });
    }
  }
  return out.sort((a, b) => b.mtimeMs - a.mtimeMs);
}

export function codexMeta(head: string | null): { id: string | null; parent: string | null; guardian: boolean; originator: string | null; cwd: string | null } {
  let e: Record<string, unknown> = {};
  try {
    e = obj(JSON.parse(head ?? ""));
  } catch {
    // a missing or corrupt first line: the id comes from the file name
  }
  const p = obj(e.payload);
  const sub = obj(obj(p.source).subagent);
  return {
    id: e.type === "session_meta" ? str(p.id) : null,
    parent: str(obj(sub.thread_spawn).parent_thread_id),
    guardian: str(sub.other) !== null,
    originator: str(p.originator),
    cwd: str(p.cwd),
  };
}

const contentText = (content: unknown): string => (Array.isArray(content) ? content.map((c) => str(obj(c).text) ?? "").join("\n").trim() : "");
// Codex prefixes prompts with context blocks (<ide_opened_file>…</ide_opened_file>); the human text follows them.
const stripTags = (t: string): string => t.replace(/^(\s*<([A-Za-z_]+)>[\s\S]*?<\/\2>)+\s*/, "");

function callLabel(name: string, p: Record<string, unknown>): string {
  let arg = str(p.input) ?? "";
  try {
    const a = obj(JSON.parse(str(p.arguments) ?? "{}"));
    const cmd = a.command ?? a.cmd;
    arg = Array.isArray(cmd) ? cmd.join(" ") : (str(cmd) ?? arg);
  } catch {
    // arguments that aren't JSON: keep the input string
  }
  return arg === "" ? name : `${name}: ${arg}`;
}

export interface ParsedCodex {
  signals: SessionSignals;
  title: string | null;
  activity: string;
  tokens: number | null;
  turnStartedAt: string | null;
  promptTimes: string[];
  timeline: TimelineEntry[];
  cwd: string | null;
  firstTs: string | null;
  waitingOnApproval: boolean;
  waitingOnUserInput: boolean;
}

export function codexSignals(entries: readonly Record<string, unknown>[], scrubber: Scrubber): ParsedCodex {
  const s = emptySignals();
  const open = new Set<string>();
  const timeline: TimelineEntry[] = [];
  const promptTimes: string[] = [];
  let title: string | null = null;
  let activity = "";
  let tokens: number | null = null;
  let turnStartedAt: string | null = null;
  let cwd: string | null = null;
  let firstTs: string | null = null;
  let approvalAt: string | null = null;
  let inputAt: string | null = null;
  const push = (ts: string | null, kind: TimelineEntry["kind"], text: string): void => {
    timeline.push({ ts, kind, text: cleanText(text, MAX_TIMELINE, scrubber) });
    if (timeline.length > 40) timeline.shift();
  };
  for (const e of entries) {
    const type = str(e.type);
    const ts = str(e.timestamp);
    const p = obj(e.payload);
    if (ts !== null) {
      s.lastWriteAt = ts;
      firstTs ??= ts;
    }
    if (type === "turn_context") cwd = str(p.cwd) ?? cwd;
    if (type === "event_msg") {
      const t = str(p.type) ?? "";
      if (t === "task_started") {
        s.turnOpen = true;
        s.terminalReason = null;
        s.apiError = null;
        turnStartedAt = ts;
      } else if (t === "task_complete") {
        s.turnOpen = false;
        s.terminalReason = "completed";
        push(ts, "turn-end", "turn finished");
      } else if (t === "turn_aborted") {
        s.turnOpen = false;
        s.terminalReason = "aborted_streaming";
        open.clear();
        push(ts, "turn-end", `interrupted (${str(p.reason) ?? "aborted"})`);
      } else if (t === "error") {
        const m = str(p.message) ?? "error";
        s.apiError = apiErrorOf(m, null);
        s.terminalReason = "error";
        s.turnOpen = false;
        push(ts, "error", m);
      } else if (t === "token_count") {
        const total = obj(obj(p.info).total_token_usage).total_tokens;
        if (typeof total === "number") tokens = total;
      } else if (t.endsWith("_approval_request")) {
        approvalAt = ts;
        push(ts, "tool", "waiting for approval");
      } else if (t === "request_user_input") inputAt = ts;
    }
    if (type === "response_item") {
      const pt = str(p.type) ?? "";
      if (pt === "message") {
        const text = contentText(p.content);
        if (p.role === "assistant") {
          s.lastOutputAt = ts ?? s.lastOutputAt;
          if (text !== "") {
            activity = text.split("\n")[0];
            push(ts, "text", text);
          }
        } else if (p.role === "user") {
          const human = stripTags(text);
          if (human !== "" && !human.startsWith("<") && !human.startsWith("# AGENTS.md")) {
            title ??= human;
            if (ts !== null) promptTimes.push(ts);
            push(ts, "prompt", human);
          }
        }
      } else if (pt.endsWith("_output")) {
        s.lastOutputAt = ts ?? s.lastOutputAt;
        const id = str(p.call_id);
        if (id !== null) open.delete(id);
        push(ts, "result", "tool result");
      } else if (pt.endsWith("_call")) {
        const id = str(p.call_id);
        if (id !== null) open.add(id);
        activity = callLabel(str(p.name) ?? pt, p);
        push(ts, "tool", activity);
      }
    }
  }
  s.openToolCall = open.size > 0;
  // A request is answered once anything is output after it, or once the turn ends.
  const unanswered = (t: string | null): boolean => t !== null && s.turnOpen && (s.lastOutputAt === null || Date.parse(s.lastOutputAt) <= Date.parse(t));
  return {
    signals: s, title: title === null ? null : cleanLine(title, MAX_TITLE, scrubber), activity: cleanLine(activity, MAX_ACTIVITY, scrubber),
    tokens, turnStartedAt, promptTimes, timeline, cwd, firstTs, waitingOnApproval: unanswered(approvalAt), waitingOnUserInput: unanswered(inputAt),
  };
}

export function discoverCodex(ctx: FleetCtx): Discovery {
  const found = listRollouts(path.join(providerHomes(ctx.deps).codex, "sessions"), ctx.deps.now().getTime() - RECENT_MS);
  const picked = found.slice(0, MAX_SESSIONS_PER_SOURCE);
  let truncated = found.length > MAX_SESSIONS_PER_SOURCE;
  // Liveness: the process holding the rollout open. One batched lsof per pass, never a pid file (Review Focus 2).
  const holders = ctx.io.proc.holders(picked.map((f) => f.file));
  const sessions: Session[] = [];
  for (const f of picked) {
    if (ctx.overBudget()) {
      truncated = true;
      break;
    }
    const meta = codexMeta(readHead(f.file));
    if (meta.guardian) continue;
    const id = meta.id ?? f.id;
    const tail = readTail(f.file);
    const p = codexSignals(tail === null ? [] : parseJsonLines(tail.lines).entries, ctx.scrubber);
    const pid = holders.get(f.file)?.[0] ?? null;
    const alive = pid !== null;
    const probe = ctx.io.codex.status(id);
    const s = p.signals;
    s.busy = alive ? s.turnOpen : null;
    s.codex = { waitingOnApproval: probe?.waitingOnApproval ?? p.waitingOnApproval, waitingOnUserInput: probe?.waitingOnUserInput ?? p.waitingOnUserInput, originator: meta.originator };
    s.spool = ctx.spool.get(spoolKey("codex", id)) ?? null;
    if (s.spool?.compacting === true) s.compacting = true;
    const chain = pid === null ? [] : ancestorsOf(ctx.procs, pid);
    sessions.push({
      provider: "codex", id, parentId: meta.parent, cwd: p.cwd ?? meta.cwd ?? "", repo: null, worktree: null,
      host: hostFromAncestors(chain) ?? "unknown", pid, tty: chain[0]?.tty ?? null, alive,
      startedAt: p.firstTs ?? iso(f.mtimeMs), updatedAt: latestIso(s.lastWriteAt, iso(f.mtimeMs)) as string,
      title: p.title ?? "(untitled)", activity: s.compacting ? "compacting context" : p.activity, signals: s,
      estimated: s.spool === null && probe === null, transcript: f.file, turnStartedAt: p.turnStartedAt, promptTimes: p.promptTimes,
      costUsd: null, tokens: p.tokens, timeline: p.timeline,
    });
  }
  return { sessions, truncated };
}
```

`sindri/src/fleet/adapters/cursor.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import { z } from "zod";

import type { Scrubber } from "../../scrub/scrub.js";
import { MAX_SESSIONS_PER_SOURCE, RECENT_MS } from "../limits.js";
import { iso, isUnder, latestIso, providerHomes } from "../paths.js";
import { ancestorsOf, hostFromAncestors } from "../proc.js";
import { parseJsonLines, readTail } from "../tail.js";
import { cleanLine, cleanText, MAX_ACTIVITY, MAX_TIMELINE, MAX_TITLE } from "../text.js";
import { emptySignals, spoolKey, type Discovery, type FleetCtx, type Session, type SessionSignals, type TerminalReason, type TimelineEntry } from "../types.js";
import { toolLabel, UUID } from "./claude.js";

const Meta = z.object({ createdAtMs: z.number(), updatedAtMs: z.number(), cwd: z.string(), hasConversation: z.boolean().optional() }).passthrough();
const AGENT_CMD = /(^|\/)cursor-agent(\s|$)|\/cursor-agent\//;
const STATUS: Record<string, TerminalReason> = { success: "completed", completed: "completed", error: "error", aborted: "aborted_streaming", cancelled: "aborted_streaming" };

const obj = (v: unknown): Record<string, unknown> => (typeof v === "object" && v !== null && !Array.isArray(v) ? (v as Record<string, unknown>) : {});
const str = (v: unknown): string | null => (typeof v === "string" ? v : null);
const ls = (d: string): string[] => {
  try {
    return fs.readdirSync(d);
  } catch {
    return [];
  }
};

function blocksOf(content: unknown): Record<string, unknown>[] {
  if (typeof content === "string") return [{ type: "text", text: content }];
  return Array.isArray(content) ? content.map(obj) : [];
}

// Cursor transcripts carry no timestamps: the file's mtime stands in for the last write and output.
export function cursorSignals(entries: readonly Record<string, unknown>[], mtimeIso: string | null, scrubber: Scrubber): { signals: SessionSignals; firstPrompt: string | null; activity: string; timeline: TimelineEntry[] } {
  const s = emptySignals();
  s.lastWriteAt = mtimeIso;
  s.lastOutputAt = mtimeIso;
  const timeline: TimelineEntry[] = [];
  let firstPrompt: string | null = null;
  let activity = "";
  const push = (kind: TimelineEntry["kind"], text: string): void => {
    timeline.push({ ts: null, kind, text: cleanText(text, MAX_TIMELINE, scrubber) });
    if (timeline.length > 40) timeline.shift();
  };
  for (const e of entries) {
    if (e.type === "turn_ended") {
      const status = str(e.status) ?? "";
      s.turnOpen = false;
      s.terminalReason = STATUS[status] ?? "completed";
      if (s.terminalReason === "error") s.apiError = { kind: "error", status: null, resetAt: null };
      push("turn-end", `turn ended (${status === "" ? "unknown" : status})`);
      continue;
    }
    const bs = blocksOf(obj(e.message).content);
    if (e.role === "user") {
      const text = bs.map((b) => str(b.text) ?? "").join("\n").trim();
      if (text === "" || text.startsWith("<")) continue;
      s.turnOpen = true;
      s.terminalReason = null;
      s.apiError = null;
      firstPrompt ??= text;
      push("prompt", text);
    } else if (e.role === "assistant") {
      for (const b of bs) {
        const text = str(b.text)?.trim() ?? "";
        if (b.type === "tool_use") {
          activity = toolLabel(str(b.name) ?? "tool", typeof b.input === "object" && b.input !== null ? (b.input as Record<string, unknown>) : undefined);
          push("tool", activity);
        } else if (text !== "") {
          activity = text.split("\n")[0];
          push("text", text);
        }
      }
    }
  }
  return { signals: s, firstPrompt: firstPrompt === null ? null : cleanLine(firstPrompt, MAX_TITLE, scrubber), activity: cleanLine(activity, MAX_ACTIVITY, scrubber), timeline };
}

interface Chat {
  id: string;
  dir: string;
  cwd: string;
  updatedAtMs: number;
}

function lastPrompt(dir: string): string | null {
  try {
    const list: unknown = JSON.parse(fs.readFileSync(path.join(dir, "prompt_history.json"), "utf8"));
    return Array.isArray(list) ? (list.filter((x): x is string => typeof x === "string").at(-1) ?? null) : null;
  } catch {
    return null;
  }
}

export function discoverCursor(ctx: FleetCtx): Discovery {
  const home = providerHomes(ctx.deps).cursor;
  const cutoff = ctx.deps.now().getTime() - RECENT_MS;
  const chats: Chat[] = [];
  for (const hash of ls(path.join(home, "chats"))) {
    for (const id of ls(path.join(home, "chats", hash)).filter((n) => UUID.test(n))) {
      const dir = path.join(home, "chats", hash, id);
      let raw: unknown;
      try {
        raw = JSON.parse(fs.readFileSync(path.join(dir, "meta.json"), "utf8"));
      } catch {
        continue;
      }
      const m = Meta.safeParse(raw);
      if (m.success && m.data.hasConversation !== false && m.data.updatedAtMs >= cutoff) chats.push({ id, dir, cwd: m.data.cwd, updatedAtMs: m.data.updatedAtMs });
    }
  }
  chats.sort((a, b) => b.updatedAtMs - a.updatedAtMs);
  const picked = chats.slice(0, MAX_SESSIONS_PER_SOURCE);
  let truncated = chats.length > MAX_SESSIONS_PER_SOURCE;
  const wanted = new Set(picked.map((c) => c.id));
  const transcripts = new Map<string, string>();
  for (const slug of ls(path.join(home, "projects"))) {
    for (const id of ls(path.join(home, "projects", slug, "agent-transcripts")).filter((n) => wanted.has(n))) {
      transcripts.set(id, path.join(home, "projects", slug, "agent-transcripts", id, `${id}.jsonl`));
    }
  }
  const agents = ctx.procs.filter((p) => AGENT_CMD.test(p.command));
  const cwds = ctx.io.proc.cwds(agents.map((a) => a.pid));
  const liveByCwd = new Map<string, number>();
  for (const a of agents) {
    const c = cwds.get(a.pid);
    if (c !== undefined) liveByCwd.set(c, a.pid);
  }
  const newestByCwd = new Map<string, string>();
  for (const c of picked) if (!newestByCwd.has(c.cwd)) newestByCwd.set(c.cwd, c.id); // picked is newest first
  const sessions: Session[] = [];
  for (const c of picked) {
    if (ctx.overBudget()) {
      truncated = true;
      break;
    }
    const spool = ctx.spool.get(spoolKey("cursor", c.id)) ?? null;
    const spoolPath = spool?.transcript ?? null;
    const transcript = transcripts.get(c.id) ?? (spoolPath !== null && isUnder(spoolPath, home) ? spoolPath : null);
    const tail = transcript === null ? null : readTail(transcript);
    const p = cursorSignals(tail === null ? [] : parseJsonLines(tail.lines).entries, iso(tail === null ? c.updatedAtMs : tail.mtimeMs), ctx.scrubber);
    const s = p.signals;
    const stop = spool?.stopStatus ?? null;
    if (stop !== null && STATUS[stop] !== undefined) {
      s.turnOpen = false;
      s.terminalReason = STATUS[stop];
    }
    const pid = newestByCwd.get(c.cwd) === c.id ? (liveByCwd.get(c.cwd) ?? null) : null;
    const alive = pid !== null;
    s.busy = alive ? s.turnOpen : null;
    s.spool = spool;
    // The agent's own process would match the Cursor pattern; the host is whatever runs it.
    const chain = pid === null ? [] : ancestorsOf(ctx.procs, pid);
    const prompt = lastPrompt(c.dir);
    sessions.push({
      provider: "cursor", id: c.id, parentId: null, cwd: c.cwd, repo: null, worktree: null,
      host: alive ? (hostFromAncestors(chain.slice(1)) ?? "terminal") : "cursor", pid, tty: chain[0]?.tty ?? null, alive,
      startedAt: iso(c.updatedAtMs), updatedAt: latestIso(s.lastWriteAt, iso(c.updatedAtMs)) as string,
      title: prompt === null ? (p.firstPrompt ?? "(untitled)") : cleanLine(prompt, MAX_TITLE, ctx.scrubber),
      activity: p.activity, signals: s, estimated: spool === null, transcript, turnStartedAt: null, promptTimes: [], costUsd: null, tokens: null, timeline: p.timeline,
    });
  }
  return { sessions, truncated };
}
```

Trace for the spool test: the chat has no `agent-transcripts` entry of its own. The spool's path `…/.cursor/projects/other/agent-transcripts/x/x.jsonl` is inside `~/.cursor`, so it is read. The stop status `aborted` sets `aborted_streaming`. With `/etc/hosts` the path is outside `~/.cursor` and is refused, leaving `transcript: null`.

- [ ] **Step 4: Run the tests**

Run: `cd sindri && npx vitest run tests/fleet-codex.test.ts tests/fleet-cursor.test.ts`
Expected: PASS. Then, once for the task: `npm test && npm run typecheck && npm run test:coverage`. Expected: all PASS, coverage 100%.

- [ ] **Step 5: Commit**

```bash
git add sindri/src/fleet sindri/tests
git commit -m "feat: sindri fleet Codex and Cursor adapters" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 4: Sindri jobs: job records from `runCli`, the heavy-lock queue and repo status

**Files:**
- Create: `sindri/src/fleet/jobs.ts`, `sindri/src/fleet/adapters/sindri.ts`
- Modify: `sindri/src/main.ts` (record jobs around `command.run`), `sindri/src/index/heavy-lock.ts` (waiter records, `heavyLockQueue`), `sindri/src/fleet/types.ts` (add `PrView`)
- Test: `sindri/tests/fleet-jobs.test.ts`, `sindri/tests/fleet-sindri.test.ts`, `sindri/tests/heavy-lock-queue.test.ts`

**Interfaces:**
- Consumes:
  - from Tasks 1–2: `fleetDir`, `cleanLine`, `sameStart`, `readTail`, `parseJsonLines`, `FleetCtx`;
  - existing code: `ulid`, `CommandResult`, `makeScrubber`, `heavyLockState`, `heavyLockDir`, `awStateDir`, `openIndexReadOnly`, `indexPath`, `meta` (index), `Ledger`, `GitRunner`.
- Produces:
  - `jobs.ts`:
    - `JobRecord`, `JobState = "running" | "ok" | "attn" | "failed" | "crashed"`, `JobView`;
    - `jobKindOf(name, rest)`: `observe` (not `--no-record`), `doctor`, `index build` (`index-build:<repo|all>` or `index-quick:<repo|all>`), `evolve weekly`/`check` (`evolve-weekly`, `evolve-check`), `scope <brief|linear:…>` (`scope`) and `shape reconcile` (`shape-reconcile`). It returns null for `--help`, `--dry-run`, `--list` and every other command;
    - `startJob(deps, name, rest): JobHandle | null` and `finishJob(deps, handle, result)`. The summary is one scrubbed line, or up to 8 for `evolve-weekly` (its steps). Neither ever throws: a failure goes to `deps.log`;
    - `jobState(rec, sys)`, `readJobs(deps): JobView[]` (the latest run per kind) and `jobHistory(deps, limit?)`.
    - Files: `fleet/jobs/latest/<kind>.json` (`:` becomes `__`) and `fleet/jobs/history.jsonl`, which rotates at 1 MiB into `history.1.jsonl`. All are written 0600, and `latest` atomically.
  - `heavy-lock.ts`: `withHeavyLock` writes `<lock>.queue/<pid>-<ulid>.json` (`{ kind, pid, host, since }`) from its first wait until it stops waiting. `heavyLockQueue(stateRoot, env, sys): QueueEntry[]` returns the waiters oldest first, with dead ones on this host dropped.
  - `types.ts`: `PrView { repo; number; title; url; draft; reviewDecision; mergeState; author; requestedMe; readyToMerge; createdAt }`.
  - `adapters/sindri.ts`:
    - `HeavyLockView`, `RepoIndexView`, `RepoView`, `ScopeRunView`, `SindriReport`;
    - `discoverSindri(ctx): Promise<SindriReport>`, returning the latest job per kind, the history, the lock holder and queue, the scope runs, and per approved repo: index age, last build job, file and symbol counts, commits behind `HEAD`, staleness, last observe and backlog for the tracker repo, and the scope runs whose output lies in that repo. `prs` and `worktrees` stay empty here; Tasks 7 and 8 fill them.

- [ ] **Step 1: Write the failing tests**

`sindri/tests/fleet-jobs.test.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { finishJob, jobHistory, jobKindOf, jobState, readJobs, startJob } from "../src/fleet/jobs.js";
import { runCli } from "../src/main.js";
import { fakeSystem, makeDeps } from "./helpers.js";

describe("jobKindOf", () => {
  it("names the commands that are jobs, and nothing else", () => {
    expect(jobKindOf("observe", [])).toEqual({ kind: "observe", repo: null });
    expect(jobKindOf("observe", ["--no-record"])).toBeNull();
    expect(jobKindOf("index", ["build", "--repo", "acme-web"])).toEqual({ kind: "index-build:acme-web", repo: "acme-web" });
    expect(jobKindOf("index", ["build", "--quick", "--repo=acme-web"])).toEqual({ kind: "index-quick:acme-web", repo: "acme-web" });
    expect(jobKindOf("index", ["build", "--repo", "Bad Name"])).toEqual({ kind: "index-build:all", repo: null });
    expect(jobKindOf("index", ["status"])).toBeNull();
    expect(jobKindOf("evolve", ["weekly"])).toEqual({ kind: "evolve-weekly", repo: null });
    expect(jobKindOf("evolve", ["weekly", "--dry-run"])).toBeNull();
    expect(jobKindOf("evolve", ["check", "--list"])).toBeNull();
    expect(jobKindOf("evolve", ["status"])).toBeNull();
    expect(jobKindOf("scope", ["brief.md"])).toEqual({ kind: "scope", repo: null });
    expect(jobKindOf("scope", ["runs"])).toBeNull();
    expect(jobKindOf("scope", [])).toBeNull();
    expect(jobKindOf("shape", ["reconcile"])).toEqual({ kind: "shape-reconcile", repo: null });
    expect(jobKindOf("doctor", ["--json"])).toEqual({ kind: "doctor", repo: null });
    expect(jobKindOf("fleet", ["--json"])).toBeNull();
    expect(jobKindOf("doctor", ["--help"])).toBeNull();
  });
});

describe("startJob, finishJob and readJobs", () => {
  it("records a start, then the end with a scrubbed one-line summary; history keeps both", () => {
    const deps = makeDeps();
    const h = startJob(deps, "index", ["build", "--repo", "acme-web"]);
    expect(readJobs(deps)).toMatchObject([{ kind: "index-build:acme-web", state: "running", endedAt: null }]);
    finishJob(deps, h, { exitCode: 2, stdout: "", stderr: `SND-INDEX-001 the heavy-job lock is busy ${"AKIA" + "ABCDEFGHIJKLMNOP"}\n  fix: wait` });
    const [j] = readJobs(deps);
    expect(j).toMatchObject({ state: "failed", exitCode: 2, summary: "SND-INDEX-001 the heavy-job lock is busy [REDACTED:aws-access-key]" });
    const file = path.join(deps.env.AW_STATE_DIR as string, "sindri", "fleet", "jobs", "latest", "index-build__acme-web.json");
    expect(fs.statSync(file).mode & 0o777).toBe(0o600);
    expect(jobHistory(deps).map((x) => [x.kind, x.endedAt === null])).toEqual([["index-build:acme-web", false]]);
    finishJob(deps, null, { exitCode: 0, stdout: "", stderr: "" }); // not a job: nothing happens
  });

  it("gives ok, attn and failed by exit code, takes summaries from stdout unless it's JSON, and rotates history", () => {
    const deps = makeDeps();
    finishJob(deps, startJob(deps, "observe", []), { exitCode: 0, stdout: "3 open items\nNext: x", stderr: "" });
    finishJob(deps, startJob(deps, "evolve", ["weekly"]), { exitCode: 1, stdout: "attn reflect: x\nok   check: 3 suites\nWeekly: 5 steps, 4 ok, 1 need attention.\n", stderr: "" });
    finishJob(deps, startJob(deps, "doctor", ["--json"]), { exitCode: 2, stdout: '[{"status":"fail"}]', stderr: "" });
    const by = Object.fromEntries(readJobs(deps).map((j) => [j.kind, [j.state, j.summary]]));
    expect(by).toEqual({ observe: ["ok", "3 open items"], "evolve-weekly": ["attn", "attn reflect: x\nok check: 3 suites\nWeekly: 5 steps, 4 ok, 1 need attention."], doctor: ["failed", ""] });
    const hist = path.join(deps.env.AW_STATE_DIR as string, "sindri", "fleet", "jobs", "history.jsonl");
    fs.appendFileSync(hist, `${"x".repeat(1_100_000)}\n`);
    startJob(deps, "observe", []);
    expect(fs.existsSync(`${hist.slice(0, -6)}.1.jsonl`)).toBe(true);
    expect(jobHistory(deps, 1)).toMatchObject([{ kind: "observe", endedAt: null }]); // the newest run survives the rotation
  });

  it("calls a job whose process is gone, or whose pid now belongs to another program, crashed", () => {
    const sys = fakeSystem();
    const rec = { id: "01hzzzzzzzzzzzzzzzzzzzzzzz", kind: "observe", repo: null, argv: [], pid: 50, pidStart: "start-50", host: "test-host", startedAt: "x", endedAt: null, exitCode: null, summary: "" };
    expect(jobState(rec, sys)).toBe("running");
    expect(jobState(rec, fakeSystem({ pidAlive: () => false }))).toBe("crashed");
    expect(jobState({ ...rec, pidStart: "start-old" }, sys)).toBe("crashed");
    expect(jobState({ ...rec, host: "other-host" }, sys)).toBe("running");
    expect(jobState({ ...rec, endedAt: "y", exitCode: 0 }, sys)).toBe("ok");
  });

  it("never throws: an unwritable state dir is logged, and junk files are skipped", () => {
    const lines: string[] = [];
    const deps = makeDeps({ log: (l) => lines.push(l) });
    const root = path.join(deps.env.AW_STATE_DIR as string, "sindri", "fleet");
    fs.mkdirSync(path.dirname(root), { recursive: true });
    fs.writeFileSync(root, "a file where the dir should be");
    expect(startJob(deps, "observe", [])).toBeNull();
    finishJob(deps, { record: { id: "01hzzzzzzzzzzzzzzzzzzzzzzz", kind: "observe", repo: null, argv: [], pid: 1, pidStart: null, host: "h", startedAt: "x", endedAt: null, exitCode: null, summary: "" }, file: "x" }, { exitCode: 0, stdout: "", stderr: "" });
    expect(lines).toHaveLength(2);
    expect(readJobs(deps)).toEqual([]);
    const ok = makeDeps();
    const dir = path.join(ok.env.AW_STATE_DIR as string, "sindri", "fleet", "jobs", "latest");
    fs.mkdirSync(dir, { recursive: true });
    fs.writeFileSync(path.join(dir, "junk.json"), "{nope");
    fs.writeFileSync(path.join(dir, "shape.json"), JSON.stringify({ kind: "x" }));
    expect(readJobs(ok)).toEqual([]);
    expect(jobHistory(ok)).toEqual([]);
  });

  it("is recorded by runCli around a job command, including one that crashes", async () => {
    const deps = makeDeps();
    await runCli(["doctor", "--json"], deps);
    expect(readJobs(deps).map((j) => j.kind)).toEqual(["doctor"]);
    await runCli(["fleet", "--help"], deps);
    expect(readJobs(deps)).toHaveLength(1);
  });
});
```

`sindri/tests/heavy-lock-queue.test.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { awStateDir } from "../src/deps.js";
import { heavyLockDir, heavyLockQueue, withHeavyLock } from "../src/index/heavy-lock.js";
import { fakeSystem, makeDeps } from "./helpers.js";

describe("heavy-lock queue", () => {
  it("records a waiter while it waits and removes it when it gets the lock or gives up", async () => {
    const deps = makeDeps();
    const root = awStateDir(deps);
    const dir = heavyLockDir(root, deps.env);
    fs.mkdirSync(dir, { recursive: true }); // held by someone else
    fs.writeFileSync(`${dir}.holder.json`, JSON.stringify({ kind: "suite:x", pid: 1, host: "test-host", bootId: "boot-1", startedAt: "2026-10-08T11:59:00.000Z" }));
    let seen: unknown[] = [];
    const waiting = { ...deps, sleep: async () => { seen = heavyLockQueue(root, deps.env, deps.system); fs.rmSync(`${dir}.holder.json`); fs.rmdirSync(dir); } };
    await withHeavyLock(waiting, "index:acme-web", 5000, async () => undefined);
    expect(seen).toEqual([{ kind: "index:acme-web", pid: 4242, host: "test-host", since: "2026-10-08T12:00:00.000Z" }]);
    expect(heavyLockQueue(root, deps.env, deps.system)).toEqual([]);
    fs.mkdirSync(dir);
    await expect(withHeavyLock(deps, "index:b", 0, async () => undefined)).rejects.toThrow(/heavy-job lock is busy/);
    expect(heavyLockQueue(root, deps.env, deps.system)).toEqual([]);
  });

  it("drops dead waiters on this host, keeps other hosts, sorts oldest first and skips junk", () => {
    const deps = makeDeps();
    const root = awStateDir(deps);
    const q = `${heavyLockDir(root, deps.env)}.queue`;
    fs.mkdirSync(q, { recursive: true });
    const put = (name: string, v: unknown) => fs.writeFileSync(path.join(q, name), typeof v === "string" ? v : JSON.stringify(v));
    put("1.json", { kind: "b", pid: 2, host: "test-host", since: "2026-10-08T11:00:02.000Z" });
    put("2.json", { kind: "a", pid: 3, host: "other", since: "2026-10-08T11:00:01.000Z" });
    put("3.json", { kind: "dead", pid: 9, host: "test-host", since: "2026-10-08T11:00:00.000Z" });
    put("4.json", "{junk");
    const sys = fakeSystem({ pidAlive: (pid) => pid !== 9 });
    expect(heavyLockQueue(root, deps.env, sys).map((e) => e.kind)).toEqual(["a", "b"]);
    expect(heavyLockQueue(path.join(root, "nowhere"), {}, sys)).toEqual([]);
  });
});
```

`sindri/tests/fleet-sindri.test.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { awStateDir } from "../src/deps.js";
import { discoverSindri } from "../src/fleet/adapters/sindri.js";
import { finishJob, startJob } from "../src/fleet/jobs.js";
import { heavyLockDir } from "../src/index/heavy-lock.js";
import { indexPath, openIndex } from "../src/index/db.js";
import { evolveFixture, git } from "./evolve-fixtures.js";
import { fleetCtx } from "./fleet-fixtures.js";

describe("discoverSindri", () => {
  it("reports jobs, the lock holder and queue, and each approved repo's index, observe and scope runs", async () => {
    const fx = await evolveFixture();
    const deps = fx.deps;
    const name = fx.ctx.loaded.profile.tracker.repo;
    const head = git(fx.repo, "rev-parse", "HEAD").trim();
    const ix = openIndex(indexPath(deps, name));
    ix.prepare("INSERT INTO meta (key, value) VALUES ('commit', ?), ('built_at', ?)").run(head, "2026-10-08T11:00:00.000Z");
    ix.prepare("INSERT INTO files (path, hash, size) VALUES ('a.ts', 'h', 1), ('b.ts', 'h', 1)").run();
    ix.close();
    fs.writeFileSync(path.join(fx.repo, "c.txt"), "x");
    git(fx.repo, "add", "-A");
    git(fx.repo, "commit", "-qm", "two");
    finishJob(deps, startJob(deps, "index", ["build", "--repo", name]), { exitCode: 0, stdout: "built", stderr: "" });
    finishJob(deps, startJob(deps, "observe", []), { exitCode: 0, stdout: "1 open", stderr: "" });
    fx.ctx.db.prepare("INSERT INTO items (source, id, title, state, content_hash, first_seen, last_seen, epoch) VALUES (?, 'i1', 't', 'open', 'h', 'x', 'x', 0)").run(`plan-file:${name}`);
    fx.ctx.db.prepare("INSERT INTO scope_runs (run_id, subject, mode, ts, status, rounds, surfaces, tokens, out_path, epoch) VALUES ('r1', 'Brief with \u001b[31mcolour', 'scope', '2026-10-08T10:00:00Z', 'ok', 1, 3, 10, ?, 0), ('r2', 'Elsewhere', 'scope', '2026-10-08T09:00:00Z', 'ok', 1, 1, 10, '/tmp/x.md', 0)").run(path.join(fx.repo, "maps", "m.md"));
    const lock = heavyLockDir(awStateDir(deps), deps.env);
    fs.mkdirSync(lock, { recursive: true });
    fs.writeFileSync(`${lock}.holder.json`, JSON.stringify({ kind: "suite:package:sindri", pid: 77, host: "test-host", bootId: "boot-1", startedAt: "2026-10-08T11:50:00.000Z" }));

    const r = await discoverSindri(fleetCtx({ deps, loaded: fx.ctx.loaded, db: fx.ctx.db }));
    expect(r.jobs.map((j) => j.kind).sort()).toEqual([`index-build:${name}`, "observe"]);
    expect(r.heavyLock).toMatchObject({ held: true, holder: { kind: "suite:package:sindri", pid: 77 }, queue: [] });
    const repo = r.repos.find((x) => x.name === name);
    expect(repo?.index).toMatchObject({ builtAt: "2026-10-08T11:00:00.000Z", ageMs: 3_600_000, stale: false, commit: head, files: 2, symbols: 0, behind: 1, lastBuild: { state: "ok" } });
    expect(repo?.observe).toMatchObject({ backlog: 1 });
    expect(repo?.observe?.at).not.toBeNull();
    expect(repo?.scopeRuns.map((x) => [x.id, x.subject])).toEqual([["r1", "Brief with \\u{001B}[31mcolour"]]);
    expect(r.scopeRuns.map((x) => x.id)).toEqual(["r1", "r2"]);
    fx.close();
  });

  it("works with no profile, no ledger and no index: empty repos, a free lock", async () => {
    const r = await discoverSindri(fleetCtx());
    expect(r).toEqual({ jobs: [], history: [], heavyLock: { held: false, holder: null, queue: [] }, repos: [], scopeRuns: [] });
  });

  it("marks a missing index stale, and leaves behind unknown when git can't answer", async () => {
    const fx = await evolveFixture();
    const ctx = fleetCtx({ deps: fx.deps, loaded: fx.ctx.loaded, db: fx.ctx.db });
    expect((await discoverSindri(ctx)).repos[0].index).toMatchObject({ builtAt: null, ageMs: null, stale: true, files: null, behind: null, lastBuild: null });
    const ix = openIndex(indexPath(fx.deps, fx.ctx.loaded.profile.tracker.repo));
    ix.prepare("INSERT INTO meta (key, value) VALUES ('commit', 'abc123'), ('built_at', '2026-10-06T12:00:00.000Z')").run();
    ix.close();
    const broken = { ...fx.deps, git: { run: async () => ({ ok: false as const, stderr: "fatal" }) } };
    const r = await discoverSindri(fleetCtx({ deps: broken, loaded: fx.ctx.loaded, db: fx.ctx.db }));
    expect(r.repos[0].index).toMatchObject({ commit: "abc123", ageMs: 172_800_000, stale: true, behind: null });
    fx.close();
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd sindri && npx vitest run tests/fleet-jobs.test.ts tests/heavy-lock-queue.test.ts tests/fleet-sindri.test.ts`
Expected: FAIL with `Failed to load url ../src/fleet/jobs.js`. The heavy-lock test fails on the missing export `heavyLockQueue`.

- [ ] **Step 3: Implement**

`sindri/src/fleet/jobs.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import { z } from "zod";

import type { Deps } from "../deps.js";
import { ulid } from "../ids.js";
import type { CommandResult } from "../output.js";
import { makeScrubber } from "../scrub/scrub.js";
import type { SystemProbe } from "../system.js";
import { fleetDir } from "./paths.js";
import { sameStart } from "./proc.js";
import { parseJsonLines, readTail } from "./tail.js";
import { cleanLine } from "./text.js";

const scrubber = makeScrubber();
const HISTORY_MAX = 1_048_576;

export type JobState = "running" | "ok" | "attn" | "failed" | "crashed";
const JobRecordSchema = z.object({
  id: z.string().regex(/^[0-9a-z]{26}$/),
  kind: z.string().regex(/^[a-z][a-z0-9-]*(:[a-z0-9-]+)?$/),
  repo: z.string().nullable(),
  argv: z.array(z.string()),
  pid: z.number().int(),
  pidStart: z.string().nullable(),
  host: z.string(),
  startedAt: z.string(),
  endedAt: z.string().nullable(),
  exitCode: z.number().int().nullable(),
  summary: z.string(),
});
export type JobRecord = z.infer<typeof JobRecordSchema>;
export type JobView = JobRecord & { state: JobState };
export interface JobHandle {
  record: JobRecord;
  file: string;
}

const REPO = /^[a-z0-9][a-z0-9-]{0,38}$/;

function flag(rest: readonly string[], name: string): string | null {
  const i = rest.indexOf(name);
  if (i >= 0 && rest[i + 1] !== undefined) return rest[i + 1];
  const eq = rest.find((a) => a.startsWith(`${name}=`));
  return eq === undefined ? null : eq.slice(name.length + 1);
}

// Spec decision 10: the runs whose outcome the dashboard shows. Read-only commands, previews and the
// fleet and dashboard themselves aren't jobs.
export function jobKindOf(name: string, rest: readonly string[]): { kind: string; repo: string | null } | null {
  if (rest.some((a) => a === "--help" || a === "-h" || a === "--dry-run" || a === "--list")) return null;
  const given = flag(rest, "--repo");
  const repo = given !== null && REPO.test(given) ? given : null;
  const sub = rest.find((a) => !a.startsWith("-"));
  switch (name) {
    case "observe":
      return rest.includes("--no-record") ? null : { kind: "observe", repo: null };
    case "doctor":
      return { kind: "doctor", repo: null };
    case "index":
      return sub === "build" ? { kind: `${rest.includes("--quick") ? "index-quick" : "index-build"}:${repo ?? "all"}`, repo } : null;
    case "evolve":
      return sub === "weekly" || sub === "check" ? { kind: `evolve-${sub}`, repo: null } : null;
    case "scope":
      return sub === undefined || sub === "runs" ? null : { kind: "scope", repo: null };
    case "shape":
      return sub === "reconcile" ? { kind: "shape-reconcile", repo: null } : null;
    default:
      return null;
  }
}

const jobsDir = (deps: Deps): string => path.join(fleetDir(deps), "jobs");

function write(deps: Deps, rec: JobRecord): string {
  const dir = jobsDir(deps);
  fs.mkdirSync(path.join(dir, "latest"), { recursive: true, mode: 0o700 });
  const file = path.join(dir, "latest", `${rec.kind.replace(":", "__")}.json`);
  const tmp = `${file}.${deps.system.pid}.tmp`;
  fs.writeFileSync(tmp, JSON.stringify(rec), { mode: 0o600 });
  fs.renameSync(tmp, file);
  const hist = path.join(dir, "history.jsonl");
  if ((fs.statSync(hist, { throwIfNoEntry: false })?.size ?? 0) > HISTORY_MAX) fs.renameSync(hist, path.join(dir, "history.1.jsonl"));
  fs.appendFileSync(hist, `${JSON.stringify(rec)}\n`, { mode: 0o600 });
  return file;
}

export function startJob(deps: Deps, name: string, rest: readonly string[]): JobHandle | null {
  const k = jobKindOf(name, rest);
  if (k === null) return null;
  try {
    const now = deps.now();
    const sys = deps.system;
    const record: JobRecord = {
      id: ulid(now), kind: k.kind, repo: k.repo, argv: [name, ...rest].slice(0, 20).map((a) => scrubber.scrub(a).text),
      pid: sys.pid, pidStart: sys.pidStartTime(sys.pid), host: sys.hostname(), startedAt: now.toISOString(), endedAt: null, exitCode: null, summary: "",
    };
    return { record, file: write(deps, record) };
  } catch (e) {
    deps.log(`could not record the ${k.kind} job: ${(e as Error).message}`);
    return null;
  }
}

// What the run said: stdout when it succeeded (unless it's --json), else stderr. One line, except the weekly
// job, whose first 8 lines are its steps (spec §5 "the weekly evolve job and its steps").
function summaryOf(r: CommandResult, kind: string): string {
  const text = r.exitCode === 2 || r.stdout.trimStart().startsWith("{") || r.stdout.trimStart().startsWith("[") ? r.stderr : r.stdout;
  const lines = text.split("\n").filter((l) => l.trim() !== "").slice(0, kind === "evolve-weekly" ? 8 : 1);
  return lines.map((l) => cleanLine(l, 200, scrubber)).join("\n");
}

export function finishJob(deps: Deps, h: JobHandle | null, r: CommandResult): void {
  if (h === null) return;
  try {
    write(deps, { ...h.record, endedAt: deps.now().toISOString(), exitCode: r.exitCode, summary: summaryOf(r, h.record.kind) });
  } catch (e) {
    deps.log(`could not record the end of the ${h.record.kind} job: ${(e as Error).message}`);
  }
}

// A run with no end is running while its process lives; gone, or a pid that another program now owns, is
// crashed. Another host's run can't be checked from here, so it stays running.
export function jobState(rec: JobRecord, sys: SystemProbe): JobState {
  if (rec.endedAt === null) {
    if (rec.host !== sys.hostname()) return "running";
    return sys.pidAlive(rec.pid) && sameStart(sys.pidStartTime(rec.pid), rec.pidStart) ? "running" : "crashed";
  }
  return rec.exitCode === 0 ? "ok" : rec.exitCode === 1 ? "attn" : "failed";
}

const parse = (v: unknown): JobRecord | null => {
  const r = JobRecordSchema.safeParse(v);
  return r.success ? r.data : null;
};

export function readJobs(deps: Deps): JobView[] {
  const dir = path.join(jobsDir(deps), "latest");
  let names: string[];
  try {
    names = fs.readdirSync(dir).filter((n) => n.endsWith(".json"));
  } catch {
    return [];
  }
  return names.flatMap((n) => {
    let v: unknown;
    try {
      v = JSON.parse(fs.readFileSync(path.join(dir, n), "utf8"));
    } catch {
      return [];
    }
    const rec = parse(v);
    return rec === null ? [] : [{ ...rec, state: jobState(rec, deps.system) }];
  }).sort((a, b) => b.startedAt.localeCompare(a.startedAt));
}

export function jobHistory(deps: Deps, limit = 50): JobView[] {
  const dir = jobsDir(deps);
  const lines = [path.join(dir, "history.1.jsonl"), path.join(dir, "history.jsonl")].flatMap((f) => readTail(f)?.lines ?? []);
  const byId = new Map<string, JobRecord>();
  for (const e of parseJsonLines(lines).entries) {
    const rec = parse(e);
    if (rec !== null) byId.set(rec.id, rec); // a run's end line comes after its start line
  }
  return [...byId.values()].sort((a, b) => b.startedAt.localeCompare(a.startedAt) || b.id.localeCompare(a.id)).slice(0, limit).map((r) => ({ ...r, state: jobState(r, deps.system) }));
}
```

`sindri/src/main.ts`: import `{ finishJob, startJob }` from `./fleet/jobs.js`, and change the tail of `runCli` to:

```ts
  if (rest.includes("--help") || rest.includes("-h")) return success(command.usage, null, false);
  // Spec decision 10: job commands leave a record (start, end, exit code) for the dashboard.
  const job = startJob(deps, name, rest);
  try {
    const r = await command.run(rest, deps);
    finishJob(deps, job, r);
    return r;
  } catch (e) {
    // Commands render SindriErrors themselves; anything reaching here is a bug.
    // Invariant 8: no secrets in output (launchd logs stderr). Built-in patterns
    // only: the profile may be what broke.
    const { scrub } = makeScrubber();
    const message = scrub(e instanceof Error ? e.message : String(e)).text;
    const details = deps.env.SINDRI_DEBUG === "1" && e instanceof Error ? scrub(e.stack ?? "").text.split("\n") : [];
    const r = failure("SND-CLI-900", `unexpected error: ${message}`, json, { details });
    finishJob(deps, job, r);
    return r;
  }
```

Add a test to `sindri/tests/main.test.ts`, beside the existing `SND-CLI-900` crash test, covering the crash branch's `finishJob`:

```ts
  it("records a crashed job command as failed", async () => {
    const deps = makeDeps();
    const saved = COMMANDS.doctor;
    COMMANDS.doctor = { ...saved, run: async () => { throw new TypeError("kaboom"); } };
    try {
      expect((await runCli(["doctor"], deps)).exitCode).toBe(2);
      expect(readJobs(deps)).toMatchObject([{ kind: "doctor", state: "failed", exitCode: 2 }]);
    } finally {
      COMMANDS.doctor = saved;
    }
  });
```

(Import `readJobs` from `../src/fleet/jobs.js` there.)

`sindri/src/index/heavy-lock.ts`: add after `heavyLockState`:

```ts
export interface QueueEntry {
  kind: string;
  pid: number;
  host: string;
  since: string;
}
const QueueSchema = z.object({ kind: z.string(), pid: z.number().int(), host: z.string(), since: z.string() });
const queueDir = (dir: string): string => `${dir}.queue`;

// Spec decision 9: a waiter leaves a record beside the lock while it waits. Best effort: a waiter that
// can't write one still waits.
function enqueue(dir: string, me: HeavyHolder, now: Date): string | null {
  try {
    fs.mkdirSync(queueDir(dir), { recursive: true, mode: 0o700 });
    const file = path.join(queueDir(dir), `${me.pid}-${ulid(now)}.json`);
    fs.writeFileSync(file, JSON.stringify({ kind: me.kind, pid: me.pid, host: me.host, since: now.toISOString() }), { mode: 0o600 });
    return file;
  } catch {
    return null;
  }
}

export function heavyLockQueue(stateRoot: string, env: NodeJS.ProcessEnv, sys: SystemProbe): QueueEntry[] {
  const q = queueDir(heavyLockDir(stateRoot, env));
  let names: string[];
  try {
    names = fs.readdirSync(q).filter((n) => n.endsWith(".json"));
  } catch {
    return [];
  }
  return names
    .flatMap((n) => {
      try {
        const r = QueueSchema.safeParse(JSON.parse(fs.readFileSync(path.join(q, n), "utf8")));
        return r.success ? [r.data] : [];
      } catch {
        return [];
      }
    })
    .filter((e) => e.host !== sys.hostname() || sys.pidAlive(e.pid))
    .sort((a, b) => a.since.localeCompare(b.since));
}
```

In `withHeavyLock`, wrap the acquisition loop so the waiter record is written on the first wait and always removed:

```ts
  let queued: string | null = null;
  try {
    for (let i = 1; ; i++) {
      // … the existing body, unchanged, except the first-wait branch:
      if (i === 1) {
        deps.log(`waiting for the heavy-job lock${who}`);
        queued = enqueue(dir, me, deps.now());
      }
      await deps.sleep(1000);
    }
  } finally {
    if (queued !== null) fs.rmSync(queued, { force: true });
  }
```

`sindri/src/fleet/types.ts`: append

```ts
export interface PrView {
  repo: string;
  number: number;
  title: string;
  url: string;
  draft: boolean;
  reviewDecision: string | null;
  mergeState: string;
  author: string;
  requestedMe: boolean;
  readyToMerge: boolean;
  createdAt: string;
}
```

`sindri/src/fleet/adapters/sindri.ts`:

```ts
import path from "node:path";

import { awStateDir } from "../../deps.js";
import { heavyLockQueue, heavyLockState, type QueueEntry } from "../../index/heavy-lock.js";
import { indexPath, meta, openIndexReadOnly } from "../../index/db.js";
import { jobHistory, readJobs, type JobView } from "../jobs.js";
import { isUnder } from "../paths.js";
import { cleanLine, MAX_TITLE } from "../text.js";
import type { FleetCtx, PrView } from "../types.js";

export interface HeavyLockView {
  held: boolean;
  holder: { kind: string; pid: number; since: string; ageMs: number | null } | null;
  queue: QueueEntry[];
}
export interface RepoIndexView {
  builtAt: string | null;
  ageMs: number | null;
  stale: boolean;
  commit: string | null;
  files: number | null;
  symbols: number | null;
  behind: number | null;
  lastBuild: JobView | null;
}
export interface ScopeRunView {
  id: string;
  subject: string;
  mode: string;
  status: string;
  ts: string;
  outPath: string;
}
export interface RepoView {
  name: string;
  path: string;
  index: RepoIndexView;
  observe: { at: string | null; backlog: number } | null;
  scopeRuns: ScopeRunView[];
  prs: PrView[];
  worktrees: string[];
}
export interface SindriReport {
  jobs: JobView[];
  history: JobView[];
  heavyLock: HeavyLockView;
  repos: RepoView[];
  scopeRuns: ScopeRunView[];
}

function count(db: ReturnType<typeof openIndexReadOnly>, table: "files" | "symbols"): number | null {
  return db === null ? null : (db.prepare(`SELECT COUNT(*) AS c FROM ${table}`).get() as { c: number }).c;
}

export async function discoverSindri(ctx: FleetCtx): Promise<SindriReport> {
  const { deps } = ctx;
  const jobs = readJobs(deps);
  const history = jobHistory(deps);
  const lock = heavyLockState(awStateDir(deps), deps.now, deps.env);
  const heavyLock: HeavyLockView = {
    held: lock.held,
    holder: lock.holder === null ? null : { kind: lock.holder.kind, pid: lock.holder.pid, since: lock.holder.startedAt, ageMs: lock.ageMs },
    queue: heavyLockQueue(awStateDir(deps), deps.env, deps.system),
  };
  const scopeRuns: ScopeRunView[] = ctx.db === null ? [] : (ctx.db.prepare("SELECT run_id, subject, mode, status, ts, out_path FROM scope_runs ORDER BY ts DESC LIMIT 10").all() as { run_id: string; subject: string; mode: string; status: string; ts: string; out_path: string }[])
    .map((r) => ({ id: r.run_id, subject: cleanLine(r.subject, MAX_TITLE, ctx.scrubber), mode: r.mode, status: r.status, ts: r.ts, outPath: r.out_path }));
  const loaded = ctx.loaded;
  if (loaded === null) return { jobs, history, heavyLock, repos: [], scopeRuns };
  const repos: RepoView[] = [];
  for (const name of loaded.profile.repos) {
    const cfg = loaded.repos[name];
    const db = openIndexReadOnly(indexPath(deps, name));
    let index: RepoIndexView;
    try {
      const m = db === null ? null : meta(db);
      const builtAt = m?.builtAt ?? null;
      const ageMs = builtAt === null ? null : Math.max(0, deps.now().getTime() - Date.parse(builtAt));
      let behind: number | null = null;
      if (m !== null && m.commit !== "") {
        const r = await deps.git.run(["rev-list", "--count", `${m.commit}..HEAD`], cfg.path);
        behind = r.ok ? Number(r.stdout.trim()) : null;
      }
      const lastBuild = history.find((j) => j.kind === `index-build:${name}` || j.kind === `index-quick:${name}` || j.kind === "index-build:all" || j.kind === "index-quick:all") ?? null;
      index = { builtAt, ageMs, stale: ageMs === null || ageMs > loaded.profile.index.maxAgeHours * 3_600_000, commit: m?.commit ?? null, files: count(db, "files"), symbols: count(db, "symbols"), behind, lastBuild };
    } finally {
      db?.close();
    }
    const tracker = loaded.profile.tracker.repo === name;
    const backlog = tracker && ctx.db !== null ? (ctx.db.prepare("SELECT COUNT(*) AS c FROM items WHERE source = ? AND state = 'open'").get(`${loaded.profile.tracker.type}:${name}`) as { c: number }).c : 0;
    repos.push({
      name, path: cfg.path, index,
      observe: tracker ? { at: jobs.find((j) => j.kind === "observe")?.endedAt ?? null, backlog } : null,
      scopeRuns: scopeRuns.filter((s) => isUnder(s.outPath, cfg.path)),
      prs: [], worktrees: [],
    });
  }
  return { jobs, history, heavyLock, repos, scopeRuns };
}
```

The test's `"Brief with \u001b[31mcolour"` subject goes into the ledger raw. `cleanLine` escapes ESC as `\u{001B}`, which is what the view shows. Rows that `path.join` builds are always absolute, so `isUnder` decides by path alone. `ScopeRunView.outPath` is internal; the UI never shows it.

- [ ] **Step 4: Run the tests**

Run: `cd sindri && npx vitest run tests/fleet-jobs.test.ts tests/heavy-lock-queue.test.ts tests/fleet-sindri.test.ts tests/heavy-lock.test.ts tests/main.test.ts`
Expected: PASS. Then, once for the task: `npm test && npm run typecheck && npm run test:coverage`. Expected: all PASS, coverage 100%.

- [ ] **Step 5: Commit**

```bash
git add sindri/src sindri/tests
git commit -m "feat: sindri job records, heavy-lock queue and repo status for the fleet" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 5: The `aw:fleet` hook, the spool and per-provider install

**Files:**
- Create: `sindri/src/fleet/spool.ts`, `config/hooks/fleet.sh` (executable), `config/hooks/tests/fleet-hook.test.sh`
- Modify:
  - `scripts/install-sindri.sh`: `aw:fleet` for every installed provider on a plain install, `--fleet-hook [--provider X]`, and the `AW_NO_FLEET_HOOK` and `AW_FLEET_PROVIDERS` variables;
  - `scripts/tests/install-sindri.test.sh`;
  - `config/hooks/adapters/README.md` (the mapping row);
  - `.agents/rules/hooks.md` (hook table and a "Fleet hook" section);
  - `AGENTS.md` (the bash test line, and the `config/` comment).
- Test: `sindri/tests/fleet-spool.test.ts`, the bash tests above

**Interfaces:**
- Consumes: `fleetDir`, `cleanLine`, `MAX_DETAIL`, `readTail`, `parseJsonLines`, `isProvider`, `PROVIDERS`, `spoolKey`, the spool types and limits (Task 1); `makeScrubber`; `Deps.stdin`; `install-lib.sh` (`aw_hooks_init`, `aw_hooks_stage`, `aw_hook_set`); the Codex and Cursor adapters, which export `AW_HOOK_PROVIDER` to the canonical script.
- Produces:
  - `spool.ts`:
    - `eventFromPayload(provider, raw, now, scrubber): SpoolEvent | null`, mapping hook payloads (Claude-shaped, as the adapters normalise them) to spool kinds;
    - `headFields(head)`, which pulls the routing fields out of a payload too big or too broken to parse;
    - `spoolFile(deps, provider, rotated?)`;
    - `appendSpool(deps, ev)`: one `write` of one line of at most 4 KiB, 0600, rotating to `<provider>.1.jsonl` at 1 MiB;
    - `runHook(provider, deps): Promise<void>`, which never throws and writes nothing for an unknown provider or an event it doesn't keep;
    - `readSpool(deps, since, scrubber): SpoolEvent[]`, in file order, rotated file first, re-scrubbed with the given (profile) scrubber, and only from the last 24 h;
    - `summarize(events): Map<spoolKey, SpoolSummary>`.
  - Spool kinds:
    - `PermissionRequest` gives `permission`;
    - `Notification` gives `permission` (`notification_type: permission_prompt`, or "needs your permission" text), `question` (`elicitation_dialog`) or a plain `notification`, which has no effect;
    - `PreToolUse` gives `question` for `AskUserQuestion` or `request_user_input`, and `plan` for `ExitPlanMode`;
    - `Stop` gives `stop` (with `bg`, the number of running `background_tasks`, and Cursor's `status` as the detail);
    - `UserPromptSubmit` gives `prompt`, `PreCompact` gives `compact`, and `SessionEnd` gives `session-end`;
    - `SubagentStart` and `SubagentStop` give `subagent-start` and `subagent-stop` (with `agent`).
  - `config/hooks/fleet.sh`: saves stdin to a 0600 temp file, runs `sindri fleet hook "${AW_HOOK_PROVIDER:-claude}"` detached with its output discarded, prints nothing and exits 0 at once. It does nothing for `AW_JUDGE_CHILD` or `AW_SINDRI_CHILD` sessions, without sindri, or for an unknown provider. (The `sindri fleet hook` command itself is registered in Task 8 and calls `runHook`.)
  - `install-sindri.sh`:
    - a plain install installs `aw:fleet` for every installed provider: Claude if `${CLAUDE_CONFIG_DIR:-~/.claude}` exists; Codex if `codex` is on `PATH` or `${CODEX_HOME:-~/.codex}` exists; Cursor if `cursor-agent` is on `PATH` or `~/.cursor` exists. `AW_FLEET_PROVIDERS="claude cursor"` overrides the detection, and `AW_NO_FLEET_HOOK=1` skips it;
    - `--fleet-hook --provider X` installs it for one provider;
    - events: Claude gets `PermissionRequest`, `Notification`, `Stop`, `SubagentStart`, `SubagentStop`, `UserPromptSubmit`, `PreCompact`, `SessionEnd` and `PreToolUse` (matcher `AskUserQuestion|ExitPlanMode`). Codex gets the same minus `Notification` and `PreToolUse`. Cursor gets `stop`, `subagentStart`, `subagentStop`, `beforeSubmitPrompt` and `preCompact`;
    - every entry ends in `# aw:fleet` and has a 5 s timeout.

- [ ] **Step 1: Write the failing tests**

`sindri/tests/fleet-spool.test.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { SPOOL_LINE_MAX, SPOOL_MAX_BYTES } from "../src/fleet/limits.js";
import { appendSpool, eventFromPayload, headFields, readSpool, runHook, spoolFile, summarize } from "../src/fleet/spool.js";
import type { SpoolEvent } from "../src/fleet/types.js";
import { makeScrubber } from "../src/scrub/scrub.js";
import { NOW, SID } from "./fleet-fixtures.js";
import { makeDeps } from "./helpers.js";

const s = makeScrubber();
const base = { session_id: SID, transcript_path: `/Users/dev/.claude/projects/-Users-dev-acme-web/${SID}.jsonl`, cwd: "/Users/dev/acme.web", permission_mode: "default" };
const ev = (p: Record<string, unknown>, provider: "claude" | "codex" | "cursor" = "claude") => eventFromPayload(provider, { ...base, ...p }, NOW, s);
const withStdin = (text: string) => makeDeps({ stdin: async () => text });
const lines = (file: string) => fs.readFileSync(file, "utf8").split("\n").filter((l) => l !== "");

describe("eventFromPayload", () => {
  it("maps real-shaped Claude Code payloads, keeping a short scrubbed detail and never the tool input", () => {
    const perm = ev({ hook_event_name: "PermissionRequest", tool_name: "Bash", tool_input: { command: "npm publish", description: "Publish the package" } });
    expect(perm).toEqual({ v: 1, provider: "claude", session: SID, event: "permission", ts: NOW.toISOString(), detail: "Bash: Publish the package", transcript: base.transcript_path });
    const write = ev({ hook_event_name: "PermissionRequest", tool_name: "Write", tool_input: { file_path: "/x/a.ts", content: "SECRET BODY ".repeat(1000) } });
    expect(write?.detail).toBe("Write: /x/a.ts");
    expect(JSON.stringify(write)).not.toContain("SECRET BODY");
    expect(ev({ hook_event_name: "Notification", notification_type: "permission_prompt", message: "Claude needs your permission to use Bash" })?.event).toBe("permission");
    expect(ev({ hook_event_name: "Notification", message: "Claude needs your permission to use Edit" })?.event).toBe("permission");
    expect(ev({ hook_event_name: "Notification", notification_type: "elicitation_dialog", message: "Pick one" })).toMatchObject({ event: "question", detail: "Pick one" });
    expect(ev({ hook_event_name: "Notification", notification_type: "idle_prompt", message: "Claude is waiting for your input" })?.event).toBe("notification");
    expect(ev({ hook_event_name: "PreToolUse", tool_name: "AskUserQuestion", tool_input: { questions: [{ question: "Which database?" }] } })).toMatchObject({ event: "question", detail: "Which database?" });
    expect(ev({ hook_event_name: "PreToolUse", tool_name: "ExitPlanMode", tool_input: { plan: "x" } })).toMatchObject({ event: "plan", detail: "a plan waits for your approval" });
    expect(ev({ hook_event_name: "PreToolUse", tool_name: "Bash", tool_input: { command: "ls" } })).toBeNull();
    expect(ev({ hook_event_name: "Stop", stop_hook_active: false, background_tasks: [{ id: "a", type: "subagent", status: "running" }, { id: "b", status: "completed" }], session_crons: [] })).toMatchObject({ event: "stop", bg: 1, detail: "" });
    expect(ev({ hook_event_name: "SubagentStart", agent_id: "abc123", agent_type: "general-purpose" })).toMatchObject({ event: "subagent-start", agent: "abc123" });
    expect(ev({ hook_event_name: "UserPromptSubmit", prompt: "go" })?.event).toBe("prompt");
    expect(ev({ hook_event_name: "PreCompact", trigger: "auto" })?.event).toBe("compact");
    expect(ev({ hook_event_name: "SessionEnd", reason: "exit" })?.event).toBe("session-end");
    expect(ev({ hook_event_name: "PermissionRequest", tool_name: "Bash", tool_input: { command: `curl -H x ${"AKIA" + "ABCDEFGHIJKLMNOP"}` } })?.detail).toContain("[REDACTED:aws-access-key]");
  });

  it("maps the Codex and Cursor payloads the adapters hand over, and refuses junk", () => {
    expect(ev({ hook_event_name: "Stop", turn_id: "t1", last_assistant_message: "done" }, "codex")).toMatchObject({ provider: "codex", event: "stop" });
    expect(ev({ hook_event_name: "PermissionRequest", tool_name: "exec_command", tool_input: { cmd: "make deploy" } }, "codex")).toMatchObject({ event: "permission", detail: "exec_command" });
    expect(eventFromPayload("cursor", { session_id: "cu-stop", hook_event_name: "Stop", status: "aborted", loop_count: 0 }, NOW, s)).toMatchObject({ provider: "cursor", session: "cu-stop", event: "stop", detail: "aborted" });
    expect(eventFromPayload("cursor", { session_id: "cu-1", hook_event_name: "SubagentStart", subagent_id: "sub-9" }, NOW, s)).toMatchObject({ agent: "sub-9" });
    expect(ev({ session_id: "../etc", hook_event_name: "Stop" })).toBeNull();
    expect(eventFromPayload("claude", { hook_event_name: "Stop" }, NOW, s)).toBeNull();
    expect(ev({ hook_event_name: "TeammateIdle" })).toBeNull();
    expect(eventFromPayload("claude", "not an object", NOW, s)).toBeNull();
    expect(ev({ hook_event_name: "Stop", transcript_path: "relative/path.jsonl" })?.transcript).toBeUndefined();
  });
});

describe("runHook and appendSpool", () => {
  it("appends one 0600 line per event for the named provider", async () => {
    const deps = withStdin(JSON.stringify({ ...base, hook_event_name: "Stop" }));
    await runHook("claude", deps);
    const file = spoolFile(deps, "claude");
    expect(lines(file)).toHaveLength(1);
    expect(JSON.parse(lines(file)[0])).toMatchObject({ provider: "claude", event: "stop" });
    expect(fs.statSync(file).mode & 0o777).toBe(0o600);
    await runHook("sindri", deps);
    await runHook("claude", withStdin("{}"));
    expect(fs.existsSync(spoolFile(deps, "codex"))).toBe(false);
  });

  it("still records one short line for a 10 MB payload or one that isn't JSON (Review Focus 4)", async () => {
    const huge = `{"session_id":"${SID}","hook_event_name":"PermissionRequest","tool_name":"Write","tool_input":{"content":"${"x".repeat(10_000_000)}"}}`;
    const deps = withStdin(huge);
    await runHook("claude", deps);
    const [line] = lines(spoolFile(deps, "claude"));
    expect(Buffer.byteLength(line)).toBeLessThan(SPOOL_LINE_MAX);
    expect(JSON.parse(line)).toMatchObject({ event: "permission", detail: "Write" });
    expect(headFields('{"session_id":"a1","hook_event_name":"Stop" , broken')).toEqual({ session_id: "a1", hook_event_name: "Stop" });
    const broken = withStdin(`{"session_id":"${SID}","hook_event_name":"Stop", trailing junk`);
    await runHook("claude", broken);
    expect(lines(spoolFile(broken, "claude"))).toHaveLength(1);
  });

  it("never throws: an unreadable stdin or an unwritable spool is swallowed", async () => {
    await runHook("claude", makeDeps({ stdin: async () => { throw new Error("EPIPE"); } }));
    const deps = withStdin(JSON.stringify({ ...base, hook_event_name: "Stop" }));
    const dir = path.dirname(spoolFile(deps, "claude"));
    fs.mkdirSync(path.dirname(dir), { recursive: true });
    fs.writeFileSync(dir, "a file where the spool dir should be");
    await expect(runHook("claude", deps)).resolves.toBeUndefined();
  });

  it("keeps every line whole through a burst that crosses the rotation size", () => {
    const deps = makeDeps();
    const file = spoolFile(deps, "claude");
    fs.mkdirSync(path.dirname(file), { recursive: true });
    const filler = JSON.stringify({ v: 1, provider: "claude", session: "old", event: "stop", ts: NOW.toISOString(), detail: "" });
    fs.writeFileSync(file, `${filler}\n`.repeat(Math.floor((SPOOL_MAX_BYTES - 200) / (filler.length + 1))));
    for (let i = 0; i < 500; i++) appendSpool(deps, { v: 1, provider: "claude", session: `s${i}`, event: "permission", ts: NOW.toISOString(), detail: "d".repeat(230) });
    expect(fs.existsSync(spoolFile(deps, "claude", true))).toBe(true);
    for (const f of [file, spoolFile(deps, "claude", true)]) for (const l of lines(f)) expect(() => JSON.parse(l)).not.toThrow();
    const read = readSpool(deps, new Date(NOW.getTime() - 3_600_000), s);
    expect(read.filter((e) => e.session.startsWith("s"))).toHaveLength(500);
    expect(read.at(-1)?.session).toBe("s499");
  });

  it("drops the transcript path and trims the detail when a line would pass 4 KiB", () => {
    const deps = makeDeps();
    appendSpool(deps, { v: 1, provider: "codex", session: "s", event: "permission", ts: NOW.toISOString(), detail: "\u0000".repeat(240).replace(/\u0000/g, "\\u{0000}"), transcript: `/${"p".repeat(2000)}` });
    const [l] = lines(spoolFile(deps, "codex"));
    expect(Buffer.byteLength(l)).toBeLessThan(SPOOL_LINE_MAX);
    expect(JSON.parse(l).transcript).toBeUndefined();
  });
});

describe("readSpool and summarize", () => {
  it("skips torn and junk lines, other providers' lines and lines older than a day", () => {
    const deps = makeDeps();
    const file = spoolFile(deps, "cursor");
    fs.mkdirSync(path.dirname(file), { recursive: true });
    const line = (o: Partial<SpoolEvent>) => JSON.stringify({ v: 1, provider: "cursor", session: "c1", event: "stop", ts: NOW.toISOString(), detail: "", ...o });
    fs.writeFileSync(file, [line({ ts: new Date(NOW.getTime() - 90_000_000).toISOString() }), "junk", line({ provider: "claude" }), line({ detail: "completed" }), '{"v":1,"provider":"cur'].join("\n"));
    expect(readSpool(deps, new Date(NOW.getTime() - 86_400_000), s).map((e) => e.detail)).toEqual(["completed"]);
  });

  it("tracks the pending event, compaction, background tasks, open subagents and the stop status", () => {
    const e = (event: SpoolEvent["event"], o: Partial<SpoolEvent> = {}): SpoolEvent => ({ v: 1, provider: "claude", session: "a", event, ts: NOW.toISOString(), detail: "", ...o });
    const one = (evs: SpoolEvent[]) => summarize(evs).get("claude:a");
    expect(one([e("permission", { detail: "Bash: x" })])?.pending?.detail).toBe("Bash: x");
    expect(one([e("permission"), e("stop", { bg: 2, detail: "aborted" })])).toMatchObject({ pending: null, bg: 2, stopStatus: "aborted" });
    expect(one([e("question"), e("plan")])?.pending?.event).toBe("plan");
    expect(one([e("permission"), e("notification")])?.pending?.event).toBe("permission");
    expect(one([e("compact")])?.compacting).toBe(true);
    expect(one([e("compact"), e("prompt")])).toMatchObject({ compacting: false, bg: null, stopStatus: null });
    expect(one([e("subagent-start", { agent: "x" }), e("subagent-start", { agent: "y" }), e("subagent-stop", { agent: "x" })])?.openAgents).toEqual(["y"]);
    expect(one([e("stop", { transcript: "/t.jsonl" })])?.transcript).toBe("/t.jsonl");
    expect(one([e("subagent-start")])?.openAgents).toEqual([]);
  });
});
```

`config/hooks/tests/fleet-hook.test.sh`:

```bash
#!/usr/bin/env bash
# aw:fleet: the hook must return at once, never print, always exit 0, and hand the payload to
# `sindri fleet hook <provider>` (a stub here) for every provider, directly and through the adapters.
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../../.." && pwd)"
HOOK="$ROOT/config/hooks/fleet.sh"
TMP="$(mktemp -d)"
trap 'find "$TMP" -depth -delete 2>/dev/null' EXIT
mkdir -p "$TMP/bin" "$TMP/home" "$TMP/log"
cat > "$TMP/bin/sindri" <<'STUB'
#!/usr/bin/env bash
sleep "${STUB_SLEEP:-0}"
f="$STUB_LOG/$$-$RANDOM"
{ echo "ARGS: $*"; cat; } > "$f.part" && mv "$f.part" "$f"
STUB
chmod +x "$TMP/bin/sindri"
export STUB_LOG="$TMP/log"
BASE_PATH="/usr/bin:/bin:$(dirname "$(command -v jq)")"

logged() { find "$STUB_LOG" -type f ! -name '*.part' | wc -l | tr -d ' '; }
wait_for() { local i=0; while [ "$(logged)" -lt "$1" ] && [ $i -lt 200 ]; do sleep 0.05; i=$((i + 1)); done; }
reset_log() { find "$STUB_LOG" -type f -delete; }
hook() { env PATH="$TMP/bin:$BASE_PATH" HOME="$TMP/home" "$@"; }

test_returns_at_once_and_hands_over_the_payload() {
  reset_log
  local start out
  start=$(date +%s)
  out="$(printf '%s' '{"session_id":"s1","hook_event_name":"Stop"}' | STUB_SLEEP=2 hook bash "$HOOK")"
  [ $(( $(date +%s) - start )) -le 1 ] || { echo "FAIL: the hook waited for the CLI"; exit 1; }
  [ -z "$out" ] || { echo "FAIL: the hook printed: $out"; exit 1; }
  wait_for 1
  grep -q "^ARGS: fleet hook claude$" "$STUB_LOG"/* || { echo "FAIL: CLI not called with fleet hook claude"; exit 1; }
  grep -q '"hook_event_name":"Stop"' "$STUB_LOG"/* || { echo "FAIL: payload not handed over"; exit 1; }
  echo "PASS: test_returns_at_once_and_hands_over_the_payload"
}

test_provider_comes_from_the_adapter() {
  reset_log
  printf '{}' | AW_HOOK_PROVIDER=codex hook bash "$HOOK"
  wait_for 1
  grep -q "^ARGS: fleet hook codex$" "$STUB_LOG"/* || { echo "FAIL: codex provider not passed"; exit 1; }
  echo "PASS: test_provider_comes_from_the_adapter"
}

test_silent_and_harmless_when_it_should_not_run() {
  reset_log
  local rc=0
  printf '{}' | env PATH="$BASE_PATH" HOME="$TMP/home" bash "$HOOK" || rc=$?
  printf '{}' | AW_JUDGE_CHILD=1 hook bash "$HOOK" || rc=$?
  printf '{}' | AW_SINDRI_CHILD=1 hook bash "$HOOK" || rc=$?
  printf '{}' | AW_HOOK_PROVIDER=bogus hook bash "$HOOK" || rc=$?
  [ "$rc" = 0 ] || { echo "FAIL: a skipped hook exited $rc"; exit 1; }
  sleep 0.3
  [ "$(logged)" = 0 ] || { echo "FAIL: the CLI ran when it should not"; exit 1; }
  echo "PASS: test_silent_and_harmless_when_it_should_not_run"
}

test_a_burst_of_thirty_never_blocks() {
  reset_log
  local start pids="" i
  start=$(date +%s)
  for i in $(seq 1 30); do
    printf '{"session_id":"s%s","hook_event_name":"SubagentStart"}' "$i" | STUB_SLEEP=1 hook bash "$HOOK" &
    pids="$pids $!"
  done
  # shellcheck disable=SC2086
  wait $pids
  [ $(( $(date +%s) - start )) -le 2 ] || { echo "FAIL: thirty hooks took over 2 s"; exit 1; }
  wait_for 30
  [ "$(logged)" = 30 ] || { echo "FAIL: expected 30 hand-overs, got $(logged)"; exit 1; }
  echo "PASS: test_a_burst_of_thirty_never_blocks"
}

test_a_10mb_payload_returns_at_once() {
  reset_log
  local start
  start=$(date +%s)
  { printf '{"session_id":"s1","hook_event_name":"PermissionRequest","tool_name":"Write","tool_input":{"content":"'; head -c 10000000 /dev/zero | tr '\0' 'x'; printf '"}}'; } | hook bash "$HOOK"
  [ $(( $(date +%s) - start )) -le 2 ] || { echo "FAIL: a big payload blocked the hook"; exit 1; }
  echo "PASS: test_a_10mb_payload_returns_at_once"
}

test_through_the_cursor_and_codex_adapters() {
  reset_log
  local out t="$TMP/transcript.jsonl"
  : > "$t"
  out="$(sed "s|__TRANSCRIPT__|$t|" "$ROOT/config/hooks/tests/fixtures/cursor/stop.json" | hook bash "$ROOT/config/hooks/adapters/cursor.sh" "$HOOK")"
  printf '%s' "$out" | jq -e 'type == "object"' > /dev/null || { echo "FAIL: cursor adapter answered invalid JSON: $out"; exit 1; }
  hook bash "$ROOT/config/hooks/adapters/codex.sh" "$HOOK" < "$ROOT/config/hooks/tests/fixtures/codex/stop-done.json" > /dev/null
  wait_for 2
  grep -q "^ARGS: fleet hook cursor$" "$STUB_LOG"/* || { echo "FAIL: cursor hand-over missing"; exit 1; }
  grep -q '"session_id":"cu-stop"' "$STUB_LOG"/* || { echo "FAIL: cursor conversation_id not mapped to session_id"; exit 1; }
  grep -q "^ARGS: fleet hook codex$" "$STUB_LOG"/* || { echo "FAIL: codex hand-over missing"; exit 1; }
  echo "PASS: test_through_the_cursor_and_codex_adapters"
}

test_returns_at_once_and_hands_over_the_payload
test_provider_comes_from_the_adapter
test_silent_and_harmless_when_it_should_not_run
test_a_burst_of_thirty_never_blocks
test_a_10mb_payload_returns_at_once
test_through_the_cursor_and_codex_adapters
```

Add to `scripts/tests/install-sindri.test.sh`. Right after `trap … EXIT`, put this line, so no existing test can touch a real hook config:

```bash
export AW_NO_FLEET_HOOK=1   # tests that want the fleet hook unset it with a scratch HOME
```

Then add these tests, and call them from the test list at the bottom:

```bash
test_fleet_hook_per_provider() {
  local home="$TMP/fleet-home" settings="$TMP/fleet-settings.json" codex="$TMP/fleet-codex.json" cursor="$TMP/fleet-cursor.json" out
  mkdir -p "$home"
  out="$(HOME="$home" AW_DRY_RUN=1 bash "$ROOT/scripts/install-sindri.sh" --fleet-hook --provider codex)"
  grep -q "would install aw:fleet (PermissionRequest,Stop,SubagentStart,SubagentStop,UserPromptSubmit,PreCompact,SessionEnd) for codex" <<<"$out" || { echo "FAIL: codex dry-run line: $out"; exit 1; }
  HOME="$home" CLAUDE_SETTINGS_FILE="$settings" CLAUDE_HOOKS_DIR="$TMP/fleet-hooks" bash "$ROOT/scripts/install-sindri.sh" --fleet-hook > /dev/null
  HOME="$home" CLAUDE_SETTINGS_FILE="$settings" CLAUDE_HOOKS_DIR="$TMP/fleet-hooks" bash "$ROOT/scripts/install-sindri.sh" --fleet-hook > /dev/null
  [ -x "$TMP/fleet-hooks/fleet.sh" ] || { echo "FAIL: claude fleet.sh not copied"; exit 1; }
  for ev in PermissionRequest Notification Stop SubagentStart SubagentStop UserPromptSubmit PreCompact SessionEnd PreToolUse; do
    [ "$(jq --arg e "$ev" '[.hooks[$e][]?.hooks[].command | select(test("fleet.sh # aw:fleet$"))] | length' "$settings")" = "1" ] || { echo "FAIL: claude $ev entry missing or duplicated"; exit 1; }
  done
  [ "$(jq -r '.hooks.PreToolUse[] | select(.hooks[0].command | test("aw:fleet")) | .matcher' "$settings")" = "AskUserQuestion|ExitPlanMode" ] || { echo "FAIL: claude PreToolUse matcher"; exit 1; }
  HOME="$home" CODEX_HOOKS_FILE="$codex" AW_HOOKS_DIR="$TMP/fleet-aw-hooks" bash "$ROOT/scripts/install-sindri.sh" --fleet-hook --provider codex > /dev/null
  [ "$(jq '[.hooks.PermissionRequest[].hooks[].command | select(test("adapters/codex.sh .*fleet.sh # aw:fleet$"))] | length' "$codex")" = "1" ] || { echo "FAIL: codex PermissionRequest entry"; exit 1; }
  [ "$(jq '.hooks.Notification // [] | length' "$codex")" = "0" ] || { echo "FAIL: codex has no Notification event"; exit 1; }
  HOME="$home" CURSOR_HOOKS_FILE="$cursor" AW_HOOKS_DIR="$TMP/fleet-aw-hooks" bash "$ROOT/scripts/install-sindri.sh" --fleet-hook --provider cursor > /dev/null
  for ev in stop subagentStart subagentStop beforeSubmitPrompt preCompact; do
    [ "$(jq --arg e "$ev" '[.hooks[$e][].command | select(test("adapters/cursor.sh .*fleet.sh # aw:fleet$"))] | length' "$cursor")" = "1" ] || { echo "FAIL: cursor $ev entry"; exit 1; }
  done
  [ -x "$TMP/fleet-aw-hooks/fleet.sh" ] || { echo "FAIL: staged fleet.sh missing"; exit 1; }
  echo "PASS: test_fleet_hook_per_provider"
}

test_plain_install_adds_the_fleet_hook_for_installed_providers() {
  local home="$TMP/plain-fleet-home" out
  mkdir -p "$home"
  out="$(env -u AW_NO_FLEET_HOOK HOME="$home" AW_FLEET_PROVIDERS="claude cursor" AW_DRY_RUN=1 bash "$ROOT/scripts/install-sindri.sh")"
  grep -q "would install aw:fleet .* for claude" <<<"$out" && grep -q "would install aw:fleet .* for cursor" <<<"$out" || { echo "FAIL: plain dry-run lines: $out"; exit 1; }
  ! grep -q "for codex" <<<"$out" || { echo "FAIL: codex is not installed here"; exit 1; }
  env -u AW_NO_FLEET_HOOK HOME="$home" AW_FLEET_PROVIDERS="claude cursor" AW_SKIP_BUILD=1 AW_SKIP_LAUNCHD=1 CLAUDE_LOCAL_BIN="$TMP/plain-fleet-bin" \
    CLAUDE_SETTINGS_FILE="$TMP/plain-settings.json" CLAUDE_HOOKS_DIR="$TMP/plain-hooks" CURSOR_HOOKS_FILE="$TMP/plain-cursor.json" AW_HOOKS_DIR="$TMP/plain-aw-hooks" \
    bash "$ROOT/scripts/install-sindri.sh" > /dev/null
  jq -e '.hooks.Stop[].hooks[].command | select(test("aw:fleet$"))' "$TMP/plain-settings.json" > /dev/null || { echo "FAIL: plain install skipped claude"; exit 1; }
  jq -e '.hooks.stop[].command | select(test("aw:fleet$"))' "$TMP/plain-cursor.json" > /dev/null || { echo "FAIL: plain install skipped cursor"; exit 1; }
  out="$(HOME="$home" AW_FLEET_PROVIDERS="claude" AW_DRY_RUN=1 bash "$ROOT/scripts/install-sindri.sh")"
  ! grep -q "aw:fleet" <<<"$out" || { echo "FAIL: AW_NO_FLEET_HOOK=1 still installs"; exit 1; }
  echo "PASS: test_plain_install_adds_the_fleet_hook_for_installed_providers"
}

test_fleet_hook_flag_combinations() {
  local rc=0 out
  out="$(bash "$ROOT/scripts/install-sindri.sh" --fleet-hook --channel next 2>&1)" || rc=$?
  [ "$rc" = 1 ] && grep -q "usage: install-sindri.sh" <<<"$out" || { echo "FAIL: --fleet-hook --channel: $rc $out"; exit 1; }
  rc=0
  out="$(bash "$ROOT/scripts/install-sindri.sh" --fleet-hook --hook-only 2>&1)" || rc=$?
  [ "$rc" = 1 ] || { echo "FAIL: --fleet-hook --hook-only: $rc $out"; exit 1; }
  echo "PASS: test_fleet_hook_flag_combinations"
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd sindri && npx vitest run tests/fleet-spool.test.ts`
Expected: FAIL with `Failed to load url ../src/fleet/spool.js`.
Run: `bash config/hooks/tests/fleet-hook.test.sh`
Expected: FAIL (`bash: …/config/hooks/fleet.sh: No such file or directory`, from the first test).
Run: `bash scripts/tests/install-sindri.test.sh`
Expected: FAIL at `test_fleet_hook_per_provider` with the usage message, because `--fleet-hook` is unknown.

- [ ] **Step 3: Implement**

`sindri/src/fleet/spool.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import { z } from "zod";

import type { Deps } from "../deps.js";
import { makeScrubber, type Scrubber } from "../scrub/scrub.js";
import { MAX_HOOK_PAYLOAD, RECENT_MS, SPOOL_LINE_MAX, SPOOL_MAX_BYTES } from "./limits.js";
import { fleetDir } from "./paths.js";
import { parseJsonLines, readTail } from "./tail.js";
import { cleanLine, MAX_DETAIL } from "./text.js";
import { isProvider, PROVIDERS, spoolKey, type Provider, type SpoolEvent, type SpoolKind, type SpoolSummary } from "./types.js";

const SESSION = /^[A-Za-z0-9._-]{1,128}$/;
const KINDS = ["permission", "question", "plan", "compact", "stop", "prompt", "session-end", "subagent-start", "subagent-stop", "notification"] as const satisfies readonly SpoolKind[];
const BY_EVENT: Record<string, SpoolKind> = {
  PermissionRequest: "permission", Notification: "notification", Stop: "stop", SubagentStart: "subagent-start", SubagentStop: "subagent-stop",
  UserPromptSubmit: "prompt", PreCompact: "compact", SessionEnd: "session-end",
};
const QUESTION_TOOLS = new Set(["AskUserQuestion", "request_user_input"]);
const PENDING = new Set<SpoolKind>(["permission", "question", "plan"]);
const CLEARS = new Set<SpoolKind>(["stop", "prompt", "session-end", "compact"]);

const obj = (v: unknown): Record<string, unknown> => (typeof v === "object" && v !== null && !Array.isArray(v) ? (v as Record<string, unknown>) : {});
const str = (v: unknown): string | null => (typeof v === "string" ? v : null);

function questionOf(input: Record<string, unknown>): string {
  const qs = input.questions;
  const first = Array.isArray(qs) ? str(obj(qs[0]).question) : null;
  return first ?? str(input.question) ?? str(input.prompt) ?? "asked you a question";
}

// Spec §4.2: provider, session, event, time and a short scrubbed detail; never the tool input itself.
export function eventFromPayload(provider: Provider, raw: unknown, now: Date, scrubber: Scrubber): SpoolEvent | null {
  const p = obj(raw);
  const session = str(p.session_id);
  if (session === null || !SESSION.test(session)) return null;
  const name = str(p.hook_event_name) ?? "";
  const tool = str(p.tool_name) ?? "";
  const input = obj(p.tool_input);
  let event: SpoolKind | undefined = BY_EVENT[name];
  if (name === "PreToolUse") event = QUESTION_TOOLS.has(tool) ? "question" : tool === "ExitPlanMode" ? "plan" : undefined;
  if (event === undefined) return null;
  let detail = "";
  if (event === "notification") {
    const type = str(p.notification_type) ?? "";
    detail = str(p.message) ?? "";
    if (type === "permission_prompt" || /needs your permission/i.test(detail)) event = "permission";
    else if (type === "elicitation_dialog") event = "question";
  } else if (event === "permission") {
    const what = str(input.description) ?? str(input.command) ?? str(input.file_path) ?? str(input.url) ?? "";
    detail = what === "" ? tool : `${tool}: ${what}`;
  } else if (event === "question") detail = questionOf(input);
  else if (event === "plan") detail = "a plan waits for your approval";
  else if (event === "stop") detail = str(p.status) ?? "";
  const ev: SpoolEvent = { v: 1, provider, session, event, ts: now.toISOString(), detail: cleanLine(detail, MAX_DETAIL, scrubber) };
  if (event === "stop" && Array.isArray(p.background_tasks)) ev.bg = p.background_tasks.filter((t) => obj(t).status === "running").length;
  if (event === "subagent-start" || event === "subagent-stop") {
    const agent = str(p.agent_id) ?? str(p.subagent_id);
    if (agent !== null && SESSION.test(agent)) ev.agent = agent;
  }
  const t = str(p.transcript_path);
  if (t !== null && path.isAbsolute(t) && t.length <= 512) ev.transcript = scrubber.scrub(t).text;
  return ev;
}

// A payload too big (a large Write) or too broken to parse: the routing fields come first in every
// host's payload, so read them by pattern from the head.
export function headFields(head: string): Record<string, string> {
  const out: Record<string, string> = {};
  for (const k of ["session_id", "hook_event_name", "tool_name", "notification_type", "transcript_path"]) {
    const m = new RegExp(`"${k}"\\s*:\\s*"([^"\\\\]{1,512})"`).exec(head);
    if (m !== null) out[k] = m[1];
  }
  return out;
}

export const spoolFile = (deps: Deps, provider: Provider, rotated = false): string =>
  path.join(fleetDir(deps), "spool", `${provider}${rotated ? ".1" : ""}.jsonl`);

// One write of one line under 4 KiB to a file opened O_APPEND: concurrent hooks never interleave inside
// a line. At 1 MiB the file moves to <provider>.1.jsonl (a race between two hooks loses only old lines).
export function appendSpool(deps: Deps, ev: SpoolEvent): void {
  const file = spoolFile(deps, ev.provider);
  fs.mkdirSync(path.dirname(file), { recursive: true, mode: 0o700 });
  if ((fs.statSync(file, { throwIfNoEntry: false })?.size ?? 0) >= SPOOL_MAX_BYTES) {
    try {
      fs.renameSync(file, spoolFile(deps, ev.provider, true));
    } catch {
      // another hook rotated it first
    }
  }
  let line = JSON.stringify(ev);
  if (Buffer.byteLength(line) >= SPOOL_LINE_MAX) line = JSON.stringify({ ...ev, transcript: undefined, detail: ev.detail.slice(0, 200) });
  const fd = fs.openSync(file, "a", 0o600);
  try {
    fs.writeSync(fd, `${line}\n`);
  } finally {
    fs.closeSync(fd);
  }
}

// The `sindri fleet hook <provider>` body. It never throws and never prints: a hook must not fail its host.
// The built-in scrubber only (no profile load on this path); readSpool scrubs again with the profile's.
export async function runHook(provider: string, deps: Deps): Promise<void> {
  if (!isProvider(provider)) return;
  let raw: string;
  try {
    raw = await deps.stdin();
  } catch {
    return;
  }
  let payload: unknown;
  try {
    payload = raw.length > MAX_HOOK_PAYLOAD ? headFields(raw.slice(0, 65_536)) : JSON.parse(raw);
  } catch {
    payload = headFields(raw.slice(0, 65_536));
  }
  const ev = eventFromPayload(provider, payload, deps.now(), makeScrubber());
  if (ev === null) return;
  try {
    appendSpool(deps, ev);
  } catch {
    // an unwritable state dir loses this event, never the host's turn
  }
}

const SpoolLine = z.object({
  v: z.literal(1),
  provider: z.enum(PROVIDERS),
  session: z.string().regex(SESSION),
  event: z.enum(KINDS),
  ts: z.string(),
  detail: z.string(),
  bg: z.number().int().nonnegative().optional(),
  agent: z.string().regex(SESSION).optional(),
  transcript: z.string().optional(),
});

// File order (the order hooks appended), not timestamp order: a clock that moved back doesn't reorder events.
export function readSpool(deps: Deps, since: Date, scrubber: Scrubber): SpoolEvent[] {
  const out: SpoolEvent[] = [];
  for (const provider of PROVIDERS) {
    for (const file of [spoolFile(deps, provider, true), spoolFile(deps, provider)]) {
      for (const e of parseJsonLines(readTail(file)?.lines ?? []).entries) {
        const r = SpoolLine.safeParse(e);
        if (!r.success || r.data.provider !== provider || !(Date.parse(r.data.ts) >= since.getTime())) continue;
        out.push({ ...r.data, detail: cleanLine(r.data.detail, MAX_DETAIL, scrubber) });
      }
    }
  }
  return out;
}

export const spoolSince = (now: Date): Date => new Date(now.getTime() - RECENT_MS);

export function summarize(events: readonly SpoolEvent[]): Map<string, SpoolSummary> {
  const m = new Map<string, SpoolSummary>();
  for (const ev of events) {
    const key = spoolKey(ev.provider, ev.session);
    const s = m.get(key) ?? { last: null, pending: null, compacting: false, bg: null, openAgents: [], transcript: null, stopStatus: null };
    if (ev.transcript !== undefined) s.transcript = ev.transcript;
    if (ev.event === "subagent-start" || ev.event === "subagent-stop") {
      if (ev.agent !== undefined) s.openAgents = ev.event === "subagent-start" ? [...new Set([...s.openAgents, ev.agent])] : s.openAgents.filter((a) => a !== ev.agent);
    } else if (ev.event !== "notification") {
      s.last = ev;
      if (PENDING.has(ev.event)) s.pending = ev;
      else if (CLEARS.has(ev.event)) s.pending = null;
      s.compacting = ev.event === "compact";
      if (ev.event === "stop") {
        s.bg = ev.bg ?? null;
        s.stopStatus = ev.detail === "" ? null : ev.detail;
      }
      if (ev.event === "prompt") {
        s.bg = null;
        s.stopStatus = null;
      }
    }
    m.set(key, s);
  }
  return m;
}
```

Trace for the oversized-line test: the detail is 240 escaped NULs, 9 characters each as `\\u{0000}` (about 2.4 KiB once JSON-escaped), and the transcript is 2001 bytes, so the line passes 4 KiB. The retry drops `transcript` and keeps 200 characters of the detail (about 220 bytes), well under the cap. (`eventFromPayload` itself never makes a transcript path over 512 bytes; the test calls `appendSpool` directly to prove the guard.)

`config/hooks/fleet.sh` (mode 755):

```bash
#!/usr/bin/env bash
# aw:fleet: records one line per hook event in the fleet spool (spec §4.2) through
# `sindri fleet hook <provider>`. Never blocks and never fails the host: stdin goes to a private temp
# file, the CLI runs detached with its output discarded, and the hook exits 0 at once. Prints nothing
# (the Cursor adapter answers its host with the default JSON). Silent without sindri, for an unknown
# provider, and in child sessions (AW_JUDGE_CHILD, AW_SINDRI_CHILD).
[ -n "${AW_JUDGE_CHILD:-}${AW_SINDRI_CHILD:-}" ] && exit 0
SINDRI="$(command -v sindri 2>/dev/null || echo "$HOME/.local/bin/sindri")"
PROVIDER="${AW_HOOK_PROVIDER:-claude}"
case "$PROVIDER" in claude|codex|cursor) ;; *) cat > /dev/null 2>&1; exit 0 ;; esac
if [ ! -x "$SINDRI" ]; then cat > /dev/null 2>&1; exit 0; fi
umask 077
EVENT_FILE="$(mktemp "${TMPDIR:-/tmp}/aw-fleet.XXXXXX" 2>/dev/null)" || { cat > /dev/null 2>&1; exit 0; }
cat > "$EVENT_FILE" 2>/dev/null
( "$SINDRI" fleet hook "$PROVIDER" < "$EVENT_FILE" > /dev/null 2>&1; unlink "$EVENT_FILE" 2>/dev/null ) < /dev/null > /dev/null 2>&1 &
exit 0
```

`scripts/install-sindri.sh`:
- Header comment: add these lines:
  - `install-sindri.sh --fleet-hook [--provider claude|codex|cursor]` installs only the aw:fleet hook for that host (default claude).
  - A plain install adds aw:fleet for every installed provider. `AW_FLEET_PROVIDERS="claude cursor"` overrides the detection, and `AW_NO_FLEET_HOOK=1` skips it.
- `USAGE`: becomes `usage: install-sindri.sh [--hook-only|--fleet-hook [--provider claude|codex|cursor]] | [--channel stable|next [--ref <sha>]]`.
- The argument loop: add `--fleet-hook) FLEET_ONLY=1; shift ;;` (with `FLEET_ONLY=0` declared beside `HOOK_ONLY=0`).
- The flag checks: add `if [ "$FLEET_ONLY" = "1" ] && { [ "$HOOK_ONLY" = "1" ] || [ -n "$CHANNEL" ]; }; then echo "$USAGE" >&2; exit 1; fi`.
- Then add, after `write_wrapper`:

```bash
# The events aw:fleet listens to, per provider (config/hooks/adapters/README.md). "Event=matcher" pairs.
fleet_events() {
  case "$1" in
    claude) echo "PermissionRequest Notification Stop SubagentStart SubagentStop UserPromptSubmit PreCompact SessionEnd PreToolUse=AskUserQuestion|ExitPlanMode" ;;
    codex) echo "PermissionRequest Stop SubagentStart SubagentStop UserPromptSubmit PreCompact SessionEnd" ;;
    cursor) echo "stop subagentStart subagentStop beforeSubmitPrompt preCompact" ;;
  esac
}

installed_providers() {
  if [ -n "${AW_FLEET_PROVIDERS:-}" ]; then echo "$AW_FLEET_PROVIDERS"; return; fi
  local out=""
  [ -d "${CLAUDE_CONFIG_DIR:-$HOME/.claude}" ] && out="$out claude"
  { command -v codex >/dev/null 2>&1 || [ -d "${CODEX_HOME:-$HOME/.codex}" ]; } && out="$out codex"
  { command -v cursor-agent >/dev/null 2>&1 || [ -d "$HOME/.cursor" ]; } && out="$out cursor"
  echo "$out"
}

install_fleet_hook() { # provider
  local p="$1" ev matcher list
  aw_hooks_init "$p"
  list="$(fleet_events "$p" | sed 's/=[^ ]*//g' | tr ' ' ',')"
  if [ "${AW_DRY_RUN:-0}" = "1" ]; then
    echo "  [dry-run] would install aw:fleet ($list) for $p in $AW_HOOKS_CONFIG"
    return 0
  fi
  if [ "$p" = "claude" ]; then
    mkdir -p "$AW_HOOKS_INSTALL_DIR"
    cp "$SCRIPT_DIR/config/hooks/fleet.sh" "$AW_HOOKS_INSTALL_DIR/fleet.sh"
    chmod +x "$AW_HOOKS_INSTALL_DIR/fleet.sh"
  else
    aw_hooks_stage
  fi
  for ev in $(fleet_events "$p"); do
    matcher=""
    case "$ev" in *=*) matcher="${ev#*=}"; ev="${ev%%=*}" ;; esac
    aw_hook_set "$ev" aw:fleet fleet.sh "$matcher" 5
  done
  echo "  sindri: aw:fleet hook installed for $p in $AW_HOOKS_CONFIG"
}
```

- Right after the `--hook-only` block, add:

```bash
if [ "$FLEET_ONLY" = "1" ]; then
  install_fleet_hook "$AW_PROVIDER"
  exit 0
fi
```

- In the plain-install dry-run branch, before its `exit 0`, and in the real branch after the wrapper is written, add:

```bash
if [ "${AW_NO_FLEET_HOOK:-0}" != "1" ]; then
  for P in $(installed_providers); do install_fleet_hook "$P"; done
fi
```

The functions must be defined above both branches, so put them right after `write_wrapper`. A `for ev in $(…)` loop splits on spaces only, so the `|` inside the matcher survives.

`config/hooks/adapters/README.md`, mapping table: add the row

```
| `fleet.sh` | PermissionRequest, Notification, Stop, SubagentStart, SubagentStop, UserPromptSubmit, PreCompact, SessionEnd, PreToolUse `AskUserQuestion\|ExitPlanMode` (`install-sindri.sh`) | PermissionRequest, Stop, SubagentStart, SubagentStop, UserPromptSubmit, PreCompact, SessionEnd (`--provider codex`) | `stop`, `subagentStart`, `subagentStop`, `beforeSubmitPrompt`, `preCompact`; no permission or question event, so Cursor states stay `estimated` |
```

`.agents/rules/hooks.md`:
- Add `fleet.sh` to the Hook Files table: `| fleet.sh | Spool (many events) | — |`.
- Add `config/hooks/tests/fleet-hook.test.sh` to the test list.
- Add a section:

```markdown
## Fleet hook (sindri dashboard)

`fleet.sh` (`# aw:fleet`) appends one line per hook event to `$AW_STATE_DIR/sindri/fleet/spool/<provider>.jsonl` through `sindri fleet hook <provider>`: provider, session, event, time, and a short scrubbed detail (never the tool input). It's the authoritative "waiting on you" signal for `sindri fleet` and the dashboard. It saves stdin to a 0600 temp file, runs the CLI detached with its output discarded, prints nothing, and exits 0 at once, so it can't block or fail a turn. It does nothing in `AW_JUDGE_CHILD` / `AW_SINDRI_CHILD` sessions or without sindri. A plain `scripts/install-sindri.sh` installs it for every installed provider (`AW_NO_FLEET_HOOK=1` skips it); `--fleet-hook --provider X` installs one. The events per provider are in `config/hooks/adapters/README.md`.
```

`AGENTS.md`:
- Under Commands, add `bash config/hooks/tests/fleet-hook.test.sh` after the `sindri-nudge` test line.
- In the directory tree, extend the `config/` comment to `… sindri-nudge SessionStart hook, aw:fleet spool hook (+ hooks/adapters/ per provider)`.

- [ ] **Step 4: Run the tests**

Run: `cd sindri && npx vitest run tests/fleet-spool.test.ts`, then from the repo root `bash config/hooks/tests/fleet-hook.test.sh && bash scripts/tests/install-sindri.test.sh && bash config/hooks/tests/provider-install-hooks.test.sh`.
Expected: all PASS. Then, once for the task: `cd sindri && npm test && npm run typecheck && npm run test:coverage`. Expected: all PASS, coverage 100%.

- [ ] **Step 5: Commit**

```bash
git add sindri/src/fleet/spool.ts sindri/tests/fleet-spool.test.ts config/hooks/fleet.sh config/hooks/tests/fleet-hook.test.sh config/hooks/adapters/README.md scripts/install-sindri.sh scripts/tests/install-sindri.test.sh .agents/rules/hooks.md AGENTS.md
git commit -m "feat: aw:fleet hook spool for every provider, with its installer" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 6: State rules (pure, table-driven) and the subagent roll-up

**Files:**
- Create: `sindri/src/fleet/state.ts`
- Test: `sindri/tests/fleet-state.test.ts`

**Interfaces:**
- Consumes: `Session`, `SessionSignals`, `SpoolEvent`, `FleetState`, `stateRank`, `RECENT_WRITE_MS`, `FUTURE_SLACK_MS` (Task 1); `claudeSignals` (Task 2) and `summarize` (Task 5), both only in the tests, which build signals from real-shaped event sequences.
- Produces:
  - `Derived { state; reason }` and `StatedSession = Session & Derived & { children: StatedSession[]; childCount: number }`.
  - `ageMs(ts, now): number | null`. It never goes negative. A timestamp more than 2 min in the future (the clock moved back) has no known age.
  - `pendingOf(signals): SpoolEvent | null` (spec decision 4).
  - `deriveState({ alive, signals }, now, stuckMinutes): Derived`: exactly one of the spec §4.3 states, with a reason in words.
    - **Alive:**
      1. `approval`: Codex `waitingOnApproval`, a pending spool permission, or the status file's `waiting`;
      2. `input`: Codex `waitingOnUserInput`, a pending spool question, or an open `AskUserQuestion`;
      3. `plan ready`;
      4. `auth`;
      5. `working`: a write in the last 60 s; busy with recent output; busy while background tasks run; or compacting;
      6. `stuck`: busy with no output for more than `stuckMinutes`;
      7. `background`: idle while background tasks or subagents will wake it;
      8. `limited`, then `failed` (only once the turn has ended), then `interrupted`, then `ready`.
    - **Gone:** `crashed` (the turn never closed, or the process stopped mid-tool-call), then `interrupted`, `limited`, `failed`, `ended`.
  - `rollUp(sessions): StatedSession[]`. Children hide under their parent (one level). `childCount` counts live children. A live child in `approval` or `input` raises the parent's state, with its reason quoted. A `ready` parent with running children becomes `background`. An orphan child (its parent wasn't discovered) shows at the top level.

- [ ] **Step 1: Write the failing tests**

`sindri/tests/fleet-state.test.ts`:

```ts
import { describe, expect, it } from "vitest";

import { claudeSignals } from "../src/fleet/adapters/claude.js";
import { summarize } from "../src/fleet/spool.js";
import { ageMs, deriveState, pendingOf, rollUp, type Derived } from "../src/fleet/state.js";
import { emptySignals, type FleetState, type Session, type SpoolEvent } from "../src/fleet/types.js";
import { makeScrubber } from "../src/scrub/scrub.js";
import { at, L, NOW } from "./fleet-fixtures.js";

const s = makeScrubber();
const sp = (event: SpoolEvent["event"], secondsAgo: number, o: Partial<SpoolEvent> = {}): SpoolEvent => ({ v: 1, provider: "claude", session: "a", event, ts: at(secondsAgo), detail: "", ...o });

interface Row {
  name: string;
  alive: boolean;
  busy?: boolean;
  waiting?: boolean;
  lines: object[];
  spool?: SpoolEvent[];
  codex?: { waitingOnApproval: boolean; waitingOnUserInput: boolean };
  state: FleetState;
  reason?: RegExp;
}

// Each row is an event sequence: a Claude transcript tail, the spool lines, and what the status file says.
const ROWS: Row[] = [
  { name: "a pending permission is approval", alive: true, busy: true, lines: [L.user("go", at(600)), L.toolUse("Bash", { command: "x" }, "t1", at(590))], spool: [sp("permission", 580, { detail: "Bash: x" })], state: "approval", reason: /^Bash: x$/ },
  { name: "a pending approval is never stuck, however long it waits", alive: true, busy: true, lines: [L.user("go", at(3600)), L.toolUse("Bash", { command: "x" }, "t1", at(3500))], spool: [sp("permission", 3400)], state: "approval", reason: /waiting for tool permission/ },
  { name: "output after the request resolves it", alive: true, busy: true, lines: [L.user("go", at(600)), L.toolUse("Bash", {}, "t1", at(590)), L.toolResult("t1", at(400)), L.assistantText("ran it", at(100), "tool_use")], spool: [sp("permission", 500)], state: "working" },
  { name: "a Stop clears a pending request", alive: true, busy: false, lines: [L.user("go", at(600)), L.assistantText("done", at(450)), L.stopSummary(at(400))], spool: [sp("permission", 500), sp("stop", 400)], state: "ready", reason: /your turn/ },
  { name: "an open AskUserQuestion is input", alive: true, busy: true, lines: [L.user("go", at(600)), L.toolUse("AskUserQuestion", { questions: [{ question: "Which database?" }] }, "q", at(500))], state: "input", reason: /^Which database\?$/ },
  { name: "a spooled question is input", alive: true, busy: true, lines: [L.user("go", at(600))], spool: [sp("question", 300, { detail: "Pick one" })], state: "input", reason: /Pick one/ },
  { name: "Codex waitingOnUserInput is input", alive: true, busy: true, lines: [], codex: { waitingOnApproval: false, waitingOnUserInput: true }, state: "input" },
  { name: "Codex waitingOnApproval is approval", alive: true, busy: true, lines: [], codex: { waitingOnApproval: true, waitingOnUserInput: false }, state: "approval" },
  { name: "the status file's waiting is approval", alive: true, waiting: true, lines: [L.user("go", at(600))], state: "approval", reason: /says it is waiting on you/ },
  { name: "an open ExitPlanMode is plan ready", alive: true, busy: true, lines: [L.user("plan it", at(600)), L.toolUse("ExitPlanMode", { plan: "x" }, "p", at(500))], state: "plan ready" },
  { name: "a spooled plan is plan ready", alive: true, busy: true, lines: [L.user("plan it", at(600))], spool: [sp("plan", 400)], state: "plan ready" },
  { name: "a login error is auth", alive: true, busy: false, lines: [L.user("go", at(600)), L.apiErrorMessage("Login expired · Please run /login", "authentication_failed", at(500))], state: "auth" },
  { name: "a write in the last minute is working, even if the status file still says idle", alive: true, busy: false, lines: [L.user("go", at(40)), L.assistantText("on it", at(30), "tool_use")], state: "working", reason: /busy/ },
  { name: "busy with output five minutes ago is working", alive: true, busy: true, lines: [L.user("go", at(600)), L.toolUse("Bash", {}, "t", at(300))], state: "working" },
  { name: "busy with no output for 20 minutes is stuck", alive: true, busy: true, lines: [L.user("go", at(1300)), L.toolUse("Bash", {}, "t", at(1200))], state: "stuck", reason: /no output for 20 min/ },
  { name: "busy while subagents run is working, not stuck", alive: true, busy: true, lines: [L.user("go", at(1300)), L.toolUse("Agent", {}, "t", at(1200))], spool: [sp("subagent-start", 1190, { agent: "x" })], state: "working", reason: /background tasks/ },
  { name: "compacting is working, said in words", alive: true, busy: true, lines: [L.user("go", at(600)), L.toolUse("Bash", {}, "t", at(500))], spool: [sp("compact", 200)], state: "working", reason: /compacting context/ },
  { name: "idle with background agents pending is background", alive: true, busy: false, lines: [L.user("go", at(600)), L.turnDuration(at(300), 2)], state: "background", reason: /2 background task/ },
  { name: "a Stop that reported background tasks is background", alive: true, busy: false, lines: [L.user("go", at(600)), L.stopSummary(at(300))], spool: [sp("stop", 300, { bg: 1 })], state: "background" },
  { name: "idle after a finished turn is ready", alive: true, busy: false, lines: [L.user("go", at(600)), L.assistantText("done", at(310)), L.stopSummary(at(300))], state: "ready" },
  { name: "aborted_tools is interrupted", alive: true, busy: false, lines: [L.user("go", at(600)), L.toolUse("Bash", {}, "t", at(500)), L.interrupted(true, at(400))], state: "interrupted", reason: /during a tool call/ },
  { name: "aborted_streaming is interrupted", alive: true, busy: false, lines: [L.user("go", at(600)), L.interrupted(false, at(400))], state: "interrupted", reason: /while answering/ },
  { name: "a 529 is failed even when the result says success", alive: true, busy: false, lines: [L.user("go", at(600)), L.apiErrorMessage("API Error: 529 Overloaded", "server_error", at(500)), L.result("completed", at(499))], state: "failed", reason: /API error 529/ },
  { name: "a 429 is limited even when the result says success", alive: true, busy: false, lines: [L.user("go", at(600)), L.apiErrorMessage("API Error: 429 usage limit, resets 5pm", "rate_limit", at(500)), L.result("completed", at(499))], state: "limited", reason: /resets 5pm/ },
  { name: "a limit with no reset time says so", alive: true, busy: false, lines: [L.user("go", at(600)), L.apiErrorMessage("Claude usage limit reached", "rate_limit", at(500))], state: "limited", reason: /usage limit reached/ },
  { name: "an error with no status is failed", alive: true, busy: false, lines: [L.user("go", at(600)), L.result("error_during_execution", at(500))], state: "failed", reason: /ended in an error/ },
  { name: "gone mid-tool-call is crashed", alive: false, lines: [L.user("go", at(600)), L.toolUse("Bash", {}, "t", at(500))], state: "crashed", reason: /mid-tool-call/ },
  { name: "gone with the turn still open is crashed", alive: false, lines: [L.user("go", at(600)), L.assistantText("thinking", at(500), "tool_use")], state: "crashed", reason: /never finished/ },
  { name: "gone after an interruption is interrupted (resumable)", alive: false, lines: [L.user("go", at(600)), L.interrupted(false, at(500))], state: "interrupted" },
  { name: "gone after a limit is limited", alive: false, lines: [L.user("go", at(600)), L.apiErrorMessage("API Error: 429", "rate_limit", at(500))], state: "limited" },
  { name: "gone after a login error is failed", alive: false, lines: [L.user("go", at(600)), L.apiErrorMessage("Login expired · Please run /login", "authentication_failed", at(500))], state: "failed" },
  { name: "gone after a finished turn is ended", alive: false, lines: [L.user("go", at(600)), L.stopSummary(at(500))], state: "ended" },
  { name: "the clock moved back: a future write is not recent and never makes a session stuck", alive: true, busy: true, lines: [L.user("go", at(-3600)), L.toolUse("Bash", {}, "t", at(-3500))], state: "working" },
];

describe("deriveState", () => {
  it.each(ROWS)("$name", (row) => {
    const p = claudeSignals(row.lines as Record<string, unknown>[], s);
    const signals = { ...p.signals, busy: row.busy ?? null, statusWaiting: row.waiting ?? false, codex: row.codex === undefined ? null : { ...row.codex, originator: null } };
    signals.spool = row.spool === undefined ? null : (summarize(row.spool).get("claude:a") ?? null);
    if (signals.spool?.compacting === true) signals.compacting = true;
    const got = deriveState({ alive: row.alive, signals }, NOW, 10);
    expect(got.state).toBe(row.state);
    if (row.reason !== undefined) expect(got.reason).toMatch(row.reason);
  });

  it("covers every state in the spec table", () => {
    expect(new Set(ROWS.map((r) => r.state))).toEqual(new Set(["approval", "input", "plan ready", "auth", "working", "background", "ready", "stuck", "interrupted", "failed", "limited", "crashed", "ended"]));
  });
});

describe("ageMs and pendingOf", () => {
  it("never goes negative, and a far-future or junk timestamp has no age", () => {
    expect(ageMs(null, NOW)).toBeNull();
    expect(ageMs("junk", NOW)).toBeNull();
    expect(ageMs(at(90), NOW)).toBe(90_000);
    expect(ageMs(at(-60), NOW)).toBe(0); // within the 2-minute slack
    expect(ageMs(at(-600), NOW)).toBeNull();
  });

  it("a spool event is pending until newer output appears", () => {
    const sig = { ...emptySignals(), spool: summarize([sp("permission", 100)]).get("claude:a") ?? null };
    expect(pendingOf(sig)?.event).toBe("permission");
    expect(pendingOf({ ...sig, lastOutputAt: at(50) })).toBeNull();
    expect(pendingOf({ ...sig, lastOutputAt: at(150) })?.event).toBe("permission");
    expect(pendingOf(emptySignals())).toBeNull();
  });
});

describe("rollUp", () => {
  const sess = (id: string, o: Partial<Session & Derived> = {}): Session & Derived => ({
    provider: "claude", id, parentId: null, cwd: "/x", repo: null, worktree: null, host: "terminal", pid: null, tty: null, alive: true,
    startedAt: at(100), updatedAt: at(10), title: id, activity: "", signals: emptySignals(), estimated: false, transcript: null,
    turnStartedAt: null, promptTimes: [], costUsd: null, tokens: null, timeline: [], state: "ready", reason: "your turn", ...o,
  });

  it("hides children under their parent, counts the live ones, and lets a child's approval raise the parent", () => {
    const out = rollUp([sess("p"), sess("c1", { parentId: "p", state: "approval", reason: "Bash: x", title: "Explore auth" }), sess("c2", { parentId: "p", alive: false, state: "ended" })]);
    expect(out).toHaveLength(1);
    expect(out[0]).toMatchObject({ id: "p", state: "approval", reason: 'subagent "Explore auth": Bash: x', childCount: 1 });
    expect(out[0].children.map((c) => c.id)).toEqual(["c1", "c2"]);
  });

  it("raises a working parent to input, makes a ready parent background while children run, and keeps orphans", () => {
    expect(rollUp([sess("p", { state: "working" }), sess("c", { parentId: "p", state: "input" })])[0].state).toBe("input");
    expect(rollUp([sess("p", { state: "approval" }), sess("c", { parentId: "p", state: "input" })])[0].state).toBe("approval");
    expect(rollUp([sess("p"), sess("c", { parentId: "p", state: "working" })])[0]).toMatchObject({ state: "background", reason: "1 subagent(s) still running" });
    expect(rollUp([sess("p", { alive: false, state: "ended" }), sess("c", { parentId: "p", alive: false, state: "ended" })])[0].state).toBe("ended");
    expect(rollUp([sess("c", { parentId: "missing" })]).map((x) => x.id)).toEqual(["c"]);
    expect(rollUp([sess("x", { provider: "codex" }), sess("c", { parentId: "x" })]).map((x) => x.id)).toEqual(["x", "c"]); // parents match within a provider
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd sindri && npx vitest run tests/fleet-state.test.ts`
Expected: FAIL with `Failed to load url ../src/fleet/state.js`.

- [ ] **Step 3: Implement**

`sindri/src/fleet/state.ts`:

```ts
import { FUTURE_SLACK_MS, RECENT_WRITE_MS } from "./limits.js";
import { stateRank, type FleetState, type Session, type SessionSignals, type SpoolEvent } from "./types.js";

export interface Derived {
  state: FleetState;
  reason: string;
}
export type StatedSession = Session & Derived & { children: StatedSession[]; childCount: number };

// Review Focus 5: a timestamp from the future (the clock moved back) has no known age, so it is never
// "recent" and never counts toward stuck; within 2 minutes of now it's clock skew and counts as 0.
export function ageMs(ts: string | null, now: Date): number | null {
  if (ts === null) return null;
  const t = Date.parse(ts);
  if (Number.isNaN(t)) return null;
  const d = now.getTime() - t;
  return d < -FUTURE_SLACK_MS ? null : Math.max(0, d);
}

// Spec decision 4: summarize() drops a pending event once a clearing spool event follows; transcript output
// newer than the event resolves it too (the tool ran, the question was answered).
export function pendingOf(s: SessionSignals): SpoolEvent | null {
  const p = s.spool?.pending ?? null;
  if (p === null) return null;
  return s.lastOutputAt !== null && Date.parse(s.lastOutputAt) > Date.parse(p.ts) ? null : p;
}

const working = (s: SessionSignals): Derived => ({ state: "working", reason: s.compacting ? "compacting context" : "busy" });
const limited = (s: SessionSignals): Derived => ({ state: "limited", reason: s.apiError?.resetAt ? `usage limit; resets ${s.apiError.resetAt}` : "usage limit reached" });
const failed = (s: SessionSignals): Derived => ({
  state: "failed",
  reason: s.apiError?.kind === "auth" ? "login expired" : s.apiError?.status ? `API error ${s.apiError.status}` : "the last turn ended in an error",
});

// Spec §4.3: exactly one state per session. The checks below are ordered so each state's rule holds
// exactly as the table words it (e.g. stuck excludes approval because approval is decided first).
export function deriveState(input: { alive: boolean; signals: SessionSignals }, now: Date, stuckMinutes: number): Derived {
  const s = input.signals;
  const aborted = s.terminalReason === "aborted_tools" || s.terminalReason === "aborted_streaming";
  const interrupted: Derived = { state: "interrupted", reason: s.terminalReason === "aborted_tools" ? "stopped during a tool call" : "stopped while answering" };
  if (!input.alive) {
    if (s.openToolCall) return { state: "crashed", reason: "the process is gone mid-tool-call" };
    if (s.turnOpen) return { state: "crashed", reason: "the process is gone but the turn never finished" };
    if (aborted) return interrupted;
    if (s.apiError?.kind === "limit") return limited(s);
    if (s.apiError !== null || s.terminalReason === "error") return failed(s);
    return { state: "ended", reason: "the session ended" };
  }
  const p = pendingOf(s);
  if (s.codex?.waitingOnApproval === true || p?.event === "permission") return { state: "approval", reason: p?.detail ? p.detail : "waiting for tool permission" };
  if (s.codex?.waitingOnUserInput === true || p?.event === "question" || s.pendingQuestion !== null) {
    return { state: "input", reason: p?.detail ? p.detail : (s.pendingQuestion ?? "asked you a question") };
  }
  if (s.statusWaiting) return { state: "approval", reason: "the session says it is waiting on you" };
  if (p?.event === "plan" || s.planPending) return { state: "plan ready", reason: "a plan waits for your approval" };
  if (s.apiError?.kind === "auth") return { state: "auth", reason: "the provider needs you to log in again" };
  const writeAge = ageMs(s.lastWriteAt, now);
  if (writeAge !== null && writeAge <= RECENT_WRITE_MS) return working(s);
  const children = s.spool?.openAgents.length ?? 0;
  if (s.busy ?? s.turnOpen) {
    if (s.backgroundTasks > 0 || children > 0) return { state: "working", reason: "waiting on background tasks" };
    const outAge = ageMs(s.lastOutputAt ?? s.lastWriteAt, now);
    if (outAge === null || outAge <= stuckMinutes * 60_000) return working(s);
    return { state: "stuck", reason: `no output for ${Math.floor(outAge / 60_000)} min` };
  }
  const bg = Math.max(s.backgroundTasks, s.spool?.bg ?? 0, children);
  if (bg > 0) return { state: "background", reason: `${bg} background task(s) will wake it` };
  if (s.apiError?.kind === "limit") return limited(s);
  if (s.apiError !== null || s.terminalReason === "error") return failed(s);
  if (aborted) return interrupted;
  return { state: "ready", reason: "your turn" };
}

const key = (provider: string, id: string): string => `${provider}:${id}`;

// Spec §4.3 "Subagents and teammates": one level of nesting (Claude keeps subagent transcripts flat
// under the session; Codex links each subagent rollout to its parent thread).
export function rollUp(all: readonly (Session & Derived)[]): StatedSession[] {
  const present = new Set(all.map((x) => key(x.provider, x.id)));
  const kids = new Map<string, StatedSession[]>();
  for (const x of all) {
    if (x.parentId === null || !present.has(key(x.provider, x.parentId))) continue;
    const k = key(x.provider, x.parentId);
    kids.set(k, [...(kids.get(k) ?? []), { ...x, children: [], childCount: 0 }]);
  }
  return all
    .filter((x) => x.parentId === null || !present.has(key(x.provider, x.parentId)))
    .map((parent) => {
      const children = kids.get(key(parent.provider, parent.id)) ?? [];
      const live = children.filter((c) => c.alive);
      let { state, reason } = parent;
      const raised = live.find((c) => c.state === "approval") ?? live.find((c) => c.state === "input");
      if (raised !== undefined && stateRank(raised.state) < stateRank(state)) {
        state = raised.state;
        reason = `subagent "${raised.title}": ${raised.reason}`;
      } else if (parent.alive && state === "ready" && live.some((c) => c.state === "working" || c.state === "background" || c.state === "stuck")) {
        state = "background";
        reason = `${live.length} subagent(s) still running`;
      }
      return { ...parent, state, reason, children, childCount: live.length };
    });
}
```

Traces:
- **The clock-moved-back row:** both lines are in the future beyond the slack, so `writeAge` is null and so is `outAge`; busy is true, so the session is `working` and never `stuck`.
- **The "write in the last minute" row:** `busy: false` from the status file, but the write 30 s ago wins, so it's `working`.
- **The 529 row:** the result line sets `terminalReason: "completed"`, but `apiError` survives (only a new human prompt clears it), so the session is `failed` with "API error 529".
- **The "gone mid-tool-call" row:** the process is dead and the `Bash` tool_use has no result.
- **The orphan row of `rollUp`:** a Claude child whose parent id names a Codex session isn't a child of it.

- [ ] **Step 4: Run the tests**

Run: `cd sindri && npx vitest run tests/fleet-state.test.ts`
Expected: PASS (33 rows plus the coverage row and the helper tests). Then, once for the task: `npm test && npm run typecheck && npm run test:coverage`. Expected: all PASS, coverage 100%.

- [ ] **Step 5: Commit**

```bash
git add sindri/src/fleet/state.ts sindri/tests/fleet-state.test.ts
git commit -m "feat: sindri fleet state rules and subagent roll-up" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 7: The needs-you aggregator: decisions, failures, gh PRs (cached), bridge unread, acks

**Files:**
- Create: `sindri/src/fleet/decisions.ts`, `sindri/src/fleet/gh.ts`, `sindri/src/fleet/bridge.ts`, `sindri/src/fleet/acks.ts`, `sindri/src/fleet/needs.ts`
- Modify: `sindri/src/evolve/cmd/channel.ts` (export `suiteRunAt`)
- Test: `sindri/tests/fleet-needs.test.ts`, `sindri/tests/fleet-decisions.test.ts`, `sindri/tests/fleet-gh.test.ts`, `sindri/tests/fleet-bridge.test.ts`

**Interfaces:**
- Consumes:
  - from Tasks 1–6: `StatedSession`, `stateRank`, `JobView`, `RepoView`, `PrView`, `fleetDir`, `cleanLine`, `GH_TTL_MS`, `JUDGE_TTL_MS`;
  - existing code: `approvalState`, `loadProfile`, `resolveProfileRoot`, `readChannels`, `canPromote`, `suiteRunAt`, `ghRepoOf`, `ghJson`, `ringZeroRepo`, `ProcessRunner`, better-sqlite3.
- Produces:
  - `acks.ts`: `isAckKey(key)`, `readAcks(deps): Record<string, string>`, and `writeAck(deps, key, now)`, which writes atomically, 0600, and prunes acks older than 30 days.
  - `decisions.ts`:
    - `Decision { kind: "proposal-won" | "profile-approval" | "proposals-staged" | "channel-promote"; id; title; since; command }`;
    - `readDecisions(deps, db, scrubber): Decision[]`;
    - `Health { doctor: JobView | null; judge: { status: "ok" | "degraded" | "missing"; failures24h: number | null; at: string } | null }`;
    - `readHealth(deps, run, jobs): Promise<Health>`, whose `judge health` result is cached 5 min in `fleet/judge-cache.json`.
  - `gh.ts`: `prsFor(deps, run, repos, scrubber): Promise<{ prs: PrView[]; errors: string[]; cached: boolean }>`. It makes one `gh api user` call and one `gh pr list --repo <origin slug>` per repo, cached 2 min in `fleet/gh-cache.json`; a cache stamped in the future is refetched. Only `https://github.com/` URLs are kept.
  - `bridge.ts`: `bridgeDbPath(deps, loaded): string | null` (`AW_BRIDGE_DB`, else `<toolkit repo>/mcp-bridge/bridge.db`) and `unreadFor(file, recipient): { unread; since } | null`, which opens the file query-only and never marks a message read.
  - `needs.ts`:
    - `NeedKind`, `NeedAction` (`attach | terminal | run(fleet.ack) | reject | open`) and `NeedItem { key; kind; state; title; reason; repo; since; link; action; secondary?; urgent; ackable }`;
    - `NeedInputs`, `needsYou(inputs): NeedItem[]` (spec §4.4, ordered as in spec decision 16), and `doneUnread(session, acks)`.
    - Keys: `session:<p>:<id>`, `failure:<p>:<id>:<state>:<turn start>`, `job:<kind>:<job id>`, `index-stale:<repo>:<built at|never>`, `judge:<date>`, `proposal-won:<id>`, `profile-approval:<hash12>`, `proposals-staged:<count>`, `channel-promote:<sha12>`, `pr:<repo>#<n>`, `bridge:<recipient>`.

- [ ] **Step 1: Write the failing tests**

`sindri/tests/fleet-needs.test.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { isAckKey, readAcks, writeAck } from "../src/fleet/acks.js";
import type { JobView } from "../src/fleet/jobs.js";
import { doneUnread, needsYou, type NeedInputs } from "../src/fleet/needs.js";
import type { StatedSession } from "../src/fleet/state.js";
import { emptySignals } from "../src/fleet/types.js";
import { at, NOW } from "./fleet-fixtures.js";
import { makeDeps } from "./helpers.js";

const sess = (id: string, o: Partial<StatedSession>): StatedSession => ({
  provider: "claude", id, parentId: null, cwd: "/x", repo: "acme-web", worktree: null, host: "terminal", pid: 1, tty: null, alive: true,
  startedAt: at(900), updatedAt: at(60), title: `t-${id}`, activity: "", signals: emptySignals(), estimated: false, transcript: null,
  turnStartedAt: at(300), promptTimes: [], costUsd: null, tokens: null, timeline: [], state: "ready", reason: "your turn", children: [], childCount: 0, ...o,
});
const job = (kind: string, state: JobView["state"], o: Partial<JobView> = {}): JobView => ({
  id: "01hzzzzzzzzzzzzzzzzzzzzzzz", kind, repo: null, argv: [], pid: 1, pidStart: null, host: "h", startedAt: at(500), endedAt: at(400), exitCode: 2, summary: "boom", state, ...o,
});
const empty: NeedInputs = { sessions: [], jobs: [], repos: [], decisions: [], health: { doctor: null, judge: null }, prs: [], bridge: null, acks: {} };

describe("needsYou", () => {
  it("lists waiting sessions first (urgent, attach), then decisions, failures, PRs and bridge messages", () => {
    const withPending = { ...emptySignals(), spool: { last: null, pending: { v: 1 as const, provider: "claude" as const, session: "a", event: "permission" as const, ts: at(200), detail: "Bash: x" }, compacting: false, bg: null, openAgents: [], transcript: null, stopStatus: null } };
    const items = needsYou({
      ...empty,
      sessions: [
        sess("w1", { state: "working" }),
        sess("in", { state: "input", reason: "Which database?", updatedAt: at(100) }),
        sess("ap", { state: "approval", reason: "Bash: x", signals: withPending }),
        sess("st", { state: "stuck", reason: "no output for 20 min" }),
        sess("pl", { state: "plan ready", updatedAt: at(50), provider: "codex" }),
      ],
      jobs: [job("index-build:acme-web", "failed"), job("evolve-weekly", "attn", { exitCode: 1, summary: "attn reflect" }), job("doctor", "failed"), job("observe", "ok")],
      repos: [{ name: "acme-web", path: "/r", index: { builtAt: null, ageMs: null, stale: true, commit: null, files: null, symbols: null, behind: null, lastBuild: null }, observe: null, scopeRuns: [], prs: [], worktrees: [] }],
      decisions: [{ kind: "proposal-won", id: "01hyyyyyyyyyyyyyyyyyyyyyyy", title: "proposal won: tighten the draft prompt", since: at(1000), command: "sindri evolve adopt 01hyyyyyyyyyyyyyyyyyyyyyyy" }],
      health: { doctor: null, judge: { status: "degraded", failures24h: 3, at: NOW.toISOString() } },
      prs: [{ repo: "acme-web", number: 7, title: "Fix login", url: "https://github.com/acme/web/pull/7", draft: false, reviewDecision: null, mergeState: "BLOCKED", author: "someone", requestedMe: true, readyToMerge: false, createdAt: at(5000) }],
      bridge: { recipient: "tester", unread: 2, since: at(700) },
    });
    expect(items.map((i) => [i.kind, i.state])).toEqual([
      ["session", "approval"], ["session", "input"], ["session", "plan ready"],
      ["decision", "won"],
      ["failure", "stuck"], ["failure", "failed"], ["failure", "failed"], ["failure", "stale"], ["failure", "attn"], ["failure", "degraded"],
      ["pr", "review requested"],
      ["bridge", "unread"],
    ]);
    expect(items[0]).toMatchObject({ key: "session:claude:ap", urgent: true, ackable: false, since: at(200), link: "/agent/claude/ap", action: { type: "attach", provider: "claude", id: "ap" } });
    expect(items[2]).toMatchObject({ key: "session:codex:pl", urgent: false });
    expect(items[3]).toMatchObject({ action: { type: "terminal", command: "sindri evolve adopt 01hyyyyyyyyyyyyyyyyyyyyyyy" }, secondary: { type: "reject", id: "01hyyyyyyyyyyyyyyyyyyyyyyy" } });
    expect(items[4]).toMatchObject({ key: `failure:claude:st:stuck:${at(300)}`, ackable: true });
    expect(items.find((i) => i.key.startsWith("job:doctor"))?.action).toEqual({ type: "terminal", command: "sindri doctor" });
    expect(items.find((i) => i.key.startsWith("index-stale"))).toMatchObject({ key: "index-stale:acme-web:never", action: { type: "terminal", command: "sindri index build --repo acme-web" } });
    expect(items.find((i) => i.kind === "pr")?.action).toEqual({ type: "open", url: "https://github.com/acme/web/pull/7" });
    expect(items.at(-1)).toMatchObject({ key: "bridge:tester", title: "2 unread bridge messages for tester" });
  });

  it("hides acknowledged failures until they happen again, never hides waiting sessions", () => {
    const failed = sess("f", { state: "failed" });
    const key = `failure:claude:f:failed:${at(300)}`;
    expect(needsYou({ ...empty, sessions: [failed], acks: { [key]: NOW.toISOString() } })).toEqual([]);
    expect(needsYou({ ...empty, sessions: [{ ...failed, turnStartedAt: at(10) }], acks: { [key]: NOW.toISOString() } })).toHaveLength(1);
    expect(needsYou({ ...empty, sessions: [sess("a", { state: "approval" })], acks: { "session:claude:a": NOW.toISOString() } })).toHaveLength(1);
    expect(needsYou({ ...empty, prs: [{ repo: "r", number: 1, title: "x", url: "https://github.com/a/b/pull/1", draft: false, reviewDecision: "APPROVED", mergeState: "CLEAN", author: "me", requestedMe: false, readyToMerge: true, createdAt: at(1) }] })[0].state).toBe("ready to merge");
    expect(needsYou({ ...empty, bridge: { recipient: "tester", unread: 0, since: null } })).toEqual([]);
    expect(needsYou({ ...empty, health: { doctor: job("doctor", "ok", { exitCode: 0 }), judge: { status: "ok", failures24h: 0, at: "x" } } })).toEqual([]);
  });

  it("puts a done marker on a ready session until it is seen", () => {
    const ready = sess("r", { state: "ready", updatedAt: at(60) });
    expect(doneUnread(ready, {})).toBe(true);
    expect(doneUnread(ready, { "done:claude:r": at(30) })).toBe(false);
    expect(doneUnread(ready, { "done:claude:r": at(120) })).toBe(true);
    expect(doneUnread(sess("w", { state: "working" }), {})).toBe(false);
  });
});

describe("acks", () => {
  it("validates keys, writes 0600 atomically, and prunes acks older than 30 days", () => {
    expect(isAckKey("failure:claude:abc:failed:2026-10-08T12:00:00.000Z")).toBe(true);
    expect(isAckKey("pr:acme-web#7")).toBe(true);
    expect(isAckKey("../etc")).toBe(false);
    expect(isAckKey("x".repeat(300))).toBe(false);
    const deps = makeDeps();
    expect(readAcks(deps)).toEqual({});
    writeAck(deps, "job:observe:1", NOW);
    writeAck(deps, "job:observe:2", new Date(NOW.getTime() + 31 * 86_400_000));
    expect(Object.keys(readAcks(deps))).toEqual(["job:observe:2"]);
    const file = path.join(deps.env.AW_STATE_DIR as string, "sindri", "fleet", "acks.json");
    expect(fs.statSync(file).mode & 0o777).toBe(0o600);
    fs.writeFileSync(file, '{"ok:1":"2026-10-08T00:00:00Z","bad":3,"../x":"y"}');
    expect(readAcks(deps)).toEqual({ "ok:1": "2026-10-08T00:00:00Z" });
    fs.writeFileSync(file, "[1]");
    expect(readAcks(deps)).toEqual({});
  });
});
```

`sindri/tests/fleet-decisions.test.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { stateDir } from "../src/deps.js";
import { readDecisions, readHealth } from "../src/fleet/decisions.js";
import type { JobView } from "../src/fleet/jobs.js";
import { makeScrubber } from "../src/scrub/scrub.js";
import { evolveFixture, fakeProc } from "./evolve-fixtures.js";
import { makeDeps } from "./helpers.js";

const s = makeScrubber();
const SHA = "a".repeat(40);

describe("readDecisions", () => {
  it("finds won and staged proposals, a pending profile change and a promotable channel build", async () => {
    const fx = await evolveFixture();
    const db = fx.ctx.db;
    const ins = db.prepare("INSERT INTO proposals (id, artifact_id, source, kind, tier, status, title, norm_title, body, created_at, updated_at, epoch) VALUES (?, 'prompt:scope.draft', 'compare', 'prompt', 'code', ?, ?, ?, '{}', ?, ?, 0)");
    ins.run("01hyyyyyyyyyyyyyyyyyyyyyyy", "won", "Tighten \u001b[2Jthe draft", "x", "2026-10-07T00:00:00Z", "2026-10-07T00:00:00Z");
    ins.run("01hxxxxxxxxxxxxxxxxxxxxxxx", "staged", "Stage me", "y", "2026-10-06T00:00:00Z", "2026-10-06T00:00:00Z");
    expect(readDecisions(fx.deps, db, s)).toEqual([
      { kind: "proposal-won", id: "01hyyyyyyyyyyyyyyyyyyyyyyy", title: "proposal won its comparison: Tighten \\u{001B}[2Jthe draft", since: "2026-10-07T00:00:00Z", command: "sindri evolve adopt 01hyyyyyyyyyyyyyyyyyyyyyyy" },
      { kind: "proposals-staged", id: "1", title: "1 staged proposal(s) ready to publish", since: "2026-10-06T00:00:00Z", command: "sindri evolve publish" },
    ]);
    fs.appendFileSync(path.join(fx.deps.env.AW_STATE_DIR as string, "profile", "profile.yaml"), "trustedBots: [ci-bot]\n");
    fs.writeFileSync(path.join(stateDir(fx.deps), "channels.json"), JSON.stringify({ stable: null, next: { sha: SHA, dir: "/x", installedAt: "2026-10-04T00:00:00.000Z" } }));
    db.prepare("INSERT INTO suite_runs (artifact_id, hash, head, dirty, ok, exit_code, ms, ts, epoch) VALUES ('package:sindri', ?, ?, 0, 1, 0, 1, 'x', 0)").run(`at:${SHA}`, SHA);
    const kinds = readDecisions(fx.deps, db, s).map((d) => [d.kind, d.command]);
    expect(kinds).toContainEqual(["profile-approval", "sindri profile approve"]);
    expect(kinds).toContainEqual(["channel-promote", `sindri channel promote ${SHA}`]);
    fx.close();
  });

  it("is empty without a ledger, and ignores a corrupt channels.json", async () => {
    expect(readDecisions(makeDeps(), null, s)).toEqual([]);
    const fx = await evolveFixture();
    fs.writeFileSync(path.join(stateDir(fx.deps), "channels.json"), "{corrupt");
    expect(readDecisions(fx.deps, fx.ctx.db, s)).toEqual([]);
    fx.close();
  });
});

describe("readHealth", () => {
  const doctor: JobView = { id: "01hzzzzzzzzzzzzzzzzzzzzzzz", kind: "doctor", repo: null, argv: [], pid: 1, pidStart: null, host: "h", startedAt: "x", endedAt: "y", exitCode: 2, summary: "", state: "failed" };

  it("takes doctor from its last job, asks judge health at most every 5 minutes, and knows a missing judge", async () => {
    const deps = makeDeps();
    const proc = fakeProc(() => ({ stdout: '{"status":"degraded","failures24h":4}' }));
    expect(await readHealth(deps, proc, [doctor])).toEqual({ doctor, judge: { status: "degraded", failures24h: 4, at: "2026-10-08T12:00:00.000Z" } });
    await readHealth(deps, proc, []);
    expect(proc.calls).toHaveLength(1); // cached
    const later = { ...deps, now: () => new Date("2026-10-08T12:06:00.000Z") };
    await readHealth(later, proc, []);
    expect(proc.calls).toHaveLength(2);
    expect(proc.calls[0]).toEqual({ argv: ["judge", "health"], cwd: deps.home });
    const missing = await readHealth(makeDeps(), fakeProc(() => ({ code: 127 })), []);
    expect(missing.judge?.status).toBe("missing");
    const junk = await readHealth(makeDeps(), fakeProc(() => ({ stdout: "not json" })), []);
    expect(junk.judge).toBeNull();
  });
});
```

`sindri/tests/fleet-gh.test.ts`:

```ts
import { describe, expect, it } from "vitest";

import { prsFor } from "../src/fleet/gh.js";
import { makeScrubber } from "../src/scrub/scrub.js";
import { fakeProc, git } from "./evolve-fixtures.js";
import { gitRepo, makeDeps } from "./helpers.js";

const s = makeScrubber();
const LIST = JSON.stringify([
  { number: 7, title: "Fix login \u001b]52;c;eA==\u0007", url: "https://github.com/acme/web/pull/7", isDraft: false, reviewDecision: "REVIEW_REQUIRED", mergeStateStatus: "BLOCKED", author: { login: "someone" }, reviewRequests: [{ login: "me" }], createdAt: "2026-10-08T10:00:00Z" },
  { number: 8, title: "Agent PR", url: "https://github.com/acme/web/pull/8", isDraft: false, reviewDecision: "APPROVED", mergeStateStatus: "CLEAN", author: { login: "me" }, reviewRequests: [], createdAt: "2026-10-08T09:00:00Z" },
  { number: 9, title: "Phishy", url: "https://evil.example/pull/9", isDraft: false, reviewDecision: null, mergeStateStatus: "CLEAN", author: { login: "me" }, reviewRequests: [], createdAt: "2026-10-08T08:00:00Z" },
]);

function setup() {
  const repo = gitRepo({ "a.txt": "x" });
  git(repo, "remote", "add", "origin", "https://github.com/acme/web.git");
  const proc = fakeProc((argv) => (argv[1] === "api" ? { stdout: '{"login":"me"}' } : { stdout: LIST }));
  return { repo, proc, repos: [{ name: "acme-web", path: repo }] };
}

describe("prsFor", () => {
  it("lists open PRs per repo with --repo pinned to origin, marks review requests and agent PRs ready to merge", async () => {
    const { repo, proc, repos } = setup();
    const r = await prsFor(makeDeps(), proc, repos, s);
    expect(proc.calls.map((c) => c.argv.slice(0, 5))).toEqual([["gh", "api", "user"], ["gh", "pr", "list", "--repo", "acme/web"]]);
    expect(proc.calls[1].cwd).toBe(repo);
    expect(r.cached).toBe(false);
    expect(r.prs.map((p) => [p.number, p.requestedMe, p.readyToMerge])).toEqual([[7, true, false], [8, false, true]]);
    expect(r.prs[0].title).toBe("Fix login \\u{001B}]52;c;eA==\\u{0007}");
  });

  it("serves the cache for 2 minutes, refetches after, and refetches a cache stamped in the future", async () => {
    const { proc, repos } = setup();
    const deps = makeDeps();
    await prsFor(deps, proc, repos, s);
    expect((await prsFor({ ...deps, now: () => new Date("2026-10-08T12:01:59.000Z") }, proc, repos, s)).cached).toBe(true);
    expect(proc.calls).toHaveLength(2);
    await prsFor({ ...deps, now: () => new Date("2026-10-08T12:02:01.000Z") }, proc, repos, s);
    expect(proc.calls).toHaveLength(4);
    await prsFor({ ...deps, now: () => new Date("2026-10-08T11:00:00.000Z") }, proc, repos, s); // the clock moved back
    expect(proc.calls).toHaveLength(6);
  });

  it("reports gh and remote errors per repo instead of throwing", async () => {
    const noGh = await prsFor(makeDeps(), fakeProc(() => ({ code: 1, stderr: "gh: not logged in" })), [{ name: "x", path: gitRepo({ "a": "x" }) }], s);
    expect(noGh.prs).toEqual([]);
    expect(noGh.errors[0]).toMatch(/gh api user failed/);
    const noOrigin = await prsFor(makeDeps(), fakeProc(() => ({ stdout: '{"login":"me"}' })), [{ name: "x", path: gitRepo({ "a": "x" }) }], s);
    expect(noOrigin.errors[0]).toMatch(/^x: .*no GitHub remote/);
    expect((await prsFor(makeDeps(), fakeProc(() => ({})), [], s)).prs).toEqual([]);
  });
});
```

`sindri/tests/fleet-bridge.test.ts`:

```ts
import path from "node:path";
import Database from "better-sqlite3";
import { describe, expect, it } from "vitest";

import { bridgeDbPath, unreadFor } from "../src/fleet/bridge.js";
import { evolveFixture } from "./evolve-fixtures.js";
import { makeDeps, tempDir } from "./helpers.js";

function bridgeDb(): string {
  const file = path.join(tempDir(), "bridge.db");
  const db = new Database(file);
  db.exec("CREATE TABLE messages (id TEXT PRIMARY KEY, conversation TEXT NOT NULL, sender TEXT NOT NULL, recipient TEXT NOT NULL, payload TEXT NOT NULL, created_at TEXT NOT NULL DEFAULT (datetime('now')), read_at TEXT)");
  const ins = db.prepare("INSERT INTO messages (id, conversation, sender, recipient, payload, created_at, read_at) VALUES (?, 'c', 'claude', ?, 'hi', ?, ?)");
  ins.run("1", "tester", "2026-10-08 10:00:00", null);
  ins.run("2", "tester", "2026-10-08 11:00:00", null);
  ins.run("3", "tester", "2026-10-08 09:00:00", "2026-10-08 09:30:00");
  ins.run("4", "codex", "2026-10-08 09:00:00", null);
  db.close();
  return file;
}

describe("bridge unread", () => {
  it("counts unread messages for the builder without marking them read", () => {
    const file = bridgeDb();
    expect(unreadFor(file, "tester")).toEqual({ unread: 2, since: "2026-10-08T10:00:00Z" });
    expect(unreadFor(file, "tester")).toEqual({ unread: 2, since: "2026-10-08T10:00:00Z" }); // still unread
    expect(unreadFor(file, "nobody")).toEqual({ unread: 0, since: null });
    expect(unreadFor(path.join(tempDir(), "missing.db"), "tester")).toBeNull();
    const bare = path.join(tempDir(), "bare.db");
    new Database(bare).close();
    expect(() => unreadFor(bare, "tester")).toThrow(/no such table/);
  });

  it("finds the bridge db from AW_BRIDGE_DB or the toolkit repo", async () => {
    expect(bridgeDbPath(makeDeps({ env: { AW_BRIDGE_DB: "/x/bridge.db" } }), null)).toBe("/x/bridge.db");
    expect(bridgeDbPath(makeDeps(), null)).toBeNull();
    const fx = await evolveFixture();
    expect(bridgeDbPath(fx.deps, fx.ctx.loaded)).toBe(path.join(fx.repo, "mcp-bridge", "bridge.db"));
    fx.close();
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd sindri && npx vitest run tests/fleet-needs.test.ts tests/fleet-decisions.test.ts tests/fleet-gh.test.ts tests/fleet-bridge.test.ts`
Expected: FAIL with `Failed to load url ../src/fleet/acks.js` (and likewise for `decisions.js`, `gh.js`, `bridge.js`).

- [ ] **Step 3: Implement**

`sindri/src/evolve/cmd/channel.ts`: change `const suiteRunAt =` to `export const suiteRunAt =`.

`sindri/src/fleet/acks.ts`:

```ts
import fs from "node:fs";
import path from "node:path";

import type { Deps } from "../deps.js";
import { fleetDir } from "./paths.js";

const KEY = /^[a-z][a-z-]*:[A-Za-z0-9:._#/-]{1,200}$/;
const KEEP_MS = 30 * 86_400_000;

export const isAckKey = (k: string): boolean => KEY.test(k) && !k.includes("..");
const file = (deps: Deps): string => path.join(fleetDir(deps), "acks.json");

export function readAcks(deps: Deps): Record<string, string> {
  let raw: unknown;
  try {
    raw = JSON.parse(fs.readFileSync(file(deps), "utf8"));
  } catch {
    return {};
  }
  if (typeof raw !== "object" || raw === null || Array.isArray(raw)) return {};
  return Object.fromEntries(Object.entries(raw).filter((e): e is [string, string] => isAckKey(e[0]) && typeof e[1] === "string"));
}

// Spec decision 11. The caller checks the key names a current item (sindri fleet ack).
export function writeAck(deps: Deps, key: string, now: Date): void {
  const acks = { ...readAcks(deps), [key]: now.toISOString() };
  const kept = Object.fromEntries(Object.entries(acks).filter(([, at]) => now.getTime() - Date.parse(at) <= KEEP_MS));
  fs.mkdirSync(fleetDir(deps), { recursive: true, mode: 0o700 });
  const tmp = `${file(deps)}.${deps.system.pid}.tmp`;
  fs.writeFileSync(tmp, JSON.stringify(kept, null, 2), { mode: 0o600 });
  fs.renameSync(tmp, file(deps));
}
```

`sindri/src/fleet/decisions.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import { z } from "zod";

import type { Deps } from "../deps.js";
import { canPromote, readChannels } from "../evolve/channel.js";
import { suiteRunAt } from "../evolve/cmd/channel.js";
import type { ProcessRunner } from "../index/io.js";
import type { Ledger } from "../ledger/db.js";
import { approvalState } from "../profile/approve.js";
import { loadProfile, resolveProfileRoot } from "../profile/load.js";
import type { Scrubber } from "../scrub/scrub.js";
import type { JobView } from "./jobs.js";
import { JUDGE_TTL_MS } from "./limits.js";
import { fleetDir, iso } from "./paths.js";
import { cleanLine, MAX_TITLE } from "./text.js";

export interface Decision {
  kind: "proposal-won" | "profile-approval" | "proposals-staged" | "channel-promote";
  id: string;
  title: string;
  since: string;
  command: string; // always a terminal-gated command: shown, never run (spec §5 view 1)
}

function channels(deps: Deps): ReturnType<typeof readChannels> | null {
  try {
    return readChannels(deps);
  } catch {
    return null; // a corrupt channels.json is doctor's and `channel status`'s to report
  }
}

export function readDecisions(deps: Deps, db: Ledger | null, scrubber: Scrubber): Decision[] {
  if (db === null) return [];
  const out: Decision[] = [];
  for (const r of db.prepare("SELECT id, title, updated_at FROM proposals WHERE status = 'won' ORDER BY updated_at").all() as { id: string; title: string; updated_at: string }[]) {
    out.push({ kind: "proposal-won", id: r.id, title: cleanLine(`proposal won its comparison: ${r.title}`, MAX_TITLE, scrubber), since: r.updated_at, command: `sindri evolve adopt ${r.id}` });
  }
  const staged = db.prepare("SELECT COUNT(*) AS c, MIN(updated_at) AS since FROM proposals WHERE status = 'staged'").get() as { c: number; since: string | null };
  if (staged.c > 0) out.push({ kind: "proposals-staged", id: String(staged.c), title: `${staged.c} staged proposal(s) ready to publish`, since: staged.since as string, command: "sindri evolve publish" });
  const root = resolveProfileRoot(deps);
  const live = root === null ? null : loadProfile(root);
  if (root !== null && live !== null && live.ok && approvalState(deps, db, live.value.hash).kind !== "approved") {
    const st = fs.statSync(path.join(root, "profile.yaml"), { throwIfNoEntry: false });
    out.push({ kind: "profile-approval", id: live.value.hash.slice(0, 12), title: "a profile change waits for your approval", since: iso(st?.mtimeMs ?? deps.now().getTime()), command: "sindri profile approve" });
  }
  const ch = channels(deps);
  if (ch?.next) {
    const can = canPromote(ch, ch.next.sha, suiteRunAt(db, ch.next.sha), deps.now());
    if (can.ok) out.push({ kind: "channel-promote", id: ch.next.sha.slice(0, 12), title: `next build ${ch.next.sha.slice(0, 12)} has soaked and is ready to promote`, since: ch.next.installedAt, command: `sindri channel promote ${ch.next.sha}` });
  }
  return out;
}

export interface Health {
  doctor: JobView | null;
  judge: { status: "ok" | "degraded" | "missing"; failures24h: number | null; at: string } | null;
}
const JudgeOut = z.object({ status: z.enum(["ok", "degraded"]), failures24h: z.number().int().nonnegative().optional() });
const JudgeCache = z.object({ status: z.enum(["ok", "degraded", "missing"]), failures24h: z.number().nullable(), at: z.string() });

// doctor's status is its last recorded run (the dashboard's Today view runs `sindri doctor --json`, which
// leaves one); judge health is asked at most every 5 minutes.
export async function readHealth(deps: Deps, run: ProcessRunner, jobs: readonly JobView[]): Promise<Health> {
  const doctor = jobs.find((j) => j.kind === "doctor") ?? null;
  const cacheFile = path.join(fleetDir(deps), "judge-cache.json");
  const nowMs = deps.now().getTime();
  try {
    const c = JudgeCache.parse(JSON.parse(fs.readFileSync(cacheFile, "utf8")));
    const age = nowMs - Date.parse(c.at);
    if (age >= 0 && age < JUDGE_TTL_MS) return { doctor, judge: c };
  } catch {
    // no cache yet, or a corrupt one: ask again
  }
  const r = await run.run(["judge", "health"], { cwd: deps.home, timeoutMs: 5000 });
  let judge: Health["judge"] = null;
  if (r.code === 127) judge = { status: "missing", failures24h: null, at: deps.now().toISOString() };
  else {
    try {
      const j = JudgeOut.parse(JSON.parse(r.stdout));
      judge = { status: j.status, failures24h: j.failures24h ?? null, at: deps.now().toISOString() };
    } catch {
      judge = null;
    }
  }
  if (judge !== null) {
    fs.mkdirSync(fleetDir(deps), { recursive: true, mode: 0o700 });
    fs.writeFileSync(cacheFile, JSON.stringify(judge), { mode: 0o600 });
  }
  return { doctor, judge };
}
```

`sindri/src/fleet/gh.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import { z } from "zod";

import type { Deps } from "../deps.js";
import { ghJson, ghRepoOf } from "../evolve/github.js";
import type { ProcessRunner } from "../index/io.js";
import type { Scrubber } from "../scrub/scrub.js";
import { GH_TTL_MS } from "./limits.js";
import { fleetDir } from "./paths.js";
import { cleanLine, MAX_TITLE } from "./text.js";
import type { PrView } from "./types.js";

const PrRow = z.object({
  number: z.number().int().positive(),
  title: z.string(),
  url: z.string(),
  isDraft: z.boolean(),
  reviewDecision: z.string().nullable().optional(),
  mergeStateStatus: z.string().optional(),
  author: z.object({ login: z.string() }),
  reviewRequests: z.array(z.object({ login: z.string().optional() }).passthrough()).optional(),
  createdAt: z.string(),
});
const PrViewSchema = z.object({
  repo: z.string(), number: z.number(), title: z.string(), url: z.string(), draft: z.boolean(), reviewDecision: z.string().nullable(),
  mergeState: z.string(), author: z.string(), requestedMe: z.boolean(), readyToMerge: z.boolean(), createdAt: z.string(),
});
const Cache = z.object({ at: z.string(), key: z.string(), prs: z.array(PrViewSchema), errors: z.array(z.string()) });
const FIELDS = "number,title,url,isDraft,reviewDecision,mergeStateStatus,author,reviewRequests,createdAt";

const messageOf = (e: unknown): string => (e instanceof Error ? e.message : String(e));

// Spec §4.4 item 4: PRs in onboarded repos that ask for the builder's review, or that agents opened (as the
// builder's gh login) and that are approved and mergeable. Cached for 2 minutes; never throws.
export async function prsFor(deps: Deps, run: ProcessRunner, repos: readonly { name: string; path: string }[], scrubber: Scrubber): Promise<{ prs: PrView[]; errors: string[]; cached: boolean }> {
  if (repos.length === 0) return { prs: [], errors: [], cached: false };
  const file = path.join(fleetDir(deps), "gh-cache.json");
  const key = repos.map((r) => r.name).join(",");
  const nowMs = deps.now().getTime();
  try {
    const c = Cache.parse(JSON.parse(fs.readFileSync(file, "utf8")));
    const age = nowMs - Date.parse(c.at);
    if (c.key === key && age >= 0 && age < GH_TTL_MS) return { prs: c.prs, errors: c.errors, cached: true };
  } catch {
    // no usable cache
  }
  const prs: PrView[] = [];
  const errors: string[] = [];
  let me: string | null = null;
  try {
    me = (await ghJson(run, ["gh", "api", "user"], repos[0].path, z.object({ login: z.string() }))).login;
  } catch (e) {
    errors.push(messageOf(e));
  }
  if (me !== null) {
    for (const r of repos) {
      try {
        const slug = await ghRepoOf(deps.git, r.path);
        const rows = await ghJson(run, ["gh", "pr", "list", "--repo", slug, "--state", "open", "--json", FIELDS, "--limit", "50"], r.path, z.array(PrRow));
        for (const p of rows) {
          if (!/^https:\/\/github\.com\/[^\s]+$/.test(p.url)) continue;
          const reviewDecision = p.reviewDecision ?? null;
          const mergeState = p.mergeStateStatus ?? "UNKNOWN";
          prs.push({
            repo: r.name, number: p.number, title: cleanLine(p.title, MAX_TITLE, scrubber), url: p.url, draft: p.isDraft, reviewDecision, mergeState,
            author: p.author.login, requestedMe: (p.reviewRequests ?? []).some((q) => q.login === me),
            readyToMerge: p.author.login === me && !p.isDraft && reviewDecision === "APPROVED" && mergeState === "CLEAN", createdAt: p.createdAt,
          });
        }
      } catch (e) {
        errors.push(`${r.name}: ${messageOf(e)}`);
      }
    }
  }
  fs.mkdirSync(fleetDir(deps), { recursive: true, mode: 0o700 });
  fs.writeFileSync(file, JSON.stringify({ at: deps.now().toISOString(), key, prs, errors }), { mode: 0o600 });
  return { prs, errors, cached: false };
}
```

`ghJson` names the first three argv words in its failure message (`gh api user failed: …`), which is what the test matches.

`sindri/src/fleet/bridge.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import Database from "better-sqlite3";

import type { Deps } from "../deps.js";
import { ringZeroRepo } from "../evolve/ctx.js";
import type { LoadedProfile } from "../profile/load.js";

// Spec decision 7: the toolkit's bridge keeps messages in SQLite. GET /messages/unread marks them read,
// so the collector counts rows on a query-only connection instead.
export function bridgeDbPath(deps: Deps, loaded: LoadedProfile | null): string | null {
  if (deps.env.AW_BRIDGE_DB !== undefined) return deps.env.AW_BRIDGE_DB;
  return loaded === null ? null : path.join(ringZeroRepo(loaded), "mcp-bridge", "bridge.db");
}

export function unreadFor(file: string, recipient: string): { unread: number; since: string | null } | null {
  if (!fs.existsSync(file)) return null;
  const db = new Database(file, { fileMustExist: true });
  try {
    db.pragma("query_only = ON");
    const r = db.prepare("SELECT COUNT(*) AS c, MIN(created_at) AS since FROM messages WHERE recipient = ? AND read_at IS NULL").get(recipient) as { c: number; since: string | null };
    // SQLite's datetime('now') is "YYYY-MM-DD HH:MM:SS" in UTC.
    return { unread: r.c, since: r.since === null ? null : `${r.since.replace(" ", "T")}Z` };
  } finally {
    db.close();
  }
}
```

`sindri/src/fleet/needs.ts`:

```ts
import type { Decision, Health } from "./decisions.js";
import type { JobView } from "./jobs.js";
import type { RepoView } from "./adapters/sindri.js";
import type { StatedSession } from "./state.js";
import { stateRank, STATES, type FleetState, type PrView, type Provider } from "./types.js";

export type NeedKind = "session" | "decision" | "failure" | "pr" | "bridge";
export type NeedAction =
  | { type: "attach"; provider: Provider; id: string }
  | { type: "terminal"; command: string }
  | { type: "run"; action: "fleet.ack"; args: [string]; label: string }
  | { type: "reject"; id: string }
  | { type: "open"; url: string };
export interface NeedItem {
  key: string;
  kind: NeedKind;
  state: string;
  title: string;
  reason: string;
  repo: string | null;
  since: string;
  link: string;
  action: NeedAction;
  secondary?: NeedAction;
  urgent: boolean; // approval and input pulse (spec §5.1)
  ackable: boolean;
}
export interface NeedInputs {
  sessions: readonly StatedSession[];
  jobs: readonly JobView[];
  repos: readonly RepoView[];
  decisions: readonly Decision[];
  health: Health;
  prs: readonly PrView[];
  bridge: { recipient: string; unread: number; since: string | null } | null;
  acks: Readonly<Record<string, string>>;
}

const WAITING = new Set<FleetState>(["approval", "input", "plan ready", "auth"]);
const FAILING = new Set<FleetState>(["failed", "limited", "stuck", "crashed"]);
const CATEGORY: Record<NeedKind, number> = { session: 0, decision: 1, failure: 2, pr: 3, bridge: 4 };
const rankOf = (state: string): number => ((STATES as readonly string[]).includes(state) ? stateRank(state as FleetState) : STATES.length);
const ack = (key: string): NeedAction => ({ type: "run", action: "fleet.ack", args: [key], label: "Acknowledge" });

export function doneUnread(s: StatedSession, acks: Readonly<Record<string, string>>): boolean {
  if (s.state !== "ready") return false;
  const seen = acks[`done:${s.provider}:${s.id}`];
  return seen === undefined || Date.parse(seen) < Date.parse(s.updatedAt);
}

// Spec §4.4, in spec decision 16's order: category, then state priority, then oldest first.
export function needsYou(i: NeedInputs): NeedItem[] {
  const items: NeedItem[] = [];
  for (const s of i.sessions) {
    const attach: NeedAction = { type: "attach", provider: s.provider, id: s.id };
    const link = `/agent/${s.provider}/${s.id}`;
    if (WAITING.has(s.state)) {
      items.push({ key: `session:${s.provider}:${s.id}`, kind: "session", state: s.state, title: s.title, reason: s.reason, repo: s.repo, since: s.signals.spool?.pending?.ts ?? s.updatedAt, link, action: attach, urgent: s.state === "approval" || s.state === "input", ackable: false });
    } else if (FAILING.has(s.state)) {
      const key = `failure:${s.provider}:${s.id}:${s.state}:${s.turnStartedAt ?? s.startedAt}`;
      items.push({ key, kind: "failure", state: s.state, title: s.title, reason: s.reason, repo: s.repo, since: s.updatedAt, link, action: attach, secondary: ack(key), urgent: false, ackable: true });
    }
  }
  for (const d of i.decisions) {
    items.push({
      key: `${d.kind}:${d.id}`, kind: "decision", state: d.kind === "proposal-won" ? "won" : d.kind === "profile-approval" ? "approval pending" : d.kind === "proposals-staged" ? "staged" : "soaked",
      title: d.title, reason: `run at a terminal: ${d.command}`, repo: null, since: d.since, link: "/auto", action: { type: "terminal", command: d.command },
      ...(d.kind === "proposal-won" ? { secondary: { type: "reject" as const, id: d.id } } : {}), urgent: false, ackable: false,
    });
  }
  for (const j of i.jobs) {
    const failed = j.state === "failed" || j.state === "crashed" || (j.kind === "evolve-weekly" && j.state === "attn");
    if (!failed) continue;
    const key = `job:${j.kind}:${j.id}`;
    const doctor = j.kind === "doctor";
    items.push({
      key, kind: "failure", state: j.state, title: doctor ? "sindri doctor reports a failing check" : `${j.kind} ${j.state === "attn" ? "needs attention" : j.state}`,
      reason: j.summary === "" ? `exit ${j.exitCode ?? "none"}` : j.summary, repo: j.repo, since: j.endedAt ?? j.startedAt, link: `/job/${j.id}`,
      action: doctor ? { type: "terminal", command: "sindri doctor" } : ack(key), urgent: false, ackable: true,
    });
  }
  for (const r of i.repos) {
    if (!r.index.stale) continue;
    const key = `index-stale:${r.name}:${r.index.builtAt ?? "never"}`;
    items.push({ key, kind: "failure", state: "stale", title: `the index for ${r.name} is ${r.index.builtAt === null ? "missing" : "stale"}`, reason: r.index.builtAt === null ? "never built" : `built ${r.index.builtAt}`, repo: r.name, since: r.index.builtAt ?? "1970-01-01T00:00:00.000Z", link: `/repo/${r.name}`, action: { type: "terminal", command: `sindri index build --repo ${r.name}` }, secondary: ack(key), urgent: false, ackable: true });
  }
  if (i.health.judge?.status === "degraded") {
    const key = `judge:${i.health.judge.at.slice(0, 10)}`;
    items.push({ key, kind: "failure", state: "degraded", title: "the judge is unhealthy", reason: `${i.health.judge.failures24h ?? "some"} failures in the last 24 h`, repo: null, since: i.health.judge.at, link: "/today", action: { type: "terminal", command: "judge health" }, secondary: ack(key), urgent: false, ackable: true });
  }
  for (const p of i.prs) {
    if (!p.requestedMe && !p.readyToMerge) continue;
    items.push({ key: `pr:${p.repo}#${p.number}`, kind: "pr", state: p.requestedMe ? "review requested" : "ready to merge", title: p.title, reason: `#${p.number} by ${p.author}`, repo: p.repo, since: p.createdAt, link: `/repo/${p.repo}`, action: { type: "open", url: p.url }, urgent: false, ackable: false });
  }
  if (i.bridge !== null && i.bridge.unread > 0) {
    const b = i.bridge;
    items.push({ key: `bridge:${b.recipient}`, kind: "bridge", state: "unread", title: `${b.unread} unread bridge message${b.unread === 1 ? "" : "s"} for ${b.recipient}`, reason: "reading them marks them read", repo: null, since: b.since ?? "1970-01-01T00:00:00.000Z", link: "/today", action: { type: "terminal", command: `curl -s "http://127.0.0.1:3100/messages/unread?recipient=${b.recipient}"` }, urgent: false, ackable: false });
  }
  return items
    .filter((it) => !it.ackable || i.acks[it.key] === undefined)
    .sort((a, b) => CATEGORY[a.kind] - CATEGORY[b.kind] || rankOf(a.state) - rankOf(b.state) || a.since.localeCompare(b.since));
}
```

Traces for the first `needsYou` test:
- **Sessions:** approval (rank 0) comes before input (1) and plan ready (2). The working session isn't listed.
- **The won decision:** one item, with the terminal command first and reject second.
- **Failures:** they sort by state rank, then by `since`:
  1. stuck (rank 7);
  2. the two `failed` jobs (rank 9). The index build and doctor share `since` (both `endedAt` = `at(400)`), so the stable sort keeps insertion order: the index build, then doctor;
  3. stale, attn and degraded, which aren't spec states (rank 13), ordered by `since`: the never-built index (1970), the weekly job (`at(400)`), then the judge (now).
- **PR, then bridge.**

`recipient` in the bridge command is the profile's `user`, which the schema limits to `[a-z0-9-]`, so it's safe inside the quoted URL.

- [ ] **Step 4: Run the tests**

Run: `cd sindri && npx vitest run tests/fleet-needs.test.ts tests/fleet-decisions.test.ts tests/fleet-gh.test.ts tests/fleet-bridge.test.ts`
Expected: PASS. Then, once for the task: `npm test && npm run typecheck && npm run test:coverage`. Expected: all PASS, coverage 100%.

- [ ] **Step 5: Commit**

```bash
git add sindri/src/fleet sindri/src/evolve/cmd/channel.ts sindri/tests
git commit -m "feat: sindri fleet needs-you aggregator with decisions, gh PRs, bridge unread and acks" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 8: The `sindri fleet` command and the collector

**Files:**
- Create: `sindri/src/fleet/place.ts`, `sindri/src/fleet/usage.ts`, `sindri/src/fleet/today.ts`, `sindri/src/fleet/sentences.ts`, `sindri/src/fleet/collect.ts`, `sindri/src/fleet/render.ts`, `sindri/src/fleet/command.ts`
- Modify:
  - `sindri/src/fleet/proc-real.ts` (add `realFleetIo()`);
  - `sindri/src/main.ts` (register `fleet`);
  - `sindri/src/errors.ts` (`SND-FLEET-001`, `-002`, `-003`).
- Generated: `docs/sindri/errors.md` (`npm run gen`)
- Test: `sindri/tests/fleet-collect.test.ts`, `sindri/tests/fleet-usage.test.ts`, `sindri/tests/fleet-command.test.ts`

**Interfaces:**
- Consumes: everything from Tasks 1–7; `openLedgerReadOnly`, `ledgerPath`, `stateDir`, `approvedProfile`, `FleetSchema`, `makeScrubber`, `compileExtraPatterns`, `readChannels`, `parseFlags`, `success`, `fromError`, `SindriError`, `realProcessRunner`, `GitRunner`.
- Produces:
  - Error codes:
    - `SND-FLEET-001` "A fleet source could not be read; the other sources still show.";
    - `SND-FLEET-002` "A fleet source ran past its time or size budget, so it shows only its newest sessions.";
    - `SND-FLEET-003` "Nothing that can be acknowledged has that key."
  - `place.ts`: `placeSessions(git, sessions, loaded)`. One `git rev-parse --show-toplevel --git-common-dir` per distinct cwd (at most 300) fills `repo` (the profile name) and `worktree` (the git top-level when it isn't the repo's main checkout, or the top-level for repos outside the profile).
  - `usage.ts`: `scorerBin(deps)` and `usageFor(deps, run, sessions): Promise<Map<id, tokens>>` (spec decision 6: cached 60 s in `fleet/usage-cache.json`, at most 20 refreshes per pass, oldest first).
  - `today.ts`: `TodayView` and `todayOf(sessions, now, budgetPerDay, sources, health)`.
  - `sentences.ts`: `EMPTY`, the empty-state sentences that the CLI and the UI share word for word (spec §5 "Empty").
  - `collect.ts`:
    - `FleetSession = StatedSession & { doneUnread; turnAgeMs }`;
    - `SourceStatus { name; on; ok; ms; count; truncated; error: { code; message } | null }`;
    - `Pipeline { proposals: Record<status, count>; channel: { stable: string | null; next: { sha; installedAt; soakedDays } | null } }`;
    - `FleetReport` (`v: 1`), holding `generatedAt`, `profile`, `counts`, `needs`, `sessions`, `jobs`, `history`, `heavyLock`, `repos`, `scopeRuns`, `decisions`, `pipeline`, `health`, `sources` and `today`;
    - `runSource(io, name, on, fn, scrubber)`;
    - `collectFleet(deps, io): Promise<FleetReport>`, which never throws for a source's sake.
  - `render.ts`: `renderFleet(report): string` (state words first, no colour, ending in a `Next:` line).
  - `command.ts`: `makeFleetCommand(io): Command`, covering:
    - `sindri fleet [--provider P] [--state S] [--json]` (exit 1 when needs isn't empty; `--state plan-ready` is accepted for `plan ready`);
    - `sindri fleet ack <key> [--json]`;
    - `sindri fleet hook <provider>`, which prints nothing and always exits 0.
  - `proc-real.ts`: `realFleetIo(): FleetIo`, with the v1 `CodexStatusProbe` returning `null`.

- [ ] **Step 1: Write the failing tests**

`sindri/tests/fleet-usage.test.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { scorerBin, usageFor } from "../src/fleet/usage.js";
import { fakeProc } from "./evolve-fixtures.js";
import { makeDeps } from "./helpers.js";

const SNAP = JSON.stringify({ v: 1, sessionId: "x", usage: { calls: 3, subagentCalls: 0, contextTokens: 1200, outputTokens: 34, callsOver200k: 0 } });

describe("usageFor", () => {
  it("asks scorer live per session, caches 60 s, refreshes at most 20 per pass and skips odd ids", async () => {
    const deps = makeDeps();
    const proc = fakeProc(() => ({ stdout: SNAP }));
    const ids = Array.from({ length: 25 }, (_, i) => ({ id: `s-${i}`, cwd: "/Users/dev/acme.web" }));
    const first = await usageFor(deps, proc, [...ids, { id: "bad id;rm", cwd: "/x" }]);
    expect(proc.calls).toHaveLength(20);
    expect(proc.calls[0]).toEqual({ argv: ["scorer", "live", "--session", "s-0", "--cwd", "/Users/dev/acme.web", "--json"], cwd: deps.home });
    expect(first.get("s-0")).toBe(1234);
    expect(first.size).toBe(20);
    const second = await usageFor(deps, proc, ids);
    expect(proc.calls).toHaveLength(25); // only the 5 not yet cached
    expect(second.size).toBe(25);
    await usageFor({ ...deps, now: () => new Date("2026-10-08T12:01:01.000Z") }, proc, ids.slice(0, 2));
    expect(proc.calls).toHaveLength(27);
  });

  it("keeps going past a failing or junk answer, and omits --cwd when there is none", async () => {
    const deps = makeDeps();
    const proc = fakeProc((argv) => (argv[3] === "a" ? { code: 1 } : argv[3] === "b" ? { stdout: "junk" } : { stdout: SNAP }));
    const r = await usageFor(deps, proc, [{ id: "a", cwd: "/x" }, { id: "b", cwd: "/x" }, { id: "c", cwd: "" }]);
    expect([...r.keys()]).toEqual(["c"]);
    expect(proc.calls[2].argv).toEqual(["scorer", "live", "--session", "c", "--json"]);
  });

  it("finds scorer in AW_SCORER_BIN, then ~/.local/bin, then PATH", () => {
    expect(scorerBin(makeDeps({ env: { AW_SCORER_BIN: "/opt/scorer" } }))).toBe("/opt/scorer");
    const deps = makeDeps();
    expect(scorerBin(deps)).toBe("scorer");
    fs.mkdirSync(path.join(deps.home, ".local", "bin"), { recursive: true });
    fs.writeFileSync(path.join(deps.home, ".local", "bin", "scorer"), "#!/bin/sh\n");
    expect(scorerBin(deps)).toBe(path.join(deps.home, ".local", "bin", "scorer"));
  });
});
```

`sindri/tests/fleet-collect.test.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { collectFleet, type FleetReport } from "../src/fleet/collect.js";
import { appendSpool } from "../src/fleet/spool.js";
import { finishJob, startJob } from "../src/fleet/jobs.js";
import { at, claudeStatusFile, claudeTranscript, codexRollout, CWD, cursorChat, CX, fakeProcTable, fleetIo, L, NOW, SID, SID2, writeFile, claudeSlug } from "./fleet-fixtures.js";
import { evolveFixture, git } from "./evolve-fixtures.js";
import { makeDeps } from "./helpers.js";

const status = (r: FleetReport, name: string) => r.sources.find((s) => s.name === name);

describe("collectFleet", () => {
  it("puts every provider, the spool and sindri's jobs into one report, approvals first", async () => {
    const deps = makeDeps();
    claudeStatusFile(deps.home, { pid: 4242, status: "busy" });
    claudeTranscript(deps.home, SID, [L.user("ship it", at(600)), L.toolUse("Bash", { command: "npm publish" }, "t1", at(500))]);
    appendSpool(deps, { v: 1, provider: "claude", session: SID, event: "permission", ts: at(490), detail: "Bash: npm publish" });
    codexRollout(deps.home, SID2, [CX.meta(SID2, CWD, at(300)), CX.taskStarted(at(299)), CX.taskComplete(at(200))]);
    cursorChat(deps.home, "33333333-3333-4333-8333-333333333333");
    finishJob(deps, startJob(deps, "observe", []), { exitCode: 0, stdout: "ok", stderr: "" });
    const r = await collectFleet(deps, fleetIo());
    expect(r).toMatchObject({ v: 1, profile: "none", generatedAt: NOW.toISOString() });
    expect(r.sessions.map((s) => [s.provider, s.state])).toEqual(expect.arrayContaining([["claude", "approval"], ["codex", "ended"], ["cursor", "ended"]]));
    expect(r.needs[0]).toMatchObject({ key: `session:claude:${SID}`, state: "approval", reason: "Bash: npm publish", urgent: true });
    expect(r.counts).toMatchObject({ live: 1, needsYou: 1, urgent: 1 });
    expect(r.jobs.map((j) => j.kind)).toEqual(["observe"]);
    expect(r.sources.map((s) => [s.name, s.on, s.ok])).toEqual([
      ["processes", true, true], ["spool", true, true], ["claude", true, true], ["codex", true, true], ["cursor", true, true],
      ["sindri", true, true], ["usage", true, true], ["gh", false, true], ["bridge", false, true],
    ]);
    expect(r.today).toMatchObject({ agents: 3, turns: 1 });
  });

  it("keeps a failing source to its own entry (SND-FLEET-001) and still shows the others (Review Focus 3)", async () => {
    const deps = makeDeps();
    claudeTranscript(deps.home, SID, [L.user("hello", at(60))]);
    writeFile(path.join(deps.home, ".claude", "sessions", "77.json"), "{corrupt");
    codexRollout(deps.home, SID2, [CX.meta(SID2, CWD, at(30))]);
    const broken = fakeProcTable();
    broken.holders = () => { throw new Error("lsof exploded"); };
    const r = await collectFleet(deps, fleetIo({ proc: broken }));
    expect(status(r, "codex")).toMatchObject({ ok: false, error: { code: "SND-FLEET-001", message: "codex: lsof exploded" } });
    expect(r.sessions.map((s) => s.id)).toEqual([SID]);
    const noPs = fakeProcTable();
    noPs.list = () => { throw new Error("ps missing"); };
    expect(status(await collectFleet(deps, fleetIo({ proc: noPs })), "processes")).toMatchObject({ ok: false });
  });

  it("caps 900 transcripts in 60 dirs and stops at the time budget with SND-FLEET-002 (Review Focus 3)", async () => {
    const deps = makeDeps();
    for (let i = 0; i < 900; i++) {
      const id = `00000000-0000-4000-8000-${String(i).padStart(12, "0")}`;
      const file = path.join(deps.home, ".claude", "projects", claudeSlug(`/Users/dev/p${i % 60}`), `${id}.jsonl`);
      writeFile(file, `${JSON.stringify(L.user(`t${i}`, at(100 + i)))}\n`, new Date(NOW.getTime() - (100 + i) * 1000));
    }
    let clock = 0;
    const r = await collectFleet(deps, fleetIo({ monotonic: () => (clock += 15) }));
    const claude = status(r, "claude");
    expect(claude).toMatchObject({ ok: true, truncated: true, error: { code: "SND-FLEET-002" } });
    expect(r.sessions.length).toBeLessThanOrEqual(200);
    expect(r.sessions.length).toBeGreaterThan(50);
    expect(r.sessions[0].title).toBe("t0");
  });

  it("treats a missing provider dir as an empty source and honours fleet.sources switches", async () => {
    const fx = await evolveFixture({ extraYaml: "fleet:\n  sources:\n    codex: false\n    gh: false\n    bridge: false\n" });
    const r = await collectFleet(fx.deps, fleetIo());
    expect(r.profile).toBe("approved");
    expect(status(r, "codex")).toEqual({ name: "codex", on: false, ok: true, ms: 0, count: 0, truncated: false, error: null });
    expect(status(r, "cursor")).toMatchObject({ on: true, ok: true, count: 0, error: null });
    fx.close();
  });

  it("places sessions in their onboarded repo and worktree, others under no repo", async () => {
    const fx = await evolveFixture({ extraYaml: "fleet:\n  sources:\n    gh: false\n    bridge: false\n" });
    const wt = path.join(path.dirname(fx.repo), `${path.basename(fx.repo)}-wt`);
    git(fx.repo, "worktree", "add", "-q", wt, "-b", "feature");
    claudeTranscript(fx.deps.home, SID, [L.user("main", at(60), fx.repo)], { cwd: fx.repo });
    claudeTranscript(fx.deps.home, SID2, [L.user("in a worktree", at(50), wt)], { cwd: wt });
    claudeTranscript(fx.deps.home, "33333333-3333-4333-8333-333333333333", [L.user("elsewhere", at(40), "/nonexistent/place")], { cwd: "/nonexistent/place" });
    const r = await collectFleet(fx.deps, fleetIo());
    const by = Object.fromEntries(r.sessions.map((s) => [s.title, [s.repo, s.worktree === null ? null : path.basename(s.worktree)]]));
    const name = fx.ctx.loaded.profile.tracker.repo;
    expect(by).toEqual({ main: [name, null], "in a worktree": [name, path.basename(wt)], elsewhere: [null, null] });
    expect(r.repos.find((x) => x.name === name)?.worktrees).toEqual([]); // none of them is alive
    fx.close();
  });
});
```

`sindri/tests/fleet-command.test.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { makeFleetCommand } from "../src/fleet/command.js";
import { readAcks } from "../src/fleet/acks.js";
import { EMPTY } from "../src/fleet/sentences.js";
import { spoolFile } from "../src/fleet/spool.js";
import { COMMANDS } from "../src/main.js";
import { at, claudeStatusFile, claudeTranscript, fleetIo, L, SID } from "./fleet-fixtures.js";
import { makeDeps } from "./helpers.js";

const fleet = makeFleetCommand(fleetIo());

function waiting() {
  const deps = makeDeps();
  claudeStatusFile(deps.home, { pid: 4242, status: "waiting", name: "Fix the login form" });
  claudeTranscript(deps.home, SID, [L.user("go", at(300)), L.toolUse("Bash", { command: "npm publish" }, "t1", at(200))]);
  return deps;
}

describe("sindri fleet", () => {
  it("prints state words first and exits 1 when something needs you", async () => {
    const r = await fleet([], waiting());
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toMatch(/^NEEDS YOU \(1\)\n {2}approval {2}claude {2}\S+ {2}Fix the login form {2}waiting \d+ s {2}the session says it is waiting on you\n/);
    expect(r.stdout).toContain("AGENTS (1 live, 0 working)");
    expect(r.stdout).toMatch(/\nNext: sindri dashboard\n$/);
    expect(r.stdout).not.toMatch(/\u001b\[/); // no colour
  });

  it("says the empty sentences and exits 0 when nothing needs you", async () => {
    const r = await fleet([], makeDeps());
    expect(r.exitCode).toBe(0);
    expect(r.stdout).toContain(EMPTY.needs(0, 0));
    expect(r.stdout).toContain(EMPTY.agents);
    expect(r.stdout).toContain(EMPTY.jobs);
  });

  it("returns the whole report with --json and filters by provider and state", async () => {
    const deps = waiting();
    const all = JSON.parse((await fleet(["--json"], deps)).stdout) as { v: number; sessions: { state: string }[]; needs: unknown[] };
    expect(all.v).toBe(1);
    expect(all.sessions).toHaveLength(1);
    expect(JSON.parse((await fleet(["--json", "--provider", "codex"], deps)).stdout).sessions).toEqual([]);
    expect(JSON.parse((await fleet(["--json", "--state", "approval"], deps)).stdout).sessions).toHaveLength(1);
    expect(JSON.parse((await fleet(["--json", "--state", "plan-ready"], deps)).stdout).sessions).toEqual([]);
    const bad = await fleet(["--provider", "slack"], deps);
    expect(bad.exitCode).toBe(2);
    expect(bad.stderr).toContain("SND-CLI-002");
    expect((await fleet(["--state", "napping", "--json"], deps)).stdout).toContain('"SND-CLI-002"');
  });

  it("hook: records the event, prints nothing and exits 0, even for junk", async () => {
    const deps = makeDeps({ stdin: async () => JSON.stringify({ session_id: SID, hook_event_name: "Stop" }) });
    expect(await fleet(["hook", "claude"], deps)).toEqual({ exitCode: 0, stdout: "", stderr: "" });
    expect(fs.readFileSync(spoolFile(deps, "claude"), "utf8")).toContain('"event":"stop"');
    expect(await fleet(["hook"], deps)).toEqual({ exitCode: 0, stdout: "", stderr: "" });
  });

  it("ack: acknowledges a current failure or a done marker, and refuses anything else", async () => {
    const deps = makeDeps();
    claudeTranscript(deps.home, SID, [L.user("go", at(900)), L.toolUse("Bash", {}, "t1", at(800))]); // dead mid-tool-call: crashed
    const report = JSON.parse((await fleet(["--json"], deps)).stdout) as { needs: { key: string; ackable: boolean }[] };
    const key = report.needs.find((n) => n.ackable)?.key as string;
    expect(key).toMatch(/^failure:claude:/);
    const r = await fleet(["ack", key], deps);
    expect(r.stdout).toBe(`Acknowledged ${key}.\nNext: sindri fleet\n`);
    expect(Object.keys(readAcks(deps))).toEqual([key]);
    expect(JSON.parse((await fleet(["--json"], deps)).stdout).needs).toEqual([]);
    expect((await fleet(["ack", `done:claude:${SID}`], deps)).exitCode).toBe(0);
    const unknown = await fleet(["ack", "job:observe:nope", "--json"], deps);
    expect(unknown.exitCode).toBe(1);
    expect(unknown.stdout).toContain("SND-FLEET-003");
    expect((await fleet(["ack", "../../etc"], deps)).stderr).toContain("SND-CLI-002");
    expect((await fleet(["ack"], deps)).stderr).toContain("SND-CLI-002");
    expect((await fleet(["ack", "done:claude:not-here"], deps)).stderr).toContain("SND-FLEET-003");
  });

  it("is registered, with usage naming all three forms", () => {
    expect(COMMANDS.fleet.usage).toContain("sindri fleet ack <key>");
    expect(COMMANDS.fleet.usage).toContain("sindri fleet hook <provider>");
    expect(path.basename(spoolFile(makeDeps(), "codex"))).toBe("codex.jsonl");
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd sindri && npx vitest run tests/fleet-usage.test.ts tests/fleet-collect.test.ts tests/fleet-command.test.ts`
Expected: FAIL with `Failed to load url ../src/fleet/usage.js` (and likewise for `collect.js`, `command.js`).

- [ ] **Step 3: Implement**

`sindri/src/errors.ts`: add after the `SND-EVOLVE-*` block:

```ts
  "SND-FLEET-001": { summary: "A fleet source could not be read; the other sources still show.", fix: "sindri fleet --json shows the source's error under sources; sindri doctor checks the paths" },
  "SND-FLEET-002": { summary: "A fleet source ran past its time or size budget, so it shows only its newest sessions.", fix: "close or archive old sessions, or turn the source off with fleet.sources.<name>: false, then sindri profile approve" },
  "SND-FLEET-003": { summary: "Nothing that can be acknowledged has that key.", fix: "sindri fleet --json lists the current keys (needs[].key where ackable is true, or done:<provider>:<id>)" },
```

Then `cd sindri && npm run gen` (rewrites `docs/sindri/errors.md`).

`sindri/src/fleet/sentences.ts`:

```ts
// Spec §5 "Empty: the same sentence the CLI prints". ui/src/model.ts keeps a copy; tests/ui-model.test.ts
// checks the two agree word for word.
export const EMPTY = {
  needs: (working: number, live: number): string => `Nothing needs you. ${working} working, ${live} live.`,
  agents: "No agents are running or were active in the last 24 hours.",
  jobs: "No sindri jobs have run yet.",
  repos: "No onboarded repos. Onboard one with: sindri repo onboard <path>",
} as const;
```

`sindri/src/fleet/usage.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import { z } from "zod";

import type { Deps } from "../deps.js";
import type { ProcessRunner } from "../index/io.js";
import { MAX_USAGE_CALLS, USAGE_TTL_MS } from "./limits.js";
import { fleetDir } from "./paths.js";

const Snapshot = z.object({ usage: z.object({ contextTokens: z.number().nonnegative(), outputTokens: z.number().nonnegative() }) });
const Cache = z.record(z.object({ tokens: z.number(), at: z.string() }));
const ID = /^[A-Za-z0-9-]{1,64}$/; // scorer live's own rule: the id becomes part of a file name

export function scorerBin(deps: Deps): string {
  if (deps.env.AW_SCORER_BIN !== undefined) return deps.env.AW_SCORER_BIN;
  const local = path.join(deps.home, ".local", "bin", "scorer");
  return fs.existsSync(local) ? local : "scorer";
}

// Spec decision 6: tokens per live session from `scorer live --json`, cached 60 s, at most 20 refreshes per
// pass (the oldest cache entries first), so hundreds of sessions never mean hundreds of child processes.
export async function usageFor(deps: Deps, run: ProcessRunner, sessions: readonly { id: string; cwd: string }[]): Promise<Map<string, number>> {
  const file = path.join(fleetDir(deps), "usage-cache.json");
  let cache: Record<string, { tokens: number; at: string }> = {};
  try {
    cache = Cache.parse(JSON.parse(fs.readFileSync(file, "utf8")));
  } catch {
    cache = {};
  }
  const nowMs = deps.now().getTime();
  const fresh = (id: string): boolean => {
    const c = cache[id];
    const age = c === undefined ? Infinity : nowMs - Date.parse(c.at);
    return age >= 0 && age < USAGE_TTL_MS;
  };
  const asked = sessions.filter((s) => ID.test(s.id));
  const due = asked.filter((s) => !fresh(s.id)).sort((a, b) => (cache[a.id]?.at ?? "").localeCompare(cache[b.id]?.at ?? "")).slice(0, MAX_USAGE_CALLS);
  for (const s of due) {
    const argv = [scorerBin(deps), "live", "--session", s.id, ...(s.cwd === "" ? [] : ["--cwd", s.cwd]), "--json"];
    const r = await run.run(argv, { cwd: deps.home, timeoutMs: 5000 });
    if (r.code !== 0) continue;
    try {
      const snap = Snapshot.parse(JSON.parse(r.stdout));
      cache[s.id] = { tokens: snap.usage.contextTokens + snap.usage.outputTokens, at: deps.now().toISOString() };
    } catch {
      // a reply that isn't a snapshot: keep the last value
    }
  }
  const kept = Object.fromEntries(asked.flatMap((s) => (cache[s.id] === undefined ? [] : [[s.id, cache[s.id]] as const])));
  fs.mkdirSync(fleetDir(deps), { recursive: true, mode: 0o700 });
  fs.writeFileSync(file, JSON.stringify(kept), { mode: 0o600 });
  return new Map(Object.entries(kept).map(([id, v]) => [id, v.tokens]));
}
```

`sindri/src/fleet/place.ts`:

```ts
import fs from "node:fs";
import path from "node:path";

import type { GitRunner } from "../git.js";
import type { LoadedProfile } from "../profile/load.js";
import { isUnder } from "./paths.js";
import type { Session } from "./types.js";

const real = (p: string): string => {
  try {
    return fs.realpathSync(p);
  } catch {
    return path.resolve(p);
  }
};

// Spec §5 "Agents: grouped by repo, then worktree" and spec decision 17.
export async function placeSessions(git: GitRunner, sessions: Session[], loaded: LoadedProfile | null): Promise<void> {
  const repos = loaded === null ? [] : loaded.profile.repos.map((name) => ({ name, path: real(loaded.repos[name].path) }));
  const byPath = (p: string): string | null => repos.find((r) => r.path === real(p))?.name ?? null;
  const placed = new Map<string, { repo: string | null; worktree: string | null }>();
  for (const cwd of [...new Set(sessions.map((s) => s.cwd))].slice(0, 300)) {
    const under = repos.find((r) => cwd !== "" && isUnder(real(cwd), r.path))?.name ?? null;
    const r = cwd !== "" && fs.existsSync(cwd) ? await git.run(["rev-parse", "--show-toplevel", "--git-common-dir"], cwd) : null;
    if (r === null || !r.ok) {
      placed.set(cwd, { repo: under, worktree: null });
      continue;
    }
    const [top, common] = r.stdout.trim().split("\n");
    const commonAbs = path.resolve(cwd, common);
    const main = path.basename(commonAbs) === ".git" ? path.dirname(commonAbs) : commonAbs;
    const repo = byPath(main) ?? byPath(top) ?? under;
    const mainCheckout = repo !== null && real(top) === repos.find((x) => x.name === repo)?.path;
    placed.set(cwd, { repo, worktree: mainCheckout ? null : top });
  }
  for (const s of sessions) {
    const p = placed.get(s.cwd);
    if (p !== undefined) {
      s.repo = p.repo;
      s.worktree = p.worktree;
    }
  }
}
```

In the placement test, `/nonexistent/place` doesn't exist, so it gets no git call, `repo: null` and `worktree: null`. The main checkout's top-level equals the repo path, so `worktree: null`. The linked worktree's common dir is `<repo>/.git`, so its repo is the profile repo and its worktree is the worktree's path.

`sindri/src/fleet/today.ts`:

```ts
import type { Health } from "./decisions.js";
import type { JobState } from "./jobs.js";
import type { StatedSession } from "./state.js";

export interface TodayView {
  since: string;
  agents: number;
  turns: number;
  tokens: number;
  costUsd: number;
  budgetTokens: number | null;
  warnings: string[];
  judge: Health["judge"];
  doctor: { state: JobState | null; at: string | null };
}

// Spec §5 view 5. "Today" is since local midnight; tokens and cost are per-session figures summed over the
// sessions active today (spec decision 6), so a long session counts in full.
export function todayOf(
  sessions: readonly StatedSession[], now: Date, budgetTokens: number | null,
  sources: readonly { name: string; error: { message: string } | null }[], health: Health,
): TodayView {
  const midnight = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const all = sessions.flatMap((s) => [s, ...s.children]);
  const active = all.filter((s) => Date.parse(s.updatedAt) >= midnight.getTime());
  return {
    since: midnight.toISOString(),
    agents: active.length,
    turns: active.reduce((n, s) => n + s.promptTimes.filter((t) => Date.parse(t) >= midnight.getTime()).length, 0),
    tokens: active.reduce((n, s) => n + (s.tokens ?? 0), 0),
    costUsd: Math.round(active.reduce((n, s) => n + (s.costUsd ?? 0), 0) * 100) / 100,
    budgetTokens,
    warnings: sources.flatMap((s) => (s.error === null ? [] : [s.error.message])),
    judge: health.judge,
    doctor: { state: health.doctor?.state ?? null, at: health.doctor?.endedAt ?? health.doctor?.startedAt ?? null },
  };
}
```

`sindri/src/fleet/collect.ts`:

```ts
import { stateDir, type Deps } from "../deps.js";
import { readChannels } from "../evolve/channel.js";
import type { ErrorCode } from "../errors.js";
import { ledgerPath, openLedgerReadOnly, type Ledger } from "../ledger/db.js";
import { approvedProfile } from "../profile/approve.js";
import type { LoadedProfile } from "../profile/load.js";
import { FleetSchema } from "../profile/schema.js";
import { compileExtraPatterns, makeScrubber, type Scrubber } from "../scrub/scrub.js";
import { readAcks } from "./acks.js";
import { discoverClaude } from "./adapters/claude.js";
import { discoverCodex } from "./adapters/codex.js";
import { discoverCursor } from "./adapters/cursor.js";
import { discoverSindri, type HeavyLockView, type RepoView, type ScopeRunView } from "./adapters/sindri.js";
import { bridgeDbPath, unreadFor } from "./bridge.js";
import { readDecisions, readHealth, type Decision, type Health } from "./decisions.js";
import { prsFor } from "./gh.js";
import type { JobView } from "./jobs.js";
import { SOURCE_BUDGET_MS } from "./limits.js";
import { doneUnread, needsYou, type NeedItem } from "./needs.js";
import { placeSessions } from "./place.js";
import { readSpool, spoolSince, summarize } from "./spool.js";
import { ageMs, deriveState, rollUp, type StatedSession } from "./state.js";
import { cleanLine } from "./text.js";
import { todayOf, type TodayView } from "./today.js";
import { stateRank, type FleetCtx, type FleetIo, type PrView, type ProcEntry, type Session, type SpoolSummary } from "./types.js";
import { usageFor } from "./usage.js";

export type FleetSession = StatedSession & { doneUnread: boolean; turnAgeMs: number | null };
export interface SourceStatus {
  name: string;
  on: boolean;
  ok: boolean;
  ms: number;
  count: number;
  truncated: boolean;
  error: { code: ErrorCode; message: string } | null;
}
export interface Pipeline {
  proposals: Record<string, number>;
  channel: { stable: string | null; next: { sha: string; installedAt: string; soakedDays: number } | null };
}
export interface FleetReport {
  v: 1;
  generatedAt: string;
  profile: "approved" | "none";
  counts: { live: number; working: number; needsYou: number; urgent: number };
  needs: NeedItem[];
  sessions: FleetSession[];
  jobs: JobView[];
  history: JobView[];
  heavyLock: HeavyLockView;
  repos: RepoView[];
  scopeRuns: ScopeRunView[];
  decisions: Decision[];
  pipeline: Pipeline;
  health: Health;
  sources: SourceStatus[];
  today: TodayView;
}

const messageOf = (e: unknown): string => (e instanceof Error ? e.message : String(e));

// Spec §4.1 "Failures stay contained" and "Bounded": each source gets its own budget clock and its own
// error entry; nothing a source throws reaches another source or the caller.
export async function runSource<T>(
  io: FleetIo, name: string, on: boolean, scrubber: Scrubber,
  fn: (overBudget: () => boolean) => { value: T; count: number; truncated: boolean } | Promise<{ value: T; count: number; truncated: boolean }>,
): Promise<{ status: SourceStatus; value: T | null }> {
  if (!on) return { status: { name, on: false, ok: true, ms: 0, count: 0, truncated: false, error: null }, value: null };
  const t0 = io.monotonic();
  const ms = (): number => Math.max(0, Math.round(io.monotonic() - t0));
  try {
    const r = await fn(() => io.monotonic() - t0 > SOURCE_BUDGET_MS);
    const error = r.truncated ? { code: "SND-FLEET-002" as const, message: `${name}: stopped at the time or size budget; showing the newest sessions only` } : null;
    return { status: { name, on: true, ok: true, ms: ms(), count: r.count, truncated: r.truncated, error }, value: r.value };
  } catch (e) {
    return { status: { name, on: true, ok: false, ms: ms(), count: 0, truncated: false, error: { code: "SND-FLEET-001", message: cleanLine(`${name}: ${messageOf(e)}`, 240, scrubber) } }, value: null };
  }
}

function openDb(deps: Deps): Ledger | null {
  try {
    return openLedgerReadOnly(ledgerPath(stateDir(deps)));
  } catch {
    return null;
  }
}

function pipelineOf(deps: Deps, db: Ledger | null): Pipeline {
  const proposals = db === null ? {} : Object.fromEntries((db.prepare("SELECT status, COUNT(*) AS c FROM proposals GROUP BY status").all() as { status: string; c: number }[]).map((r) => [r.status, r.c]));
  let channel: Pipeline["channel"] = { stable: null, next: null };
  try {
    const c = readChannels(deps);
    channel = {
      stable: c.stable?.sha ?? null,
      next: c.next === null ? null : { sha: c.next.sha, installedAt: c.next.installedAt, soakedDays: Math.max(0, Math.floor((deps.now().getTime() - Date.parse(c.next.installedAt)) / 86_400_000)) },
    };
  } catch {
    // a corrupt channels.json shows as no channel; `sindri channel status` reports it
  }
  return { proposals, channel };
}

const sortKey = (s: FleetSession): string => `${s.repo ?? "~"}\u0000${s.worktree ?? ""}`;

export async function collectFleet(deps: Deps, io: FleetIo): Promise<FleetReport> {
  const now = deps.now();
  const db = openDb(deps);
  try {
    const loaded: LoadedProfile | null = db === null ? null : approvedProfile(deps, db);
    const cfg = loaded?.profile.fleet ?? FleetSchema.parse(undefined);
    const scrubber = makeScrubber(loaded === null ? [] : compileExtraPatterns(loaded.profile.scrub.extraPatterns));
    const sources: SourceStatus[] = [];
    const ctx: FleetCtx = { deps, io, loaded, db, cfg, scrubber, procs: [], spool: new Map<string, SpoolSummary>(), overBudget: () => false };

    const procs = await runSource(io, "processes", true, scrubber, () => {
      const v = io.proc.list();
      return { value: v, count: v.length, truncated: false };
    });
    sources.push(procs.status);
    ctx.procs = procs.value ?? ([] as ProcEntry[]);
    const spool = await runSource(io, "spool", true, scrubber, () => {
      const events = readSpool(deps, spoolSince(now), scrubber);
      return { value: summarize(events), count: events.length, truncated: false };
    });
    sources.push(spool.status);
    ctx.spool = spool.value ?? new Map<string, SpoolSummary>();

    const sessions: Session[] = [];
    for (const [name, discover] of [["claude", discoverClaude], ["codex", discoverCodex], ["cursor", discoverCursor]] as const) {
      const r = await runSource(io, name, cfg.sources[name], scrubber, (overBudget) => {
        const d = discover({ ...ctx, overBudget });
        return { value: d.sessions, count: d.sessions.length, truncated: d.truncated };
      });
      sources.push(r.status);
      sessions.push(...(r.value ?? []));
    }

    const sindri = await runSource(io, "sindri", cfg.sources.sindri, scrubber, async () => {
      const report = await discoverSindri(ctx);
      return { value: { report, decisions: readDecisions(deps, db, scrubber), health: await readHealth(deps, io.run, report.jobs) }, count: report.jobs.length, truncated: false };
    });
    sources.push(sindri.status);

    await placeSessions(deps.git, sessions, loaded);
    const top = rollUp(sessions.map((s) => ({ ...s, ...deriveState(s, now, cfg.stuckMinutes) })));

    const usage = await runSource(io, "usage", cfg.sources.claude, scrubber, async () => {
      const m = await usageFor(deps, io.run, top.filter((s) => s.provider === "claude" && s.alive).map((s) => ({ id: s.id, cwd: s.cwd })));
      return { value: m, count: m.size, truncated: false };
    });
    sources.push(usage.status);
    for (const s of top) if (s.provider === "claude") s.tokens = usage.value?.get(s.id) ?? s.tokens;

    const repos = sindri.value?.report.repos ?? [];
    const gh = await runSource(io, "gh", cfg.sources.gh && repos.length > 0, scrubber, async () => {
      const r = await prsFor(deps, io.run, repos.map((x) => ({ name: x.name, path: x.path })), scrubber);
      if (r.prs.length === 0 && r.errors.length > 0) throw new Error(r.errors[0]);
      return { value: r.prs, count: r.prs.length, truncated: false };
    });
    sources.push(gh.status);
    const prs: PrView[] = gh.value ?? [];
    for (const r of repos) {
      r.prs = prs.filter((p) => p.repo === r.name);
      r.worktrees = [...new Set(top.filter((s) => s.alive && s.repo === r.name && s.worktree !== null).map((s) => s.worktree as string))];
    }

    const bridge = await runSource(io, "bridge", cfg.sources.bridge && loaded !== null, scrubber, () => {
      const recipient = (loaded as LoadedProfile).profile.user;
      const file = bridgeDbPath(deps, loaded);
      const u = file === null ? null : unreadFor(file, recipient);
      return { value: u === null ? null : { recipient, ...u }, count: u?.unread ?? 0, truncated: false };
    });
    sources.push(bridge.status);

    const health: Health = sindri.value?.health ?? { doctor: null, judge: null };
    const decisions = sindri.value?.decisions ?? [];
    const jobs = sindri.value?.report.jobs ?? [];
    const acks = readAcks(deps);
    const needs = needsYou({ sessions: top, jobs, repos, decisions, health, prs, bridge: bridge.value ?? null, acks });
    const out: FleetSession[] = top
      .map((s) => ({ ...s, doneUnread: doneUnread(s, acks), turnAgeMs: ageMs(s.turnStartedAt, now) }))
      .sort((a, b) => sortKey(a).localeCompare(sortKey(b)) || stateRank(a.state) - stateRank(b.state) || b.updatedAt.localeCompare(a.updatedAt));
    const budget = loaded?.profile.budget.perDay ?? null;
    return {
      v: 1, generatedAt: now.toISOString(), profile: loaded === null ? "none" : "approved",
      counts: { live: top.filter((s) => s.alive).length, working: top.filter((s) => s.state === "working").length, needsYou: needs.length, urgent: needs.filter((n) => n.urgent).length },
      needs, sessions: out, jobs, history: sindri.value?.report.history ?? [],
      heavyLock: sindri.value?.report.heavyLock ?? { held: false, holder: null, queue: [] },
      repos, scopeRuns: sindri.value?.report.scopeRuns ?? [], decisions, pipeline: pipelineOf(deps, db), health, sources,
      today: todayOf(top, now, budget, sources, health),
    };
  } finally {
    db?.close();
  }
}
```

Traces:
- **The first collect test:** the claude session's spool permission has no newer output, so it is `approval`; the waiting row comes first. Codex: no holder, so it's dead with a completed turn: `ended`. Cursor has no live agent and no transcript, so its turn is closed and it's `ended`. There's no profile, so `gh` and `bridge` are `on: false` (gh needs repos, bridge needs a profile). Today counts three agents and one prompt.
- **The 900-transcript test:** each `monotonic()` call adds 15 ms. The Claude source checks `overBudget` once per transcript, so it stops after about 130 transcripts (2000 / 15); it is `truncated` with `SND-FLEET-002`, and the newest transcript (`t0`) comes first.

`sindri/src/fleet/render.ts`:

```ts
import type { FleetReport, FleetSession } from "./collect.js";
import { EMPTY } from "./sentences.js";

export function age(ms: number | null): string {
  if (ms === null) return "unknown";
  if (ms < 60_000) return `${Math.floor(ms / 1000)} s`;
  if (ms < 3_600_000) return `${Math.floor(ms / 60_000)} min`;
  if (ms < 86_400_000) return `${Math.floor(ms / 3_600_000)} h`;
  return `${Math.floor(ms / 86_400_000)} d`;
}

const since = (iso: string, now: string): number => Math.max(0, Date.parse(now) - Date.parse(iso));

function sessionLine(s: FleetSession, indent: string): string {
  const kids = s.childCount > 0 ? `  · ${s.childCount} subagent${s.childCount === 1 ? "" : "s"}` : "";
  const est = s.estimated ? " (estimated)" : "";
  return `${indent}${`${s.state}${est}`.padEnd(12)}  ${s.provider.padEnd(6)}  ${s.host.padEnd(8)}  ${s.title}  turn ${age(s.turnAgeMs)}  ${s.activity}${kids}${s.doneUnread ? "  · done" : ""}`;
}

// Spec §10.3 output contract: state words first, no colour, Next: at the end.
export function renderFleet(r: FleetReport): string {
  const now = r.generatedAt;
  const lines: string[] = [];
  lines.push(r.needs.length === 0 ? EMPTY.needs(r.counts.working, r.counts.live) : `NEEDS YOU (${r.needs.length})`);
  for (const n of r.needs) lines.push(`  ${n.state}  ${n.kind === "session" ? (n.link.split("/")[2] ?? "") : n.kind}  ${n.repo ?? "-"}  ${n.title}  waiting ${age(since(n.since, now))}  ${n.reason}`);
  lines.push("", r.sessions.length === 0 ? EMPTY.agents : `AGENTS (${r.counts.live} live, ${r.counts.working} working)`);
  let group = "";
  for (const s of r.sessions) {
    const g = `${s.repo ?? "Other"}${s.worktree === null ? "" : ` / ${s.worktree}`}`;
    if (g !== group) {
      lines.push(`  ${g}`);
      group = g;
    }
    lines.push(sessionLine(s, "    "));
  }
  lines.push("", r.jobs.length === 0 ? EMPTY.jobs : "JOBS");
  for (const j of r.jobs) lines.push(`  ${j.state.padEnd(8)}  ${j.kind.padEnd(28)}  ${age(since(j.endedAt ?? j.startedAt, now))} ago  ${j.summary.split("\n")[0]}`);
  const h = r.heavyLock;
  lines.push("", h.holder === null ? `HEAVY LOCK  ${h.held ? "held (no holder record)" : "free"}` : `HEAVY LOCK  held by ${h.holder.kind} (pid ${h.holder.pid}) for ${age(h.holder.ageMs)}; ${h.queue.length} waiting`);
  lines.push(`SOURCES  ${r.sources.map((s) => (!s.on ? `${s.name} off` : s.error === null ? `${s.name} ok` : `${s.name} ${s.error.code}`)).join(" · ")}`);
  const first = r.needs[0];
  lines.push(`Next: ${first?.action.type === "terminal" ? first.action.command : "sindri dashboard"}`);
  return lines.join("\n");
}
```

Trace for the first command test: the needs line reads `approval  claude  -  Fix the login form  waiting 0 s  the session says it is waiting on you`. `n.link.split("/")[2]` takes the provider out of `/agent/claude/<id>`. With no profile, the repo column is `-`. A waiting row's `since` is the spool's pending time or the session's `updatedAt`; here that's the transcript's mtime, which the fixture sets to `NOW`, so the age is `0 s`.

`sindri/src/fleet/command.ts`:

```ts
import { parseFlags } from "../args.js";
import type { Deps } from "../deps.js";
import { SindriError } from "../errors.js";
import type { Command } from "../main.js";
import { fromError, success, type CommandResult } from "../output.js";
import { isAckKey, writeAck } from "./acks.js";
import { collectFleet } from "./collect.js";
import { renderFleet } from "./render.js";
import { runHook } from "./spool.js";
import { isProvider, STATES, type FleetIo, type FleetState } from "./types.js";

async function list(args: string[], deps: Deps, io: FleetIo): Promise<CommandResult> {
  const { values } = parseFlags(args, { json: { type: "boolean" }, provider: { type: "string" }, state: { type: "string" } });
  const provider = values.provider;
  if (provider !== undefined && !isProvider(provider)) throw new SindriError("SND-CLI-002", `--provider must be claude, codex or cursor (got ${provider})`);
  const state = values.state?.replace(/-/g, " ");
  if (state !== undefined && !(STATES as readonly string[]).includes(state)) throw new SindriError("SND-CLI-002", `--state must be one of: ${STATES.join(", ")}`);
  const report = await collectFleet(deps, io);
  const sessions = report.sessions.filter((s) => (provider === undefined || s.provider === provider) && (state === undefined || s.state === (state as FleetState)));
  const shown = { ...report, sessions };
  return success(renderFleet(shown), shown, values.json === true, report.needs.length > 0 ? 1 : 0);
}

async function ack(args: string[], deps: Deps, io: FleetIo): Promise<CommandResult> {
  const { values, positionals } = parseFlags(args, { json: { type: "boolean" } });
  const key = positionals[0];
  if (key === undefined || !isAckKey(key)) throw new SindriError("SND-CLI-002", "usage: sindri fleet ack <key> (a needs[].key from sindri fleet --json, or done:<provider>:<id>)");
  const report = await collectFleet(deps, io);
  const done = /^done:([a-z]+):(.+)$/.exec(key);
  const known = done !== null ? report.sessions.some((s) => s.provider === done[1] && s.id === done[2]) : report.needs.some((n) => n.ackable && n.key === key);
  if (!known) throw new SindriError("SND-FLEET-003", `nothing that can be acknowledged has the key ${key}`, { exitCode: 1 });
  writeAck(deps, key, deps.now());
  return success(`Acknowledged ${key}.\nNext: sindri fleet`, { key, ackedAt: deps.now().toISOString() }, values.json === true);
}

export function makeFleetCommand(io: FleetIo): Command {
  return async (args, deps) => {
    const [sub, ...rest] = args;
    // The hook must never fail its host: no output, exit 0, whatever happens (spec §4.2).
    if (sub === "hook") {
      await runHook(rest[0] ?? "", deps);
      return { exitCode: 0, stdout: "", stderr: "" };
    }
    try {
      return sub === "ack" ? await ack(rest, deps, io) : await list(args, deps, io);
    } catch (e) {
      return fromError(e, args.includes("--json"));
    }
  };
}
```

`sindri/src/fleet/proc-real.ts`: append

```ts
import { performance } from "node:perf_hooks";

import { realProcessRunner } from "../index/sandbox-real.js";
import type { FleetIo } from "./types.js";

// Spec decision 5: no Codex app-server is reachable for sessions sindri didn't start; the probe says so.
export function realFleetIo(): FleetIo {
  return { proc: realProcessTable(), run: realProcessRunner(), codex: { status: () => null }, monotonic: () => performance.now() };
}
```

(Move the new imports to the top of the file.)

`sindri/src/main.ts`: import `{ makeFleetCommand }` from `./fleet/command.js` and `{ realFleetIo }` from `./fleet/proc-real.js`, and add to `COMMANDS`:

```ts
  fleet: {
    summary: "Every agent on this machine (Claude Code, Codex, Cursor), sindri's jobs, and what needs you",
    usage: [
      "Usage:",
      "  sindri fleet [--provider claude|codex|cursor] [--state S] [--json]   (exit 1 when something needs you)",
      "  sindri fleet ack <key> [--json]   (hide a failure until it happens again; done:<provider>:<id> marks a finished turn seen)",
      "  sindri fleet hook <provider>   (the aw:fleet hook: reads the hook payload on stdin, prints nothing, always exits 0)",
    ].join("\n"),
    run: (args, deps) => makeFleetCommand(realFleetIo())(args, deps),
  },
```

- [ ] **Step 4: Run the tests**

Run: `cd sindri && npx vitest run tests/fleet-usage.test.ts tests/fleet-collect.test.ts tests/fleet-command.test.ts tests/errors.test.ts tests/main.test.ts`
Expected: PASS. Then, once for the task: `npm test && npm run typecheck && npm run test:coverage`. Expected: all PASS, coverage 100%. Then a manual read-only smoke run against this machine: `npm run build && node dist/cli.js fleet; echo "exit $?"`. Expected: the NEEDS YOU or "Nothing needs you." line, your live sessions under AGENTS, and exit 1 or 0 to match. Paste nothing from that output into commits or the plan.

- [ ] **Step 5: Commit**

```bash
git add sindri/src sindri/tests docs/sindri/errors.md
git commit -m "feat: sindri fleet command and collector" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 9: Dashboard server security, the read map and the action allowlist (`sindri dashboard`)

**Files:**
- Create: `sindri/src/dashboard/tokens.ts`, `sindri/src/dashboard/security.ts`, `sindri/src/dashboard/actions.ts`, `sindri/src/dashboard/server.ts`, `sindri/src/dashboard/command.ts`, `sindri/src/dashboard/io-real.ts`
- Modify: `sindri/src/main.ts` (register `dashboard`), `sindri/src/errors.ts` (`SND-DASH-001`–`004`), `sindri/vitest.config.ts` (exclude `src/dashboard/io-real.ts`)
- Generated: `docs/sindri/errors.md`
- Test: `sindri/tests/dashboard-fixtures.ts` (shared), `sindri/tests/dashboard-tokens.test.ts`, `sindri/tests/dashboard-server.test.ts`, `sindri/tests/dashboard-command.test.ts`

**Interfaces:**
- Consumes: `isAckKey` (Task 7); `dashboardDir`, `sameStart` (Tasks 1–2); `stripInvisible` (`evolve/invisible.ts`); `DashboardSchema`, `approvedProfile`, `openLedgerReadOnly`, `ProcessRunner`, `SindriError`, `parseFlags`, `success`, `fromError`.
- Produces:
  - Error codes:
    - `SND-DASH-001` "The dashboard port is already in use." (fix: set `dashboard.port` or pass `--port N`);
    - `SND-DASH-002` "The dashboard isn't running." (fix: `sindri dashboard`);
    - `SND-DASH-003` "The dashboard refused the request: no valid session, or a wrong Host, Origin or CSRF token." (fix: run `sindri dashboard --url`);
    - `SND-DASH-004` "That action can't run from the dashboard." (fix: copy the command and run it at a terminal).
  - `tokens.ts`:
    - `TOKEN_TTL_MS` (10 min), `COOKIE` (`sindri_session`), `mintToken(key, now, nonce?)`, `cookieHeader(id)`, `parseCookie(header)`;
    - `class TokenStore { exchange(token): Session | null; session(cookieHeader): Session | null }`. A token is used once and within 10 minutes, its HMAC is checked in constant time, and at most 50 sessions are kept.
  - `security.ts`: `CSP`, `securityHeaders()`, `hostOk(host, port)` and `originOk(origin, host, port)`.
  - `actions.ts`:
    - `READS` (`fleet` 4 s, `evolve` 30 s, `index` 60 s, `doctor` 300 s, `channel` 60 s, `scope` 60 s, each with its argv and its CLI hint) and `ReadName`;
    - `SAFE_ACTIONS` (`fleet.ack` with 1 checked argument; `evolve.reject` with an id and a reason of 3–200 visible characters);
    - `TERMINAL_ONLY` (`evolve.adopt`, `evolve.publish`, `channel.promote`, `profile.approve`);
    - `checkAction(action, args): { kind: "run"; argv } | { kind: "terminal"; command } | { kind: "refused"; why }`.
  - `server.ts`:
    - `SindriRunner`, `AttachHandler` (filled by Task 11), `DashboardOptions`, `PAGE` (the deep-link pattern);
    - `createDashboardHandler(o): http.RequestListener`;
    - `listenLoopback(handler, port): Promise<{ port; close() }>`, which binds `127.0.0.1` only. `EADDRINUSE` becomes `SND-DASH-001`, naming the port and `dashboard.port`.
    - Routes:
      - `GET /` and the deep links (`/needs`, `/agents`, `/repos`, `/auto`, `/today`, `/agent/<provider>/<id>`, `/repo/<name>`, `/job/<id>`): with `?t=` they exchange the token for a cookie and answer 303 to the same path; without a valid cookie they serve the 401 page;
      - `GET /static/<fixed file>`;
      - `GET /api/session` returns `{ csrf, notify, pollMs, overSsh }`;
      - `GET /api/read/<name>`;
      - `POST /api/action` needs `Origin`, `X-Sindri-CSRF` and a JSON body of at most 8 KiB.
  - `command.ts`:
    - `DashboardIo { run; listen; untilStopped; randomKey; node; cliPath; uiDir; jsDir; monotonic }`;
    - `serverFile(deps)` (`$AW_STATE_DIR/sindri/dashboard/server.json`, 0600) and `liveServer(deps)`;
    - `makeDashboardCommand(io): Command` for `sindri dashboard [--port N] | --url | --open <path>` (`--json` on each).
  - `io-real.ts`: `realDashboardIo()`.

- [ ] **Step 1: Write the failing tests**

`sindri/tests/dashboard-fixtures.ts`:

```ts
import fs from "node:fs";
import http from "node:http";
import net from "node:net";
import path from "node:path";

import { createDashboardHandler, listenLoopback, type DashboardOptions, type SindriRunner } from "../src/dashboard/server.js";
import { tempDir } from "./helpers.js";

export async function freePort(): Promise<number> {
  const s = net.createServer();
  await new Promise<void>((r) => s.listen(0, "127.0.0.1", r));
  const { port } = s.address() as net.AddressInfo;
  await new Promise<void>((r) => s.close(() => r()));
  return port;
}

export function uiDirs(): { uiDir: string; jsDir: string } {
  const uiDir = tempDir("sindri-ui-");
  const jsDir = tempDir("sindri-js-");
  fs.writeFileSync(path.join(uiDir, "index.html"), "<!doctype html><title>Sindri</title><main id=app></main>");
  fs.writeFileSync(path.join(uiDir, "app.css"), "body{}");
  fs.writeFileSync(path.join(uiDir, "favicon.svg"), "<svg/>");
  fs.writeFileSync(path.join(uiDir, "favicon-attn.svg"), "<svg/>");
  fs.writeFileSync(path.join(jsDir, "app.js"), "export {};");
  fs.writeFileSync(path.join(jsDir, "model.js"), "export {};");
  return { uiDir, jsDir };
}

// A runner that answers `sindri <args> --json` from a function and records every argv.
export function fakeSindri(answer: (args: string[]) => { code?: number; stdout?: string; stderr?: string } = () => ({ stdout: "{}" })): SindriRunner & { calls: string[][] } {
  const calls: string[][] = [];
  const run = (async (args: string[]) => {
    calls.push(args);
    const a = answer(args);
    return { code: a.code ?? 0, stdout: a.stdout ?? "", stderr: a.stderr ?? "" };
  }) as SindriRunner & { calls: string[][] };
  run.calls = calls;
  return run;
}

export async function startServer(over: Partial<DashboardOptions> = {}) {
  const port = await freePort();
  const opts: DashboardOptions = {
    port, key: Buffer.alloc(32, 7), run: fakeSindri(), ...uiDirs(), notify: true, pollMs: 5000, overSsh: false,
    now: () => new Date("2026-10-08T12:00:00.000Z"), monotonic: () => 0, attach: null, ...over,
  };
  const srv = await listenLoopback(createDashboardHandler(opts), port);
  return { ...srv, opts, base: `http://127.0.0.1:${port}` };
}

export interface Reply { status: number; headers: http.IncomingHttpHeaders; body: string }

// http.request (not fetch): tests need to set Host and Origin themselves.
export function request(port: number, o: { method?: string; path: string; headers?: Record<string, string>; body?: string }): Promise<Reply> {
  return new Promise((resolve, reject) => {
    const r = http.request({ host: "127.0.0.1", port, method: o.method ?? "GET", path: o.path, headers: { host: `127.0.0.1:${port}`, ...o.headers } }, (res) => {
      let body = "";
      res.setEncoding("utf8");
      res.on("data", (c: string) => (body += c));
      res.on("end", () => resolve({ status: res.statusCode as number, headers: res.headers, body }));
    });
    r.on("error", reject);
    if (o.body !== undefined) r.write(o.body);
    r.end();
  });
}

// Exchange a fresh token for a session: the cookie and the CSRF token.
export async function login(port: number, key: Buffer, mint: (key: Buffer) => string): Promise<{ cookie: string; csrf: string }> {
  const r = await request(port, { path: `/?t=${mint(key)}` });
  const cookie = String(r.headers["set-cookie"]?.[0]).split(";")[0];
  const s = await request(port, { path: "/api/session", headers: { cookie } });
  return { cookie, csrf: (JSON.parse(s.body) as { csrf: string }).csrf };
}
```

`sindri/tests/dashboard-tokens.test.ts`:

```ts
import { describe, expect, it } from "vitest";

import { cookieHeader, mintToken, parseCookie, TOKEN_TTL_MS, TokenStore } from "../src/dashboard/tokens.js";

const KEY = Buffer.alloc(32, 1);
const NOW = new Date("2026-10-08T12:00:00.000Z");

describe("TokenStore", () => {
  it("exchanges a token once for a session with its own CSRF token", () => {
    const store = new TokenStore(KEY, () => NOW);
    const t = mintToken(KEY, NOW);
    const s = store.exchange(t);
    expect(s?.id).toMatch(/^[0-9a-f]{64}$/);
    expect(s?.csrf).toMatch(/^[0-9a-f]{64}$/);
    expect(s?.csrf).not.toBe(s?.id);
    expect(store.exchange(t)).toBeNull(); // used once
    expect(store.session(`other=1; sindri_session=${s?.id}`)).toEqual(s);
    expect(store.session("sindri_session=nope")).toBeNull();
    expect(store.session(undefined)).toBeNull();
  });

  it("refuses an expired, future-dated, forged or malformed token, and one from another key (a restart)", () => {
    const store = new TokenStore(KEY, () => NOW);
    expect(store.exchange(mintToken(KEY, new Date(NOW.getTime() - TOKEN_TTL_MS - 1000)))).toBeNull();
    expect(store.exchange(mintToken(KEY, new Date(NOW.getTime() + 3_600_000)))).toBeNull();
    const t = mintToken(KEY, NOW);
    expect(store.exchange(`${t.slice(0, -2)}AA`)).toBeNull();
    expect(store.exchange("junk")).toBeNull();
    expect(store.exchange(mintToken(Buffer.alloc(32, 2), NOW))).toBeNull();
  });

  it("forgets used nonces once they expire, and keeps at most 50 sessions", () => {
    let now = NOW;
    const store = new TokenStore(KEY, () => now);
    const first = store.exchange(mintToken(KEY, now));
    for (let i = 0; i < 50; i++) store.exchange(mintToken(KEY, now));
    expect(store.session(`sindri_session=${first?.id}`)).toBeNull(); // the oldest was evicted
    now = new Date(NOW.getTime() + TOKEN_TTL_MS + 1);
    expect(store.exchange(mintToken(KEY, now))).not.toBeNull();
  });
});

describe("cookies", () => {
  it("is HttpOnly and SameSite=Strict, and parses a cookie header", () => {
    expect(cookieHeader("abc")).toBe("sindri_session=abc; HttpOnly; SameSite=Strict; Path=/");
    expect(parseCookie(" a=1; b = 2 ;junk; c=x=y")).toEqual({ a: "1", b: "2", c: "x=y" });
    expect(parseCookie(undefined)).toEqual({});
  });
});
```

`sindri/tests/dashboard-server.test.ts`:

```ts
import net from "node:net";
import { afterEach, describe, expect, it } from "vitest";

import { mintToken, TOKEN_TTL_MS } from "../src/dashboard/tokens.js";
import { listenLoopback } from "../src/dashboard/server.js";
import { SindriError } from "../src/errors.js";
import { fakeSindri, freePort, login, request, startServer } from "./dashboard-fixtures.js";

const NOW = new Date("2026-10-08T12:00:00.000Z");
const mint = (key: Buffer) => mintToken(key, NOW);
const ULID = "01hyyyyyyyyyyyyyyyyyyyyyyy";
let close: (() => Promise<void>) | null = null;
afterEach(async () => {
  await close?.();
  close = null;
});
async function server(over: Parameters<typeof startServer>[0] = {}) {
  const s = await startServer(over);
  close = s.close;
  return s;
}

describe("binding", () => {
  it("listens on 127.0.0.1 only, and a taken port is SND-DASH-001 naming the port and the profile key", async () => {
    const s = await server();
    expect(s.port).toBe(s.opts.port);
    const port = await freePort();
    const squatter = net.createServer();
    await new Promise<void>((r) => squatter.listen(port, "127.0.0.1", r));
    const err = await listenLoopback(() => undefined, port).catch((e: unknown) => e);
    expect(err).toBeInstanceOf(SindriError);
    expect((err as SindriError).code).toBe("SND-DASH-001");
    expect((err as SindriError).message).toBe(`port ${port} is in use (dashboard.port)`);
    await new Promise<void>((r) => squatter.close(() => r()));
  });
});

describe("token, cookie and session", () => {
  it("exchanges a one-time token for an HttpOnly SameSite=Strict cookie and drops it from the URL", async () => {
    const s = await server();
    const t = mint(s.opts.key);
    const r = await request(s.port, { path: `/agent/claude/abc-123?t=${t}` });
    expect(r.status).toBe(303);
    expect(r.headers.location).toBe("/agent/claude/abc-123");
    expect(r.headers["set-cookie"]?.[0]).toMatch(/^sindri_session=[0-9a-f]{64}; HttpOnly; SameSite=Strict; Path=\/$/);
    const again = await request(s.port, { path: `/?t=${t}` });
    expect(again.status).toBe(401);
    expect(again.body).toContain("run <code>sindri dashboard --url</code>");
    const cookie = String(r.headers["set-cookie"]?.[0]).split(";")[0];
    const page = await request(s.port, { path: "/agent/claude/abc-123", headers: { cookie } });
    expect(page.status).toBe(200);
    expect(page.body).toContain("<main id=app>");
  });

  it("refuses an expired token, a token or cookie from before a restart, and pages or APIs without a session", async () => {
    const s = await server();
    expect((await request(s.port, { path: `/?t=${mintToken(s.opts.key, new Date(NOW.getTime() - TOKEN_TTL_MS - 1))}` })).status).toBe(401);
    const { cookie } = await login(s.port, s.opts.key, mint);
    await s.close();
    const restarted = await server({ port: s.opts.port, key: Buffer.alloc(32, 9) });
    expect((await request(restarted.port, { path: `/?t=${mint(s.opts.key)}` })).status).toBe(401);
    expect((await request(restarted.port, { path: "/", headers: { cookie } })).status).toBe(401);
    const api = await request(restarted.port, { path: "/api/read/fleet" });
    expect(api.status).toBe(401);
    expect(JSON.parse(api.body)).toEqual({ ok: false, error: { code: "SND-DASH-003", message: "no valid session", fix: "run `sindri dashboard --url` and open the printed URL" } });
  });
});

describe("request checks and headers", () => {
  it("refuses a wrong Host everywhere and accepts localhost:<port>", async () => {
    const s = await server();
    for (const host of ["evil.example", `evil.example:${s.port}`, `127.0.0.1:${s.port + 1}`, "127.0.0.1"]) {
      const r = await request(s.port, { path: "/static/app.css", headers: { host } });
      expect(r.status).toBe(403);
      expect(JSON.parse(r.body).error.code).toBe("SND-DASH-003");
    }
    expect((await request(s.port, { path: "/static/app.css", headers: { host: `localhost:${s.port}` } })).status).toBe(200);
  });

  it("sends the CSP and the other headers on every response, and never a CORS header", async () => {
    const s = await server();
    for (const p of ["/", "/static/app.js", "/api/session", "/nope"]) {
      const r = await request(s.port, { path: p, headers: { origin: "http://evil.example" } });
      expect(r.headers["content-security-policy"]).toBe("default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; connect-src 'self'; font-src 'self'; object-src 'none'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'");
      expect(r.headers["referrer-policy"]).toBe("no-referrer");
      expect(r.headers["x-frame-options"]).toBe("DENY");
      expect(r.headers["x-content-type-options"]).toBe("nosniff");
      expect(Object.keys(r.headers).filter((h) => h.startsWith("access-control-"))).toEqual([]);
    }
  });

  it("serves only the fixed static files with their types", async () => {
    const s = await server();
    expect((await request(s.port, { path: "/static/app.js" })).headers["content-type"]).toBe("text/javascript; charset=utf-8");
    expect((await request(s.port, { path: "/static/favicon-attn.svg" })).headers["content-type"]).toBe("image/svg+xml");
    expect((await request(s.port, { path: "/static/../index.html" })).status).toBe(404);
    expect((await request(s.port, { path: "/static/secret.txt" })).status).toBe(404);
    expect((await request(s.port, { path: "/repo/Bad_Name" })).status).toBe(404);
  });
});

describe("actions", () => {
  const post = (port: number, body: unknown, headers: Record<string, string>) =>
    request(port, { method: "POST", path: "/api/action", headers: { "content-type": "application/json", ...headers }, body: typeof body === "string" ? body : JSON.stringify(body) });

  it("needs the cookie, a matching Origin and the CSRF token, then runs the checked argv", async () => {
    const run = fakeSindri(() => ({ stdout: '{"key":"job:observe:1"}' }));
    const s = await server({ run });
    const { cookie, csrf } = await login(s.port, s.opts.key, mint);
    const ok = { cookie, origin: `http://127.0.0.1:${s.port}`, "x-sindri-csrf": csrf };
    const body = { action: "fleet.ack", args: ["job:observe:1"] };
    expect((await post(s.port, body, { origin: ok.origin, "x-sindri-csrf": csrf })).status).toBe(401);
    expect((await post(s.port, body, { cookie, "x-sindri-csrf": csrf })).status).toBe(403);
    expect((await post(s.port, body, { ...ok, origin: "http://evil.example" })).status).toBe(403);
    expect((await post(s.port, body, { ...ok, origin: `http://localhost:${s.port}` })).status).toBe(403); // Origin must match the Host used
    expect((await post(s.port, body, { cookie, origin: ok.origin })).status).toBe(403);
    expect((await post(s.port, body, { ...ok, "x-sindri-csrf": "wrong" })).status).toBe(403);
    expect(run.calls).toEqual([]);
    const r = await post(s.port, body, ok);
    expect(r.status).toBe(200);
    expect(JSON.parse(r.body)).toEqual({ ok: true, exitCode: 0, data: { key: "job:observe:1" }, stderr: "" });
    expect(run.calls).toEqual([["fleet", "ack", "job:observe:1", "--json"]]);
    expect((await post(s.port, "{not json", ok)).status).toBe(400);
    expect((await post(s.port, "x".repeat(9000), ok)).status).toBe(400);
  });

  it("refuses commands outside the allowlist and bad arguments; terminal-gated commands are shown, never run", async () => {
    const run = fakeSindri();
    const s = await server({ run });
    const { cookie, csrf } = await login(s.port, s.opts.key, mint);
    const ok = { cookie, origin: `http://127.0.0.1:${s.port}`, "x-sindri-csrf": csrf };
    for (const body of [
      { action: "shell", args: ["ls"] },
      { action: "fleet.ack", args: ["../etc"] },
      { action: "fleet.ack", args: ["job:observe:1", "--extra"] },
      { action: "evolve.reject", args: [ULID, "\u001b[2Jhide this"] },
      { action: "evolve.reject", args: [ULID, "--force"] },
      { action: "evolve.reject", args: ["not-an-id", "a fine reason"] },
      { action: "fleet.ack", args: [7] },
      { action: "constructor", args: [] },
    ]) {
      const r = await post(s.port, body, ok);
      expect(r.status).toBe(403);
      expect(JSON.parse(r.body).error.code).toBe("SND-DASH-004");
    }
    const adopt = await post(s.port, { action: "evolve.adopt", args: [ULID] }, ok);
    expect(adopt.status).toBe(403);
    expect(JSON.parse(adopt.body)).toMatchObject({ ok: false, copy: `sindri evolve adopt ${ULID}`, error: { code: "SND-DASH-004" } });
    expect(JSON.parse((await post(s.port, { action: "profile.approve", args: [] }, ok)).body).copy).toBe("sindri profile approve");
    expect(JSON.parse((await post(s.port, { action: "channel.promote", args: ["a".repeat(40)] }, ok)).body).copy).toBe(`sindri channel promote ${"a".repeat(40)}`);
    expect(JSON.parse((await post(s.port, { action: "evolve.publish", args: [] }, ok)).body).copy).toBe("sindri evolve publish");
    expect((await post(s.port, { action: "channel.promote", args: ["HEAD"] }, ok)).status).toBe(403);
    expect(run.calls).toEqual([]);
    await post(s.port, { action: "evolve.reject", args: [ULID, "the variant is worse on long briefs"] }, ok);
    expect(run.calls).toEqual([["evolve", "reject", ULID, "--reason", "the variant is worse on long briefs", "--json"]]);
  });
});

describe("reads", () => {
  it("runs each read through the CLI, caches it for its TTL, dedupes concurrent polls and reports errors with the CLI hint", async () => {
    let clock = 0;
    const run = fakeSindri((args) => (args[0] === "doctor" ? { code: 2, stdout: '{"ok":false,"error":{"code":"SND-PROFILE-002","message":"no profile","fix":"sindri profile init","details":[]}}' } : { code: 1, stdout: '{"v":1}' }));
    const s = await server({ run, monotonic: () => clock });
    const { cookie } = await login(s.port, s.opts.key, mint);
    const [a, b] = await Promise.all([request(s.port, { path: "/api/read/fleet", headers: { cookie } }), request(s.port, { path: "/api/read/fleet", headers: { cookie } })]);
    expect(JSON.parse(a.body)).toEqual({ ok: true, exitCode: 1, data: { v: 1 } });
    expect(b.body).toBe(a.body);
    expect(run.calls).toEqual([["fleet", "--json"]]);
    clock = 4001;
    await request(s.port, { path: "/api/read/fleet", headers: { cookie } });
    expect(run.calls).toHaveLength(2);
    const doc = JSON.parse((await request(s.port, { path: "/api/read/doctor", headers: { cookie } })).body);
    expect(doc).toEqual({ ok: false, exitCode: 2, error: { code: "SND-PROFILE-002", message: "no profile", fix: "sindri profile init" }, cli: "sindri doctor" });
    expect((await request(s.port, { path: "/api/read/secrets", headers: { cookie } })).status).toBe(404);
    expect(JSON.parse((await request(s.port, { path: "/api/session", headers: { cookie } })).body)).toMatchObject({ notify: true, pollMs: 5000, overSsh: false });
  });

  it("turns a crash with no JSON into SND-CLI-900 with the first stderr line", async () => {
    const s = await server({ run: fakeSindri(() => ({ code: 2, stdout: "", stderr: "boom\nstack" })) });
    const { cookie } = await login(s.port, s.opts.key, mint);
    expect(JSON.parse((await request(s.port, { path: "/api/read/index", headers: { cookie } })).body)).toMatchObject({ ok: false, error: { code: "SND-CLI-900", message: "boom" }, cli: "sindri index status" });
  });
});
```

`sindri/tests/dashboard-command.test.ts`:

```ts
import fs from "node:fs";
import { describe, expect, it } from "vitest";

import { liveServer, makeDashboardCommand, serverFile, type DashboardIo } from "../src/dashboard/command.js";
import { listenLoopback } from "../src/dashboard/server.js";
import { COMMANDS } from "../src/main.js";
import { fakeProc } from "./evolve-fixtures.js";
import { evolveFixture } from "./evolve-fixtures.js";
import { freePort, request, uiDirs } from "./dashboard-fixtures.js";
import { fakeSystem, makeDeps } from "./helpers.js";

function io(over: Partial<DashboardIo> = {}): DashboardIo & { stop: () => void } {
  let stop: () => void = () => undefined;
  return {
    run: fakeProc(() => ({})), listen: listenLoopback, randomKey: () => Buffer.alloc(32, 3), node: "/usr/bin/node", cliPath: "/x/dist/cli.js",
    ...uiDirs(), monotonic: () => 0, untilStopped: () => new Promise<void>((r) => (stop = r)), get stop() { return stop; }, ...over,
  };
}

describe("sindri dashboard", () => {
  it("serves until stopped, writes server.json 0600, logs a one-time URL, and --url mints more that the server accepts", async () => {
    const port = await freePort();
    const lines: string[] = [];
    const deps = makeDeps({ log: (l) => lines.push(l) });
    const d = io();
    const running = makeDashboardCommand(d)(["--port", String(port)], deps);
    await new Promise((r) => setTimeout(r, 50));
    expect(fs.statSync(serverFile(deps)).mode & 0o777).toBe(0o600);
    expect(lines[0]).toMatch(new RegExp(`^Dashboard: http://127\\.0\\.0\\.1:${port}/\\?t=[0-9a-f]{32}\\.\\d{13}\\.[A-Za-z0-9_-]{43}$`));
    const url = await makeDashboardCommand(d)(["--url"], deps);
    const token = /\?t=(\S+)/.exec(url.stdout)?.[1] as string;
    expect((await request(port, { path: `/?t=${token}` })).status).toBe(303);
    expect(JSON.parse((await makeDashboardCommand(d)(["--url", "--json"], deps)).stdout).url).toMatch(/^http:\/\/127\.0\.0\.1:/);
    const again = await makeDashboardCommand(d)(["--port", String(port)], deps);
    expect(again).toMatchObject({ exitCode: 1 });
    expect(again.stdout).toContain(`already running on port ${port}`);
    d.stop();
    expect((await running).stdout).toBe("Dashboard stopped.\n");
    expect(fs.existsSync(serverFile(deps))).toBe(false);
  });

  it("exits with SND-DASH-001 when the port is taken, and validates --port", async () => {
    const port = await freePort();
    const squat = await listenLoopback(() => undefined, port);
    const r = await makeDashboardCommand(io())(["--port", String(port)], makeDeps());
    expect(r.exitCode).toBe(2);
    expect(r.stderr).toContain(`SND-DASH-001 port ${port} is in use (dashboard.port)`);
    expect(r.stderr).toContain("fix: set dashboard.port in profile.yaml");
    await squat.close();
    expect((await makeDashboardCommand(io())(["--port", "80"], makeDeps())).stderr).toContain("SND-CLI-002");
  });

  it("takes the port from the approved profile", async () => {
    const fx = await evolveFixture({ extraYaml: "dashboard:\n  port: 7291\n" });
    let asked = 0;
    const d = io({ listen: async (_h, p) => { asked = p; throw new Error("stop here"); } });
    await makeDashboardCommand(d)([], fx.deps).catch(() => undefined);
    expect(asked).toBe(7291);
    fx.close();
  });

  it("--url and --open need a live server; a stale server.json (dead or recycled pid) counts as not running", async () => {
    const deps = makeDeps();
    const none = await makeDashboardCommand(io())(["--url"], deps);
    expect(none.stderr).toContain("SND-DASH-002");
    fs.mkdirSync(serverFile(deps).replace(/\/server\.json$/, ""), { recursive: true });
    fs.writeFileSync(serverFile(deps), JSON.stringify({ pid: 4242, pidStart: "start-old", host: "test-host", port: 7190, key: "ab".repeat(32), startedAt: "x" }));
    expect(liveServer(deps)).toBeNull();
    fs.writeFileSync(serverFile(deps), JSON.stringify({ pid: 4242, pidStart: "start-4242", host: "test-host", port: 7190, key: "ab".repeat(32), startedAt: "x" }));
    expect(liveServer(deps)?.port).toBe(7190);
    expect(liveServer({ ...deps, system: fakeSystem({ pidAlive: () => false }) })).toBeNull();
    fs.writeFileSync(serverFile(deps), "{junk");
    expect(liveServer(deps)).toBeNull();
  });

  it("--open mints a URL for a deep link and opens it; refuses other paths; reports a failed opener", async () => {
    const deps = makeDeps();
    fs.mkdirSync(serverFile(deps).replace(/\/server\.json$/, ""), { recursive: true });
    fs.writeFileSync(serverFile(deps), JSON.stringify({ pid: 4242, pidStart: "start-4242", host: "test-host", port: 7190, key: "ab".repeat(32), startedAt: "x" }));
    const run = fakeProc(() => ({}));
    const r = await makeDashboardCommand(io({ run }))(["--open", "/agent/claude/abc"], deps);
    expect(r.exitCode).toBe(0);
    expect(run.calls[0].argv[0]).toBe("open");
    expect(run.calls[0].argv[1]).toMatch(/^http:\/\/127\.0\.0\.1:7190\/agent\/claude\/abc\?t=/);
    expect((await makeDashboardCommand(io({ run }))(["--open", "https://evil.example"], deps)).stderr).toContain("SND-CLI-002");
    const failed = await makeDashboardCommand(io({ run: fakeProc(() => ({ code: 1 })) }))(["--open", "/needs"], deps);
    expect(failed.exitCode).toBe(1);
    expect(failed.stdout).toMatch(/^Couldn't open a browser; open this yourself: http:\/\/127\.0\.0\.1:7190\/needs\?t=/);
  });

  it("is registered", () => {
    expect(COMMANDS.dashboard.usage).toContain("sindri dashboard --url");
    expect(COMMANDS.dashboard.usage).toContain("--open <path>");
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd sindri && npx vitest run tests/dashboard-tokens.test.ts tests/dashboard-server.test.ts tests/dashboard-command.test.ts`
Expected: FAIL with `Failed to load url ../src/dashboard/tokens.js` (and likewise for `server.js` and `command.js`).

- [ ] **Step 3: Implement**

`sindri/src/errors.ts`, after the `SND-FLEET-*` codes:

```ts
  "SND-DASH-001": { summary: "The dashboard port is already in use.", fix: "set dashboard.port in profile.yaml, then sindri profile approve; or pass --port N" },
  "SND-DASH-002": { summary: "The dashboard isn't running.", fix: "sindri dashboard" },
  "SND-DASH-003": { summary: "The dashboard refused the request: no valid session, or a wrong Host, Origin or CSRF token.", fix: "run `sindri dashboard --url` and open the printed URL" },
  "SND-DASH-004": { summary: "That action can't run from the dashboard.", fix: "commands that need a typed confirmation run only at a terminal: copy the command the dashboard shows" },
```

Then `cd sindri && npm run gen`.

`sindri/src/dashboard/tokens.ts`:

```ts
import { createHmac, randomBytes, timingSafeEqual } from "node:crypto";

export const TOKEN_TTL_MS = 10 * 60_000;
export const COOKIE = "sindri_session";
const MAX_SESSIONS = 50;
const TOKEN = /^([0-9a-f]{32})\.(\d{13})\.([A-Za-z0-9_-]{43})$/;

const sign = (key: Buffer, msg: string): string => createHmac("sha256", key).update(msg).digest("base64url");

// Spec decision 12: nonce.expiry.hmac under the running server's key. A restart makes a new key, so every
// older token and cookie stops working (spec §6 "The token changes on every restart").
export function mintToken(key: Buffer, now: Date, nonce: string = randomBytes(16).toString("hex")): string {
  const exp = now.getTime() + TOKEN_TTL_MS;
  return `${nonce}.${exp}.${sign(key, `${nonce}.${exp}`)}`;
}

export const cookieHeader = (id: string): string => `${COOKIE}=${id}; HttpOnly; SameSite=Strict; Path=/`;

export function parseCookie(header: string | undefined): Record<string, string> {
  const out: Record<string, string> = {};
  for (const part of (header ?? "").split(";")) {
    const i = part.indexOf("=");
    if (i > 0) out[part.slice(0, i).trim()] = part.slice(i + 1).trim();
  }
  return out;
}

export interface Session {
  id: string;
  csrf: string;
  createdAt: number;
}

export class TokenStore {
  private readonly used = new Map<string, number>();
  private readonly sessions = new Map<string, Session>();

  constructor(private readonly key: Buffer, private readonly now: () => Date) {}

  exchange(token: string): Session | null {
    const m = TOKEN.exec(token);
    if (m === null) return null;
    const t = this.now().getTime();
    for (const [nonce, exp] of this.used) if (exp < t) this.used.delete(nonce);
    const exp = Number(m[2]);
    if (exp < t || exp > t + TOKEN_TTL_MS + 60_000) return null;
    const want = Buffer.from(sign(this.key, `${m[1]}.${m[2]}`));
    const got = Buffer.from(m[3]);
    if (want.length !== got.length || !timingSafeEqual(want, got) || this.used.has(m[1])) return null;
    this.used.set(m[1], exp);
    const s: Session = { id: randomBytes(32).toString("hex"), csrf: randomBytes(32).toString("hex"), createdAt: t };
    this.sessions.set(s.id, s);
    while (this.sessions.size > MAX_SESSIONS) this.sessions.delete(this.sessions.keys().next().value as string);
    return s;
  }

  session(header: string | undefined): Session | null {
    const id = parseCookie(header)[COOKIE];
    return id === undefined ? null : (this.sessions.get(id) ?? null);
  }
}
```

`sindri/src/dashboard/security.ts`:

```ts
// Spec §6 "Headers": nothing inline, nothing from another origin, never framed.
export const CSP = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; connect-src 'self'; font-src 'self'; object-src 'none'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'";

export function securityHeaders(): Record<string, string> {
  return {
    "Content-Security-Policy": CSP,
    "X-Frame-Options": "DENY",
    "Referrer-Policy": "no-referrer",
    "X-Content-Type-Options": "nosniff",
    "Cache-Control": "no-store",
    "Cross-Origin-Opener-Policy": "same-origin",
    "Cross-Origin-Resource-Policy": "same-origin",
  };
}

// Spec §6 "Request checks": a Host of 127.0.0.1:<port> or localhost:<port> (DNS rebinding sends another name).
export const hostOk = (host: string | undefined, port: number): boolean =>
  host !== undefined && [`127.0.0.1:${port}`, `localhost:${port}`].includes(host.toLowerCase());

// Every action also needs an Origin that names exactly the host the page was loaded from.
export const originOk = (origin: string | undefined, host: string | undefined, port: number): boolean =>
  origin !== undefined && host !== undefined && hostOk(host, port) && origin.toLowerCase() === `http://${host.toLowerCase()}`;
```

`sindri/src/dashboard/actions.ts`:

```ts
import { stripInvisible } from "../evolve/invisible.js";
import { isAckKey } from "../fleet/acks.js";

export interface ReadDef {
  argv: readonly string[];
  ttlMs: number;
  cli: string;
}
// Spec §3 "One source of truth": every view is a sindri --json run.
export const READS = {
  fleet: { argv: ["fleet", "--json"], ttlMs: 4_000, cli: "sindri fleet" },
  evolve: { argv: ["evolve", "status", "--json"], ttlMs: 30_000, cli: "sindri evolve status" },
  index: { argv: ["index", "status", "--json"], ttlMs: 60_000, cli: "sindri index status" },
  doctor: { argv: ["doctor", "--json"], ttlMs: 300_000, cli: "sindri doctor" },
  channel: { argv: ["channel", "status", "--json"], ttlMs: 60_000, cli: "sindri channel status" },
  scope: { argv: ["scope", "runs", "--json"], ttlMs: 60_000, cli: "sindri scope runs" },
} as const satisfies Record<string, ReadDef>;
export type ReadName = keyof typeof READS;

const ULID = /^[0-9a-hjkmnp-tv-z]{26}$/;
const SHA = /^[0-9a-f]{40}$/;
const reasonOk = (r: string): boolean => r.trim().length >= 3 && r.length <= 200 && stripInvisible(r) === r && !r.startsWith("-");

interface SafeAction {
  arity: number;
  check: (a: readonly string[]) => boolean;
  argv: (a: readonly string[]) => string[];
}
// Spec §6 "Actions": a fixed list, checked arguments, run as argv with no shell.
export const SAFE_ACTIONS: Record<string, SafeAction> = {
  "fleet.ack": { arity: 1, check: (a) => isAckKey(a[0]), argv: (a) => ["fleet", "ack", a[0], "--json"] },
  "evolve.reject": { arity: 2, check: (a) => ULID.test(a[0]) && reasonOk(a[1]), argv: (a) => ["evolve", "reject", a[0], "--reason", a[1], "--json"] },
};

// Spec §5 view 1: these need a typed confirmation at a terminal. The dashboard shows them; it never runs them.
export const TERMINAL_ONLY: Record<string, (a: readonly string[]) => string | null> = {
  "evolve.adopt": (a) => (a.length === 1 && ULID.test(a[0]) ? `sindri evolve adopt ${a[0]}` : null),
  "evolve.publish": (a) => (a.length === 0 ? "sindri evolve publish" : null),
  "channel.promote": (a) => (a.length === 1 && SHA.test(a[0]) ? `sindri channel promote ${a[0]}` : null),
  "profile.approve": (a) => (a.length === 0 ? "sindri profile approve" : null),
};

export type ActionCheck = { kind: "run"; argv: string[] } | { kind: "terminal"; command: string } | { kind: "refused"; why: string };

export function checkAction(action: unknown, args: unknown): ActionCheck {
  if (typeof action !== "string" || !Array.isArray(args) || !args.every((a): a is string => typeof a === "string")) return { kind: "refused", why: "the action and its arguments must be strings" };
  if (Object.hasOwn(TERMINAL_ONLY, action)) {
    const command = TERMINAL_ONLY[action](args);
    return command === null ? { kind: "refused", why: `the arguments for ${action} failed the check` } : { kind: "terminal", command };
  }
  if (!Object.hasOwn(SAFE_ACTIONS, action)) return { kind: "refused", why: `${action} is not an action the dashboard can run` };
  const def = SAFE_ACTIONS[action];
  if (args.length !== def.arity || !def.check(args)) return { kind: "refused", why: `the arguments for ${action} failed the check` };
  return { kind: "run", argv: def.argv(args) };
}
```

`sindri/src/dashboard/server.ts`:

```ts
import fs from "node:fs";
import http from "node:http";
import type { AddressInfo } from "node:net";
import path from "node:path";

import { SindriError, type ErrorCode } from "../errors.js";
import { checkAction, READS, type ReadName } from "./actions.js";
import { hostOk, originOk, securityHeaders } from "./security.js";
import { cookieHeader, TokenStore } from "./tokens.js";

export type SindriRunner = (args: string[]) => Promise<{ code: number; stdout: string; stderr: string }>;
export type AttachHandler = (provider: string, id: string) => Promise<{ status: number; body: unknown }>;
export interface DashboardOptions {
  port: number;
  key: Buffer;
  run: SindriRunner;
  uiDir: string;
  jsDir: string;
  notify: boolean;
  pollMs: number;
  overSsh: boolean;
  now: () => Date;
  monotonic: () => number;
  attach: AttachHandler | null;
}

// Spec §5 deep links, plus one path per view.
export const PAGE = /^\/(?:needs|agents|repos|auto|today|agent\/(?:claude|codex|cursor)\/[A-Za-z0-9._-]{1,128}|repo\/[a-z0-9][a-z0-9-]{0,38}|job\/[0-9a-z]{26})?$/;
const STATIC: Record<string, { dir: "ui" | "js"; name: string; type: string }> = {
  "/static/app.css": { dir: "ui", name: "app.css", type: "text/css; charset=utf-8" },
  "/static/favicon.svg": { dir: "ui", name: "favicon.svg", type: "image/svg+xml" },
  "/static/favicon-attn.svg": { dir: "ui", name: "favicon-attn.svg", type: "image/svg+xml" },
  "/static/app.js": { dir: "js", name: "app.js", type: "text/javascript; charset=utf-8" },
  "/static/model.js": { dir: "js", name: "model.js", type: "text/javascript; charset=utf-8" },
};
const REFUSED: ErrorCode = "SND-DASH-003";
const NOT_ALLOWED: ErrorCode = "SND-DASH-004";
const PAGE_401 = "<!doctype html><html lang=en><meta charset=utf-8><title>Sindri</title><p>No dashboard session in this browser (or the dashboard restarted): run <code>sindri dashboard --url</code> and open the printed URL.</p></html>";
const MAX_BODY = 8_192;

type ReadResult = { ok: true; exitCode: number; data: unknown } | { ok: false; exitCode: number; error: { code: string; message: string; fix: string }; cli: string };

function sendJson(res: http.ServerResponse, status: number, body: unknown): void {
  res.statusCode = status;
  res.setHeader("Content-Type", "application/json; charset=utf-8");
  res.end(JSON.stringify(body));
}

function refuse(res: http.ServerResponse, status: 401 | 403, why: string): void {
  sendJson(res, status, { ok: false, error: { code: REFUSED, message: why, fix: "run `sindri dashboard --url` and open the printed URL" } });
}

function readBody(req: http.IncomingMessage): Promise<Record<string, unknown> | null> {
  return new Promise((resolve) => {
    let size = 0;
    const chunks: Buffer[] = [];
    req.on("data", (c: Buffer) => {
      size += c.length;
      if (size <= MAX_BODY) chunks.push(c);
    });
    req.on("end", () => {
      if (size > MAX_BODY) return resolve(null);
      try {
        const v: unknown = JSON.parse(Buffer.concat(chunks).toString("utf8"));
        resolve(typeof v === "object" && v !== null && !Array.isArray(v) ? (v as Record<string, unknown>) : null);
      } catch {
        resolve(null);
      }
    });
  });
}

function parseJson(text: string): unknown {
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
}

export function createDashboardHandler(o: DashboardOptions): http.RequestListener {
  const tokens = new TokenStore(o.key, o.now);
  const cache = new Map<ReadName, { at: number; result: Promise<ReadResult> }>();

  // Spec §3 "Polling": one CLI run per read per TTL, however many tabs poll; concurrent polls share it.
  function read(name: ReadName): Promise<ReadResult> {
    const hit = cache.get(name);
    if (hit !== undefined && o.monotonic() - hit.at <= READS[name].ttlMs) return hit.result;
    const result = o.run([...READS[name].argv]).then((r): ReadResult => {
      const parsed = parseJson(r.stdout);
      if (r.code !== 2) return { ok: true, exitCode: r.code, data: parsed };
      const err = (parsed as { error?: { code?: unknown; message?: unknown; fix?: unknown } } | null)?.error;
      return typeof err?.code === "string"
        ? { ok: false, exitCode: 2, error: { code: err.code, message: String(err.message), fix: String(err.fix) }, cli: READS[name].cli }
        : { ok: false, exitCode: 2, error: { code: "SND-CLI-900", message: r.stderr.split("\n")[0], fix: "rerun with SINDRI_DEBUG=1 and report the output" }, cli: READS[name].cli };
    });
    cache.set(name, { at: o.monotonic(), result });
    return result;
  }

  async function handle(req: http.IncomingMessage, res: http.ServerResponse): Promise<void> {
    for (const [k, v] of Object.entries(securityHeaders())) res.setHeader(k, v);
    if (!hostOk(req.headers.host, o.port)) return refuse(res, 403, "wrong Host header");
    const url = new URL(req.url ?? "/", "http://127.0.0.1"); // never parsed against the request's Host
    const file = STATIC[url.pathname];
    if (req.method === "GET" && file !== undefined) {
      res.setHeader("Content-Type", file.type);
      res.end(fs.readFileSync(path.join(file.dir === "ui" ? o.uiDir : o.jsDir, file.name)));
      return;
    }
    if (req.method === "GET" && PAGE.test(url.pathname)) {
      const t = url.searchParams.get("t");
      const fresh = t === null ? null : tokens.exchange(t);
      if (fresh !== null) {
        res.statusCode = 303;
        res.setHeader("Set-Cookie", cookieHeader(fresh.id));
        res.setHeader("Location", url.pathname);
        res.end();
        return;
      }
      res.setHeader("Content-Type", "text/html; charset=utf-8");
      if (t !== null || tokens.session(req.headers.cookie) === null) {
        res.statusCode = 401;
        res.end(PAGE_401);
        return;
      }
      res.end(fs.readFileSync(path.join(o.uiDir, "index.html")));
      return;
    }
    if (!url.pathname.startsWith("/api/")) return sendJson(res, 404, { ok: false, error: { code: REFUSED, message: "not found", fix: "open the URL that sindri dashboard --url prints" } });
    const session = tokens.session(req.headers.cookie);
    if (session === null) return refuse(res, 401, "no valid session");
    if (req.method === "GET" && url.pathname === "/api/session") return sendJson(res, 200, { csrf: session.csrf, notify: o.notify, pollMs: o.pollMs, overSsh: o.overSsh });
    const m = /^\/api\/read\/([a-z]+)$/.exec(url.pathname);
    if (req.method === "GET" && m !== null && Object.hasOwn(READS, m[1])) return sendJson(res, 200, await read(m[1] as ReadName));
    if (req.method !== "POST" || (url.pathname !== "/api/action" && url.pathname !== "/api/attach")) return sendJson(res, 404, { ok: false, error: { code: REFUSED, message: "not found", fix: "open the URL that sindri dashboard --url prints" } });
    if (!originOk(req.headers.origin, req.headers.host, o.port)) return refuse(res, 403, "missing or wrong Origin");
    if (req.headers["x-sindri-csrf"] !== session.csrf) return refuse(res, 403, "missing or wrong CSRF token");
    const body = await readBody(req);
    if (body === null) return sendJson(res, 400, { ok: false, error: { code: REFUSED, message: "the request body must be a JSON object under 8 KiB", fix: "reload the page" } });
    if (url.pathname === "/api/attach") {
      if (o.attach === null) return sendJson(res, 404, { ok: false, error: { code: NOT_ALLOWED, message: "attach is not available", fix: "run the command yourself" } });
      const a = await o.attach(String(body.provider), String(body.id));
      return sendJson(res, a.status, a.body);
    }
    const c = checkAction(body.action, body.args);
    if (c.kind === "refused") return sendJson(res, 403, { ok: false, error: { code: NOT_ALLOWED, message: c.why, fix: "the dashboard runs only its fixed list of safe commands" } });
    if (c.kind === "terminal") return sendJson(res, 403, { ok: false, copy: c.command, error: { code: NOT_ALLOWED, message: "this command needs a typed confirmation at a terminal", fix: `run it yourself: ${c.command}` } });
    const r = await o.run(c.argv);
    cache.delete("fleet"); // the next poll shows the action's effect
    return sendJson(res, 200, { ok: r.code !== 2, exitCode: r.code, data: parseJson(r.stdout), stderr: r.stderr.split("\n")[0] });
  }

  return (req, res) => {
    handle(req, res).catch(() => {
      if (!res.headersSent) sendJson(res, 500, { ok: false, error: { code: "SND-CLI-900", message: "the dashboard hit an internal error", fix: "sindri doctor" } });
      else res.end();
    });
  };
}

// Spec §6 "Binding": 127.0.0.1 and nothing else; a taken port is an error naming the port and the key.
export function listenLoopback(handler: http.RequestListener, port: number): Promise<{ port: number; close(): Promise<void> }> {
  return new Promise((resolve, reject) => {
    const server = http.createServer(handler);
    server.once("error", (e: NodeJS.ErrnoException) => {
      reject(e.code === "EADDRINUSE" ? new SindriError("SND-DASH-001", `port ${port} is in use (dashboard.port)`) : e);
    });
    server.listen(port, "127.0.0.1", () => {
      const bound = (server.address() as AddressInfo).port;
      resolve({
        port: bound,
        close: () => new Promise<void>((r) => {
          server.closeAllConnections();
          server.close(() => r());
        }),
      });
    });
  });
}
```

The `/api/attach` route is wired here, but Task 11 gives it a handler. Until then `attach: null` answers 404.

`sindri/src/dashboard/command.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import { z } from "zod";

import { parseFlags } from "../args.js";
import { stateDir, type Deps } from "../deps.js";
import { SindriError } from "../errors.js";
import { dashboardDir } from "../fleet/paths.js";
import { sameStart } from "../fleet/proc.js";
import type { ProcessRunner } from "../index/io.js";
import { ledgerPath, openLedgerReadOnly } from "../ledger/db.js";
import type { Command } from "../main.js";
import { fromError, success, type CommandResult } from "../output.js";
import { approvedProfile } from "../profile/approve.js";
import { DashboardSchema } from "../profile/schema.js";
import { createDashboardHandler, PAGE, type AttachHandler, type listenLoopback } from "./server.js";
import { mintToken } from "./tokens.js";

export interface DashboardIo {
  run: ProcessRunner;
  listen: typeof listenLoopback;
  untilStopped: () => Promise<void>;
  randomKey: () => Buffer;
  node: string;
  cliPath: string;
  uiDir: string;
  jsDir: string;
  monotonic: () => number;
}

const ServerFile = z.object({ pid: z.number().int(), pidStart: z.string().nullable(), host: z.string(), port: z.number().int(), key: z.string().regex(/^[0-9a-f]{64}$/), startedAt: z.string() });
type ServerRecord = z.infer<typeof ServerFile>;

export const serverFile = (deps: Deps): string => path.join(dashboardDir(deps), "server.json");

// The running dashboard, or null: a record whose pid is gone or now belongs to another program is stale.
export function liveServer(deps: Deps): ServerRecord | null {
  let rec: ServerRecord;
  try {
    rec = ServerFile.parse(JSON.parse(fs.readFileSync(serverFile(deps), "utf8")));
  } catch {
    return null;
  }
  const sys = deps.system;
  return rec.host === sys.hostname() && sys.pidAlive(rec.pid) && sameStart(sys.pidStartTime(rec.pid), rec.pidStart) ? rec : null;
}

function profileDashboard(deps: Deps): { port: number; notify: boolean } {
  let db: ReturnType<typeof openLedgerReadOnly> = null;
  try {
    db = openLedgerReadOnly(ledgerPath(stateDir(deps)));
    return (db === null ? null : approvedProfile(deps, db))?.profile.dashboard ?? DashboardSchema.parse(undefined);
  } catch {
    return DashboardSchema.parse(undefined);
  } finally {
    db?.close();
  }
}

const urlFor = (port: number, key: Buffer, now: Date, p = "/"): string => `http://127.0.0.1:${port}${p}?t=${mintToken(key, now)}`;

export function makeDashboardCommand(io: DashboardIo, attach: (deps: Deps) => AttachHandler | null = () => null): Command {
  return async (args: string[], deps: Deps): Promise<CommandResult> => {
    const json = args.includes("--json");
    try {
      const { values } = parseFlags(args, { url: { type: "boolean" }, open: { type: "string" }, port: { type: "string" }, json: { type: "boolean" } });
      if (values.url === true || values.open !== undefined) {
        const live = liveServer(deps);
        if (live === null) throw new SindriError("SND-DASH-002", "the dashboard isn't running", { exitCode: 1 });
        const key = Buffer.from(live.key, "hex");
        if (values.open === undefined) {
          const url = urlFor(live.port, key, deps.now());
          return success(url, { url }, json);
        }
        if (!PAGE.test(values.open)) throw new SindriError("SND-CLI-002", "--open takes a dashboard path such as /needs or /agent/claude/<id>");
        const url = urlFor(live.port, key, deps.now(), values.open);
        const r = await io.run.run([deps.system.platform === "darwin" ? "open" : "xdg-open", url], { cwd: deps.home, timeoutMs: 10_000 });
        if (r.code !== 0) return success(`Couldn't open a browser; open this yourself: ${url}`, { url, opened: false }, json, 1);
        return success(`Opened ${values.open} in the browser.`, { url, opened: true }, json);
      }
      const cfg = profileDashboard(deps);
      let port = cfg.port;
      if (values.port !== undefined) {
        if (!/^\d{4,5}$/.test(values.port) || Number(values.port) < 1024 || Number(values.port) > 65535) throw new SindriError("SND-CLI-002", "--port must be a number from 1024 to 65535");
        port = Number(values.port);
      }
      const running = liveServer(deps);
      if (running !== null) return success(`The dashboard is already running on port ${running.port}.\nNext: sindri dashboard --url`, { running: true, port: running.port }, json, 1);
      const key = io.randomKey();
      const handler = createDashboardHandler({
        port, key, notify: cfg.notify, pollMs: 5000, overSsh: deps.env.SSH_CONNECTION !== undefined, now: deps.now, monotonic: io.monotonic,
        uiDir: io.uiDir, jsDir: io.jsDir, attach: attach(deps),
        run: (a) => io.run.run([io.node, io.cliPath, ...a], { cwd: deps.home, timeoutMs: 30_000 }),
      });
      const srv = await io.listen(handler, port);
      const sys = deps.system;
      fs.mkdirSync(dashboardDir(deps), { recursive: true, mode: 0o700 });
      const rec: ServerRecord = { pid: sys.pid, pidStart: sys.pidStartTime(sys.pid), host: sys.hostname(), port: srv.port, key: key.toString("hex"), startedAt: deps.now().toISOString() };
      fs.writeFileSync(serverFile(deps), JSON.stringify(rec), { mode: 0o600 });
      deps.log(`Dashboard: ${urlFor(srv.port, key, deps.now())}`);
      deps.log("Stop it with Ctrl-C. Another one-time link: sindri dashboard --url");
      try {
        await io.untilStopped();
      } finally {
        await srv.close();
        fs.rmSync(serverFile(deps), { force: true });
      }
      return success("Dashboard stopped.", { stopped: true }, json);
    } catch (e) {
      return fromError(e, json);
    }
  };
}
```

`fs.rmSync(serverFile(deps), { force: true })` removes one file, with no recursion.

`sindri/src/dashboard/io-real.ts` (excluded from coverage; exercised by the end-to-end run in Task 13):

```ts
import { randomBytes } from "node:crypto";
import path from "node:path";
import { performance } from "node:perf_hooks";
import { fileURLToPath } from "node:url";

import { realProcessRunner } from "../index/sandbox-real.js";
import type { DashboardIo } from "./command.js";
import { listenLoopback } from "./server.js";

// dist/dashboard/io-real.js → the package root is two levels up.
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");

export function realDashboardIo(): DashboardIo {
  return {
    run: realProcessRunner(),
    listen: listenLoopback,
    randomKey: () => randomBytes(32),
    node: process.execPath,
    cliPath: path.join(root, "dist", "cli.js"),
    uiDir: path.join(root, "ui"),
    jsDir: path.join(root, "dist", "ui"),
    monotonic: () => performance.now(),
    untilStopped: () =>
      new Promise<void>((resolve) => {
        process.once("SIGINT", () => resolve());
        process.once("SIGTERM", () => resolve());
      }),
  };
}
```

`sindri/src/main.ts`: import `{ makeDashboardCommand }` from `./dashboard/command.js` and `{ realDashboardIo }` from `./dashboard/io-real.js`, and add:

```ts
  dashboard: {
    summary: "The local dashboard: every agent, what needs you, sindri's own work (127.0.0.1 only)",
    usage: [
      "Usage:",
      "  sindri dashboard [--port N]      (serve on 127.0.0.1 until Ctrl-C; logs a one-time URL)",
      "  sindri dashboard --url [--json]  (a fresh one-time URL for the running dashboard)",
      "  sindri dashboard --open <path>   (open a deep link such as /agent/claude/<id> in the browser)",
    ].join("\n"),
    run: (args, deps) => makeDashboardCommand(realDashboardIo())(args, deps),
  },
```

In `sindri/vitest.config.ts`, add `"src/dashboard/io-real.ts"` to `coverage.exclude`, with the comment `// signal wait and real paths; exercised end to end (Task 13)`.

- [ ] **Step 4: Run the tests**

Run: `cd sindri && npx vitest run tests/dashboard-tokens.test.ts tests/dashboard-server.test.ts tests/dashboard-command.test.ts tests/errors.test.ts tests/main.test.ts`
Expected: PASS. Then, once for the task: `npm test && npm run typecheck && npm run test:coverage`. Expected: all PASS, coverage 100%.

- [ ] **Step 5: Commit**

```bash
git add sindri/src sindri/tests sindri/vitest.config.ts docs/sindri/errors.md
git commit -m "feat: sindri dashboard server: loopback bind, one-time tokens, Host/Origin/CSRF checks, CSP, fixed actions" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 10: The dashboard UI: five views, deep links, polling, the states, and the attention signal

**Files:**
- Create: `sindri/ui/index.html`, `sindri/ui/app.css`, `sindri/ui/favicon.svg`, `sindri/ui/favicon-attn.svg`, `sindri/ui/tsconfig.json`, `sindri/ui/src/model.ts`, `sindri/ui/src/app.ts`
- Modify:
  - `sindri/package.json`: `build` also compiles the UI; `typecheck` also checks it; add the `playwright` devDependency;
  - `sindri/vitest.config.ts`: coverage includes `ui/src/model.ts`;
  - `sindri/tsconfig.test.json`: include `ui/src/model.ts`.
- Test: `sindri/tests/ui-model.test.ts`, `sindri/tests/browser/fixture.ts`, `sindri/tests/browser/dashboard.browser.test.ts`

**Interfaces:**
- Consumes: the HTTP surface from Task 9 (`/api/session`, `/api/read/<name>`, `POST /api/action`, `POST /api/attach`); the `FleetReport` JSON from Task 8; `EMPTY` (Task 8), which the tests compare with the UI's copy; `createDashboardHandler`, `listenLoopback`, `mintToken` (Task 9) for the browser tests.
- Produces:
  - `ui/src/model.ts` (pure, no DOM, 100% covered):
    - `EMPTY`, `ATTN` (the attention colours and their backgrounds), `PIPELINE`;
    - `route(pathname): Route`, `landing(needCount)`, `tabTitle(n)`, `announcement(prev, next)`;
    - `visible(s, keepNewlines?)`, which shows every control or invisible character as `\u{XXXX}`;
    - `age(ms)`, `staleText(lastOk, now, failures)`;
    - `marks(needs, seen): Map<key, "pulse" | "attn" | "calm">`, `arrivals(prevKeys, needs)`, `toNotify(needs, notified, enabled)`;
    - `filterSessions(sessions, filters)`, `groupSessions(sessions)`, `contrast(a, b)`.
  - `ui/src/app.ts`: the DOM glue. It renders with `textContent` only and polls with one `setTimeout` chain. Every list is a keyed sync that never re-creates a row, never moves the row that holds focus, and never calls `focus()` or scrolls during a poll.
  - Behaviour (spec §5 and §5.1):
    - **Landing:** opens on Needs you when it isn't empty, otherwise on Agents. Deep links: `/agent/<p>/<id>` (that row expanded, plus a `done:` ack when it carries the done marker), `/repo/<name>` and `/job/<id>`.
    - **States:** loading skeletons on the first load only; empty sentences; an error banner (code, message and the CLI command); "updated Ns ago"; after 3 failed polls, "sindri not responding: run `sindri doctor`"; on a 401, one session-ended banner, and polling stops.
    - **Attention:** a new row slides in. `approval` and `input` rows pulse, with the nav item and the header band, until the row has been 50% visible for 1 s in a focused tab, or is focused, clicked or acted on. `failed` and `limited` rows show the colour with no motion. Under `prefers-reduced-motion`, a static 3 px outline replaces the motion.
    - **Announcements and signals:** one polite `aria-live` region, the tab title `(N) Needs you · Sindri`, a favicon that blinks once to its attention version, and an OS notification for `approval` and `input` only, once per item, when `dashboard.notify` is on and the builder granted permission.

- [ ] **Step 1: Write the failing tests**

`sindri/tests/ui-model.test.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { EMPTY as CLI_EMPTY } from "../src/fleet/sentences.js";
import { age, announcement, arrivals, ATTN, contrast, EMPTY, filterSessions, groupSessions, landing, marks, route, staleText, tabTitle, toNotify, visible } from "../ui/src/model.js";

const need = (key: string, state: string, urgent = false) => ({ key, state, urgent, title: "t", reason: "r" });

describe("routes and landing", () => {
  it("maps every deep link and view path, and lets / fall to the landing rule", () => {
    expect(route("/agent/claude/abc-1")).toEqual({ view: "agents", agent: { provider: "claude", id: "abc-1" }, repo: null, job: null, explicit: true });
    expect(route("/repo/acme-web")).toMatchObject({ view: "repos", repo: "acme-web" });
    expect(route("/job/01hzzzzzzzzzzzzzzzzzzzzzzz")).toMatchObject({ view: "auto", job: "01hzzzzzzzzzzzzzzzzzzzzzzz" });
    expect(route("/today")).toMatchObject({ view: "today", explicit: true });
    expect(route("/")).toMatchObject({ view: "needs", explicit: false });
    expect(route("/agent/slack/x")).toMatchObject({ explicit: false });
    expect(landing(2)).toBe("needs");
    expect(landing(0)).toBe("agents");
  });
});

describe("signals", () => {
  it("titles the tab, announces changes once, and says how fresh the data is", () => {
    expect(tabTitle(3)).toBe("(3) Needs you · Sindri");
    expect(tabTitle(0)).toBe("Sindri");
    expect(announcement(null, 2)).toBeNull();
    expect(announcement(2, 2)).toBeNull();
    expect(announcement(0, 1)).toBe("1 now needs you");
    expect(announcement(1, 3)).toBe("3 now need you");
    expect(announcement(3, 0)).toBe("Nothing needs you now");
    expect(staleText(null, 10_000, 0)).toBe("loading…");
    expect(staleText(4_000, 10_400, 1)).toBe("updated 6s ago");
    expect(staleText(4_000, 3_000, 0)).toBe("updated 0s ago"); // the clock moved back
    expect(staleText(4_000, 10_000, 3)).toBe("sindri not responding: run `sindri doctor`");
  });

  it("pulses unseen approvals and inputs, colours failed and limited, keeps the rest calm", () => {
    const m = marks([need("a", "approval", true), need("i", "input", true), need("f", "failed"), need("l", "limited"), need("d", "won"), need("s", "approval", true)], new Set(["s"]));
    expect(Object.fromEntries(m)).toEqual({ a: "pulse", i: "pulse", f: "attn", l: "attn", d: "calm", s: "attn" });
    expect(arrivals(null, [need("a", "approval")])).toEqual([]);
    expect(arrivals(new Set(["a"]), [need("a", "approval"), need("b", "input")])).toEqual(["b"]);
    expect(toNotify([need("a", "approval", true), need("f", "failed")], new Set(), true).map((n) => n.key)).toEqual(["a"]);
    expect(toNotify([need("a", "approval", true)], new Set(["a"]), true)).toEqual([]);
    expect(toNotify([need("a", "approval", true)], new Set(), false)).toEqual([]);
  });
});

describe("text and formatting", () => {
  it("shows control and invisible characters escaped, keeping newlines only when asked", () => {
    expect(visible("a\u001b[31mb​‮c")).toBe("a\\u{001B}[31mb\\u{200B}\\u{202E}c");
    expect(visible("x\ny")).toBe("x\\u{000A}y");
    expect(visible("x\ny", true)).toBe("x\ny");
    expect([age(null), age(5_000), age(120_000), age(7_200_000), age(172_800_000)]).toEqual(["unknown", "5 s", "2 min", "2 h", "2 d"]);
  });

  it("uses the CLI's empty sentences word for word", () => {
    expect(EMPTY.needs(2, 5)).toBe(CLI_EMPTY.needs(2, 5));
    expect(EMPTY.agents).toBe(CLI_EMPTY.agents);
    expect(EMPTY.jobs).toBe(CLI_EMPTY.jobs);
    expect(EMPTY.repos).toBe(CLI_EMPTY.repos);
  });

  it("keeps the attention colour readable in light and dark mode, and the stylesheet uses exactly these values", () => {
    expect(contrast(ATTN.light, ATTN.lightBg)).toBeGreaterThanOrEqual(4.5);
    expect(contrast(ATTN.light, ATTN.lightTint)).toBeGreaterThanOrEqual(4.5);
    expect(contrast(ATTN.dark, ATTN.darkBg)).toBeGreaterThanOrEqual(4.5);
    expect(contrast(ATTN.dark, ATTN.darkTint)).toBeGreaterThanOrEqual(4.5);
    expect(contrast("#000000", "#ffffff")).toBeCloseTo(21, 5);
    const css = fs.readFileSync(path.resolve(import.meta.dirname, "../ui/app.css"), "utf8");
    for (const c of Object.values(ATTN)) expect(css.toLowerCase()).toContain(c);
    expect(css).not.toMatch(/url\(\s*["']?https?:/); // no external fonts or images
  });
});

describe("sessions", () => {
  const s = (o: Partial<{ provider: string; state: string; repo: string | null; worktree: string | null; cwd: string; alive: boolean }>) => ({ provider: "claude", state: "working", repo: "acme-web", worktree: null, cwd: "/x", alive: true, ...o });
  it("filters by provider, state and repo, an empty filter meaning all", () => {
    const xs = [s({}), s({ provider: "codex", state: "ready" }), s({ repo: null })];
    expect(filterSessions(xs, { provider: "", state: "", repo: "" })).toHaveLength(3);
    expect(filterSessions(xs, { provider: "codex", state: "", repo: "" })).toHaveLength(1);
    expect(filterSessions(xs, { provider: "", state: "working", repo: "acme-web" })).toHaveLength(1);
    expect(filterSessions(xs, { provider: "", state: "", repo: "Other" })).toHaveLength(1);
  });

  it("groups by repo, then worktree, with sessions outside onboarded repos under Other", () => {
    const g = groupSessions([s({}), s({ worktree: "/w/feature" }), s({ repo: null, worktree: "/elsewhere" }), s({ repo: null, cwd: "/tmp/x" }), s({})]);
    expect(g.map((x) => [x.repo, x.worktrees.map((w) => [w.worktree, w.sessions.length])])).toEqual([
      ["acme-web", [[null, 2], ["/w/feature", 1]]],
      ["Other", [["/elsewhere", 1], ["/tmp/x", 1]]],
    ]);
  });
});
```

`sindri/tests/browser/fixture.ts`:

```ts
import { execFileSync } from "node:child_process";
import { randomBytes } from "node:crypto";
import path from "node:path";
import { createRequire } from "node:module";

import { createDashboardHandler, listenLoopback } from "../../src/dashboard/server.js";
import { mintToken } from "../../src/dashboard/tokens.js";
import { fakeSindri, freePort } from "../dashboard-fixtures.js";
import { tempDir } from "../helpers.js";

export const UI_DIR = path.resolve(import.meta.dirname, "../../ui");

// Compile the UI once per run into a temp dir (the real build writes dist/ui).
export function compileUi(): string {
  const out = tempDir("sindri-ui-js-");
  const tsc = createRequire(import.meta.url).resolve("typescript/bin/tsc");
  execFileSync(process.execPath, [tsc, "-p", path.join(UI_DIR, "tsconfig.json"), "--outDir", out], { stdio: "inherit" });
  return out;
}

export const NOW_ISO = () => new Date().toISOString();
export const ago = (s: number): string => new Date(Date.now() - s * 1000).toISOString();

export function session(id: string, o: Record<string, unknown> = {}) {
  return {
    provider: "claude", id, parentId: null, cwd: "/Users/dev/acme.web", repo: "acme-web", worktree: null, host: "terminal", pid: 1, tty: "ttys001", alive: true,
    startedAt: ago(900), updatedAt: ago(30), title: `Task ${id}`, activity: "Bash: npm test", signals: {}, estimated: false, transcript: null,
    turnStartedAt: ago(120), promptTimes: [ago(120)], costUsd: 0.5, tokens: 1200, timeline: [{ ts: ago(120), kind: "prompt", text: "do it" }],
    state: "working", reason: "busy", children: [], childCount: 0, doneUnread: false, turnAgeMs: 120_000, ...o,
  };
}

export function needFor(s: { provider: string; id: string; title: string }, state: string, o: Record<string, unknown> = {}) {
  return { key: `session:${s.provider}:${s.id}`, kind: "session", state, title: s.title, reason: "Bash: npm publish", repo: "acme-web", since: ago(60), link: `/agent/${s.provider}/${s.id}`, action: { type: "attach", provider: s.provider, id: s.id }, urgent: state === "approval" || state === "input", ackable: false, ...o };
}

export function report(o: { needs?: unknown[]; sessions?: unknown[] } = {}) {
  const sessions = o.sessions ?? [];
  const needs = o.needs ?? [];
  return {
    v: 1, generatedAt: NOW_ISO(), profile: "approved",
    counts: { live: sessions.length, working: sessions.filter((x) => (x as { state: string }).state === "working").length, needsYou: needs.length, urgent: needs.filter((n) => (n as { urgent: boolean }).urgent).length },
    needs, sessions, jobs: [], history: [], heavyLock: { held: false, holder: null, queue: [] },
    repos: [{ name: "acme-web", path: "/Users/dev/acme.web", index: { builtAt: ago(3600), ageMs: 3_600_000, stale: false, commit: "abc", files: 10, symbols: 40, behind: 0, lastBuild: null }, observe: { at: ago(600), backlog: 3 }, scopeRuns: [], prs: [], worktrees: [] }],
    scopeRuns: [], decisions: [], pipeline: { proposals: {}, channel: { stable: null, next: null } }, health: { doctor: null, judge: null },
    sources: [{ name: "claude", on: true, ok: true, ms: 3, count: sessions.length, truncated: false, error: null }],
    today: { since: ago(36_000), agents: sessions.length, turns: 2, tokens: 2400, costUsd: 1, budgetTokens: null, warnings: [], judge: null, doctor: { state: null, at: null } },
  };
}

// A real dashboard server on a free port whose fleet read answers from `current()` on every poll.
export async function dashboard(current: () => unknown, o: { jsDir: string; pollMs?: number; notify?: boolean; fail?: () => boolean }) {
  const port = await freePort();
  const key = randomBytes(32);
  let tick = 0;
  const run = fakeSindri((args) => (o.fail?.() === true ? { code: 2, stdout: "", stderr: "boom" } : args[0] === "fleet" ? { code: 0, stdout: JSON.stringify(current()) } : { stdout: "[]" }));
  const srv = await listenLoopback(createDashboardHandler({
    port, key, run, uiDir: UI_DIR, jsDir: o.jsDir, notify: o.notify ?? false, pollMs: o.pollMs ?? 200, overSsh: false,
    now: () => new Date(), monotonic: () => (tick += 10_000), attach: null, // every poll misses the cache
  }), port);
  return { ...srv, key, run, url: (p = "/") => `http://127.0.0.1:${port}${p}?t=${mintToken(key, new Date())}` };
}
```

`sindri/tests/browser/dashboard.browser.test.ts`:

```ts
import { chromium, type Browser, type BrowserContext, type Page } from "playwright";
import { afterAll, afterEach, beforeAll, describe, expect, it, vi } from "vitest";

import { compileUi, dashboard, needFor, report, session } from "./fixture.js";

vi.setConfig({ testTimeout: 30_000 }); // real Chromium; the long-open test alone runs about 7 s

let browser: Browser;
let jsDir: string;
const cleanups: (() => Promise<void>)[] = [];
beforeAll(async () => {
  jsDir = compileUi();
  browser = await chromium.launch({ headless: true });
}, 60_000);
afterAll(async () => browser.close());
afterEach(async () => {
  for (const c of cleanups.splice(0)) await c();
});

async function open(current: () => unknown, o: { path?: string; reducedMotion?: boolean; notify?: boolean; pollMs?: number; fail?: () => boolean; init?: string } = {}): Promise<{ page: Page; context: BrowserContext; srv: Awaited<ReturnType<typeof dashboard>> }> {
  const srv = await dashboard(current, { jsDir, pollMs: o.pollMs, notify: o.notify, fail: o.fail });
  const context = await browser.newContext({ reducedMotion: o.reducedMotion === true ? "reduce" : "no-preference", viewport: { width: 1200, height: 700 } });
  // Headless tabs report focus inconsistently; tests decide it (window.__focused), like the Notification stub below.
  await context.addInitScript(UNFOCUSED);
  if (o.init !== undefined) await context.addInitScript(o.init);
  const page = await context.newPage();
  cleanups.push(async () => {
    await context.close();
    await srv.close();
  });
  await page.goto(srv.url(o.path ?? "/"));
  return { page, context, srv };
}

const A = session("a1");
const UNFOCUSED = "window.__focused = false; Document.prototype.hasFocus = function () { return window.__focused === true; };";
const visibleView = (page: Page) => page.locator("main > section:not([hidden]) h1").innerText();

describe("landing, deep links and states", () => {
  it("opens on Needs you when something needs you, otherwise on Agents", async () => {
    const busy = await open(() => report({ sessions: [{ ...A, state: "approval" }], needs: [needFor(A, "approval")] }));
    await expect.poll(() => visibleView(busy.page)).toBe("Needs you");
    expect(new URL(busy.page.url()).search).toBe(""); // the token left the URL
    const calm = await open(() => report({ sessions: [A] }));
    await expect.poll(() => visibleView(calm.page)).toBe("Agents");
    const empty = await open(() => report({}), { path: "/agents" });
    await expect.poll(() => empty.page.locator("main section:not([hidden]) .empty").innerText()).toBe("No agents are running or were active in the last 24 hours.");
  });

  it("opens a deep link to an agent with its row expanded", async () => {
    const { page } = await open(() => report({ sessions: [A, session("b2")] }), { path: "/agent/claude/a1" });
    await expect.poll(() => page.locator('[data-key="s:claude:a1"] button[aria-expanded]').getAttribute("aria-expanded")).toBe("true");
    await expect.poll(() => page.locator('[data-key="s:claude:a1"] .timeline').innerText()).toContain("do it");
  });

  it("shows skeletons on the first load only, an error banner with the CLI command, and 'not responding' after 3 failures", async () => {
    let failing = false;
    const { page } = await open(() => report({ sessions: [A] }), { fail: () => failing, pollMs: 150 });
    await expect.poll(() => page.locator(".skeleton").count()).toBe(0);
    failing = true;
    await expect.poll(() => page.locator("#banner").innerText()).toContain("SND-CLI-900 boom (more: sindri fleet)");
    expect(await page.locator(".skeleton").count()).toBe(0);
    await page.route("**/api/read/fleet", (r) => r.abort());
    await expect.poll(() => page.locator("#banner").innerText(), { timeout: 5000 }).toBe("sindri not responding: run `sindri doctor`");
  });
});

describe("the needs-you signal", () => {
  it("announces arrivals politely, and polling never moves focus, scroll position or typed input", async () => {
    const won = { key: "proposal-won:01hyyyyyyyyyyyyyyyyyyyyyyy", kind: "decision", state: "won", title: "proposal won its comparison", reason: "run at a terminal", repo: null, since: new Date(0).toISOString(), link: "/auto", action: { type: "terminal", command: "sindri evolve adopt 01hyyyyyyyyyyyyyyyyyyyyyyy" }, secondary: { type: "reject", id: "01hyyyyyyyyyyyyyyyyyyyyyyy" }, urgent: false, ackable: false };
    const fails = Array.from({ length: 30 }, (_, i) => ({ ...needFor(session(`f${i}`), "failed"), key: `failure:claude:f${i}:failed:x`, kind: "failure", ackable: true }));
    let needs: unknown[] = [won, ...fails];
    const { page } = await open(() => report({ needs }));
    const reason = page.locator('[data-key="proposal-won:01hyyyyyyyyyyyyyyyyyyyyyyy"] input[aria-label="Reason"]');
    await page.locator('[data-key="proposal-won:01hyyyyyyyyyyyyyyyyyyyyyyy"] summary').click();
    await reason.click();
    await reason.fill("half a reason");
    await page.evaluate(() => window.scrollTo(0, 300));
    const before = await reason.evaluate((e) => e.getBoundingClientRect().top);
    needs = [needFor(A, "approval"), won, ...fails];
    await expect.poll(() => page.locator("#announce").innerText()).toBe("32 now need you");
    expect(await page.evaluate(() => document.activeElement?.getAttribute("aria-label"))).toBe("Reason");
    expect(await reason.inputValue()).toBe("half a reason");
    expect(Math.abs((await reason.evaluate((e) => e.getBoundingClientRect().top)) - before)).toBeLessThanOrEqual(2);
  });

  it("pulses a new approval with the nav item and the band, keeps pulsing while the tab is unfocused, and stops when the row is focused", async () => {
    let needs: unknown[] = [];
    const { page } = await open(() => report({ sessions: [A], needs }), { path: "/needs" });
    await expect.poll(() => page.locator("main section:not([hidden]) .empty").count()).toBe(1);
    needs = [needFor(A, "approval")];
    const row = page.locator('[data-key="session:claude:a1"]');
    await expect.poll(() => row.getAttribute("class")).toContain("pulse");
    expect(await row.evaluate((e) => getComputedStyle(e, "::before").animationName)).toBe("sindri-pulse");
    expect(await page.locator("#nav-needs").getAttribute("class")).toContain("pulse");
    expect(await page.locator("#band").getAttribute("class")).toContain("pulse");
    await page.waitForTimeout(1500); // in view, but the tab isn't focused: not seen yet
    expect(await row.getAttribute("class")).toContain("pulse");
    await row.focus();
    await expect.poll(() => row.getAttribute("class")).not.toContain("pulse");
    expect(await row.getAttribute("class")).toContain("attn");
    expect(await row.evaluate((e) => getComputedStyle(e, "::before").animationName)).toBe("none");
    expect(await page.locator("#band").getAttribute("class")).not.toContain("pulse");
  });

  it("stops pulsing by itself once the row has been in view for a second in a focused tab", async () => {
    let needs: unknown[] = [];
    const { page } = await open(() => report({ sessions: [A], needs }), { path: "/needs" });
    await page.evaluate(() => { (window as unknown as { __focused: boolean }).__focused = true; });
    needs = [needFor(A, "approval")];
    const row = page.locator('[data-key="session:claude:a1"]');
    await expect.poll(() => row.getAttribute("class")).toContain("pulse");
    await expect.poll(() => row.getAttribute("class"), { timeout: 3000 }).not.toContain("pulse");
  });

  it("shows failed and limited in the attention colour without motion", async () => {
    const { page } = await open(() => report({ needs: [{ ...needFor(A, "failed"), key: "failure:claude:a1:failed:x", kind: "failure" }] }), { path: "/needs" });
    const row = page.locator('[data-key="failure:claude:a1:failed:x"]');
    await expect.poll(() => row.getAttribute("class")).toContain("attn");
    expect(await row.evaluate((e) => getComputedStyle(e, "::before").animationName)).toBe("none");
  });

  it("under prefers-reduced-motion shows a static outline instead of motion", async () => {
    const { page } = await open(() => report({ needs: [needFor(A, "approval")] }), { reducedMotion: true, path: "/needs" });
    const row = page.locator('[data-key="session:claude:a1"]');
    await expect.poll(() => row.getAttribute("class")).toContain("pulse");
    expect(await row.evaluate((e) => getComputedStyle(e, "::before").animationName)).toBe("none");
    expect(await row.evaluate((e) => getComputedStyle(e).outlineStyle)).toBe("solid");
    expect(await page.locator("#band").evaluate((e) => getComputedStyle(e).animationName)).toBe("none");
  });

  it("sets the tab title and the favicon from the count", async () => {
    let needs: unknown[] = [needFor(A, "approval")];
    const { page } = await open(() => report({ needs }));
    await expect.poll(() => page.title()).toBe("(1) Needs you · Sindri");
    await expect.poll(() => page.locator("#favicon").getAttribute("href")).toBe("/static/favicon-attn.svg");
    needs = [];
    await expect.poll(() => page.title()).toBe("Sindri");
    await expect.poll(() => page.locator("#favicon").getAttribute("href")).toBe("/static/favicon.svg");
  });

  it("fires one OS notification per approval or input, none for failures", async () => {
    const init = "window.__notes = []; window.Notification = class { static permission = 'granted'; static requestPermission() { return Promise.resolve('granted'); } constructor(t, o) { window.__notes.push([t, o.tag]); } };";
    const { page } = await open(() => report({ needs: [needFor(A, "approval"), { ...needFor(session("x"), "failed"), key: "failure:claude:x:failed:y", kind: "failure" }] }), { notify: true, init, pollMs: 100 });
    await expect.poll(() => page.evaluate(() => (window as unknown as { __notes: unknown[] }).__notes.length)).toBe(1);
    await page.waitForTimeout(500); // several more polls
    expect(await page.evaluate(() => (window as unknown as { __notes: [string, string][] }).__notes)).toEqual([["Sindri: approval", "session:claude:a1"]]);
  });
});

describe("keyboard and untrusted text", () => {
  it("reaches the skip link, every view and the row's action by keyboard, in visible order", async () => {
    const { page } = await open(() => report({ sessions: [{ ...A, state: "approval" }], needs: [needFor(A, "approval")] }));
    await expect.poll(() => page.locator('[data-key="session:claude:a1"]').count()).toBe(1);
    const order: string[] = [];
    for (let i = 0; i < 9; i++) {
      await page.keyboard.press("Tab");
      order.push(await page.evaluate(() => (document.activeElement?.getAttribute("aria-label") ?? document.activeElement?.textContent ?? "").trim()));
    }
    const prefixes = ["Skip to content", "Needs you", "Agents", "Repos", "Automatic work", "Today", "approval"];
    prefixes.forEach((p, i) => expect(order[i].startsWith(p), `tab stop ${i}: ${order[i]}`).toBe(true));
    expect(order).toContain("Attach");
  });

  it("keeps HTML, ANSI and OSC payloads inert and visible as text", async () => {
    const evil = session("e1", { title: '<img src=x onerror="window.__pwned=1">', activity: "\u001b]52;c;aGk=\u0007\u001b[31mred", timeline: [{ ts: null, kind: "text", text: "<script>window.__pwned=2</script>" }] });
    const { page } = await open(() => report({ sessions: [evil] }), { path: "/agent/claude/e1" });
    const row = page.locator('[data-key="s:claude:e1"]');
    await expect.poll(() => row.innerText()).toContain('<img src=x onerror="window.__pwned=1">');
    expect(await row.innerText()).toContain("\\u{001B}]52;c;aGk=\\u{0007}\\u{001B}[31mred");
    expect(await page.locator("main img, main script").count()).toBe(0);
    expect(await page.evaluate(() => (window as unknown as { __pwned?: number }).__pwned)).toBeUndefined();
  });
});

describe("a tab left open for days (Review Focus 5)", () => {
  it("never stacks polls or grows the DOM, and after a restart says so once and stops polling", async () => {
    const { page, srv } = await open(() => report({ sessions: [A, session("b2")], needs: [needFor(A, "approval")] }), { pollMs: 25 });
    let polls = 0;
    page.on("request", (r) => {
      if (r.url().endsWith("/api/read/fleet")) polls += 1;
    });
    await page.waitForTimeout(500);
    const nodes = await page.evaluate(() => document.querySelectorAll("*").length);
    const start = polls;
    await page.waitForTimeout(5000);
    expect(polls - start).toBeGreaterThan(50);
    expect(polls - start).toBeLessThanOrEqual(5000 / 25 + 2); // one request per interval at most: no stacking
    expect(Math.abs((await page.evaluate(() => document.querySelectorAll("*").length)) - nodes)).toBeLessThanOrEqual(2);
    await srv.close();
    await page.route("**/api/read/fleet", async (r) => r.fulfill({ status: 401, contentType: "application/json", body: '{"ok":false,"error":{"code":"SND-DASH-003","message":"no valid session","fix":"run `sindri dashboard --url`"}}' }));
    await expect.poll(() => page.locator("#banner").innerText()).toContain("run `sindri dashboard --url`");
    const stopped = polls;
    await page.waitForTimeout(500);
    expect(polls - stopped).toBeLessThanOrEqual(1);
  });
});
```

The restart half routes the fleet read to a 401 instead of restarting on the same port, because Playwright keeps the page's origin. What it proves is the client's behaviour on a 401. That the server answers 401 after a restart is pinned in Task 9 (`refuses … a token or cookie from before a restart`).

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd sindri && npm install --save-dev playwright@^1.48.0 && npx playwright install chromium`. This is the one new dependency; skip the install if `node_modules/playwright` is already there. `npx playwright install chromium` is a no-op when the browser is cached.
Run: `cd sindri && npx vitest run tests/ui-model.test.ts tests/browser/dashboard.browser.test.ts`
Expected: FAIL with `Failed to load url ../ui/src/model.js` and, for the browser file, `error TS5058: The specified path does not exist: '…/ui/tsconfig.json'` from `compileUi`.

- [ ] **Step 3: Implement**

`sindri/ui/tsconfig.json`:

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "ES2022",
    "moduleResolution": "Bundler",
    "lib": ["ES2022", "DOM", "DOM.Iterable"],
    "strict": true,
    "skipLibCheck": true,
    "rootDir": "src",
    "outDir": "../dist/ui",
    "declaration": false,
    "sourceMap": false
  },
  "include": ["src/**/*"]
}
```

`sindri/package.json`: set `"build": "tsc && tsc -p ui/tsconfig.json"` and `"typecheck": "tsc --noEmit && tsc --noEmit -p tsconfig.test.json && tsc --noEmit -p ui/tsconfig.json"`. The devDependency `"playwright": "^1.48.0"` comes from the install above.
`sindri/tsconfig.test.json`: `"include": ["src/**/*", "tests/**/*", "ui/src/model.ts", "vitest.config.ts"]`.
`sindri/vitest.config.ts`: `include: ["src/**/*.ts", "ui/src/model.ts"]`. Add the comment `// ui/src/app.ts is DOM glue, exercised in real Chromium by tests/browser (the ui-evidence precedent); it is outside include, so it needs no exclude`.

`sindri/ui/index.html`:

```html
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Sindri</title>
<link rel="icon" id="favicon" href="/static/favicon.svg" type="image/svg+xml">
<link rel="stylesheet" href="/static/app.css">
<script type="module" src="/static/app.js"></script>
</head>
<body>
<a class="skip" href="#main">Skip to content</a>
<header id="band" class="band">
  <span class="brand">Sindri</span>
  <span id="updated" class="updated">loading…</span>
</header>
<nav aria-label="Views">
  <a href="/needs" data-view="needs" id="nav-needs">Needs you <span class="count" data-count="needs"></span></a>
  <a href="/agents" data-view="agents">Agents <span class="count" data-count="agents"></span></a>
  <a href="/repos" data-view="repos">Repos</a>
  <a href="/auto" data-view="auto">Automatic work</a>
  <a href="/today" data-view="today">Today</a>
</nav>
<div id="banner" class="banner" role="alert" hidden></div>
<main id="main" tabindex="-1"></main>
<div id="announce" class="visually-hidden" aria-live="polite" aria-atomic="true"></div>
</body>
</html>
```

`sindri/ui/favicon.svg`:

```xml
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32"><rect width="32" height="32" rx="7" fill="#1a1c21"/><path d="M9 22l7-12 7 12z" fill="#e8eaee"/></svg>
```

`sindri/ui/favicon-attn.svg` (the same mark with the attention dot):

```xml
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32"><rect width="32" height="32" rx="7" fill="#1a1c21"/><path d="M9 22l7-12 7 12z" fill="#e8eaee"/><circle cx="25" cy="7" r="6" fill="#c2410c" stroke="#ffffff" stroke-width="1.5"/></svg>
```

`sindri/ui/app.css`:

```css
/* Sindri dashboard. One attention colour (--attn), used only for needs-you (spec §5.1). No external
   fonts or images; no inline styles (the CSP forbids them). */
:root {
  color-scheme: light dark;
  --bg: #ffffff;
  --fg: #1a1c21;
  --muted: #5b616e;
  --line: #d9dce3;
  --attn: #c2410c;
  --attn-tint: #fff4ed;
  --focus: #2457c5;
  font: 15px/1.45 system-ui, -apple-system, "Segoe UI", sans-serif;
}
@media (prefers-color-scheme: dark) {
  :root {
    --bg: #111318;
    --fg: #e8eaee;
    --muted: #a2a8b4;
    --line: #2c313b;
    --attn: #fb923c;
    --attn-tint: #2a1a10;
    --focus: #8ab4ff;
  }
}
body { margin: 0; background: var(--bg); color: var(--fg); }
a { color: inherit; }
:focus-visible { outline: 3px solid var(--focus); outline-offset: 2px; }
.skip { position: absolute; left: -999px; top: 8px; }
.skip:focus { left: 8px; z-index: 10; background: var(--bg); padding: 4px 8px; }
.visually-hidden { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }
.band { display: flex; justify-content: space-between; align-items: baseline; padding: 10px 16px; border-bottom: 4px solid transparent; }
.band.attn { border-bottom-color: var(--attn); }
.brand { font-weight: 700; }
.updated { color: var(--muted); font-size: 13px; }
nav { display: flex; flex-wrap: wrap; gap: 4px; padding: 0 16px; border-bottom: 1px solid var(--line); }
nav a { padding: 10px 12px; text-decoration: none; border-bottom: 3px solid transparent; }
nav a[aria-current="page"] { border-bottom-color: var(--fg); font-weight: 600; }
nav a.attn .count { color: var(--attn); font-weight: 700; }
.banner { padding: 8px 16px; border-bottom: 2px solid var(--attn); }
main { padding: 8px 16px 48px; }
h1 { font-size: 18px; margin: 12px 0; }
h2 { font-size: 15px; margin: 18px 0 4px; }
.empty { color: var(--muted); }
.rows { list-style: none; margin: 0; padding: 0; }
.row { position: relative; display: grid; grid-template-columns: 10rem 1fr auto; gap: 4px 12px; padding: 10px 12px 10px 20px; border-bottom: 1px solid var(--line); }
.row::before { content: ""; position: absolute; left: 0; top: 0; bottom: 0; width: 6px; background: transparent; }
.row.attn { background: var(--attn-tint); }
.row.attn::before, .row.pulse::before { background: var(--attn); }
.row.pulse::before { animation: sindri-pulse 1.2s ease-in-out infinite; }
.band.pulse, nav a.pulse { animation: sindri-pulse 1.6s ease-in-out infinite; }
.row.arrive { animation: sindri-arrive 240ms ease-out; }
.row.group { display: block; padding: 14px 0 4px; font-weight: 600; border-bottom: none; }
.row.group.wt { padding-left: 12px; font-weight: 500; color: var(--muted); }
.state { font-weight: 600; }
.meta { color: var(--muted); font-size: 13px; }
.actions { display: flex; flex-wrap: wrap; gap: 6px; align-items: center; }
.detail { grid-column: 1 / -1; }
.timeline { white-space: pre-wrap; font: 13px/1.4 ui-monospace, SFMono-Regular, Menlo, monospace; margin: 4px 0; }
.copy input { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; width: 28rem; max-width: 100%; }
.filters { display: flex; flex-wrap: wrap; gap: 12px; margin: 8px 0; }
.skeleton { height: 2.6rem; margin: 8px 0; border-radius: 4px; background: var(--line); }
dl.today { display: grid; grid-template-columns: 14rem 1fr; gap: 6px 16px; }
button, select, input { font: inherit; }
button { padding: 4px 10px; }
@keyframes sindri-pulse { 0%, 100% { opacity: 1; } 50% { opacity: 0.25; } }
@keyframes sindri-arrive { from { transform: translateX(-12px); opacity: 0; } to { transform: none; opacity: 1; } }
@media (prefers-reduced-motion: reduce) {
  .row.pulse::before, .band.pulse, nav a.pulse, .row.arrive { animation: none; }
  .row.pulse { outline: 3px solid var(--attn); outline-offset: -3px; }
  .band.pulse, nav a.pulse { outline: 3px solid var(--attn); }
}
/* Colour pairs checked in tests/ui-model.test.ts: #c2410c on #ffffff and #fff4ed; #fb923c on #111318 and #2a1a10. */
```

`sindri/ui/src/model.ts`:

```ts
// Pure view logic for the dashboard: app.ts uses it in the browser, tests/ui-model.test.ts covers it in full.
// No DOM types here, so it type-checks under the package's Node config too.

// Spec §5 "Empty: the same sentence the CLI prints" (src/fleet/sentences.ts; the tests compare them).
export const EMPTY = {
  needs: (working: number, live: number): string => `Nothing needs you. ${working} working, ${live} live.`,
  agents: "No agents are running or were active in the last 24 hours.",
  jobs: "No sindri jobs have run yet.",
  repos: "No onboarded repos. Onboard one with: sindri repo onboard <path>",
} as const;

// Spec §5.1: one amber-to-red attention colour, checked for contrast in light and dark mode.
export const ATTN = { light: "#c2410c", lightBg: "#ffffff", lightTint: "#fff4ed", dark: "#fb923c", darkBg: "#111318", darkTint: "#2a1a10" } as const;
export const PIPELINE = ["proposed", "evaluating", "won", "lost", "insufficient-corpus", "staged", "held", "published", "merged", "adopted", "rejected"] as const;

export type ViewName = "needs" | "agents" | "repos" | "auto" | "today";
export interface Route {
  view: ViewName;
  agent: { provider: string; id: string } | null;
  repo: string | null;
  job: string | null;
  explicit: boolean;
}

export function route(pathname: string): Route {
  const base = { agent: null, repo: null, job: null, explicit: true };
  const a = /^\/agent\/(claude|codex|cursor)\/([A-Za-z0-9._-]{1,128})$/.exec(pathname);
  if (a !== null) return { ...base, view: "agents", agent: { provider: a[1], id: a[2] } };
  const r = /^\/repo\/([a-z0-9][a-z0-9-]{0,38})$/.exec(pathname);
  if (r !== null) return { ...base, view: "repos", repo: r[1] };
  const j = /^\/job\/([0-9a-z]{26})$/.exec(pathname);
  if (j !== null) return { ...base, view: "auto", job: j[1] };
  const v = /^\/(needs|agents|repos|auto|today)$/.exec(pathname);
  if (v !== null) return { ...base, view: v[1] as ViewName };
  return { ...base, view: "needs", explicit: false }; // "/": landing() decides once data arrives
}

export const landing = (needs: number): ViewName => (needs > 0 ? "needs" : "agents");
export const tabTitle = (n: number): string => (n > 0 ? `(${n}) Needs you · Sindri` : "Sindri");

export function announcement(prev: number | null, next: number): string | null {
  if (prev === null || prev === next) return null;
  if (next === 0) return "Nothing needs you now";
  return `${next} now ${next === 1 ? "needs" : "need"} you`;
}

// The collector already escapes; this is the browser's own guard (spec §6 "Untrusted text").
const INVISIBLE = /[\p{Cc}\p{Cf}\p{Zl}\p{Zp}\p{Default_Ignorable_Code_Point}]/gu;
export const visible = (s: string, keepNewlines = false): string =>
  s.replace(INVISIBLE, (c) => (keepNewlines && c === "\n" ? c : `\\u{${(c.codePointAt(0) as number).toString(16).toUpperCase().padStart(4, "0")}}`));

export function age(ms: number | null): string {
  if (ms === null) return "unknown";
  if (ms < 60_000) return `${Math.floor(ms / 1000)} s`;
  if (ms < 3_600_000) return `${Math.floor(ms / 60_000)} min`;
  if (ms < 86_400_000) return `${Math.floor(ms / 3_600_000)} h`;
  return `${Math.floor(ms / 86_400_000)} d`;
}

export function staleText(lastOk: number | null, now: number, failures: number): string {
  if (failures >= 3) return "sindri not responding: run `sindri doctor`";
  if (lastOk === null) return "loading…";
  return `updated ${Math.max(0, Math.round((now - lastOk) / 1000))}s ago`;
}

export interface NeedLite {
  key: string;
  state: string;
  urgent: boolean;
  title: string;
  reason: string;
}

// Spec §5.1 "Urgency": approval and input pulse until seen; failed and limited get the colour without motion.
export function marks(needs: readonly NeedLite[], seen: ReadonlySet<string>): Map<string, "pulse" | "attn" | "calm"> {
  const m = new Map<string, "pulse" | "attn" | "calm">();
  for (const n of needs) m.set(n.key, n.urgent && !seen.has(n.key) ? "pulse" : n.urgent || n.state === "failed" || n.state === "limited" ? "attn" : "calm");
  return m;
}

export const arrivals = (prev: ReadonlySet<string> | null, needs: readonly NeedLite[]): string[] => (prev === null ? [] : needs.filter((n) => !prev.has(n.key)).map((n) => n.key));
export const toNotify = (needs: readonly NeedLite[], notified: ReadonlySet<string>, enabled: boolean): NeedLite[] => (enabled ? needs.filter((n) => n.urgent && !notified.has(n.key)) : []);

export interface SessionLite {
  provider: string;
  state: string;
  repo: string | null;
  worktree: string | null;
  cwd: string;
}
export const repoLabel = (s: SessionLite): string => s.repo ?? "Other";

export function filterSessions<T extends SessionLite>(xs: readonly T[], f: { provider: string; state: string; repo: string }): T[] {
  return xs.filter((s) => (f.provider === "" || s.provider === f.provider) && (f.state === "" || s.state === f.state) && (f.repo === "" || repoLabel(s) === f.repo));
}

// Spec §5 view 2: grouped by repo, then worktree; outside onboarded repos, by git top-level or cwd (spec decision 17).
export function groupSessions<T extends SessionLite>(xs: readonly T[]): { repo: string; worktrees: { worktree: string | null; sessions: T[] }[] }[] {
  const out: { repo: string; worktrees: { worktree: string | null; sessions: T[] }[] }[] = [];
  for (const s of xs) {
    const repo = repoLabel(s);
    const wt = s.repo === null ? (s.worktree ?? s.cwd) : s.worktree;
    let g = out.find((x) => x.repo === repo);
    if (g === undefined) out.push((g = { repo, worktrees: [] }));
    let w = g.worktrees.find((x) => x.worktree === wt);
    if (w === undefined) g.worktrees.push((w = { worktree: wt, sessions: [] }));
    w.sessions.push(s);
  }
  return out;
}

function luminance(hex: string): number {
  const ch = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255).map((c) => (c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4));
  return 0.2126 * ch[0] + 0.7152 * ch[1] + 0.0722 * ch[2];
}

export function contrast(a: string, b: string): number {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (hi + 0.05) / (lo + 0.05);
}
```

`sindri/ui/src/app.ts`:

```ts
// The dashboard's DOM glue (spec §5). Every agent-written string reaches the page through text() (textContent,
// after visible()); there is no innerHTML anywhere. Polling updates rows in place and never moves focus,
// scroll position or form input.
import { age, announcement, arrivals, EMPTY, filterSessions, groupSessions, landing, marks, PIPELINE, route, staleText, tabTitle, toNotify, visible, type NeedLite, type Route, type ViewName } from "./model.js";

type Action =
  | { type: "attach"; provider: string; id: string }
  | { type: "terminal"; command: string }
  | { type: "run"; action: string; args: string[]; label: string }
  | { type: "reject"; id: string }
  | { type: "open"; url: string };
interface Need extends NeedLite { kind: string; repo: string | null; since: string; link: string; action: Action; secondary?: Action; ackable: boolean }
interface Sess {
  provider: string; id: string; title: string; state: string; reason: string; host: string; activity: string; repo: string | null; worktree: string | null;
  cwd: string; alive: boolean; estimated: boolean; turnAgeMs: number | null; tokens: number | null; costUsd: number | null; childCount: number;
  children: Sess[]; timeline: { ts: string | null; kind: string; text: string }[]; doneUnread: boolean;
}
interface Job { id: string; kind: string; state: string; startedAt: string; endedAt: string | null; exitCode: number | null; summary: string; argv: string[] }
interface Repo {
  name: string; index: { builtAt: string | null; ageMs: number | null; stale: boolean; files: number | null; symbols: number | null; behind: number | null; lastBuild: Job | null };
  observe: { at: string | null; backlog: number } | null; scopeRuns: { id: string; subject: string; status: string; ts: string }[];
  prs: { number: number; title: string; url: string; reviewDecision: string | null; mergeState: string }[]; worktrees: string[];
}
interface Report {
  counts: { live: number; working: number; needsYou: number; urgent: number }; needs: Need[]; sessions: Sess[]; jobs: Job[]; history: Job[]; repos: Repo[];
  heavyLock: { held: boolean; holder: { kind: string; pid: number; ageMs: number | null } | null; queue: { kind: string; pid: number }[] };
  scopeRuns: { id: string; subject: string; status: string; ts: string }[]; pipeline: { proposals: Record<string, number>; channel: { stable: string | null; next: { sha: string; soakedDays: number } | null } };
  health: { judge: { status: string; failures24h: number | null } | null };
  today: { agents: number; turns: number; tokens: number; costUsd: number; budgetTokens: number | null; warnings: string[]; doctor: { state: string | null; at: string | null } };
}
type Read<T> = { ok: true; exitCode: number; data: T } | { ok: false; error: { code: string; message: string }; cli: string };

const TITLES: Record<ViewName, string> = { needs: "Needs you", agents: "Agents", repos: "Repos", auto: "Automatic work", today: "Today" };
const byId = <T extends HTMLElement>(id: string): T => document.getElementById(id) as T;

function text(e: Element, s: string, keepNewlines = false): void {
  const v = visible(s, keepNewlines);
  if (e.textContent !== v) e.textContent = v;
}
function el<K extends keyof HTMLElementTagNameMap>(tag: K, attrs: Record<string, string> = {}, content?: string): HTMLElementTagNameMap[K] {
  const e = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs)) e.setAttribute(k, v);
  if (content !== undefined) text(e, content);
  return e;
}

const st = {
  csrf: "", notify: false, pollMs: 5000, report: null as Report | null, route: route(location.pathname) as Route,
  seen: new Set<string>(), notified: new Set<string>(), prevKeys: null as Set<string> | null, prevCount: null as number | null,
  lastOk: null as number | null, failures: 0, stopped: false, expanded: new Set<string>(), filters: { provider: "", state: "", repo: "" },
  notificationsSent: 0, first: true, deepLinkDone: false,
};

async function call<T>(path: string, init?: RequestInit): Promise<{ status: number; body: T | null }> {
  const ctl = new AbortController();
  const timer = setTimeout(() => ctl.abort(), 8000);
  try {
    const r = await fetch(path, { ...init, signal: ctl.signal, credentials: "same-origin" });
    let body: T | null = null;
    try {
      body = (await r.json()) as T;
    } catch {
      body = null;
    }
    return { status: r.status, body };
  } finally {
    clearTimeout(timer);
  }
}
const post = <T>(path: string, body: unknown) => call<T>(path, { method: "POST", headers: { "content-type": "application/json", "x-sindri-csrf": st.csrf }, body: JSON.stringify(body) });

function banner(msg: string | null): void {
  const b = byId<HTMLDivElement>("banner");
  b.hidden = msg === null;
  text(b, msg ?? "");
}

function sessionEnded(): void {
  st.stopped = true;
  banner("No dashboard session in this tab (the dashboard may have restarted): run `sindri dashboard --url` and open the new link.");
}

function markSeen(key: string): void {
  if (st.seen.has(key)) return;
  st.seen.add(key);
  render([]);
}

// "Seen" (spec §5.1): half the row in view for a second while this tab is visible and focused.
const seenTimers = new Map<Element, number>();
const observer = new IntersectionObserver((entries) => {
  for (const en of entries) {
    const key = (en.target as HTMLElement).dataset.key as string;
    if (en.isIntersecting && en.intersectionRatio >= 0.5) {
      if (!seenTimers.has(en.target)) {
        seenTimers.set(en.target, window.setTimeout(() => {
          seenTimers.delete(en.target);
          if (document.visibilityState === "visible" && document.hasFocus()) markSeen(key);
        }, 1000));
      }
    } else {
      clearTimeout(seenTimers.get(en.target));
      seenTimers.delete(en.target);
    }
  }
}, { threshold: [0, 0.5, 1] });

// Keyed rows, updated in place. New rows join; gone rows leave; the order is fixed only when no row in
// the list holds focus (moving a focused node would drop its focus).
function syncRows<T>(list: HTMLElement, items: readonly T[], key: (t: T) => string, make: (t: T) => HTMLElement, fill: (e: HTMLElement, t: T) => void): void {
  const existing = new Map<string, HTMLElement>();
  for (const c of Array.from(list.children) as HTMLElement[]) existing.set(c.dataset.key as string, c);
  const want = items.map((t) => {
    const k = key(t);
    let e = existing.get(k);
    if (e === undefined) {
      e = make(t);
      e.dataset.key = k;
    }
    fill(e, t);
    return e;
  });
  const keep = new Set(want);
  for (const c of existing.values()) if (!keep.has(c)) c.remove();
  const current = Array.from(list.children);
  if (want.length === current.length && want.every((e, i) => current[i] === e)) return;
  if (want.some((e) => e.isConnected && e.contains(document.activeElement))) {
    for (const e of want) if (!e.isConnected) list.append(e);
    return;
  }
  list.append(...want);
}

function section(v: ViewName): HTMLElement {
  let s = document.querySelector<HTMLElement>(`main > section[data-view="${v}"]`);
  if (s === null) {
    s = el("section", { "data-view": v, "aria-labelledby": `h-${v}` });
    s.append(el("h1", { id: `h-${v}` }, TITLES[v]), el("p", { class: "empty" }), el("ul", { class: "rows" }));
    byId("main").append(s);
  }
  return s;
}
const listOf = (s: HTMLElement): HTMLElement => s.querySelector("ul.rows") as HTMLElement;
function empty(s: HTMLElement, msg: string | null): void {
  const p = s.querySelector(".empty") as HTMLElement;
  p.hidden = msg === null;
  text(p, msg ?? "");
}

function copyBox(command: string): HTMLElement {
  const box = el("span", { class: "copy" });
  const input = el("input", { readonly: "", "aria-label": "Command to run at a terminal" });
  input.value = command;
  const btn = el("button", { type: "button" }, "Copy");
  btn.addEventListener("click", () => void navigator.clipboard?.writeText(command));
  box.append(el("span", { class: "meta" }, "Run at a terminal: "), input, btn);
  return box;
}

function actionEl(a: Action, key: string, status: HTMLElement): HTMLElement {
  const acted = (): void => markSeen(key);
  if (a.type === "terminal") return copyBox(a.command);
  if (a.type === "open") {
    const link = el("a", { href: a.url, target: "_blank", rel: "noopener noreferrer" }, "Open PR");
    link.addEventListener("click", acted);
    return link;
  }
  if (a.type === "reject") {
    const d = el("details");
    const input = el("input", { "aria-label": "Reason", maxlength: "200" });
    const btn = el("button", { type: "button" }, "Reject");
    btn.addEventListener("click", async () => {
      acted();
      const r = await post<{ ok: boolean; stderr?: string; error?: { message: string } }>("/api/action", { action: "evolve.reject", args: [a.id, input.value] });
      text(status, r.body?.ok === true ? "Rejected." : `Not rejected: ${r.body?.error?.message ?? r.body?.stderr ?? "error"}`);
    });
    d.append(el("summary", {}, "Reject…"), input, btn);
    return d;
  }
  const label = a.type === "attach" ? "Attach" : a.label;
  const btn = el("button", { type: "button" }, label);
  btn.addEventListener("click", async () => {
    acted();
    if (a.type === "attach") {
      const r = await post<{ ok: boolean; message?: string; copy?: string }>("/api/attach", { provider: a.provider, id: a.id });
      text(status, r.body?.message ?? "");
      if (r.body?.copy !== undefined) status.append(copyBox(r.body.copy));
    } else {
      const r = await post<{ ok: boolean; stderr?: string }>("/api/action", { action: a.action, args: a.args });
      text(status, r.body?.ok === true ? "Done." : (r.body?.stderr ?? "Already handled, or not allowed."));
    }
  });
  return btn;
}

function needRow(n: Need): HTMLElement {
  const li = el("li", { class: "row", tabindex: "0" });
  li.append(el("span", { class: "state" }), el("span", { class: "what" }), el("span", { class: "actions" }), el("span", { class: "meta status detail" }));
  li.addEventListener("focusin", () => markSeen(n.key));
  li.addEventListener("click", () => markSeen(n.key));
  observer.observe(li);
  return li;
}

function fillNeed(li: HTMLElement, n: Need, mark: string, arrived: boolean): void {
  li.classList.toggle("pulse", mark === "pulse");
  li.classList.toggle("attn", mark === "attn");
  if (arrived) {
    li.classList.add("arrive");
    setTimeout(() => li.classList.remove("arrive"), 400);
  }
  const [stateEl, what, actions, status] = Array.from(li.children) as HTMLElement[];
  text(stateEl, n.state);
  text(what, `${n.title} · ${n.repo ?? "no repo"} · waiting ${age(Math.max(0, Date.now() - Date.parse(n.since)))} · ${n.reason}`);
  const sig = JSON.stringify([n.action, n.secondary ?? null]);
  if (actions.dataset.sig !== sig) {
    actions.replaceChildren(actionEl(n.action, n.key, status), ...(n.secondary === undefined ? [] : [actionEl(n.secondary, n.key, status)]));
    actions.dataset.sig = sig;
  }
}

function renderNeeds(arrived: string[]): void {
  const r = st.report as Report;
  const s = section("needs");
  empty(s, r.needs.length === 0 ? EMPTY.needs(r.counts.working, r.counts.live) : null);
  const m = marks(r.needs, st.seen);
  syncRows(listOf(s), r.needs, (n) => n.key, needRow, (li, n) => fillNeed(li, n, m.get(n.key) as string, arrived.includes(n.key)));
}

type AgentItem = { kind: "repo"; label: string } | { kind: "wt"; label: string; repo: string } | { kind: "s"; s: Sess };

function sessionRow(it: AgentItem): HTMLElement {
  if (it.kind !== "s") return el("li", { class: `row group${it.kind === "wt" ? " wt" : ""}` });
  const li = el("li", { class: "row" });
  const toggle = el("button", { type: "button", "aria-expanded": "false" }, "Details");
  const k = `${it.s.provider}:${it.s.id}`;
  toggle.addEventListener("click", () => {
    if (st.expanded.has(k)) st.expanded.delete(k);
    else st.expanded.add(k);
    render([]);
  });
  li.append(el("span", { class: "state" }), el("span", { class: "what" }), el("span", { class: "actions" }), el("div", { class: "detail", hidden: "" }));
  (li.children[2] as HTMLElement).append(toggle, actionEl({ type: "attach", provider: it.s.provider, id: it.s.id }, `s:${k}`, li.children[3] as HTMLElement));
  return li;
}

function fillSession(li: HTMLElement, it: AgentItem): void {
  if (it.kind !== "s") return text(li, it.label);
  const s = it.s;
  const [stateEl, what, actions, detail] = Array.from(li.children) as HTMLElement[];
  text(stateEl, `${s.state}${s.estimated ? " (estimated)" : ""}${s.doneUnread ? " · done" : ""}`);
  const cost = s.costUsd === null ? "" : ` · $${s.costUsd.toFixed(2)}`;
  const tokens = s.tokens === null ? "" : ` · ${s.tokens} tokens`;
  const kids = s.childCount > 0 ? ` · ${s.childCount} subagent${s.childCount === 1 ? "" : "s"}` : "";
  text(what, `${s.provider} · ${s.host} · ${s.title} · ${s.activity} · turn ${age(s.turnAgeMs)}${tokens}${cost}${kids}`);
  const open = st.expanded.has(`${s.provider}:${s.id}`);
  (actions.firstElementChild as HTMLElement).setAttribute("aria-expanded", String(open));
  detail.hidden = !open;
  if (open) {
    let pre = detail.querySelector("pre.timeline");
    if (pre === null) detail.append((pre = el("pre", { class: "timeline", "aria-label": "Recent timeline" })));
    text(pre, [...s.timeline.map((t) => `${t.kind.padEnd(10)} ${t.text}`), ...s.children.map((c) => `subagent   ${c.state}: ${c.title}`)].join("\n"), true);
  }
}

function filterBar(s: HTMLElement): HTMLElement {
  let bar = s.querySelector<HTMLElement>(".filters");
  if (bar !== null) return bar;
  bar = el("div", { class: "filters" });
  for (const name of ["provider", "state", "repo"] as const) {
    const label = el("label", {}, `${name[0].toUpperCase()}${name.slice(1)} `);
    const sel = el("select", { "data-filter": name });
    sel.append(el("option", { value: "" }, "all"));
    sel.addEventListener("change", () => {
      st.filters[name] = sel.value;
      render([]);
    });
    label.append(sel);
    bar.append(label);
  }
  s.insertBefore(bar, s.querySelector("p.empty"));
  return bar;
}

function syncOptions(bar: HTMLElement, name: "provider" | "state" | "repo", values: string[]): void {
  const sel = bar.querySelector(`select[data-filter="${name}"]`) as HTMLSelectElement;
  const have = Array.from(sel.options).map((o) => o.value).slice(1);
  if (JSON.stringify(have) === JSON.stringify(values)) return;
  const keep = sel.value;
  sel.replaceChildren(el("option", { value: "" }, "all"), ...values.map((v) => el("option", { value: v }, v)));
  sel.value = values.includes(keep) ? keep : "";
}

function renderAgents(): void {
  const r = st.report as Report;
  const s = section("agents");
  const bar = filterBar(s);
  syncOptions(bar, "provider", [...new Set(r.sessions.map((x) => x.provider))].sort());
  syncOptions(bar, "state", [...new Set(r.sessions.map((x) => x.state))].sort());
  syncOptions(bar, "repo", [...new Set(r.sessions.map((x) => x.repo ?? "Other"))].sort());
  const shown = filterSessions(r.sessions, st.filters);
  empty(s, r.sessions.length === 0 ? EMPTY.agents : null);
  const items: AgentItem[] = [];
  for (const g of groupSessions(shown)) {
    items.push({ kind: "repo", label: g.repo });
    for (const w of g.worktrees) {
      if (w.worktree !== null) items.push({ kind: "wt", label: w.worktree, repo: g.repo });
      for (const x of w.sessions) items.push({ kind: "s", s: x });
    }
  }
  const keyOf = (it: AgentItem): string => (it.kind === "repo" ? `repo:${it.label}` : it.kind === "wt" ? `wt:${it.repo}:${it.label}` : `s:${it.s.provider}:${it.s.id}`);
  syncRows(listOf(s), items, keyOf, sessionRow, fillSession);
}

function lineRow(): HTMLElement {
  return el("li", { class: "row" });
}

function renderRepos(): void {
  const r = st.report as Report;
  const s = section("repos");
  const repos = st.route.repo === null ? r.repos : r.repos.filter((x) => x.name === st.route.repo);
  empty(s, repos.length === 0 ? EMPTY.repos : null);
  syncRows(listOf(s), repos, (x) => x.name, lineRow, (li, x) => {
    const ix = x.index;
    const lines = [
      `${x.name}`,
      `index: ${ix.builtAt === null ? "never built" : `built ${age(ix.ageMs)} ago${ix.stale ? " (stale)" : ""}`} · last build ${ix.lastBuild?.state ?? "unknown"} · ${ix.files ?? "?"} files · ${ix.symbols ?? "?"} symbols · ${ix.behind ?? "?"} commits behind HEAD`,
      x.observe === null ? "" : `observe: ${x.observe.at === null ? "never" : `${age(Date.now() - Date.parse(x.observe.at))} ago`} · backlog ${x.observe.backlog}`,
      ...x.scopeRuns.map((sr) => `scope run ${sr.status}: ${sr.subject}`),
      ...x.prs.map((p) => `PR #${p.number} ${p.reviewDecision ?? "no review"} · ${p.mergeState}: ${p.title}`),
      ...x.worktrees.map((w) => `live agents in ${w}`),
    ].filter((l) => l !== "");
    text(li, lines.join("\n"), true);
  });
}

function renderAuto(): void {
  const r = st.report as Report;
  const s = section("auto");
  const job = st.route.job === null ? null : (r.history.find((j) => j.id === st.route.job) ?? null);
  const p = r.pipeline;
  const lock = r.heavyLock.holder === null ? (r.heavyLock.held ? "held (no holder record)" : "free") : `held by ${r.heavyLock.holder.kind} (pid ${r.heavyLock.holder.pid}) for ${age(r.heavyLock.holder.ageMs)}`;
  const rows: { key: string; text: string }[] = [
    ...(job === null ? [] : [{ key: `job-detail:${job.id}`, text: `${job.kind}: ${job.state}\nsindri ${job.argv.join(" ")}\nstarted ${job.startedAt}${job.endedAt === null ? "" : `, ended ${job.endedAt}`} · exit ${job.exitCode ?? "none"}\n${job.summary}` }]),
    { key: "lock", text: `heavy-job lock: ${lock}; ${r.heavyLock.queue.length} waiting${r.heavyLock.queue.map((q) => `\n  waiting: ${q.kind} (pid ${q.pid})`).join("")}` },
    { key: "pipeline", text: `proposals: ${PIPELINE.filter((k) => (p.proposals[k] ?? 0) > 0).map((k) => `${k} ${p.proposals[k]}`).join(" → ") || "none"}` },
    { key: "channel", text: `channels: stable ${p.channel.stable?.slice(0, 12) ?? "none"} · next ${p.channel.next === null ? "none" : `${p.channel.next.sha.slice(0, 12)} soaked ${p.channel.next.soakedDays} of 3 days`}` },
    ...r.jobs.map((j) => ({ key: `job:${j.id}`, text: `${j.state}  ${j.kind}  ${age(Date.now() - Date.parse(j.endedAt ?? j.startedAt))} ago  ${j.summary}` })),
    ...r.scopeRuns.map((sr) => ({ key: `scope:${sr.id}`, text: `scope run ${sr.status}: ${sr.subject}` })),
  ];
  empty(s, r.jobs.length === 0 ? EMPTY.jobs : null);
  syncRows(listOf(s), rows, (x) => x.key, lineRow, (li, x) => text(li, x.text, true));
}

function renderToday(): void {
  const r = st.report as Report;
  const s = section("today");
  const t = r.today;
  const budget = t.budgetTokens === null ? "no daily budget set" : `${Math.round((t.tokens / t.budgetTokens) * 100)}% of ${t.budgetTokens}`;
  const rows = [
    `cost: $${t.costUsd.toFixed(2)} · tokens ${t.tokens} (${budget})`,
    `agents active today: ${t.agents} · turns: ${t.turns}`,
    `judge: ${r.health.judge === null ? "unknown" : `${r.health.judge.status}${r.health.judge.failures24h === null ? "" : `, ${r.health.judge.failures24h} failures in 24 h`}`}`,
    `doctor: ${t.doctor.state ?? "not run yet"}${t.doctor.at === null ? "" : ` (${t.doctor.at})`}`,
    `notifications sent from this tab: ${st.notificationsSent}`,
    ...t.warnings.map((w) => `warning: ${w}`),
  ];
  empty(s, null);
  syncRows(listOf(s), rows.map((x, i) => ({ k: String(i), x })), (o) => o.k, lineRow, (li, o) => text(li, o.x));
  if (st.notify && "Notification" in window && Notification.permission === "default" && s.querySelector("button.notify") === null) {
    const b = el("button", { type: "button", class: "notify" }, "Turn on notifications for approvals and questions");
    b.addEventListener("click", () => void Notification.requestPermission().then(() => b.remove()));
    s.append(b);
  }
}

function favicon(attn: boolean, blink: boolean): void {
  const link = byId<HTMLLinkElement>("favicon");
  const set = (on: boolean): void => link.setAttribute("href", on ? "/static/favicon-attn.svg" : "/static/favicon.svg");
  set(attn);
  if (attn && blink) {
    setTimeout(() => set(false), 300);
    setTimeout(() => set(byId("nav-needs").classList.contains("attn")), 600);
  }
}

function notify(needs: readonly Need[]): void {
  if (!st.notify || !("Notification" in window) || Notification.permission !== "granted") return;
  for (const n of toNotify(needs, st.notified, true)) {
    st.notified.add(n.key);
    new Notification(`Sindri: ${n.state}`, { body: visible(`${n.title}: ${n.reason}`), tag: n.key });
    st.notificationsSent += 1;
  }
}

function render(arrived: string[]): void {
  const r = st.report;
  if (r === null) return;
  document.querySelectorAll(".skeleton").forEach((s) => s.remove());
  const m = marks(r.needs, st.seen);
  const pulsing = [...m.values()].includes("pulse");
  const nav = byId("nav-needs");
  nav.classList.toggle("attn", r.needs.length > 0);
  nav.classList.toggle("pulse", pulsing);
  byId("band").classList.toggle("attn", r.needs.length > 0);
  byId("band").classList.toggle("pulse", pulsing);
  text(document.querySelector('[data-count="needs"]') as HTMLElement, r.needs.length > 0 ? `(${r.needs.length})` : "");
  text(document.querySelector('[data-count="agents"]') as HTMLElement, `(${r.counts.live})`);
  for (const a of Array.from(document.querySelectorAll<HTMLAnchorElement>("nav a"))) {
    if (a.dataset.view === st.route.view) a.setAttribute("aria-current", "page");
    else a.removeAttribute("aria-current");
  }
  renderNeeds(arrived);
  renderAgents();
  renderRepos();
  renderAuto();
  renderToday();
  for (const s of Array.from(document.querySelectorAll<HTMLElement>("main > section"))) s.hidden = s.dataset.view !== st.route.view;
  const target = st.route.agent;
  if (target !== null && !st.deepLinkDone) {
    st.deepLinkDone = true;
    const row = document.querySelector<HTMLElement>(`[data-key="s:${target.provider}:${target.id}"]`);
    const s = r.sessions.find((x) => x.provider === target.provider && x.id === target.id);
    if (row !== null && s !== undefined) {
      st.expanded.add(`${target.provider}:${target.id}`);
      fillSession(row, { kind: "s", s });
      row.scrollIntoView({ block: "center" }); // the builder navigated here: not a poll
      if (s.doneUnread) void post("/api/action", { action: "fleet.ack", args: [`done:${s.provider}:${s.id}`] });
    }
  }
}

function update(r: Report): void {
  const arrived = arrivals(st.prevKeys, r.needs);
  st.prevKeys = new Set(r.needs.map((n) => n.key));
  for (const k of [...st.seen]) if (!st.prevKeys.has(k)) st.seen.delete(k); // a need that comes back pulses again
  const msg = announcement(st.prevCount, r.needs.length);
  if (msg !== null) text(byId("announce"), msg);
  st.prevCount = r.needs.length;
  document.title = tabTitle(r.needs.length);
  st.report = r;
  if (st.first && !st.route.explicit) st.route = { ...st.route, view: landing(r.needs.length) };
  st.first = false;
  render(arrived);
  favicon(r.needs.length > 0, arrived.length > 0);
  notify(r.needs);
}

// One setTimeout chain: the next poll is scheduled only after this one ends, so polls never stack.
async function poll(): Promise<void> {
  if (st.stopped) return;
  try {
    const r = await call<Read<Report>>("/api/read/fleet");
    if (r.status === 401) return sessionEnded();
    if (r.status !== 200 || r.body === null) throw new Error(`HTTP ${r.status}`);
    st.failures = 0;
    st.lastOk = Date.now();
    if (r.body.ok) {
      banner(null);
      update(r.body.data);
    } else banner(`${r.body.error.code} ${r.body.error.message} (more: ${r.body.cli})`);
  } catch {
    st.failures += 1;
    if (st.failures >= 3) banner("sindri not responding: run `sindri doctor`");
  }
  text(byId("updated"), staleText(st.lastOk, Date.now(), st.failures));
  setTimeout(() => void poll(), st.pollMs);
}

function navigate(href: string): void {
  history.pushState({}, "", href);
  st.route = route(href);
  st.deepLinkDone = false;
  render([]);
}

async function boot(): Promise<void> {
  const main = byId("main");
  for (let i = 0; i < 3; i++) main.append(el("div", { class: "skeleton", "aria-hidden": "true" }));
  const s = await call<{ csrf: string; notify: boolean; pollMs: number }>("/api/session");
  if (s.status !== 200 || s.body === null) return sessionEnded();
  st.csrf = s.body.csrf;
  st.notify = s.body.notify;
  st.pollMs = s.body.pollMs;
  for (const a of Array.from(document.querySelectorAll<HTMLAnchorElement>("nav a"))) {
    a.addEventListener("click", (e) => {
      e.preventDefault();
      navigate(a.getAttribute("href") as string);
    });
  }
  window.addEventListener("popstate", () => {
    st.route = route(location.pathname);
    render([]);
  });
  void poll();
}

void boot();
```

Notes for the implementer:
- `Read<T>` mirrors Task 9's `ReadResult`.
- `PIPELINE.filter(...).join(" → ") || "none"` falls back to "none" when the pipeline is empty.
- `fillSession` returns `void` in both branches; `text()` returns void.
- The browser fixture's `report()` passes `signals: {}` because the UI never reads `signals`.

- [ ] **Step 4: Run the tests**

Run: `cd sindri && npx vitest run tests/ui-model.test.ts tests/browser/dashboard.browser.test.ts`
Expected: PASS (the browser file runs about 20 s in real headless Chromium).
Then, once for the task: `npm test && npm run typecheck && npm run test:coverage`. Expected: all PASS; coverage 100%, now including `ui/src/model.ts`.
Then `npm run build && ls dist/ui`. Expected: `app.js  model.js`.

- [ ] **Step 5: Commit**

```bash
git add sindri/ui sindri/tests sindri/package.json sindri/package-lock.json sindri/vitest.config.ts sindri/tsconfig.test.json
git commit -m "feat: sindri dashboard UI with five views, deep links and the needs-you attention signal" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 11: Attach: resolver and launcher, never resuming a live session

**Files:**
- Create: `sindri/src/dashboard/attach.ts`
- Modify:
  - `sindri/src/dashboard/command.ts` (the attach factory gets the CLI runner);
  - `sindri/src/main.ts` (pass the real attach factory);
  - `sindri/src/errors.ts` (`SND-DASH-005`).
- Generated: `docs/sindri/errors.md`
- Test: `sindri/tests/dashboard-attach.test.ts`

**Interfaces:**
- Consumes: `FleetReport`, `FleetSession` (Task 8); `isUnder` (Task 1); `isProvider`, `HOSTS` (Task 1); `SindriRunner`, `AttachHandler`, `createDashboardHandler`'s `POST /api/attach` (Task 9); `ProcessRunner`.
- Produces:
  - Error code `SND-DASH-005` "Sindri couldn't safely work out how to attach to that session." (fix: run the command the dashboard shows yourself, in a terminal).
  - Types: `AttachTarget` (the slice of a session attach needs), `LaunchStep = { argv } | { warp: { cwd; command } }`, and `AttachPlan = { kind: "focus" | "resume"; steps; label } | { kind: "copy"; command; why; candidates }`.
  - `AttachEnv { overSsh; platform; roots; exists; apps; onPath }`.
  - `resolveAttach(target, env): AttachPlan`, following spec §7's table:
    - **Liveness:** alive means focus the host; ended, crashed or interrupted means resume. A live session is never resumed, and `assertSafe` throws if a plan would.
    - **Checks:** ids must match the provider's format; paths must exist inside a known repo or worktree.
    - **When unsure** (the host is uncertain, the app or path is missing, or the dashboard runs over ssh), it gives the candidates and a copyable command.
  - `assertSafe(target, plan)`, `warpYaml(cwd, command)`.
  - `Launcher { run(argv): Promise<number>; writeWarpConfig(yaml): string }` and `launch(plan, launcher, fallback)`. The only commands it runs are `open -a <app> [path]`, `open warp://launch/sindri-attach.yaml`, `cursor <path>` and `tmux`, the last only inside a Warp launch config.
  - `realLauncher(deps, run)` (the Warp config goes to `~/.warp/launch_configurations/sindri-attach.yaml`, 0600), `onPath(deps, bin)`, `appExists(deps, name)`.
  - `makeAttachHandler(deps, run, overSsh, launcher, apps?, exists?): AttachHandler`. It re-reads `sindri fleet --json` before every attach, so it never acts on a stale poll, and it calls `assertSafe` before launching.

- [ ] **Step 1: Write the failing tests**

`sindri/tests/dashboard-attach.test.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { assertSafe, launch, makeAttachHandler, resolveAttach, warpYaml, type AttachEnv, type AttachPlan, type AttachTarget, type Launcher } from "../src/dashboard/attach.js";
import { HOSTS, PROVIDERS } from "../src/fleet/types.js";
import { fakeSindri } from "./dashboard-fixtures.js";
import { makeDeps, tempDir } from "./helpers.js";

const ID = "11111111-1111-4111-8111-111111111111";
const REPO = "/Users/dev/acme.web";
const target = (o: Partial<AttachTarget> = {}): AttachTarget => ({ provider: "claude", id: ID, alive: true, state: "working", host: "warp", cwd: REPO, tty: "ttys003", title: "Fix the login form", worktree: null, originator: null, ...o });
const env = (o: Partial<AttachEnv> = {}): AttachEnv => ({ overSsh: false, platform: "darwin", roots: [REPO], exists: () => true, apps: (n) => n === "Warp" || n === "T3 Code (Alpha)" || n === "Cursor" || n === "Codex", onPath: () => false, ...o });
const words = (p: AttachPlan): string => JSON.stringify(p);

describe("resolveAttach (spec §7)", () => {
  it("brings a live Warp session's window to the front, labelled with its tty and title", () => {
    const p = resolveAttach(target(), env());
    expect(p).toEqual({ kind: "focus", steps: [{ argv: ["open", "-a", "Warp"] }], label: 'Brought Warp to the front: the session is the tab on ttys003, titled "Fix the login form".' });
  });

  it("resumes an ended, crashed or interrupted Claude session in a new Warp tab at its cwd", () => {
    for (const state of ["ended", "crashed", "interrupted"]) {
      expect(resolveAttach(target({ alive: false, state }), env())).toEqual({ kind: "resume", steps: [{ warp: { cwd: REPO, command: `claude --resume ${ID}` } }], label: `Opened a new Warp tab in ${REPO} running claude --resume ${ID}.` });
    }
  });

  it("shows the command instead when the path isn't inside a known repo or worktree, or doesn't exist", () => {
    expect(resolveAttach(target({ alive: false, state: "ended", cwd: "/tmp/elsewhere" }), env())).toEqual({ kind: "copy", command: `cd '/tmp/elsewhere' && claude --resume ${ID}`, why: "its folder isn't inside a repo or worktree sindri knows, so sindri won't open it for you", candidates: [] });
    expect(resolveAttach(target({ alive: false, state: "ended" }), env({ exists: () => false }))).toMatchObject({ kind: "copy" });
    expect(resolveAttach(target({ alive: false, state: "ended" }), env({ apps: () => false }))).toMatchObject({ kind: "copy", why: "Warp isn't installed; run this in a terminal" });
  });

  it("focuses T3 Code for its sessions, alive or not, naming the thread and worktree", () => {
    const t3 = target({ host: "t3", worktree: "/Users/dev/.t3/worktrees/acme/t1" });
    const alive = resolveAttach(t3, env());
    expect(alive).toEqual({ kind: "focus", steps: [{ argv: ["open", "-a", "T3 Code (Alpha)"] }], label: 'Brought T3 Code (Alpha) to the front: open the thread "Fix the login form" in /Users/dev/.t3/worktrees/acme/t1 (T3 has no deep link to a thread).' });
    expect(resolveAttach({ ...t3, alive: false, state: "ended" }, env())).toEqual({ ...alive, label: `${alive.kind === "focus" ? alive.label : ""} Resume it inside T3.` });
    expect(resolveAttach(t3, env({ apps: () => false }))).toMatchObject({ kind: "copy", candidates: ["T3 Code"] });
  });

  it("handles Codex: the desktop app, a terminal, and codex resume for ended sessions", () => {
    expect(resolveAttach(target({ provider: "codex", host: "unknown", originator: "Codex Desktop" }), env())).toMatchObject({ kind: "focus", steps: [{ argv: ["open", "-a", "Codex"] }] });
    expect(resolveAttach(target({ provider: "codex", host: "warp" }), env())).toMatchObject({ kind: "focus", steps: [{ argv: ["open", "-a", "Warp"] }] });
    expect(resolveAttach(target({ provider: "codex", alive: false, state: "interrupted" }), env())).toMatchObject({ kind: "resume", steps: [{ warp: { command: `codex resume ${ID}` } }] });
  });

  it("opens the workspace for Cursor and says a chat can't be opened directly", () => {
    const viaCli = resolveAttach(target({ provider: "cursor", host: "cursor" }), env({ onPath: (b) => b === "cursor" }));
    expect(viaCli).toEqual({ kind: "focus", steps: [{ argv: ["cursor", REPO] }], label: "Opened the workspace in Cursor. A specific chat can't be opened directly: pick it in Cursor's chat history." });
    expect(resolveAttach(target({ provider: "cursor", host: "cursor", alive: false, state: "ended" }), env())).toMatchObject({ steps: [{ argv: ["open", "-a", "Cursor", REPO] }] });
  });

  it("attaches to a tmux worker in a new Warp tab", () => {
    const tm = target({ host: "tmux", id: "snd-01hzzzzzzzzzzzzzzzzzzzzzzz" });
    expect(resolveAttach(tm, env())).toMatchObject({ kind: "focus", steps: [{ warp: { command: "tmux attach -t snd-01hzzzzzzzzzzzzzzzzzzzzzzz" } }] });
  });

  it("shows candidates when it can't tell which window owns a live session", () => {
    expect(resolveAttach(target({ host: "unknown" }), env())).toEqual({ kind: "copy", command: "", why: "sindri can't tell which window owns this live session (tty ttys003); bring it to the front yourself", candidates: ["Warp", "T3 Code", "Cursor", "a terminal on ttys003"] });
    expect(resolveAttach(target({ host: "terminal", tty: null }), env())).toMatchObject({ kind: "copy", candidates: ["Warp", "T3 Code", "Cursor"] });
  });

  it("over an ssh tunnel shows the local command instead of launching", () => {
    expect(resolveAttach(target({ alive: false, state: "ended" }), env({ overSsh: true }))).toEqual({ kind: "copy", command: `cd '${REPO}' && claude --resume ${ID}`, why: "the dashboard runs over an ssh tunnel: run this in a terminal on the machine where the session ran", candidates: [] });
    expect(resolveAttach(target(), env({ overSsh: true }))).toMatchObject({ kind: "copy", command: "" });
  });

  it("refuses ids that don't match the provider's format", () => {
    expect(resolveAttach(target({ id: "abc;rm" }), env())).toMatchObject({ kind: "copy", command: "", why: "the session id doesn't have the expected format" });
    expect(resolveAttach(target({ host: "tmux", id: "x" }), env())).toMatchObject({ kind: "copy" });
  });

  it("never resumes a live session, for any provider and host (and a recycled pid reads as not alive, so it is resumed, not focused)", () => {
    for (const provider of PROVIDERS) for (const host of HOSTS) {
      const p = resolveAttach(target({ provider, host, id: host === "tmux" ? "snd-01hzzzzzzzzzzzzzzzzzzzzzzz" : ID }), env());
      expect(words(p)).not.toMatch(/--resume| resume /);
      expect(p.kind).not.toBe("resume");
    }
    expect(() => assertSafe(target(), { kind: "resume", steps: [{ warp: { cwd: REPO, command: `claude --resume ${ID}` } }], label: "" })).toThrow(/live session/);
    expect(() => assertSafe(target({ alive: false }), { kind: "resume", steps: [], label: "" })).not.toThrow();
  });
});

describe("launch", () => {
  function fake(codes: number[] = []): Launcher & { argv: string[][]; yaml: string[] } {
    const argv: string[][] = [];
    const yaml: string[] = [];
    return { argv, yaml, run: async (a) => { argv.push(a); return codes.shift() ?? 0; }, writeWarpConfig: (y) => { yaml.push(y); return "/home/.warp/launch_configurations/sindri-attach.yaml"; } };
  }

  it("runs the allowed argv, writes the Warp launch config and opens it", async () => {
    const l = fake();
    expect(await launch({ kind: "focus", steps: [{ argv: ["open", "-a", "Warp"] }], label: "ok" }, l, "")).toEqual({ ok: true, message: "ok" });
    const r = await launch({ kind: "resume", steps: [{ warp: { cwd: REPO, command: `claude --resume ${ID}` } }], label: "tab" }, l, "fallback");
    expect(r).toEqual({ ok: true, message: "tab" });
    expect(l.argv).toEqual([["open", "-a", "Warp"], ["open", "warp://launch/sindri-attach.yaml"]]);
    expect(l.yaml[0]).toBe(warpYaml(REPO, `claude --resume ${ID}`));
    expect(l.yaml[0]).toContain(`cwd: "${REPO}"`);
    expect(l.yaml[0]).toContain(`- exec: "claude --resume ${ID}"`);
  });

  it("refuses anything outside the allowlist, and falls back to the command when a launch fails", async () => {
    await expect(launch({ kind: "focus", steps: [{ argv: ["sh", "-c", "x"] }], label: "" }, fake(), "")).rejects.toThrow(/not an allowed launcher/);
    await expect(launch({ kind: "focus", steps: [{ argv: ["open", "https://evil.example"] }], label: "" }, fake(), "")).rejects.toThrow(/not an allowed launcher/);
    expect(await launch({ kind: "focus", steps: [{ argv: ["open", "-a", "Warp"] }], label: "x" }, fake([1]), `cd '${REPO}'`)).toEqual({ ok: false, message: "Couldn't start open; run this yourself.", copy: `cd '${REPO}'` });
    expect(await launch({ kind: "copy", command: "c", why: "w", candidates: ["Warp"] }, fake(), "")).toEqual({ ok: true, message: "w (try: Warp)", copy: "c" });
    expect(await launch({ kind: "copy", command: "", why: "w", candidates: [] }, fake(), "")).toEqual({ ok: true, message: "w" });
  });

  it("escapes YAML strings safely", () => {
    expect(warpYaml('/a "b"', "x")).toContain('cwd: "/a \\"b\\""');
  });
});

describe("makeAttachHandler", () => {
  const report = (alive: boolean, cwd: string) => ({ sessions: [{ provider: "claude", id: ID, alive, state: alive ? "working" : "ended", host: "warp", cwd, tty: "ttys003", title: "t", worktree: null, repo: "acme-web", signals: { codex: null } }], repos: [{ name: "acme-web", path: cwd }] });

  it("re-reads the fleet before acting, so a session that came back to life is focused, not resumed", async () => {
    const dir = tempDir();
    const run = fakeSindri(() => ({ code: 1, stdout: JSON.stringify(report(true, dir)) }));
    const launched: string[][] = [];
    const deps = makeDeps({ env: { PATH: "" } });
    const handler = makeAttachHandler(deps, run, false, { run: async (a) => { launched.push(a); return 0; }, writeWarpConfig: () => "x" }, () => true);
    const r = await handler("claude", ID);
    expect(run.calls).toEqual([["fleet", "--json"]]);
    expect(r).toEqual({ status: 200, body: { ok: true, kind: "focus", message: expect.stringContaining("Brought Warp to the front") } });
    expect(launched).toEqual([["open", "-a", "Warp"]]);
  });

  it("answers SND-DASH-005 for a malformed id or a session that's gone, and when the fleet can't be read", async () => {
    const deps = makeDeps();
    const run = fakeSindri(() => ({ code: 0, stdout: JSON.stringify(report(false, "/x")) }));
    const noLaunch: Launcher = { run: async () => { throw new Error("no launch expected"); }, writeWarpConfig: () => "x" };
    const h = makeAttachHandler(deps, run, false, noLaunch);
    expect(await h("claude", "../x")).toMatchObject({ status: 400, body: { error: { code: "SND-DASH-005" } } });
    expect(await h("slack", ID)).toMatchObject({ status: 400 });
    expect(await h("claude", "22222222-2222-4222-8222-222222222222")).toMatchObject({ status: 404, body: { error: { code: "SND-DASH-005", message: "no such session now" } } });
    const broken = makeAttachHandler(deps, fakeSindri(() => ({ code: 2, stdout: "", stderr: "boom" })), false, noLaunch);
    expect(await broken("claude", ID)).toMatchObject({ status: 503, body: { error: { code: "SND-DASH-005" } } });
  });

  it("writes the real Warp config 0600 under ~/.warp and finds binaries on PATH", async () => {
    const { realLauncher, onPath, appExists } = await import("../src/dashboard/attach.js");
    const deps = makeDeps();
    const l = realLauncher(deps, { run: async () => ({ code: 0, stdout: "", stderr: "" }) });
    const file = l.writeWarpConfig("name: x\n");
    expect(file).toBe(path.join(deps.home, ".warp", "launch_configurations", "sindri-attach.yaml"));
    expect(fs.statSync(file).mode & 0o777).toBe(0o600);
    expect(await l.run(["open", "-a", "Warp"])).toBe(0);
    const bin = tempDir();
    fs.writeFileSync(path.join(bin, "cursor"), "#!/bin/sh\n", { mode: 0o755 });
    expect(onPath({ ...deps, env: { PATH: `/nowhere:${bin}` } }, "cursor")).toBe(true);
    expect(onPath({ ...deps, env: {} }, "cursor")).toBe(false);
    fs.mkdirSync(path.join(deps.home, "Applications", "Warp.app"), { recursive: true });
    expect(appExists(deps, "Warp")).toBe(true);
    expect(appExists(deps, "Nope")).toBe(false);
  });
});
```

Add to `sindri/tests/dashboard-server.test.ts`:

```ts
describe("attach route", () => {
  it("passes provider and id to the attach handler, behind the same Origin and CSRF checks as actions", async () => {
    const seen: string[][] = [];
    const s = await server({ attach: async (p, id) => { seen.push([p, id]); return { status: 200, body: { ok: true, kind: "focus", message: "Brought Warp to the front" } }; } });
    const { cookie, csrf } = await login(s.port, s.opts.key, mint);
    const body = JSON.stringify({ provider: "claude", id: "abc" });
    expect((await request(s.port, { method: "POST", path: "/api/attach", headers: { cookie, "x-sindri-csrf": csrf }, body })).status).toBe(403);
    const r = await request(s.port, { method: "POST", path: "/api/attach", headers: { cookie, origin: `http://127.0.0.1:${s.port}`, "x-sindri-csrf": csrf }, body });
    expect(JSON.parse(r.body)).toEqual({ ok: true, kind: "focus", message: "Brought Warp to the front" });
    expect(seen).toEqual([["claude", "abc"]]);
  });

  it("answers 404 when no attach handler is wired", async () => {
    const s = await server();
    const { cookie, csrf } = await login(s.port, s.opts.key, mint);
    expect((await request(s.port, { method: "POST", path: "/api/attach", headers: { cookie, origin: `http://127.0.0.1:${s.port}`, "x-sindri-csrf": csrf }, body: "{}" })).status).toBe(404);
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd sindri && npx vitest run tests/dashboard-attach.test.ts tests/dashboard-server.test.ts`
Expected: FAIL with `Failed to load url ../src/dashboard/attach.js`. The attach-route tests already pass, because the route came in Task 9.

- [ ] **Step 3: Implement**

`sindri/src/errors.ts`, after `SND-DASH-004`:

```ts
  "SND-DASH-005": { summary: "Sindri couldn't safely work out how to attach to that session.", fix: "run the command the dashboard shows yourself, in a terminal" },
```

Then `cd sindri && npm run gen`.

`sindri/src/dashboard/attach.ts`:

```ts
import fs from "node:fs";
import path from "node:path";

import type { Deps } from "../deps.js";
import type { ErrorCode } from "../errors.js";
import { isUnder } from "../fleet/paths.js";
import { isProvider, type Host, type Provider } from "../fleet/types.js";
import type { ProcessRunner } from "../index/io.js";
import type { AttachHandler, SindriRunner } from "./server.js";

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
const TMUX = /^snd-[0-9a-z]{26}$/;
const SAFE_ID = /^[A-Za-z0-9._-]{1,128}$/;
const T3_APPS = ["T3 Code", "T3 Code (Alpha)"];
const ATTACH_ERR: ErrorCode = "SND-DASH-005";
const FIX = "run the command the dashboard shows yourself, in a terminal";

export interface AttachTarget {
  provider: Provider;
  id: string;
  alive: boolean;
  state: string;
  host: Host;
  cwd: string;
  tty: string | null;
  title: string;
  worktree: string | null;
  originator: string | null;
}
export type LaunchStep = { argv: string[] } | { warp: { cwd: string; command: string } };
export type AttachPlan = { kind: "focus" | "resume"; steps: LaunchStep[]; label: string } | { kind: "copy"; command: string; why: string; candidates: string[] };
export interface AttachEnv {
  overSsh: boolean;
  platform: NodeJS.Platform;
  roots: readonly string[];
  exists: (p: string) => boolean;
  apps: (name: string) => boolean;
  onPath: (bin: string) => boolean;
}

const shq = (s: string): string => `'${s.replace(/'/g, `'\\''`)}'`;
const resumeCmd = (t: AttachTarget): string | null => (t.provider === "claude" ? `claude --resume ${t.id}` : t.provider === "codex" ? `codex resume ${t.id}` : null);

// Spec §7: ids must match the provider's format.
function idOk(t: AttachTarget): boolean {
  return t.host === "tmux" ? TMUX.test(t.id) : UUID.test(t.id);
}

// Spec §7 table. Alive: bring its host to the front. Ended, crashed or interrupted: resume it in a new tab.
// Never resume a live session: two processes writing one session corrupt it.
export function resolveAttach(t: AttachTarget, env: AttachEnv): AttachPlan {
  const copy = (command: string, why: string, candidates: string[] = []): AttachPlan => ({ kind: "copy", command, why, candidates });
  if (!idOk(t)) return copy("", "the session id doesn't have the expected format");
  const resume = t.alive ? null : resumeCmd(t);
  const local = resume === null ? "" : `cd ${shq(t.cwd)} && ${resume}`;
  if (env.overSsh) return copy(local, t.alive ? `the dashboard runs over an ssh tunnel: bring the session forward on that machine${t.tty === null ? "" : ` (tty ${t.tty})`}` : "the dashboard runs over an ssh tunnel: run this in a terminal on the machine where the session ran");
  const pathOk = env.exists(t.cwd) && env.roots.some((r) => isUnder(t.cwd, r));
  const warpTab = (command: string, verb: "focus" | "resume", label: string): AttachPlan => {
    const full = `cd ${shq(t.cwd)} && ${command}`;
    if (!env.apps("Warp")) return copy(full, "Warp isn't installed; run this in a terminal");
    if (!pathOk) return copy(full, "its folder isn't inside a repo or worktree sindri knows, so sindri won't open it for you");
    return { kind: verb, steps: [{ warp: { cwd: t.cwd, command } }], label };
  };
  const focusApp = (app: string, label: string): AttachPlan => ({ kind: "focus", steps: [{ argv: ["open", "-a", app] }], label });

  if (t.host === "t3") {
    const app = T3_APPS.find((a) => env.apps(a));
    if (app === undefined) return copy(local, "T3 Code isn't installed where sindri looks; open it yourself", ["T3 Code"]);
    const label = `Brought ${app} to the front: open the thread "${t.title}" in ${t.worktree ?? t.cwd} (T3 has no deep link to a thread).`;
    return focusApp(app, t.alive ? label : `${label} Resume it inside T3.`);
  }
  if (t.host === "tmux") {
    const command = `tmux attach -t ${t.id}`;
    return warpTab(command, "focus", `Opened a new Warp tab attached to tmux session ${t.id}.`);
  }
  if (t.provider === "cursor" || t.host === "cursor") {
    if (!pathOk) return copy(`cursor ${shq(t.cwd)}`, "its folder isn't inside a repo or worktree sindri knows, so sindri won't open it for you");
    const label = "Opened the workspace in Cursor. A specific chat can't be opened directly: pick it in Cursor's chat history.";
    if (env.onPath("cursor")) return { kind: "focus", steps: [{ argv: ["cursor", t.cwd] }], label };
    if (env.apps("Cursor")) return { kind: "focus", steps: [{ argv: ["open", "-a", "Cursor", t.cwd] }], label };
    return copy(`cursor ${shq(t.cwd)}`, "Cursor isn't installed where sindri looks");
  }
  if (!t.alive) {
    if (resume === null) return copy("", "this provider has no resume command");
    return warpTab(resume, "resume", `Opened a new Warp tab in ${t.cwd} running ${resume}.`);
  }
  if (t.provider === "codex" && t.originator === "Codex Desktop" && env.apps("Codex")) return focusApp("Codex", "Brought Codex to the front: the session is open there.");
  if (t.host === "warp" && env.apps("Warp")) return focusApp("Warp", `Brought Warp to the front: the session is the tab on ${t.tty ?? "an unknown tty"}, titled "${t.title}".`);
  const where = t.tty === null ? "" : ` (tty ${t.tty})`;
  return copy("", `sindri can't tell which window owns this live session${where}; bring it to the front yourself`, ["Warp", "T3 Code", "Cursor", ...(t.tty === null ? [] : [`a terminal on ${t.tty}`])]);
}

export function assertSafe(t: AttachTarget, plan: AttachPlan): void {
  if (t.alive && (plan.kind === "resume" || /--resume| resume /.test(JSON.stringify(plan)))) throw new Error("refusing to resume a live session");
}

export function warpYaml(cwd: string, command: string): string {
  return [
    "---",
    "name: sindri-attach",
    "windows:",
    "  - tabs:",
    '      - title: "sindri attach"',
    "        layout:",
    `          cwd: ${JSON.stringify(cwd)}`,
    "          commands:",
    `            - exec: ${JSON.stringify(command)}`,
    "",
  ].join("\n");
}

export interface Launcher {
  run(argv: string[]): Promise<number>;
  writeWarpConfig(yaml: string): string;
}

// Spec §7 "Launching": a fixed list of commands with checked arguments, run as argv (no shell).
function allowed(argv: readonly string[]): boolean {
  if (argv[0] === "open") return (argv[1] === "-a" && typeof argv[2] === "string" && argv.length <= 4) || (argv.length === 2 && argv[1] === "warp://launch/sindri-attach.yaml");
  return argv[0] === "cursor" && argv.length === 2 && path.isAbsolute(argv[1]);
}

export async function launch(plan: AttachPlan, l: Launcher, fallback: string): Promise<{ ok: boolean; message: string; copy?: string }> {
  if (plan.kind === "copy") {
    const message = plan.candidates.length === 0 ? plan.why : `${plan.why} (try: ${plan.candidates.join(", ")})`;
    return plan.command === "" ? { ok: true, message } : { ok: true, message, copy: plan.command };
  }
  for (const step of plan.steps) {
    const argv = "argv" in step ? step.argv : ["open", "warp://launch/sindri-attach.yaml"];
    if (!allowed(argv)) throw new Error(`${argv[0]} is not an allowed launcher`);
    if ("warp" in step) l.writeWarpConfig(warpYaml(step.warp.cwd, step.warp.command));
    if ((await l.run(argv)) !== 0) return { ok: false, message: `Couldn't start ${argv[0]}; run this yourself.`, copy: fallback };
  }
  return { ok: true, message: plan.label };
}

export function onPath(deps: Deps, bin: string): boolean {
  return (deps.env.PATH ?? "").split(":").some((d) => d !== "" && fs.existsSync(path.join(d, bin)));
}

export function appExists(deps: Deps, name: string): boolean {
  return [path.join("/Applications", `${name}.app`), path.join(deps.home, "Applications", `${name}.app`)].some((p) => fs.existsSync(p));
}

export function realLauncher(deps: Deps, run: ProcessRunner): Launcher {
  return {
    run: async (argv) => (await run.run(argv, { cwd: deps.home, timeoutMs: 10_000 })).code,
    writeWarpConfig: (yaml) => {
      const dir = path.join(deps.home, ".warp", "launch_configurations");
      fs.mkdirSync(dir, { recursive: true, mode: 0o700 });
      const file = path.join(dir, "sindri-attach.yaml");
      fs.writeFileSync(file, yaml, { mode: 0o600 });
      fs.chmodSync(file, 0o600);
      return file;
    },
  };
}

interface FleetSlice {
  sessions: { provider: string; id: string; alive: boolean; state: string; host: Host; cwd: string; tty: string | null; title: string; worktree: string | null; repo: string | null; signals: { codex: { originator: string | null } | null } }[];
  repos: { path: string }[];
}

// Before every attach, read the fleet again: a session that came back to life since the last poll is focused,
// never resumed (spec §7 rule).
export function makeAttachHandler(
  deps: Deps, sindri: SindriRunner, overSsh: boolean, launcher: Launcher,
  apps: (name: string) => boolean = (n) => appExists(deps, n), exists: (p: string) => boolean = fs.existsSync,
): AttachHandler {
  return async (provider, id) => {
    const err = (status: number, message: string) => ({ status, body: { ok: false, error: { code: ATTACH_ERR, message, fix: FIX } } });
    if (!isProvider(provider) || !SAFE_ID.test(id)) return err(400, "unknown provider or malformed session id");
    const r = await sindri(["fleet", "--json"]);
    let fleet: FleetSlice;
    try {
      if (r.code === 2) throw new Error(r.stderr);
      fleet = JSON.parse(r.stdout) as FleetSlice;
    } catch {
      return err(503, "sindri fleet couldn't be read, so attach can't check the session is still in the state shown");
    }
    const s = fleet.sessions.find((x) => x.provider === provider && x.id === id);
    if (s === undefined) return err(404, "no such session now");
    const roots = [...fleet.repos.map((x) => x.path), ...fleet.sessions.filter((x) => x.repo !== null && x.worktree !== null).map((x) => x.worktree as string)];
    const t: AttachTarget = { provider, id, alive: s.alive, state: s.state, host: s.host, cwd: s.cwd, tty: s.tty, title: s.title, worktree: s.worktree, originator: s.signals.codex?.originator ?? null };
    const plan = resolveAttach(t, { overSsh, platform: deps.system.platform, roots, exists, apps, onPath: (b) => onPath(deps, b) });
    assertSafe(t, plan);
    const resume = resumeCmd(t);
    const out = await launch(plan, launcher, t.alive || resume === null ? "" : `cd ${shq(t.cwd)} && ${resume}`);
    return { status: 200, body: { ok: out.ok, kind: plan.kind, message: out.message, ...(out.copy === undefined ? {} : { copy: out.copy }) } };
  };
}
```

In the first handler test, the `apps` probe is passed as `() => true`, so the result doesn't depend on whether `/Applications/Warp.app` exists on the test machine. The fixture's `tempDir()` path exists, and the report's repo path equals it, so `pathOk` holds. `main.ts` passes `realLauncher(deps, realProcessRunner())` and the default probes.

`sindri/src/dashboard/command.ts`: change the factory parameter to `attach: (deps: Deps, run: SindriRunner) => AttachHandler | null = () => null`. Hoist the runner above `createDashboardHandler`:

```ts
      const run: SindriRunner = (a) => io.run.run([io.node, io.cliPath, ...a], { cwd: deps.home, timeoutMs: 30_000 });
```

Then pass `run` and `attach: attach(deps, run)` to it. Import `type SindriRunner` from `./server.js`.

`sindri/src/main.ts`: pass the real attach factory:

```ts
    run: (args, deps) => makeDashboardCommand(realDashboardIo(), (d, run) => makeAttachHandler(d, run, d.env.SSH_CONNECTION !== undefined, realLauncher(d, realProcessRunner())))(args, deps),
```

(Import `makeAttachHandler` and `realLauncher` from `./dashboard/attach.js`; `realProcessRunner` is already imported.)

- [ ] **Step 4: Run the tests**

Run: `cd sindri && npx vitest run tests/dashboard-attach.test.ts tests/dashboard-server.test.ts tests/dashboard-command.test.ts tests/errors.test.ts`
Expected: PASS. Then, once for the task: `npm test && npm run typecheck && npm run test:coverage`. Expected: all PASS, coverage 100%.

- [ ] **Step 5: Commit**

```bash
git add sindri/src sindri/tests docs/sindri/errors.md
git commit -m "feat: sindri dashboard attach: focus live sessions, resume only ended ones" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 12: The SwiftBar badge and its installer step (`sindri fleet --badge`)

**Files:**
- Create: `sindri/src/fleet/badge.ts`, `config/swiftbar/sindri-fleet.30s.sh` (executable), `scripts/tests/swiftbar-plugin.test.sh`
- Modify:
  - `sindri/src/fleet/command.ts` (`--badge`);
  - `scripts/install-sindri.sh` (install the plugin when SwiftBar is present);
  - `scripts/tests/install-sindri.test.sh`;
  - `AGENTS.md` (the bash test line).
- Test: `sindri/tests/fleet-badge.test.ts`, the bash tests above

**Interfaces:**
- Consumes: `FleetReport` (Task 8), `liveServer` (Task 9), `ATTN.light` (the same colour as the UI, `#c2410c`).
- Produces:
  - `renderBadge(report, sindriBin, dashboardUp): string` in SwiftBar's format (spec §5.2 and main spec §10.5):
    - the title is `N working · M needs you` in the attention colour, or `N working`, or `sindri idle`;
    - the dropdown lists up to 20 needs-you items. While the dashboard runs, each item opens `sindri dashboard --open <deep link>`; otherwise the dropdown says how to start it;
    - no action runs from the menu bar. `|` and newlines are stripped from item text.
  - `sindri fleet --badge` prints that and always exits 0.
  - `config/swiftbar/sindri-fleet.30s.sh` refreshes every 30 s (SwiftBar reads the interval from the file name). It kills a `sindri fleet` that runs past 10 s (`AW_SWIFTBAR_BUDGET_TICKS`, in 0.1 s ticks, default 100) and shows `sindri ? (stale)` when it fails or is slow.
  - `install-sindri.sh` writes the plugin into SwiftBar's plugin folder (`AW_SWIFTBAR_DIR`, else `defaults read com.ameba.SwiftBar PluginDirectory`) with `__BIN__` filled in. `AW_NO_SWIFTBAR=1` skips it. Without SwiftBar it prints that it skipped the step.

- [ ] **Step 1: Write the failing tests**

`sindri/tests/fleet-badge.test.ts`:

```ts
import fs from "node:fs";
import { describe, expect, it } from "vitest";

import { renderBadge } from "../src/fleet/badge.js";
import type { FleetReport } from "../src/fleet/collect.js";
import { makeFleetCommand } from "../src/fleet/command.js";
import { serverFile } from "../src/dashboard/command.js";
import { at, claudeStatusFile, claudeTranscript, fleetIo, L, SID } from "./fleet-fixtures.js";
import { makeDeps } from "./helpers.js";

const need = (i: number, title = `Task ${i}`) => ({ key: `session:claude:${i}`, kind: "session", state: "approval", title, reason: "r", repo: null, since: at(10), link: `/agent/claude/id-${i}`, action: { type: "attach", provider: "claude", id: `id-${i}` }, urgent: true, ackable: false });
const rep = (needs: unknown[], live = 3, working = 2) => ({ needs, counts: { live, working, needsYou: needs.length, urgent: needs.length } }) as unknown as FleetReport;

describe("renderBadge", () => {
  it("shows working and needs-you counts in the attention colour, and opens deep links from the dropdown", () => {
    const out = renderBadge(rep([need(1, "Fix | the\nform")]), "/Users/dev/.local/bin/sindri", true).split("\n");
    expect(out[0]).toBe("2 working · 1 needs you | color=#c2410c");
    expect(out).toContain("Needs you");
    expect(out).toContain('approval: Fix the form | bash="/Users/dev/.local/bin/sindri" param1=dashboard param2=--open param3=/agent/claude/id-1 terminal=false');
    expect(out.at(-1)).toBe('Open the dashboard | bash="/Users/dev/.local/bin/sindri" param1=dashboard param2=--open param3=/ terminal=false');
  });

  it("caps the list at 20, says idle or quiet states in words, and doesn't offer links while the dashboard is down", () => {
    const many = renderBadge(rep(Array.from({ length: 25 }, (_, i) => need(i))), "sindri", true);
    expect(many.split("\n").filter((l) => l.startsWith("approval: "))).toHaveLength(20);
    expect(many).toContain("… and 5 more | bash=\"sindri\" param1=dashboard param2=--open param3=/needs terminal=false");
    expect(renderBadge(rep([], 3, 1), "sindri", true).split("\n")[0]).toBe("1 working");
    expect(renderBadge(rep([], 0, 0), "sindri", true).split("\n").slice(0, 3)).toEqual(["sindri idle", "---", "Nothing needs you. 0 working, 0 live."]);
    const down = renderBadge(rep([need(1)]), "sindri", false);
    expect(down).not.toContain("bash=");
    expect(down).toContain("The dashboard isn't running: start it in a terminal with sindri dashboard");
  });
});

describe("sindri fleet --badge", () => {
  it("prints the badge and always exits 0, using SINDRI_BIN for the click target", async () => {
    const deps = makeDeps({ env: { SINDRI_BIN: "/opt/bin/sindri" } });
    claudeStatusFile(deps.home, { pid: 4242, status: "waiting" });
    claudeTranscript(deps.home, SID, [L.user("go", at(100))]);
    fs.mkdirSync(serverFile(deps).replace(/\/server\.json$/, ""), { recursive: true });
    fs.writeFileSync(serverFile(deps), JSON.stringify({ pid: 4242, pidStart: "start-4242", host: "test-host", port: 7190, key: "ab".repeat(32), startedAt: "x" }));
    const r = await makeFleetCommand(fleetIo())(["--badge"], deps);
    expect(r.exitCode).toBe(0);
    expect(r.stdout.split("\n")[0]).toBe("0 working · 1 needs you | color=#c2410c");
    expect(r.stdout).toContain(`bash="/opt/bin/sindri" param1=dashboard param2=--open param3=/agent/claude/${SID}`);
  });
});
```

`scripts/tests/swiftbar-plugin.test.sh`:

```bash
#!/usr/bin/env bash
# The SwiftBar plugin: prints `sindri fleet --badge`, and a stale line when sindri fails, is slow or is missing.
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"
PLUGIN="$ROOT/config/swiftbar/sindri-fleet.30s.sh"
TMP="$(mktemp -d)"
trap 'find "$TMP" -depth -delete 2>/dev/null' EXIT
mkdir -p "$TMP/bin"
stub() { # body
  printf '#!/usr/bin/env bash\n%s\n' "$1" > "$TMP/bin/sindri"
  chmod +x "$TMP/bin/sindri"
}

test_prints_the_badge() {
  stub 'if [ "$1 $2" = "fleet --badge" ]; then printf "2 working · 1 needs you | color=#c2410c\n---\nNeeds you\n"; fi'
  local out
  out="$(AW_SINDRI_BIN="$TMP/bin/sindri" bash "$PLUGIN")"
  [ "$(head -n 1 <<<"$out")" = "2 working · 1 needs you | color=#c2410c" ] || { echo "FAIL: badge line: $out"; exit 1; }
  echo "PASS: test_prints_the_badge"
}

test_failing_or_slow_sindri_is_stale() {
  local out start
  stub 'exit 2'
  out="$(AW_SINDRI_BIN="$TMP/bin/sindri" bash "$PLUGIN")"
  [ "$(head -n 1 <<<"$out")" = "sindri ? (stale)" ] || { echo "FAIL: failing sindri: $out"; exit 1; }
  stub 'sleep 30'
  start=$(date +%s)
  out="$(AW_SINDRI_BIN="$TMP/bin/sindri" AW_SWIFTBAR_BUDGET_TICKS=5 bash "$PLUGIN")"
  [ $(( $(date +%s) - start )) -le 3 ] || { echo "FAIL: the plugin waited for a slow sindri"; exit 1; }
  grep -q "took over" <<<"$out" || { echo "FAIL: slow sindri: $out"; exit 1; }
  echo "PASS: test_failing_or_slow_sindri_is_stale"
}

test_missing_sindri() {
  local out
  out="$(env PATH=/usr/bin:/bin AW_SINDRI_BIN="$TMP/none" bash "$PLUGIN")"
  [ "$(head -n 1 <<<"$out")" = "sindri ?" ] || { echo "FAIL: missing sindri: $out"; exit 1; }
  echo "PASS: test_missing_sindri"
}

test_prints_the_badge
test_failing_or_slow_sindri_is_stale
test_missing_sindri
```

Add to `scripts/tests/install-sindri.test.sh`. Beside `export AW_NO_FLEET_HOOK=1`, add `export AW_NO_SWIFTBAR=1`. Then add this test, and call it at the bottom:

```bash
test_swiftbar_plugin() {
  local plug="$TMP/swiftbar" out
  mkdir -p "$plug"
  out="$(env -u AW_NO_SWIFTBAR AW_SWIFTBAR_DIR="$plug" AW_DRY_RUN=1 CLAUDE_LOCAL_BIN="$TMP/sb-bin" bash "$ROOT/scripts/install-sindri.sh")"
  grep -q "would install the SwiftBar plugin into $plug" <<<"$out" || { echo "FAIL: swiftbar dry-run line: $out"; exit 1; }
  env -u AW_NO_SWIFTBAR AW_SWIFTBAR_DIR="$plug" AW_SKIP_BUILD=1 AW_SKIP_LAUNCHD=1 CLAUDE_LOCAL_BIN="$TMP/sb-bin" bash "$ROOT/scripts/install-sindri.sh" > /dev/null
  [ -x "$plug/sindri-fleet.30s.sh" ] || { echo "FAIL: plugin not installed"; exit 1; }
  grep -q "AW_SINDRI_BIN:-$TMP/sb-bin/sindri" "$plug/sindri-fleet.30s.sh" || { echo "FAIL: __BIN__ not substituted"; exit 1; }
  ! grep -q "__BIN__" "$plug/sindri-fleet.30s.sh" || { echo "FAIL: placeholder left"; exit 1; }
  out="$(env -u AW_NO_SWIFTBAR AW_SWIFTBAR_DIR="$TMP/no-such-dir" AW_DRY_RUN=1 bash "$ROOT/scripts/install-sindri.sh")"
  grep -q "SwiftBar not found" <<<"$out" || { echo "FAIL: missing SwiftBar not reported: $out"; exit 1; }
  echo "PASS: test_swiftbar_plugin"
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd sindri && npx vitest run tests/fleet-badge.test.ts`
Expected: FAIL with `Failed to load url ../src/fleet/badge.js`.
Run: `bash scripts/tests/swiftbar-plugin.test.sh`
Expected: FAIL (`…/config/swiftbar/sindri-fleet.30s.sh: No such file or directory`).

- [ ] **Step 3: Implement**

`sindri/src/fleet/badge.ts`:

```ts
import type { FleetReport } from "./collect.js";
import { EMPTY } from "./sentences.js";

const ATTN = "#c2410c"; // ui/src/model.ts ATTN.light
const MAX_ITEMS = 20;
// SwiftBar splits a line at "|" into text and parameters; newlines would start new items.
const clean = (s: string): string => s.replace(/[|\r\n]+/g, " ").replace(/\s+/g, " ").trim().slice(0, 80);

// Spec §5.2: "3 working · 1 needs you", the needs-you items in the dropdown, a click opens the dashboard at
// that item. No action runs from the menu bar.
export function renderBadge(r: FleetReport, sindriBin: string, dashboardUp: boolean): string {
  const n = r.needs.length;
  const w = r.counts.working;
  const open = (p: string): string => (dashboardUp ? ` | bash="${sindriBin.replace(/"/g, "")}" param1=dashboard param2=--open param3=${p} terminal=false` : "");
  const lines = [n > 0 ? `${w} working · ${n} needs you | color=${ATTN}` : r.counts.live > 0 ? `${w} working` : "sindri idle", "---"];
  if (n === 0) lines.push(EMPTY.needs(w, r.counts.live));
  else {
    lines.push("Needs you");
    for (const it of r.needs.slice(0, MAX_ITEMS)) lines.push(`${clean(`${it.state}: ${it.title}`)}${open(it.link)}`);
    if (n > MAX_ITEMS) lines.push(`… and ${n - MAX_ITEMS} more${open("/needs")}`);
  }
  lines.push("---", dashboardUp ? `Open the dashboard${open("/")}` : "The dashboard isn't running: start it in a terminal with sindri dashboard");
  return lines.join("\n");
}
```

`sindri/src/fleet/command.ts`: in `list`, add `badge: { type: "boolean" }` to the flags. After collecting:

```ts
  if (values.badge === true) return success(renderBadge(report, deps.env.SINDRI_BIN ?? "sindri", liveServer(deps) !== null), report, false, 0);
```

(Import `renderBadge` from `./badge.js` and `liveServer` from `../dashboard/command.js`. Add `[--badge]` to the `fleet` usage line in `main.ts`: `sindri fleet [--provider claude|codex|cursor] [--state S] [--json|--badge]`.)

`config/swiftbar/sindri-fleet.30s.sh` (mode 755):

```bash
#!/usr/bin/env bash
# <xbar.title>Sindri fleet</xbar.title>
# <xbar.desc>Agents working and what needs you, from sindri fleet --badge. Read-only: a click opens the dashboard.</xbar.desc>
# <swiftbar.hideRunInTerminal>true</swiftbar.hideRunInTerminal>
# Installed by scripts/install-sindri.sh, which fills in __BIN__. Refreshes every 30 s (the name says so).
SINDRI="${AW_SINDRI_BIN:-__BIN__/sindri}"
[ -x "$SINDRI" ] || SINDRI="$(command -v sindri 2>/dev/null || true)"
if [ -z "$SINDRI" ] || [ ! -x "$SINDRI" ]; then
  printf 'sindri ?\n---\nsindri is not installed: scripts/install-sindri.sh\n'
  exit 0
fi
TICKS="${AW_SWIFTBAR_BUDGET_TICKS:-100}"
case "$TICKS" in ''|*[!0-9]*) TICKS=100 ;; esac
OUT="$(mktemp 2>/dev/null)" || { printf 'sindri ?\n'; exit 0; }
trap 'unlink "$OUT" 2>/dev/null' EXIT
( SINDRI_BIN="$SINDRI" "$SINDRI" fleet --badge > "$OUT" 2>/dev/null ) < /dev/null &
PID=$!
while kill -0 "$PID" 2>/dev/null; do
  if [ "$TICKS" -le 0 ]; then
    pkill -P "$PID" 2>/dev/null
    kill -9 "$PID" 2>/dev/null
    printf 'sindri ? (stale)\n---\nsindri fleet took over 10 s: run sindri doctor\n'
    exit 0
  fi
  TICKS=$((TICKS - 1))
  sleep 0.1
done
if ! wait "$PID" || [ ! -s "$OUT" ]; then
  printf 'sindri ? (stale)\n---\nsindri not responding: run sindri doctor\n'
  exit 0
fi
cat "$OUT"
exit 0
```

`scripts/install-sindri.sh`: add after `install_fleet_hook`:

```bash
# The menu-bar badge (spec §5.2): only when SwiftBar is installed. AW_SWIFTBAR_DIR overrides the lookup.
swiftbar_dir() {
  if [ -n "${AW_SWIFTBAR_DIR:-}" ]; then echo "$AW_SWIFTBAR_DIR"; return; fi
  [ "$(uname -s)" = "Darwin" ] || return 0
  defaults read com.ameba.SwiftBar PluginDirectory 2>/dev/null || true
}

install_swiftbar_plugin() {
  [ "${AW_NO_SWIFTBAR:-0}" = "1" ] && return 0
  local dir
  dir="$(swiftbar_dir)"
  if [ -z "$dir" ] || [ ! -d "$dir" ]; then
    echo "  sindri: SwiftBar not found; skipped the menu-bar badge (install SwiftBar, then rerun this script)"
    return 0
  fi
  if [ "${AW_DRY_RUN:-0}" = "1" ]; then
    echo "  [dry-run] would install the SwiftBar plugin into $dir"
    return 0
  fi
  sed -e "s|__BIN__|$BIN_DIR|g" "$SCRIPT_DIR/config/swiftbar/sindri-fleet.30s.sh" > "$dir/sindri-fleet.30s.sh"
  chmod 755 "$dir/sindri-fleet.30s.sh"
  echo "  sindri: menu-bar badge installed ($dir/sindri-fleet.30s.sh)"
}
```

Call `install_swiftbar_plugin` in the plain-install dry-run branch (before its `exit 0`) and in the real branch after the fleet hooks.

`AGENTS.md`: add `bash scripts/tests/swiftbar-plugin.test.sh` after `bash scripts/tests/install-sindri.test.sh`.

- [ ] **Step 4: Run the tests**

Run: `cd sindri && npx vitest run tests/fleet-badge.test.ts tests/fleet-command.test.ts`, then from the repo root `bash scripts/tests/swiftbar-plugin.test.sh && bash scripts/tests/install-sindri.test.sh`.
Expected: all PASS. Then, once for the task: `cd sindri && npm test && npm run typecheck && npm run test:coverage`. Expected: all PASS, coverage 100%.

- [ ] **Step 5: Commit**

```bash
git add sindri/src/fleet sindri/src/main.ts sindri/tests config/swiftbar scripts/install-sindri.sh scripts/tests AGENTS.md
git commit -m "feat: sindri menu-bar badge (SwiftBar) and fleet --badge" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 13: Docs, the end-to-end evidence run, and the merge gate

**Files:**
- Create: `docs/sindri/dashboard.md`, `sindri/scripts/dashboard-evidence.mjs`
- Modify:
  - `docs/sindri/README.md` (command rows, "Where things live");
  - `AGENTS.md` (Commands; the `sindri/` and `config/` directory comments);
  - `planning/ARCHITECTURE.md` (the Sindri section);
  - `.agents/rules/testing.md` (the sindri row: new coverage excludes and the browser tests);
  - `docs/superpowers/specs/2026-10-09-sindri-dashboard-design.md` (a closing "Decisions made in the plan" section).
- Test: the full merge gate; the end-to-end run against this machine's real sessions, with screenshots inspected.

**Interfaces:**
- Consumes: everything above.
- Produces:
  - Docs that match the shipped behaviour.
  - `sindri/scripts/dashboard-evidence.mjs <url> <out-dir>`: a Playwright script, not part of `npm test`, that saves screenshots of every view in light, dark and reduced-motion modes.
  - The evidence checklist recorded in the PR body (text only, no images).

- [ ] **Step 1: Write the docs**

`docs/sindri/dashboard.md`:

````markdown
# Sindri dashboard and fleet

One place to see every agent on this machine (Claude Code, Codex, Cursor), what sindri is doing on its own, and what needs you. Spec: `docs/superpowers/specs/2026-10-09-sindri-dashboard-design.md`.

## Commands

| Command | What it does |
|---|---|
| `sindri fleet [--provider P] [--state S] [--json\|--badge]` | Every session with one state each, sindri's jobs, the heavy-job lock, and the needs-you list. Exit 1 when something needs you |
| `sindri fleet ack <key>` | Hide a failure until it happens again; `done:<provider>:<id>` marks a finished turn seen |
| `sindri fleet hook <provider>` | The `aw:fleet` hook's entry point (stdin: the hook payload; prints nothing; always exits 0) |
| `sindri dashboard [--port N]` | Serve the dashboard on 127.0.0.1 until Ctrl-C; logs a one-time URL |
| `sindri dashboard --url` | A fresh one-time URL for the running dashboard |
| `sindri dashboard --open <path>` | Open a deep link (`/needs`, `/agent/claude/<id>`, `/repo/<name>`, `/job/<id>`) in the browser |

## States

Exactly one per session, always in words, highest priority first: `approval`, `input`, `plan ready`, `auth`, `working`, `background`, `ready`, `stuck`, `interrupted`, `failed`, `limited`, `crashed`, `ended`. Rows marked `estimated` were guessed from transcript activity, which is every Cursor session and any Codex session without hook events. Subagents sit under their parent. A child waiting on you raises the parent's state.

## The needs-you signal

The attention colour (`#c2410c` light, `#fb923c` dark) is used for nothing else.
- **Pulsing:** `approval` and `input` rows pulse, with the "Needs you" tab and the header band, until you've seen the row: it has been half in view for a second while the tab has focus, or you focused, clicked or acted on it.
- **Colour, no motion:** `failed` and `limited` rows.
- **Reduced motion:** with reduced motion on, a static outline replaces the pulse.
- **Signals elsewhere:** the tab title reads `(N) Needs you · Sindri`, the favicon gets a dot, one polite screen-reader announcement says "2 now need you", and the menu bar shows the count. An OS notification fires for approvals and questions if `dashboard.notify` is on and you allowed notifications.

## Security

- **Binding:** it listens on `127.0.0.1:<dashboard.port>` only. A taken port is `SND-DASH-001`; it never picks another port.
- **Tokens:** `--url` mints a one-time token (10 minutes) that the first page load exchanges for an `HttpOnly`, `SameSite=Strict` cookie, then drops from the URL. A restart changes the key, so old links and cookies stop working: a 401 says "run `sindri dashboard --url`".
- **Request checks:** every request needs a `Host` of `127.0.0.1:<port>` or `localhost:<port>`, and every action also needs a matching `Origin` and the CSRF token. No CORS headers are ever sent.
- **Headers:** CSP `default-src 'self'`, with no inline script or style, nothing from another origin, and `frame-ancestors 'none'`; plus `Referrer-Policy: no-referrer`.
- **Actions:** the dashboard runs only `sindri fleet ack <key>` and `sindri evolve reject <id> --reason <text>`, with checked arguments and no shell. `evolve adopt`, `evolve publish`, `channel promote` and `profile approve` need a typed confirmation, so the dashboard only shows them as a command to copy.
- **Untrusted text:** agent-written text is scrubbed and escaped in the collector, and the page sets it as text only. Control and invisible characters show as `\u{XXXX}`.
- **Over ssh:** forward the same port on both ends, `ssh -L 7190:127.0.0.1:7190 <box>`, because the Host check needs it. Attach then shows the command to run instead of launching.

## Attach

Sindri never resumes a session that is still running: two processes writing one session corrupt it. Before every attach it reads the fleet again.

| Host | Alive | Ended, crashed or interrupted |
|---|---|---|
| T3 Code | Bring T3 to the front, naming the thread and worktree (T3 has no deep link) | Same; resume inside T3 |
| Warp or a terminal (Claude CLI) | Bring Warp to the front; the row names the tab's tty and title. In another terminal, sindri shows the candidates | A new Warp tab at the session's folder running `claude --resume <id>` (through `~/.warp/launch_configurations/sindri-attach.yaml`) |
| Codex | The Codex app, or Warp | `codex resume <id>` in a new Warp tab |
| Cursor | `cursor <folder>` (or `open -a Cursor <folder>`); a specific chat can't be opened | Same |
| tmux (sindri workers, later) | `tmux attach -t <session>` in a new Warp tab | Same |

Ids must match the provider's format, and folders must exist inside an onboarded repo or one of its worktrees. Otherwise you get the command to copy.

## Where things live

| Path | What |
|---|---|
| `$AW_STATE_DIR/sindri/fleet/spool/<provider>.jsonl` (+ `.1.jsonl`) | One line per hook event (0600); rotated at 1 MiB |
| `$AW_STATE_DIR/sindri/fleet/jobs/latest/*.json`, `history.jsonl` | The last run of each job, and recent runs |
| `$AW_STATE_DIR/sindri/fleet/acks.json` | Acknowledged failures and seen "done" markers (30 days) |
| `$AW_STATE_DIR/sindri/fleet/{gh,usage,judge}-cache.json` | Caches: gh PRs (2 min), `scorer live` tokens (60 s), `judge health` (5 min) |
| `$AW_STATE_DIR/locks/heavy-job.lock.queue/` | Who is waiting for the heavy-job lock (only waiters that use sindri) |
| `$AW_STATE_DIR/sindri/dashboard/server.json` | The running dashboard: pid, port, the per-restart token key (0600) |

## Decisions and limits

- **Transcripts and turn outcomes.** Claude transcripts on disk have no SDK `result` record, so a turn's outcome comes from the interruption markers, the turn-end lines and the API-error lines in the transcript.
- **429 versus 529.** A 429 or a usage limit is `limited`; a 529 or any other API error is `failed`.
- **Codex.** An observer can't reach the app-server of a session it didn't start. Codex approvals come from the `PermissionRequest` hook and from approval events in the rollout.
- **Tokens and cost.** Tokens come from `scorer live` (live Claude sessions, cached 60 s) and from Codex's `token_count`. Dollars come from Claude's `cost-state` line. Figures are per session; Today sums the sessions active since local midnight.
- **Bridge.** Unread bridge messages are counted read-only in `mcp-bridge/bridge.db` (`AW_BRIDGE_DB` overrides the path). The REST endpoint marks messages as read, so the collector never calls it.
- **Bounds.** Each pass reads at most 1 MiB per transcript, 200 sessions per source and 2 s per source, and only files changed in the last 24 h unless the process is alive. A Codex session started more than 60 day-directories ago doesn't show.
- **Spool.** A pending permission or question clears on the next `Stop`, prompt, session end, compaction, or transcript output.
- **Menu bar.** The menu-bar badge opens deep links through `sindri dashboard --open`.

## Troubleshooting

| You see | Do |
|---|---|
| `SND-DASH-001 port 7190 is in use (dashboard.port)` | Another program has the port: set `dashboard.port` (then `sindri profile approve`) or pass `--port` |
| "No dashboard session in this tab" | The dashboard restarted: `sindri dashboard --url` |
| "sindri not responding: run `sindri doctor`" | The dashboard can't run `sindri fleet`; check `sindri doctor` |
| A source shows `SND-FLEET-001` | That provider's store couldn't be read; the message says why. The other sources still show |
| A source shows `SND-FLEET-002` | It hit the time or size budget: only its newest sessions show |
| Approvals don't show | Is the hook installed? `jq '.hooks.PermissionRequest' ~/.claude/settings.json` should list `# aw:fleet`. Reinstall: `scripts/install-sindri.sh --fleet-hook --provider claude` |
| Codex hooks don't run | Open `codex`, run `/hooks`, and trust the `aw:*` entries |
````

`docs/sindri/README.md`:
- Add two command rows: `sindri fleet [--provider P] [--state S] [--json|--badge]`, `fleet ack <key>`, `fleet hook <provider>`, described as "Every agent on this machine, one state each, and what needs you. See `dashboard.md`"; and `sindri dashboard [--port N] | --url | --open <path>`, described as "The local dashboard (127.0.0.1): five views, attach, the needs-you signal. See `dashboard.md`".
- Add to "Where things live" the rows for `$AW_STATE_DIR/sindri/fleet/` (spool, jobs, acks, caches) and `$AW_STATE_DIR/sindri/dashboard/server.json`.
- Change the intro sentence "What exists today (Plans 2 and 3; …)" so it covers Plans 2–6.

`AGENTS.md`:
- Under Commands, after `sindri observe`, add:

```bash
sindri fleet [--json]                   # every agent (Claude Code, Codex, Cursor), sindri's jobs, what needs you
sindri dashboard                        # local dashboard on 127.0.0.1:7190 (sindri dashboard --url for a link)
```

- Change the `sindri/` tree comment to `# Sindri core: profile, ledger, lock, scrubber, plan-file tracker, observe, doctor, fleet collector, dashboard (CLI)`.
- Make the `config/` comment `… sindri-nudge SessionStart hook, aw:fleet spool hook, SwiftBar badge (+ hooks/adapters/ per provider)`.

`planning/ARCHITECTURE.md`, Sindri section: add this paragraph after the Plan 5 one:

```markdown
Plan 6 adds the fleet collector and the dashboard (`sindri/src/fleet/`, `sindri/src/dashboard/`, `sindri/ui/`; doc: `docs/sindri/dashboard.md`). `sindri fleet` reads, never writes, the provider stores: Claude Code's `~/.claude/sessions/<pid>.json` (live only while the pid's start time matches `procStart`) and its transcripts and subagents; Codex rollouts (live while a process holds the file open); and Cursor's chat metadata (estimated). It reads only bounded tails, with a budget and an error entry per source. The `aw:fleet` hook, installed for every provider, appends one short scrubbed line per event to a spool, which is the authoritative "waiting on you" signal. Pure state rules give each session exactly one state; a needs-you aggregator adds sindri's decisions (won or staged proposals, a pending profile approval, a soaked channel build), failures (jobs recorded by `runCli`, stale indexes, doctor, judge health), gh PRs (cached) and unread bridge messages (counted read-only). `sindri dashboard` serves that on 127.0.0.1 only: one-time tokens become an HttpOnly SameSite=Strict cookie; Host, Origin and CSRF checks; CSP `default-src 'self'`; views are cached `sindri … --json` runs and actions a fixed argv list, and terminal-gated commands are shown, never run. Attach focuses a live session's host and resumes only ended ones. A SwiftBar plugin shows the counts in the menu bar.
```

`.agents/rules/testing.md`, the sindri row:
- Coverage excludes: add `src/fleet/proc-real.ts` (ps and lsof; smoke-tested in `tests/real.test.ts`), `src/dashboard/io-real.ts` (signal wait; exercised by the end-to-end run), and `ui/src/app.ts` (DOM glue; driven in real Chromium by `tests/browser/`, the ui-evidence precedent).
- Tests: add fleet fixtures (`tests/fleet-fixtures.ts`: real-shaped Claude, Codex and Cursor stores under a temp HOME) and dashboard fixtures (`tests/dashboard-fixtures.ts`: a real loopback server and raw `http.request` so tests set Host and Origin).

`docs/superpowers/specs/2026-10-09-sindri-dashboard-design.md`: append

```markdown
## 11. Decisions made in the implementation plan

The plan (`docs/superpowers/plans/2026-10-09-sindri-plan-6-dashboard.md`, "Spec decisions in this plan") settled seventeen points the spec left open. Among them:
- turn outcomes come from transcript markers, because on-disk transcripts have no SDK result record;
- 429 means limited and 529 means failed;
- a Claude status of `waiting` means approval;
- the Codex app-server is behind a probe port, which returns nothing in v1;
- tokens come from `scorer live` and dollars from `cost-state`;
- bridge unread is counted read-only;
- six `fleet.sources` switches;
- a heavy-lock waiter queue and job records written by `runCli`;
- `sindri fleet ack` and `sindri dashboard --open`.

`docs/sindri/dashboard.md` restates each one.
```

- [ ] **Step 2: Write the evidence script**

`sindri/scripts/dashboard-evidence.mjs`:

```js
// Screenshots of every dashboard view for PR evidence (not part of npm test).
// Usage: node scripts/dashboard-evidence.mjs <one-time-url> <out-dir>
// The URL comes from `sindri dashboard --url`; the screenshots show real session titles, so <out-dir> must be
// outside the repo (use $AW_STATE_DIR/sindri/evidence/plan-6/<run-id>/) and the files are never committed.
import fs from "node:fs";
import path from "node:path";
import { chromium } from "playwright";

const [url, out] = process.argv.slice(2);
if (url === undefined || out === undefined) {
  console.error("usage: node scripts/dashboard-evidence.mjs <one-time-url> <out-dir>");
  process.exit(2);
}
if (path.resolve(out).startsWith(path.resolve(import.meta.dirname, "../.."))) {
  console.error("refusing: write evidence outside the repo");
  process.exit(2);
}
fs.mkdirSync(out, { recursive: true, mode: 0o700 });
const browser = await chromium.launch({ headless: true });
const modes = [{ name: "light", colorScheme: "light" }, { name: "dark", colorScheme: "dark" }, { name: "reduced", colorScheme: "light", reducedMotion: "reduce" }];
const origin = new URL(url).origin;
let first = true;
let cookies = [];
for (const m of modes) {
  const context = await browser.newContext({ colorScheme: m.colorScheme, reducedMotion: m.reducedMotion ?? "no-preference", viewport: { width: 1400, height: 900 } });
  const page = await context.newPage();
  // The token works once: the first context exchanges it, later contexts reuse that cookie.
  if (first) {
    await page.goto(url);
    first = false;
    cookies = await context.cookies();
  } else {
    await context.addCookies(cookies);
  }
  for (const view of ["needs", "agents", "repos", "auto", "today"]) {
    await page.goto(`${origin}/${view}`);
    await page.waitForTimeout(1500);
    await page.screenshot({ path: path.join(out, `${m.name}-${view}.png`), fullPage: true });
  }
  const title = await page.title();
  const href = await page.locator("#favicon").getAttribute("href");
  fs.appendFileSync(path.join(out, "facts.txt"), `${m.name}: title=${title} favicon=${href} url-has-token=${page.url().includes("?t=")}\n`);
  await context.close();
}
await browser.close();
console.log(`wrote ${modes.length * 5} screenshots and facts.txt to ${out}`);
```

- [ ] **Step 3: Run the end-to-end check against this machine's real sessions**

Run each heavy command alone; wait for any heavy-lock holder first.
1. **Build:** `cd sindri && npm run build`. Expected: `dist/cli.js`, `dist/ui/app.js` and `dist/ui/model.js` exist.
2. **The hook:** if `jq -r '.hooks.PermissionRequest[]?.hooks[]?.command' ~/.claude/settings.json | grep -c 'aw:fleet'` prints `0`, install it with `scripts/install-sindri.sh --fleet-hook --provider claude` (the feature under test; it edits `~/.claude/settings.json` through `merge_hook`, touching only `# aw:fleet` entries).
3. **A known approval:** in a scratch folder (`mkdir -p /tmp/sindri-e2e && cd /tmp/sindri-e2e && claude`), ask the new session to run a shell command that needs permission, and leave the prompt waiting. Then run `node sindri/dist/cli.js fleet; echo "exit $?"`. Expected:
   - the scratch session listed under NEEDS YOU as `approval`, with the command in the reason;
   - exit 1;
   - a new line in `$AW_STATE_DIR/sindri/fleet/spool/claude.jsonl` whose `event` is `permission`.

   If the session shows as `working` and the spool has no line, the host reaped the hook's background process before it wrote. In that case change `fleet.sh` to run the CLI in the foreground under the same kill-after-budget loop as `sindri-nudge.sh` (`AW_FLEET_HOOK_BUDGET_MS`, default 1000), rerun Task 5's bash tests (the 30-hook burst must still finish within 2 s), then repeat this step.
4. **Serve:** `node sindri/dist/cli.js dashboard --port 7191 &` (wait for its `Dashboard:` line), then `URL="$(node sindri/dist/cli.js dashboard --url)"`.
5. **Screenshots:** `RUN="$(date -u +%Y%m%dT%H%M%SZ)"; node sindri/scripts/dashboard-evidence.mjs "$URL" "${AW_STATE_DIR:-$HOME/.agentic-workflow}/sindri/evidence/plan-6/$RUN"`. Expected: 15 PNGs and `facts.txt`.
6. **Inspect every screenshot yourself** (open each PNG). Compare with this list, and record PASS or FAIL per file in the PR body, as text only, with no images and no session titles:
   - `*-needs.png`: the scratch session's `approval` row is first, with the left bar in the attention colour (light: orange-red; dark: lighter orange). The state is written in words, and the Attach button is present. Nothing else uses that colour.
   - `reduced-needs.png`: the same row has a solid outline and no half-faded bar.
   - `*-agents.png`: the sessions are grouped by repo, then worktree. Every row shows provider, host, title, state, activity and turn age. No raw escape sequences or HTML markup are rendered as formatting.
   - `*-repos.png`: each onboarded repo shows index age, files, symbols and commits behind.
   - `*-auto.png`: jobs (at least `doctor` once the Today view has run it), the heavy-job lock line, and the proposal and channel lines.
   - `*-today.png`: cost and tokens, agents and turns, judge and doctor status.
   - `facts.txt`: every line has `title=(1) Needs you · Sindri` (or the real count), `favicon=/static/favicon-attn.svg` and `url-has-token=false`.
   - Forbidden in any screenshot: a token in the address, a "No dashboard session" banner, an error banner other than an expected `SND-FLEET-00x` source note, and text that is cut off and overlapping.

   A screenshot that is stale, from the wrong run, or visibly failing is a FAIL, whatever `facts.txt` says.
7. **Attach:** in the browser (`open "$(node sindri/dist/cli.js dashboard --url)"`):
   - click Attach on the waiting scratch session: its host comes to the front, and it is never resumed;
   - end the scratch session (Ctrl-D), wait one poll, and click Attach again: a new Warp tab opens in `/tmp/sindri-e2e` running `claude --resume <id>`. If Warp isn't installed, the dashboard shows the command instead.
8. **Stop:** stop the dashboard (`kill %1`). Expected: `Dashboard stopped.`, and `$AW_STATE_DIR/sindri/dashboard/server.json` is gone. The scratch folder may be removed by hand.

- [ ] **Step 4: Run the merge gate (once, serially)**

```bash
for p in mcp-bridge scorer judge sindri skills/ui-evidence skills/bugFixOrchestrator; do (cd "$p" && npm run typecheck && npm test) || echo "FAILED: $p"; done
(cd sindri && npm run test:coverage)
for t in providers/tests/install.test.sh scripts/tests/sync-rules.test.sh scripts/tests/probe.test.sh scripts/tests/find-duplicate-skills.test.sh scripts/tests/install-scorer-audit.test.sh scripts/tests/install-sindri.test.sh scripts/tests/swiftbar-plugin.test.sh config/hooks/tests/codex-adapter.test.sh config/hooks/tests/cursor-adapter.test.sh config/hooks/tests/provider-install-hooks.test.sh config/hooks/tests/probe-log.test.sh config/hooks/tests/judge-health.test.sh config/hooks/tests/sindri-nudge.test.sh config/hooks/tests/fleet-hook.test.sh; do bash "$t" || echo "FAILED: $t"; done
for t in config/lib/tests/*.test.sh; do bash "$t" || echo "FAILED: $t"; done
claude plugin validate mods/aw-live && claude plugin test mods/aw-live
scripts/sync-rules.sh --check
./setup.sh --providers claude,codex,cursor --dry-run
grep -rn "v8 ignore" sindri/src sindri/ui/src && echo "FAILED: v8 ignore" || true
grep -rnE ":\s*any\b|as any\b|<any>" sindri/src sindri/ui/src && echo "FAILED: any" || true
(cd sindri && node --input-type=module -e "import fs from 'node:fs'; import YAML from 'yaml'; const dir = process.env.AW_PROFILE_DIR ?? (process.env.AW_STATE_DIR ?? process.env.HOME + '/.agentic-workflow') + '/profile'; const p = YAML.parse(fs.readFileSync(dir + '/profile.yaml', 'utf8')); console.log((p.privacy?.denyTerms ?? []).join('\\n'));") > "${TMPDIR:-/tmp}/sindri-deny-terms.txt"
[ -s "${TMPDIR:-/tmp}/sindri-deny-terms.txt" ] && grep -rnwiF -f "${TMPDIR:-/tmp}/sindri-deny-terms.txt" sindri/src sindri/ui sindri/tests sindri/scripts config scripts docs/sindri docs/superpowers/plans/2026-10-09-sindri-plan-6-dashboard.md && echo "FAILED: a private term (privacy.denyTerms) is in the change" || true
```

Expected: no `FAILED:` line; coverage 100% (lines, functions, branches and statements); the dry run prints `aw:fleet` and SwiftBar lines and writes nothing. The private-term check reads the words from your own profile's `privacy.denyTerms`, so no workplace name is ever written into the repo or this plan; with an empty list it checks nothing, and you must then read the diff yourself for workplace names.

- [ ] **Step 5: Commit**

```bash
git add docs/sindri docs/superpowers/specs/2026-10-09-sindri-dashboard-design.md sindri/scripts/dashboard-evidence.mjs AGENTS.md planning/ARCHITECTURE.md .agents/rules/testing.md docs/superpowers/plans/2026-10-09-sindri-plan-6-dashboard.md
git commit -m "docs: sindri dashboard and fleet docs, evidence script, spec decisions" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## Done criteria for this plan

- **Fleet:** `sindri fleet` lists every live agent on the machine within one pass:
  - Claude Code, Codex and Cursor, including sessions Sindri didn't start;
  - each with exactly one state in words; approvals and questions come from the `aw:fleet` spool, and Cursor rows are labelled `estimated`;
  - exit 1 when something needs you.
- **Bounded and contained:** a missing or corrupt provider store, a recycled pid, a 50 MB or half-written transcript, 900 transcripts and a burst of 30 hooks are each processed as Review Focus 1–4 say, and each is pinned by a test.
- **Dashboard:**
  - `sindri dashboard` binds 127.0.0.1 only and refuses a taken port with `SND-DASH-001`;
  - tokens work once and change on restart, and Host, Origin and CSRF checks refuse everything else;
  - it sends the CSP and no CORS headers, runs only its fixed actions, and only shows terminal-gated commands.
- **The needs-you signal:** you can notice it without reading (colour, motion, tab title, favicon, menu-bar badge, optional OS notification), with the reduced-motion fallback and the polite announcements. Polling never moves focus, scroll position or input. A tab left open survives hundreds of polls and a restart (Review Focus 5).
- **Attach** never resumes a live session.
- **Evidence and merge:** the end-to-end run against real sessions has its screenshots inspected by the implementer, PASS for every file, recorded as text in the PR. The merge gate is green, with 100% coverage on the new sindri code.
