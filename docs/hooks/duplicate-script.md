# Duplicate-script gate

Before an agent writes or runs a script that redoes a helper the command catalogs already have, the gate points at that
helper with its exact invocation (issue 4221, section 3). Hook: `config/hooks/dup-script-gate.sh`, with the work after its
pre-filter in `config/lib/dup-script.sh`. Policy: `duplicate-script/1`.

It covers Claude Code's PreToolUse `Bash` and `Write`, Codex's shell and `apply_patch`, and Cursor's `preToolUse` Shell and
Write (through the adapters). `scripts/install-dup-script-guard.sh` registers it; every provider's `install.sh` runs it.
Running a scratch script and the prompt to register a new helper come in a later part of the issue.

## Coverage

| Host | Event | Shell | Script write | Block mode denies |
|---|---|---|---|---|
| Claude Code | PreToolUse `Bash\|Write` | `Bash` | `Write` (`file_path`, `content`) | both |
| Codex | PreToolUse `.*` | the shell, mapped to `Bash` | `apply_patch`: its `*** Add File:` sections | the shell only; `apply_patch` gets advice only (**unverified: no captured payload**) |
| Cursor | `preToolUse` | `Shell`, mapped to `Bash` | `Write` (`file_path`/`path`/`target_file`, `content`/`contents`/`text`) | the shell only; Write gets advice only (**unverified: no captured payload**) |

Not covered: Claude `Edit`/`MultiEdit` (fragments, not whole scripts), Cursor `beforeShellExecution` (left out so a Shell call
is checked once), `bash -c "$(curl ...)"`, `eval`, a script built over several calls, and a file written at runtime by
another program. Those calls pass with no check.

## What it reads

- The catalogs `scripts/aw-commands.sh` reads, found without git:
  - the repo's: the first `.agentic-workflow/commands.json` walking up from the call's `cwd`;
  - the toolkit's: `AW_COMMANDS_TOOLKIT` when set, else `$AW_STATE_DIR/bin/cs/current/commands.json` (install-cs ships it),
    else `config/commands.toolkit.json` beside the hook's lib in a checkout.
- An entry's optional `detect` (`schemas/repo-config/commands.schema.json`):
  - `all`: regexes that must all match the script text, case-insensitive;
  - `params`: per declared param, a regex whose first capture group that matched is the value.

  An entry without `detect` validates and behaves as before. The gate can only ever match it as "uncertain".

## How it decides

1. **Pre-filter.** Pure bash on the raw payload: no jq, no git, no file read. A Bash call goes on only with a heredoc (`<<`), a
   `while`/`until` loop with `sleep`, or output sent to a `.sh`/`.bash`/`.py` file. A Write goes on only with a shebang or a
   script extension (`.sh .bash .py .mjs .js`), and a Codex `apply_patch` only with an `*** Add File:` section. Every other
   call exits 0.
2. **Index.** Both catalogs are flattened once per revision (path, size and mtime of each) into
   `$AW_STATE_DIR/duplicate-script/index-<key>.json`. The repo's entry wins when an id is in both. No catalog: allow, plus a
   `HOOK-FAILOPEN` line `no-catalog`.
3. **Match.** One jq run over the script text (a Write's content, the whole Bash command with any heredoc, or the `+` lines
   of the first `apply_patch` Add File section that is a script; at most 64 KiB). The payload is read from stdin, never
   passed as an argument, so a large one doesn't fail. For each entry:
   - **reuse:** the text runs the entry (its `argv[0]`, plus `argv[1]` when that is a word). The entry is skipped;
   - **canonical:** a Write to the entry's own entrypoint (same base name). The call is no duplicate;
   - **unambiguous:** every `detect.all` regex matches and every required param is found. The invocation is filled in;
   - **uncertain:** `detect` matches but a param is missing, or 3 or more of the entry's id and terms are words of the text.

   The best match wins: unambiguous over uncertain, then the higher score.

| Match | Advisory (default) | Block mode |
|---|---|---|
| unambiguous | allow + suggestion, `wouldDeny` +1 | deny (exit 2) + suggestion, every time |
| uncertain | allow + advice once per entry per session | the same |
| helper's `argv[0]` not on PATH | allow + install hint, once per entry | the same, never a deny |
| `# aw-dup-ok: <reason>` | allow, recorded | allow, recorded |
| none, canonical, reuse, malformed, no catalog | allow, silent (malformed and no catalog log a failopen line) | the same |

## Mode

`ds_mode` in the lib:
- When `config/lib/policy.sh` sits beside the lib, it calls `policy_mode duplicate-script/1`.
- Until that file lands (the #4257 P0 PR), it reads `AW_POLICY_DUPLICATE_SCRIPT_1_MODE` directly. The read is marked
  `# TODO(#4257)`.

Only `block` turns block mode on. Unset or any other value is advisory.

## The exception

A line `# aw-dup-ok: <reason>` in the script, or at the end of the Bash command, lets the call through. It is recorded in
`exceptions`. The reason is required: control characters become spaces, it is trimmed, and cut to 200 characters. A marker
with an empty or blank reason is no exception, and a deny then says a reason is needed. Repeating the same duplicate
without a reason is denied again in block mode.

## Text the agent sees

- Unambiguous: ``duplicate-script: this script redoes `<id>` (<purpose>). Run it instead: `<invocation>` (`aw-commands show <id>`). If it can't do what you need, add `# aw-dup-ok: <why>` and go on.``
- Block mode: the line `duplicate-script: blocked (policy duplicate-script/1, block mode).` first, on stderr.
- Uncertain: ``duplicate-script: `<id>` may already do this (<purpose>): `aw-commands show <id>`. Shown once per session.``
- Not installed: ``duplicate-script: `<id>` does this (<purpose>), but `<argv0>` is not on PATH: <first prerequisite>.``

The text holds only catalog fields and the param values found. It never quotes the script.

## The state file

`$AW_STATE_DIR/duplicate-script/<session id>.json`. The folder is 0700 and files are 0600. The session id must match
`^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$`. Writes take the mkdir lock `<file>.lock` (pid inside, retried every 50 ms for 2 s)
and rename a whole new file over the old one. It holds no script text.

| Field | Type | Meaning |
|---|---|---|
| `version` | integer | `1` |
| `policy` | string | `duplicate-script/1`; a file with another policy or version is read as empty |
| `session`, `provider`, `updatedAt` | string | as in the retry guard's file |
| `advised` | object | entry id (or `ni:<id>` for a not-installed hint) to the UTC time it was shown; dedupes advice |
| `exceptions` | array | the last 50 `{id, tool, path, reason, at}`; `path` is the Write's file path, `""` for Bash |
| `counters.suggestions` | integer | unambiguous matches shown (advisory or block) |
| `counters.wouldDeny` | integer | unambiguous matches allowed with the suggestion (advisory mode) |
| `counters.denies` | integer | unambiguous matches denied (block mode) |
| `counters.advisories` | integer | uncertain matches shown |
| `counters.deduped` | integer | uncertain matches not shown again in the session |
| `counters.notInstalled` | integer | install hints shown |
| `counters.exceptions` | integer | calls let through by `# aw-dup-ok:` |

## Budget

- No pre-filter hit: under 20 ms median over 50 runs. The suite measures it (7.7 ms on the build box, 2026-11-24).
- Candidate path: about 140 ms warm and 180 ms cold (the index is built once per catalog revision). That is measured, not gated.
