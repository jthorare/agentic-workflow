---
description: Safety and context hooks — per-provider adapters, protocols, installation
globs:
  - "config/hooks/**"
  - "config/settings.json"
paths:
  - "config/hooks/**"
  - "config/settings.json"
alwaysApply: false
---

# Hooks Rules

Safety hooks run automatically via each provider's hook system (Claude Code, Codex, Cursor). Hook logic is written once in `config/hooks/*.sh` against the Claude Code protocol (JSON on stdin, exit 2 = deny). Claude Code runs those scripts directly; for Codex and Cursor an adapter translates the host's hook I/O to that protocol. The full event mapping (including unmapped levers) is in `config/hooks/adapters/README.md`.

## Per-Provider Adapters

| Provider | Hook config | Adapter | Installer |
|----------|-------------|---------|-----------|
| Claude Code | `~/.claude/settings.json` `hooks` (`PreToolUse`, `SessionStart`, …) | none — canonical scripts run directly from `~/.claude/hooks/` | `providers/claude/install-hooks.sh` |
| Codex | `~/.codex/hooks.json` | `config/hooks/adapters/codex.sh` | `providers/codex/install-hooks.sh` |
| Cursor | `~/.cursor/hooks.json` (`beforeShellExecution`, `sessionStart`, `stop`, …) | `config/hooks/adapters/cursor.sh` | `providers/cursor/install-hooks.sh` |

An adapter reads the host's stdin, normalizes it to `{"tool_name": "...", "tool_input": {...}}`, `cd`s to the session's working directory, exports `AW_PROVIDER=codex|cursor`, runs the canonical hook, and maps its exit code/stdout back to the host's allow/deny response. `config/hooks/adapters/common.sh` holds shared parsing; `install-lib.sh` holds the install helpers (`aw_hooks_init`, `aw_hooks_stage`, `aw_hook_set`, `aw_hook_unset`).

Put provider-specific parsing in the adapter, not the canonical hook. The one exception is data the adapter can't provide from stdin. For example, `context-guard.sh` falls back to Codex rollout `token_count` lines when the transcript has no Claude `assistant` line, and `done-gate.sh` falls back to `last_assistant_message`. Such fallbacks must leave Claude Code behavior unchanged. List every one of them in the adapters README.

`setup.sh` runs `providers/<name>/install-hooks.sh` for each selected provider, then the lever installers (see `planning/PROVIDERS.md`).

**Provider facts:**
- **Codex** only runs hooks you have trusted. After installing, open `codex`, run `/hooks`, and trust the `aw:*` entries. Re-trust after a reinstall that changes a command.
- **Cursor** runs user hooks from `~/.cursor/`, so the adapter `cd`s to `cwd` → `workspace_roots[0]` → `$CURSOR_PROJECT_DIR`. Cursor blocks the action when a hook returns invalid or missing JSON, so the adapter always prints valid JSON and falls back to `allow` on its own errors.
- Codex and Cursor copies of the scripts go to `~/.agentic-workflow/hooks/` (with `adapters/` and `lib/locks.sh`). Every entry ends in `# aw:<id>`, and `config/lib/merge-hook.sh` only touches entries carrying its own tag.

## PreToolUse Hooks (matcher: `Bash`; Cursor: `beforeShellExecution`)

| Hook | Blocks | Suggestion |
|------|--------|------------|
| `block-destructive.sh` | `rm -rf`, `git reset --hard`, `git push --force` (not `--force-with-lease`), `git checkout .`, `git clean -f` | Use safer alternatives (trash, stash, etc.) |
| `block-push-main.sh` | `git push` to main/master (explicit ref or implicit via current branch) | Create a feature branch and open a PR |
| `detect-secrets.sh` | AWS keys (`AKIA...`), GitHub tokens (`ghp_/gho_/ghs_`), Bearer tokens in curl, secret env var assignments (>20 chars) | Use `.env` files, secrets manager, or `gh auth login` |
| `rtk-rewrite.sh` | Rewrites eligible commands (`git`, `vitest`, `npm test`, `tsc`, `eslint`, `cargo`, `next build`) to `rtk <command>` for token compression | — |

