# Retry guard: the state file

The retry guard stops an agent from repeating a tool call that already failed when nothing has changed since (issue 4248).
Two hooks share one state file per session:

- `config/hooks/retry-record.sh` runs after a tool call. On a failure, it classifies the error text and records the
  problem. On a success, it clears the problem of that same call, if there is one.
- `config/hooks/retry-gate.sh` runs before a tool call. With no state file, it exits 0 at once. Otherwise, it hashes the
  call and denies it or adds advice, by the table in "What the gate does".

aw-meta (`problem-sync`, Phase B) and the judge (`RepeatProgressQuestion`, Phase C) read this file. They code against this
document, so a change to a field here is a change to their contract: bump `version`.

## Where it lives

`$AW_STATE_DIR/retry-guard/<session id>.json` (default `AW_STATE_DIR` is `~/.agentic-workflow`). The folder is 0700 and the
file 0600. A session id must match `^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$`, or the hooks write nothing.

- Writers take the mkdir lock `<file>.lock` holding their pid (`rg_lock` in `config/lib/retry-guard.sh`: the lock
  `config/lib/locks.sh` makes, retried every 50 ms for 2 s, a dead holder reclaimed). A writer writes the whole file to
  `<file>.tmp.<pid>` and renames it over the file, so a reader never sees half a file. Readers take no lock.
- turn-origin's 24 h sweep removes files older than a day, as it does for `judge/sessions`.
- A file that does not parse, or has another `version`, is removed. The hook then logs one `HOOK-FAILOPEN` line (cause
  `state-unreadable`) and allows the call. The next failure starts a new file.

## Fields

| Field | Type | Meaning |
|---|---|---|
| `version` | integer | The file format, `1`. |
| `policy` | string | The classifier version, `retry-guard/1`. A file with another policy is read as empty, so old problems never match new rules. |
| `session` | string | The session id (the file name without `.json`). |
| `provider` | string | `claude`, `codex` or `cursor` (`AW_PROVIDER`, default `claude`). |
| `updatedAt` | string | UTC time of the last write, `YYYY-MM-DDTHH:MM:SSZ`. |
| `problems` | object | The open problems, keyed by call hash (see below). A cleared problem is removed. |
| `overrides` | array | The last 20 overrides, oldest first (see below). |
| `counters` | object | Totals for the session (see below). |

### A problem: `problems.<call hash>`

| Field | Type | Meaning |
|---|---|---|
| `key` | string | `<session>\|<kind>\|<resource>\|<policy>`: the one problem key aw-meta dedupes on. |
| `kind` | string | `missing-exe`, `auth-expired`, `bad-args`, `missing-path`, `transient` or `unknown`. |
| `resource` | string | What to fix: the executable name (`missing-exe`), the auth host such as `github.com` (`auth-expired`), the missing path, absolute (`missing-path`), or the call hash (`bad-args`, `transient`, `unknown`). At most 256 characters, no control characters. |
| `tool` | string | The tool name after the adapter's mapping (`Bash`, `Read`, `mcp__github__get_pr`, ...). |
| `exe` | string | The first word of a Bash command (`gh`, `rg`), or `""` for other tools. Used in the deny text. |
| `failures` | integer | Identical failures recorded for this hash, 1 or more. |
| `denies` | integer | Calls of this hash the gate denied. |
| `advisories` | integer | Calls of this hash the gate allowed with advice. |
| `firstAt`, `lastAt` | string | UTC times of the first and the latest recorded failure. |
| `retryAfter` | string | `transient` only: the earliest time a retry is not "too soon", or `""`. The wait doubles from 30 s per failure, up to 300 s. |
| `authStamp` | string | `auth-expired` only: the mtime (epoch seconds) of the provider's auth file when the failure was recorded, or `""` when no auth file is known for the host. For `github.com` the file is `${GH_CONFIG_DIR:-~/.config/gh}/hosts.yml`. |

### An override: `overrides[]`

| Field | Type | Meaning |
|---|---|---|
| `hash` | string | The call hash that was let through. |
| `kind` | string | The kind of its problem. |
| `reason` | string | The text after `# aw-retry:` in the Bash command, at most 120 characters, control characters removed. |
| `at` | string | UTC time. |

### Counters: `counters`

All integers, starting at 0.

| Field | Counts |
|---|---|
| `failures` | Failures recorded (every kind). |
| `denies` | Calls denied. |
| `advisories` | Calls allowed with advice. |
| `overrides` | Calls let through by `# aw-retry:`. |
| `cleared` | Problems removed because the same call then succeeded, or because the gate saw the prerequisite change. |
| `clearedAfterChange` | Of `cleared`: problems that had been denied or advised at least once before they cleared. Each one is a retry the guard avoided. |

