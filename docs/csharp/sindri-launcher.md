# Sindri session launcher: `launcher.json`

The launcher puts a Sindri session in any terminal and any agent CLI. This page is the file contract and its worked
examples (phase SL-0 of `docs/superpowers/specs/2026-10-15-sindri-session-launcher-design.md`, "the spec" below). The
bash reader is in `aw-meta-launch` (SL-1, `scripts/launcher-lib.sh`; `docs/csharp/meta.md` "Launcher"); the C# ports
inside API P10a (SL-2) are still to come.

## The file

`$AW_STATE_DIR/launcher.json` (override the path with `AW_LAUNCHER_CONFIG`, for tests and the prover's temp home). It is
machine-wide, needs no profile approval, and never holds secrets. One JSON Schema, `schemas/launcher.schema.json`
(2020-12), is the contract for both readers; unknown keys are errors.

```jsonc
{
  "schema": 1,
  "viewer": { "kind": "exec-template", "name": "kitty",
              "argv": ["kitty", "--title", "{title}", "--directory", "{cwd}", "--", "{cmd...}"] },   // warp | exec-template | print
  "host": { "kind": "tmux" },                  // tmux (default) | none (attended opens only)
  "agents": {
    "aider": { "label": "Aider", "bin": "aider", "open": ["{bin}", "--message", "{seed}"],
               "resume": null, "sessionId": "none", "usage": "none", "mcp": "none" },
    "claude-code": { "override": true, "bin": "/opt/claude/bin/claude" }   // a built-in id needs "override": true
  },
  "meta": { "lanesAgent": "claude-code", "lanes": { "review": "codex" } }
}
```

| Key | Meaning |
|---|---|
| `schema` | The format version; this build reads `1`. Required |
| `viewer` | What a human sees. `warp`, `print` (nothing opens; the attach command is printed) or `exec-template` (`name`, the UI label, and `argv`, both required) |
| `host` | Who owns the process: `tmux` (default) or `none` (a GUI app that owns its own agent; attended opens only). Unattended Steps always use tmux |
| `agents.<id>` | An added catalog entry (`label`, `bin`, `open`, `sessionId` required; `sessionIdPattern` required unless `sessionId` is `none`; `reportedId` required for `reported`), or, with `"override": true`, the named fields of a built-in one. Ids match `^[a-z][a-z0-9-]{1,31}$` |
| `agents.<id>.model`, `.policy` | Decided (owner, 2026-10-16). Per-entry form names, like `mcp`, for an unattended Step's per-run model and policy file; see "Model and policy forms" below. Absent means `none` |
| `meta` | `lanesAgent` and per-lane `lanes.<lane>` name catalog agents for `aw-meta-launch` |
| `sessions` | The session collector's settings (`enabled`, `pollSeconds` 1 to 30, `providers`, `tmuxSockets`, `claudeSessionsDir`); optional, and the bash reader accepts and ignores it. See `docs/csharp/sindri-sessions.md` |

Built-in entries (`claude-code`, `codex`, `cursor`) live once in `config/launcher/builtins.json`. The terminal templates
below live once in `config/launcher/terminals.json`; `sindri launch init` and `aw-meta-launch --init-launcher` write
from it.

### Placeholders

A closed set: `{bin}`, `{cwd}`, `{title}`, `{name}`, `{seed}`, `{sessionId}`, `{mcpConfig}`, `{cmd...}`. `{cmd...}` is the
host's attach argv, spliced as separate arguments, so it is a whole element; every other placeholder expands to
exactly one argument and may sit inside one (`--title={title}`). Decision (SL-0 review): embedded placeholders are allowed
although spec section 4 says "no string concatenation inside an argument", because section 5's GNOME Terminal and Konsole
rows and UI section 6.1 (`--resume={sessionId}`) need them; section 4's ban stands for `{cmd...}` only. There is no shell. An expanded `{title}`, `{name}`,
`{sessionId}` or `{seed}` that starts with `-` is refused (the rule applies to the expanded value at launch, not to the
template: `--title={title}` always starts with `--`, and a bare `{title}` element is refused for a title such as `-x`), and `{cwd}` is always absolute. Use the argv forms below
(`--` before `{cmd...}`); a terminal that re-parses a single string after `-e` runs a shell on it.

### `argvHash`

`sha256:` plus the lowercase hex SHA-256 of the RFC 8785 (JCS) form, in UTF-8, of
`{bin (basename), open, resume (null included), sessionId, mcp, model, policy}` taken from the merged entry. `bin`'s directory, `label`,
`processName`, `transcripts`, `usage`, `sessionIdPattern` and `reportedId` are not inputs, so a `bin`-directory-only
override hashes equal to its built-in. The goldens are `tests/fixtures/launcher/argv-hash/*.json`; `node
scripts/check-launcher.mjs` recomputes each.

Defaults for the merged entry, so a C# and a bash implementation hash the same file: an absent `resume` is `null` (no
resume argv; it is in the input as `null`), and an absent `mcp`, `model` or `policy` is `"none"`. `aider-added-defaults.json` leaves them out
and pins the hash; the schema repeats the defaults in the two properties' descriptions.