`rtk-rewrite.sh` runs 4th in the Bash chain — after all three safety hooks — so safety checks always see the original unmodified command.

## SessionStart Hooks

| Hook | Outputs |
|------|---------|
| `git-context.sh` | Current branch, last 5 commits, working tree status |
| `prism-context.sh` | One-line warning if prism-mcp dashboard at `PRISM_DASHBOARD_PORT` (default 7180) is unreachable; silent on success |
| `sindri-nudge.sh` | One line when the session's repo is not in the approved sindri profile; silent otherwise; bounded by `AW_SINDRI_NUDGE_BUDGET_MS` |

`providers/claude/install-hooks.sh` also removes the SessionStart entry and `~/.claude/hooks/` copy of the retired `bridge-context.sh` hook left by older installs.

## Hook Protocols

**PreToolUse protocol:**
- Scripts read JSON from stdin: `{"tool_name": "...", "tool_input": {...}}`
- Exit 0 = allow the tool call to proceed
- Exit 2 = deny with a message on stdout explaining why

**SessionStart protocol:**
- No stdin input — scripts receive no data from the host (adapters discard any provider payload)
- Output context or information to stdout (the host injects it at session start)
- Exit code is ignored

## Hook Files

All hook scripts live in `config/hooks/`:

| File | Type | Matcher |
|------|------|---------|
| `block-destructive.sh` | PreToolUse | `Bash` |
| `block-push-main.sh` | PreToolUse | `Bash` |
| `detect-secrets.sh` | PreToolUse | `Bash` |
| `rtk-rewrite.sh` | PreToolUse | `Bash` |
| `git-context.sh` | SessionStart | — |
| `prism-context.sh` | SessionStart | — |
| `sindri-nudge.sh` | SessionStart | — |
| `prompt-sort.sh` | UserPromptSubmit | — |
| `meta-stop-gate.sh` | Stop | — |
| `meta-outbox.sh` | PostToolUse | `SendMessage` |
| `meta-lane-gate.sh` | UserPromptSubmit, Stop (Codex); sessionStart, postToolUse, stop (Cursor) | — |
| `session-id-report.sh` | SessionStart (Codex); sessionStart (Cursor) | — |

Each provider's `install-hooks.sh` installs them by copying, not symlinking, so they survive repo moves. Claude Code's copies go to `~/.claude/hooks/`, and Codex and Cursor copies go to `~/.agentic-workflow/hooks/`. The script then registers the command in that provider's hook config, wrapped by the adapter for Codex and Cursor.

**Lever hooks** (context-guard, done-gate, scope-gate, external-write-guard, wake gating, judge-health, prompt-sort, sindri-nudge) are installed by `scripts/install-*.sh --provider claude|codex|cursor`, with `claude` as the default. `setup.sh` runs them for each selected provider. A lever with no equivalent event on a provider prints `skipped for <provider>` and writes nothing. For example, context-guard is skipped on Cursor.

Tests (all run under a temp `HOME`): `config/hooks/tests/{codex-adapter,cursor-adapter,failopen,provider-install-hooks,probe-log,judge-health,sindri-nudge,meta-hooks}.test.sh`, plus `config/lib/tests/{prompt-sort,install-prompt-sort}.test.sh` (run through the `config/lib/tests/*.test.sh` glob).

## Fail-open logging (HOOK-FAILOPEN)

A hook **fails open** when its own error turns into an allow or a let-through: judge missing or erroring, a payload it can't read, unreadable state. Each such path calls `failopen <cause>` right before it allows, which appends one line to `${AW_STATE_DIR:-~/.agentic-workflow}/logs/failopen.log`:

```json
{"ts":"2026-10-10T06:30:00Z","event":"HOOK-FAILOPEN","hook":"send-gate","cause":"judge-exit-127","provider":"claude"}
```