## The call hash

`sha256` (hex, lowercase) of the tool name, one newline, then `jq -S -c .tool_input` of the payload. For `Bash`, a trailing
`# aw-retry: <reason>` comment is removed from `tool_input.command` first, so an override hashes the same as the call it
repeats. Any changed argument gives a new hash. The hooks store the hash, never the input or the tool output.

## What the gate does

| Kind | Classified from (case-insensitive) | Gate, for the same hash |
|---|---|---|
| `missing-exe` | `command not found`, `Exit code 127` | Deny while `command -v <exe>` still fails. Once it succeeds, clear the problem. |
| `auth-expired` | `401`, `bad credentials`, `token expired`, `gh auth login`, `authentication required`, `not logged in` | Deny once while the auth file's mtime is unchanged and less than 10 min have passed. After that, advise. A changed mtime clears the problem. |
| `bad-args` | `unknown flag`, `unknown option`, `unknown command`, `invalid option`, `unrecognized option`, `usage:`, `invalid argument` | Bash: deny. Other tools: advise only. |
| `missing-path` | `No such file or directory` with a path in the text, or `File does not exist` from a tool with a `file_path` or `path` argument | Deny while the path still does not exist. Once it exists, clear the problem. |
| `transient` | `timed out`, `timeout`, `could not resolve host`, `temporary failure in name resolution`, `connection reset`, `connection refused`, `HTTP 5xx`, `rate limit` | Never deny. Advise "wait N s" while `retryAfter` is in the future. |
| `unknown` | anything else | Never deny. Advise from the 3rd identical failure on. |

The table is checked top to bottom: the first kind that matches wins. An interrupt (`is_interrupt: true`) is never recorded.

A deny exits 2 with one line on stderr that names the fix, for example:
`` retry-guard: `rg` is not installed (this exact call failed with "command not found"). Install it or use another command. To repeat it on purpose, end the command with `# aw-retry: <reason>`. ``

Advice exits 0 with `{"hookSpecificOutput":{"hookEventName":"PreToolUse","additionalContext":"retry-guard: ..."}}` on stdout.

## Example

```json
{
  "version": 1,
  "policy": "retry-guard/1",
  "session": "5f0c2a9e-1b7d-4c1e-9d2a-0a6b1c2d3e4f",
  "provider": "claude",
  "updatedAt": "2026-11-24T10:04:12Z",
  "problems": {
    "3b1f0c4e9a7d2b6c8e5f1a0d4c7b9e2f6a3d8c1b5e0f7a2d9c4b6e1f8a3d5c7b": {
      "key": "5f0c2a9e-1b7d-4c1e-9d2a-0a6b1c2d3e4f|missing-exe|rg|retry-guard/1",
      "kind": "missing-exe",
      "resource": "rg",
      "tool": "Bash",
      "exe": "rg",
      "failures": 1,
      "denies": 2,
      "advisories": 0,
      "firstAt": "2026-11-24T10:03:40Z",
      "lastAt": "2026-11-24T10:03:40Z",
      "retryAfter": "",
      "authStamp": ""
    }
  },
  "overrides": [
    { "hash": "9e2f6a3d8c1b5e0f7a2d9c4b6e1f8a3d5c7b3b1f0c4e9a7d2b6c8e5f1a0d4c7b", "kind": "unknown", "reason": "polling the deploy", "at": "2026-11-24T10:01:02Z" }
  ],
  "counters": { "failures": 4, "denies": 2, "advisories": 1, "overrides": 1, "cleared": 2, "clearedAfterChange": 1 }
}
```

## Provider coverage

| Provider | Failure input | Pre-tool deny |
|---|---|---|
| Claude Code | `PostToolUseFailure` (`error`, `is_interrupt`); `PostToolUse` clears | every tool (`PreToolUse .*`) |
| Codex | `PostToolUse` whose shell `tool_response` says `Exit code: N` or `Process exited with code N`, or has an `exit_code`, with N not 0. The adapter keeps the event `PostToolUse` (other hooks see no change) and adds `aw_tool_failed: true` and `error` (the result text, 8 KiB at most) | every tool the Codex adapter maps |
| Cursor | `postToolUseFailure` (`error_message`, `is_interrupt`); `postToolUse` clears | `preToolUse` and `beforeShellExecution`. Post-tool output is advice only (`additional_context`) |

The adapters README holds the full table (`config/hooks/adapters/README.md`).