The `model` and `policy` inputs are the form names only. The per-run values (the model id, the policy file's path) are
never hashed, so one approval holds across runs; changing a form name changes the hash (`claude-code-model-none-override.json`).

### Model and policy forms (decided, owner, 2026-10-16)

Scoping-station spec section 12.5, row L-17. An entry names, like `mcp`, which built-in form passes a per-run model id
(`model`) and a Step's policy (`policy`: a settings file for `claude-settings-file`, typed `-c` overrides with no file for
`codex-overrides`) to its CLI. The Step's policy is target-neutral (owner, 2026-10-17, amending L-17(a)):
`StepPolicy(AllowedTools, Mode {ReadOnly, Plan, Default}, WritablePath)`. The engine writes no policy file; the launcher renders
it per form, writes any file 0600 and deletes it with the launch. A Step only tightens the profile: bypass or full access is never a
Step mode, and a mode the entry can't express is `SND-LAUNCH-013`. Both are enums, default `none`, and an added entry defaults to
`none`; an override keeps the built-in's forms unless it names them. A form adds its arguments only on an **unattended Step
launch** (`attended: false`), after the expanded `open` template. Human opens, `resume` and Meta lanes never pass them. The
closed placeholder set is unchanged: no `{model}` or `{policy}`, so no placeholder expands to an empty or optional argument.
The bash reader (`scripts/launcher-lib.sh`) accepts both keys and never applies them (lanes are attended); it does not check
their values, as for `mcp`. The rendering below is the C# `BuildArgv`'s (SL-2, API P10a U3), with `<id>` the per-run model
id and `<path>` the 0600 settings file the launcher renders from the Step's policy:

| Key | Form | Adds (unattended only) | Built-in |
|---|---|---|---|
| `model` | `claude-flag` | `--model <id>` | claude-code |
| `model` | `codex-flag` | `-m <id>` | codex |
| `model` | `cursor-flag` | **reserved**: nothing until a box with the Cursor CLI verifies its model flag (expected `--model <id>`); a Step that requests it parks with `SND-LAUNCH-013` | none |
| `model` | `none` | nothing | cursor |
| `policy` | `claude-settings-file` | `--settings <path>` | claude-code |
| `policy` | `codex-overrides` | `-c key=value` overrides, one pair per setting the launcher renders from the Step's policy (sandbox, approvals, the writable path) | codex |
| `policy` | `cursor-config` | **reserved**: nothing until the Cursor CLI's config argument is verified; a Step that requests it parks with `SND-LAUNCH-013` | none |
| `policy` | `none` | nothing | cursor |

A Step that requests a model or a policy on an entry whose matching form is `none`, or a reserved form (`cursor-flag`,
`cursor-config`), parks with `SND-LAUNCH-013` and never
launches unrestricted (the API implements the check).

## Worked examples for `exec-template`

Data in `config/launcher/terminals.json`, not built-in code. `node scripts/check-launcher.mjs` checks that each row below
is the file's argv and validates against the schema.