- The logger is `config/lib/failopen.sh` (`aw_failopen <hook> <cause>`). `aw_failopen <hook> input "$INPUT" '<jq predicate>'` classifies a payload the hook couldn't use: `empty-input`, `jq-missing`, `input-unparsable`, or `field-missing` when the predicate holds (the payload names the hook's own tool but lacks the field it checks). It logs nothing for any other readable payload. Installers copy it to `<hooks-dir>/lib/failopen.sh`; a hook looks there first, then at `../lib/` (a repo checkout).
- **Logging only.** Each hook's `failopen()` wrapper (and the adapters' `aw_adapter_failopen`) runs the logger in a subshell with `set +eu`, its output discarded and `|| true`. Nothing the logger hits (HOME unset, `date` missing, an unwritable dir) can change the hook's exit code, stdout or stderr. With neither `AW_STATE_DIR` nor `HOME` set, it logs nothing. Payload content never reaches the log. The logger writes only to a regular file, or creates one, with mode `0600`. A symlink, FIFO or device at the log path is never opened, because opening a FIFO would block the hook into its host timeout. The log rotates to `failopen.log.1` past `AW_FAILOPEN_MAX_BYTES` (default 1 MiB). Only one writer rotates at a time (an mkdir lock, re-checking the size under it), and a non-regular `.1` is never replaced. An adapter that already logged the host payload exports `AW_FAILOPEN_INPUT_LOGGED=1`, so the hook behind it doesn't count the same payload again as `field-missing`.
- `config/hooks/tests/failopen.test.sh` runs every documented cause against the current hook and against the baseline copy in `config/hooks/tests/fixtures/failopen-baseline/` (the hooks before logging was added). It requires identical stdout, stderr and exit code under a 10 s watchdog. It also runs every fail-open hook and both adapters with HOME unset, with `date` missing and with a FIFO log. If you change a hook's decision on purpose, refresh its baseline copy in the same PR.
- Fail-open hooks and their causes:
  - `send-gate`: `locks-lib-missing`, `judge-exit-<N>`, `judge-output-unparsable`, `judge-decision-missing`
  - `scope-gate`: the judge causes plus `judge-decision-unknown`
  - `external-write-guard`: `session-id-missing`, `session-state-unreadable`
  - `done-gate`: `transcript-unreadable`, `no-assistant-line`, `transcript-line-unparsable`, `judge-ask-check-exit-<N>`, `judge-brief-exit-<N>` (but not "no brief for …")
  - `outbox-flush`: `outbox-unreadable`
  - `meta-stop-gate`: `meta-exit-<N>` (aw-meta erroring; exit 1 is its block, not a failure)
  - `meta-outbox`: `meta-exit-<N>`
  - `meta-lane-gate`: `lane-inbox-exit-<N>`, `lane-reply-exit-<N>`, `lane-stop-check-exit-<N>` (exit 1 is its block, not a failure), each followed by `-AW-META-NNN` when aw-meta printed a coded error (the code only, never its text), and `lane-name-invalid`
  - `session-id-report`: `field-missing`, `id-invalid`, `lane-name-invalid`, `launch-id-invalid`, `write-failed`
  - the three Bash safety hooks: the input causes only
  - the Codex/Cursor adapters (`<provider>-adapter`): the input causes, `args-invalid`, `payload-unparsable`, `hook-script-missing`, `hook-exit-<N>`, and `result-unparsable` (Cursor only)

  Every hook in that list also logs the input causes.
- **Crashes are not counted under Claude Code (known gap).** No fail-open hook has an `EXIT` trap. On bash 3.2 a trap turns a `set -u` or `${x:?}` abort under `set -e` into exit 0, and it defers `SIGTERM` until a running `$(judge …)` returns, so a hook killed by the host's timeout would live seconds longer. Both are decision or behavior changes. A hook that crashes (exit other than 0 or 2) keeps its exit code, and Claude Code treats it as a non-blocking error. Under Codex/Cursor the adapter logs `hook-exit-<N>`. The test pins the crash exit codes and checks that `SIGTERM` during a slow judge exits as fast as the baseline.
- **Not counted (known gap):** a hook killed by the host's own timeout. On `SIGKILL` no trap runs. On `SIGTERM` the hook dies at once, as it did before the logging. A `TERM` trap that logs and re-raises would count it, but it isn't decision-neutral: with a `TERM` trap set, bash waits for the running foreground command (a slow `judge`) to finish before handling the signal, instead of dying at once. Wrapping `judge` in a shorter timeout would change when a slow judge's decision is used. Both are decision changes, so they are out of scope for logging-only work.
- **Not counted (known limit):** an event written while `date` is missing from PATH has an empty `ts`. The line is still written, but the scorer can't place it in a window and skips it. `date` is a POSIX base utility in `/bin`, and bash 3.2 (macOS) has no builtin clock to fall back on.
- Advisory hooks with no allow/deny (context-guard, prompt-sort, done-gate-annotate, rtk-rewrite, the SessionStart hooks) don't log. Neither do hooks that fail closed (turn-origin; send-gate's unknown decision, which queues and denies).
- `scorer`'s daily report lists the events per hook and cause (`## Hook fail-open`). It reads `failopen.log.1` and `failopen.log` as untrusted input. The log is general observability: it shows when and why a hook let something through on its own error. It is not a release or cutover gate.
- A new hook with a fail-open path must call `failopen` with a fixed cause word on it.

## Adding a New Hook

The steps below apply to **PreToolUse hooks**. SessionStart hooks have different behavior: they receive no stdin, write informational output to stdout, and their exit code is ignored.

**PreToolUse hook steps:**

1. Create the script in `config/hooks/<name>.sh` — make it executable (`chmod +x`)
2. Read JSON from stdin: `input=$(cat)` then parse with `jq`
3. Exit 0 to allow the action, exit 2 to block with a message on stdout
4. Register it in each `providers/<name>/install-hooks.sh` (or a `scripts/install-*.sh --provider` lever installer) via `aw_hook_set`; skip providers whose hook system lacks the event and record it in the `config/hooks/adapters/README.md` mapping table
5. Document it in this file and in AGENTS.md's `config/` directory comment

## Probe Hook (cheap-agent-harness rollout step 0.5)

`probe-log.sh <Event>` appends raw hook stdin to `~/.agentic-workflow/probe/<Event>.jsonl`. It prints nothing and always exits 0. `scripts/probe.sh on|off|status` installs and removes it on `UserPromptSubmit`, `Stop`, `SubagentStop`, `TeammateIdle`, `SubagentStart`, and `PreToolUse`/`PostToolUse` (matcher `Agent|SendMessage`). Every probe command ends in `# aw:probe`, and `config/lib/merge-hook.sh` only ever touches commands with its own tag. `scorer probe` summarizes the logs. `scripts/probe.sh --provider codex|cursor on|off|status` installs the Codex/Cursor equivalents (no TeammateIdle; Cursor entries use the adapter's `--raw` mode) and logs to `probe/<provider>/`.

`prompt-sort.sh` (`# aw:prompt-sort`) is the prompt sorter's UserPromptSubmit hook. It sorts the real prompt with `judge prompt-sort` (one Jev request), prints a scaffold note only when a scaffold switch is on and fires, never prints skill names (Prism's `prism-route` hook owns skill routing), always exits 0, and kills the judge after `AW_PROMPT_SORT_BUDGET_MS` (default 1500). A non-zero exit or crash of the judge is swallowed. Skips machine text, slash commands and `AW_JUDGE_CHILD`. Install opt-out: `AW_NO_PROMPT_SORT=1`.

`sindri-nudge.sh` (`# aw:sindri-nudge`) is installed only by `scripts/install-sindri.sh --hook-only --provider X` (`setup.sh --with-sindri` runs it per provider); a plain `install-sindri.sh` prints the hint. It runs `sindri repo status --nudge`, which reads the approved profile read-only and never writes the ledger, prints at most one line, kills the CLI after `AW_SINDRI_NUDGE_BUDGET_MS` (default 1500), always exits 0, and is silent outside git, without sindri, before any approval, for an onboarded repo (a linked worktree counts), on any error, and for `AW_JUDGE_CHILD` / `AW_SINDRI_CHILD` sessions.