| Terminal | Binary | `argv` |
|---|---|---|
| kitty | `kitty` | `["kitty", "--title", "{title}", "--directory", "{cwd}", "--", "{cmd...}"]` |
| WezTerm | `wezterm` | `["wezterm", "start", "--cwd", "{cwd}", "--", "{cmd...}"]` |
| GNOME Terminal | `gnome-terminal` | `["gnome-terminal", "--title={title}", "--working-directory={cwd}", "--", "{cmd...}"]` |
| Alacritty | `alacritty` | `["alacritty", "--title", "{title}", "--working-directory", "{cwd}", "-e", "{cmd...}"]` |
| Konsole | `konsole` | `["konsole", "--workdir", "{cwd}", "-p", "tabtitle={title}", "-e", "{cmd...}"]` |
| foot | `foot` | `["foot", "--title", "{title}", "--working-directory", "{cwd}", "{cmd...}"]` |
| xterm | `xterm` | `["xterm", "-T", "{title}", "-e", "{cmd...}"]` |

GNOME Terminal and `kitty --single-instance` exit 0 through a server even when no window appears; the launcher's client
check (spec §3), not the exit code, decides whether the outcome is `opened`. WezTerm's `start` has no title flag, so its
template omits `{title}`.

## Terminal process names

Each `config/launcher/terminals.json` row also carries `processNames`: the names its terminal runs as (`wezterm-gui`,
`gnome-terminal-server`), which the session collector's host detection matches against a session's parent chain (by argv[0] or exe basename, not `comm`, which the kernel cuts to 15 characters)
(`docs/csharp/sindri-sessions.md`). The launch argv does not use them. `node scripts/check-launcher.mjs` fails a row
without a non-empty `processNames` and a name two rows share. The `focus` argv templates are CO-4c's, not part of this file's contract yet.

## The bash reader

`scripts/launcher-lib.sh` (sourced by `scripts/meta-launch.sh` and `scripts/meta-lane.sh`) reads the file with `jq`. It
promises less than the C# reader (spec section 4): it refuses unknown keys, unknown placeholders, `{cmd...}` inside an
argument, a viewer kind or host kind outside the closed lists, a `print` or `warp` viewer with a name or argv, a lane agent
id that is not an agent id, an unsupported `schema`, and wrong JSON types for the fields it uses; each as
`SND-LAUNCH-011: <reason>`. Everything else (an added entry without `label`, a built-in id without `"override": true` that no
lane uses, an unknown `usage` reader) is `sindri launch show`'s. Its key lists and placeholder set are the schema's `x-aw-keys`
block, read at run time. `scripts/tests/meta-launch.test.sh` runs every fixture below against its `bash:` tag. With no
file, `aw-meta-launch` keeps the Warp viewer it always had (the spec's in-memory detection is Sindri's; the Meta launcher
must not change what a box without a file gets).

The `sessions` key is new in schema 1 (additive, optional). The reader refuses unknown keys from `x-aw-keys.root`, so an
`aw-meta-launch` installed before the key was added refuses a file that has it ("unknown key \"sessions\"") until
`scripts/install-meta.sh` reinstalls it; one installed from this change on accepts the key and ignores it.

## Fixtures and checks

`tests/fixtures/launcher/` holds the launcher.json fixtures and `manifest.json`: per fixture, the expected `schema`
verdict (`valid` or `invalid`) and the `bash` tag (`accept` or `refuse`) the SL-1 bash reader must honour. The C# reader
(SL-2) runs the same set with the same verdict. `node scripts/check-launcher.mjs` validates them with Ajv 8's `Ajv2020`
class and `strict: false` (the schema's `x-aw-keys` block, which the bash reader reads for its key lists and placeholder
set, is an unknown keyword to 2020-12), and `bash scripts/tests/check-launcher.test.sh` proves each kind of drift
fails. Ajv is a dev dependency of `scripts/` (`npm ci --prefix scripts`; the test installs it when missing).

## T3 Code fallback

T3 Code starts and owns its agent process and can't adopt a `claude --session-id` or `codex resume` session, so it is
not a viewer until the spike (spec §7, phase SL-T) says otherwise. A T3 Code user sets the `print` viewer (or their
terminal's `exec-template`), and `sindri open ITM-…` prints the `tmux -L sindri-launch attach …` command to run in any terminal,
including one inside T3 Code if it has one. The session is the normal tmux-hosted CLI, so liveness, usage, budgets and
takeover all work; T3 Code just isn't the window.

```json
{ "schema": 1, "viewer": { "kind": "print" } }
```