## Meta harness hooks (`/meta`)

`meta-stop-gate.sh` (`# aw:meta-stop-gate`, Stop) and `meta-outbox.sh` (`# aw:meta-outbox`, PostToolUse matcher `SendMessage`) are installed only by `scripts/install-meta.sh --provider claude` (`./setup.sh --with-meta`; Linux and macOS: aw-meta reads /proc on Linux and the system tools sysctl, vm_stat, ps and lsof on macOS, and any other OS gets AW-META-010 naming it), for Claude Code only: Codex and Cursor have no messages between sessions. Both exit 0 at once without `aw-meta` (`$AW_META_BIN`, default `$AW_STATE_DIR/bin/aw-meta`) and for every session except the one `aw-meta role-set` registered as the meta orchestrator (aw-meta decides that).

- `meta-stop-gate.sh` runs `aw-meta stop-check --session <session_id> --transcript <transcript_path> [--hook-active]`, which reads new inbound messages from the transcript (they reach no hook any other way) and lists what the meta still owes. aw-meta exit 1 → the hook exits 2 with the list on stderr, so the meta can't go idle with a reply owed. It blocks even when `stop_hook_active` is set: aw-meta's own loop guard lets an unchanged list through after `AW_META_STOP_MAX` (default 3) re-blocks in one turn and logs an incident; its warning comes back with exit 0 and the hook shows it as a `systemMessage`. Any other aw-meta exit fails open (`meta-exit-<N>`).
- `meta-outbox.sh` pipes the hook payload to `aw-meta outbox-record`, which closes the obligations a send answers. It always exits 0 and prints nothing.

The non-Claude lane hooks are installed user-wide by `scripts/install-meta.sh --provider codex|cursor` (through the adapters; Codex then needs the `/hooks` trust step):

- `meta-lane-gate.sh` (`# aw:meta-lane-gate`) acts only when `AW_META_TEAM` and `AW_META_LANE` are set (`scripts/meta-lane.sh` sets them in a lane's environment); anything else exits 0 at once, as does `AW_JUDGE_CHILD`, a missing aw-meta, or a session that is not the lane's own agent: no `AW_META_LANE_PID` (which `scripts/meta-lane.sh` exports in every lane, with `AW_META_LANE_AGENT` and `AW_META_LANE_PROC`), a Claude lane, or a nested instance (a `codex exec` the agent started). Both lane hooks take the lane's agent root as the ancestor whose parent is the lane script, whatever it is called (the npm install of Codex is a node wrapper), and treat a second agent-named stretch below it as nested. UserPromptSubmit/sessionStart/postToolUse run `aw-meta lane-inbox` (Codex: plain stdout; Cursor: `additionalContext`). Stop pipes the text after the last `REPLY TO META:` line of `last_assistant_message` to `lane-reply --stdin`, (once per reply: a checksum in `lane-sessions/<lane>.replied`, cleared when new mail is delivered, by `lane-inbox` or by a `lane-stop-check` block showing a new owed list), then runs `lane-stop-check`: exit 1 → the hook exits 2 with the reason on stderr; every other exit passes (`lane-inbox-exit-<N>`, `lane-reply-exit-<N>`, `lane-stop-check-exit-<N>` are logged).
- `session-id-report.sh` (`# aw:session-id-report`) writes the `session_id` atomically (0600) to `lane-sessions/<lane>` for a lane, and to `$AW_SINDRI_HOME/launches/<launchId>.session-id` when `AW_SINDRI_LAUNCH` is set; otherwise exit 0 at once. Causes: `field-missing`, `id-invalid`, `lane-name-invalid`, `launch-id-invalid`, `write-failed`, plus the input causes.

Contract: `docs/csharp/meta.md`. Tests: `config/hooks/tests/meta-hooks.test.sh` (a stub aw-meta, directly and through both adapters) and `failopen.test.sh`.
