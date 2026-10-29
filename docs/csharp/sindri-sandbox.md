# Sindri sandbox: contracts

The contract of the Sindri sandbox phase (spec `docs/superpowers/specs/2026-10-18-sindri-sandbox-design.md`, revision 6, "the spec"). This page
is written in SB-0 and holds what the later phases (SB-1 to SB-11) build against: the types, the codes and their kinds, the owner-voice
copy, the profile keys, the plan-file schema, the shim table and what works inside a Step. **No sandbox behavior exists yet.** Until SB-5a
the product Host registers only `UnavailableSandbox`, so every Step launch is refused with `SND-LAUNCH-014`, and a `sindri open` session
opens unconfined as today until SB-5b (spec §3.9).

Every session Sindri launches for a Step run runs confined or does not run (spec SB1). The mechanism is bubblewrap and a systemd cgroup
scope on Linux (SB-2, SB-3a), Seatbelt on macOS (SB-9). The agent CLI's own policy file is the second layer. The threat model is honest
mistakes, not a deliberate escape (spec §2).

## Glossary

| Word | Meaning |
|---|---|
| **Plan** | The data the pure builder produces for one launch: mounts (read-only, read-write, denied), network rules, environment allowlist, limits, credentials. Written to `<launch dir>/areas/<id>/sandbox-plan.json`; hashed; summarised on `StartedLaunch`. |
| **`planHash`** | The hash of the plan, including the resolved login source. Two launches with the same hash were confined the same way. |
| **Park** | A run waiting for the owner. A **`launch` park** (`-015`, `-016`) resumes into a **new run** (new token, launch id and worktree). A sandbox-marked **NoRuntime park** (`-014`, `-017`) resumes into a new agent run that tries the launch again. |
| **Step failure** | `step.failed(launch)` (`-018`, `-021`) or `step.failed(limit)` (`-019`): the Step ends, the owner retries or uses the desk twin; never auto-retried in the same run. |
| **Resource wait** | `-020`: a queue condition, not a fault. The launch activity retries until the Step's state timeout. |
| **Check again** | The button and the `sindri sandbox check` command: re-runs the box probe, the form check and the login check, shows every blocker on one card and lists the parked Steps it would resume (oldest first, up to the free session slots). |
| **Credential source** | The adapter behind `IModelCredentialSource` that supplies the model login: `owner-copy` (the default) or `separate-login`. |
| **Attended session** | A session the owner opens with `sindri open` (no Step run). Sandboxed from SB-5b on. |

## Types (`Aw.Sindri.Core`)

| Type | Where | Notes |
|---|---|---|
| `SessionEndReason.Limit` | `Events/` | A sandbox limit killed the session. The engine maps it to `step.failed(limit)`. |
| `SessionEnded` | `Events/` | Gains `kind` (`LimitKind`: `memory`, `wall`), `limit`, `peak` (longs: bytes for memory, seconds for wall) and `peaks` (`LimitPeaks`). All left out of the JSON when null, so the exit and lost shapes are unchanged. |
| `StartedLaunch.Sandbox`, `LaunchStarted.Sandbox`, `LaunchStartedPublic.Sandbox` | `Launch/`, `Events/` | `SandboxSummary`: mechanism, `planHash`, writable paths, network group names, network mode, inner sandbox, limits, credentials, login source, `attended`. Paths are listed, contents never. Left out when the launch was not sandboxed. |
| `StepPolicy.LoginSource` | `Launch/` | `ModelLoginSource?`: the stricter login source a Step asks for (`OwnerCopy` < `SeparateLogin`); null means no ask and is left out of the JSON. The launcher's stricter-of merge is SB-1/SB-5a. |
| `ModelLoginSource` | `Sandbox/` | `owner-copy`, `separate-login`. |
| `IModelCredentialSource`, `LoginStatus`, `LoginState`, `PreparedLogin`, `LoginMount`, `LoginHandle` | `Sandbox/Credentials/` (its own namespace: it needs `AgentId` from `Launch`, which uses `Sandbox`) | The credential port (SB18). `Prepare` returns plain data the plan builder consumes. The fake, the contract base and the adapters are SB-1 and SB-2. |
| `SandboxReasons`, `SandboxCodes`, `SandboxRefusal`, `LaunchCodeKind` | `Sandbox/` | The closed reason sets, the kind table (with the `step.failed` reason of a step-failure code: `limit` for `-019`, `launch` for `-018` and `-021`), and the factory that puts `reason`, `source` and named details (`which`, `tool`, `path`, `cap`, `running`, ...) on the error as extensions. `ReasonOf(error)` and `SourceOf(error)` read them back. |
| `SandboxDenied`, `SandboxLimitHit` | `Events/` | The `sandbox.denied` and `sandbox.limit-hit` payloads (emitted from SB-4b/SB-5a). |
| `SandboxLearnEnded` | `Events/` | The `sandbox.learn-ended` payload (`sindri sandbox learn off`). |
| `SandboxSettings` and its records | `Profile/` | The profile's `sandbox:` section. |
| `SandboxEnvironment` | `Sandbox/` | The three test-redirect variable names. |

## Codes and kinds

The engine routes on the code, never on text. `Golden/Sandbox/code-kinds.json` is the one table both sides read; `SandboxCodes.All` is its
code form, and `Code_kinds_match_the_engine_table` pins them together.

| Code | Kind | HTTP | Reasons (`reason` extension) |
|---|---|---|---|
| `SND-LAUNCH-014` | no runtime | 503 | `no-bwrap`, `no-user-namespace`, `no-user-manager`, `no-cgroup-controllers` (which), `no-forwarder`, `unsupported-platform` |
| `SND-LAUNCH-015` | launch park | 409 | `form-none`, `form-reserved`, `tool-not-expressible`, `bin-denied`, `host-not-tmux`, `worktree-outside-repo`, `no-stored-plan` |
| `SND-LAUNCH-016` | launch park | 409 | the failing canary: `fs-deny`, `fs-write`, `git-ref`, `env`, `login`, `hooks-run`, `net-forward`, `net-deny` |
| `SND-LAUNCH-017` | no runtime | 409 | `no-login`, `expired`, `source-unsupported`, with the `source` extension (`owner-copy`, `separate-login`) |
| `SND-LAUNCH-018` | step failure | 503 | `no-key`, `http-<status>`, `no-network` |
| `SND-LAUNCH-019` | step failure | none | `memory`, `wall`. Catalogue only: it rides on `session.ended` and `step.failed` and is never an HTTP response |
| `SND-LAUNCH-020` | resource wait | 503 | `sessions` |
| `SND-LAUNCH-021` | step failure | 503 | `install-failed`, `lockfile-missing`, `timeout` |

`FailedLaunch` carries the error. The reason and source also reach the ledger and the launches view: `launch.failed` carries them in its `detail` as `detail.reason` and `detail.source` (`LaunchFailureDetail.Of` reads the error's `reason` and `source` extensions; `source` is left out when null; there is no top-level `reason` or `source`), and `GET /sindri/launches/{id}` serves them (read from the detail), so the engine's NoRuntime park and the app pick the sentence by `(code, reason)` without the in-process error. The named details stay on the in-process error until SB-5a needs them on the card. Build the error with `SandboxRefusal.For(definition, reason, detail, source)`, which refuses a reason the code does
not define. The sentence the owner sees for each `(code, reason)` is in `Golden/Sandbox/copy.json` (a Step wording and an attended wording;
the `deskCapable` suffix is added to every `-014` and `-017` sentence). A golden test checks every owner string against the word list: the
words "scope", "plan", "canary" and "confined" never appear.

## Events

| Event | Fields |
|---|---|
| `launch.started` | gains `sandbox` (the `SandboxSummary`) |
| `session.ended` | `reason: limit` adds `kind`, `limit`, `peak` and `peaks`; a final usage sample precedes it. Written by `LaunchSessionEvents.EndByLimit`; the launches view's `ended` carries `kind`, `limit` and `peak` only: the tripped limit is what the limit ending shows, so `peaks` stays on the event |
| `sandbox.denied` | `{launchId, kind: network\|policy\|path, target, rule, count}`; the 1st, 10th and 100th denial of each (kind, target) per run; at most 200 distinct targets, then one event per `kind` (the key includes the kind, so up to three) with `overflow: true`, an empty `target`, rule `overflow` and `count` the denials not counted by target; a denied command line is never recorded whole |
| `sandbox.limit-hit` | `{launchId, kind: pids\|memory\|disk, peak}` |
| `sandbox.learn-ended` | `{repo?, at}`; the owner ended network learn mode early (`repo` left out: every repo). Public, invalidates no view. Key `sandbox.learn-ended:<repo\|all>:<at ticks>` |

Wire shapes are goldens under `Golden/Sandbox/events/`. The spec writes `launchId` in these payloads; on the wire the field is `launch`, as in every other launch event.

## Profile keys

The `sandbox:` section is documented key by key in `docs/csharp/sindri-profile.md` (checked by `scripts/check-profile-doc.mjs`). A profile
with no `sandbox:` section is valid and gets `SandboxDefaults`; `sindri doctor` prints "using the built-in sandbox defaults". The retired key
`sandbox.agentHome` is not a key.

## The plan file

`$AW_SINDRI_HOME/launch/areas/<launchId>/sandbox-plan.json` (0600), written by the launcher before the agent starts and read by a resume, which is
refused with `-015 no-stored-plan` if it is gone. `AW_SANDBOX_PLAN` names a value-free copy that `sindri sandbox explain` reads and nothing
else. The summary's `inner` is the resolved `on` or `off` (`InnerState`), never the profile's `auto`. Shape (camelCase, enums by their wire names; SB-1 owns the C# type and a golden of it):

```json
{
  "planVersion": 1,
  "mechanism": "bubblewrap",
  "planHash": "9f2c0a41d8e6",
  "attended": false,
  "launch": "LCH-…",
  "worktree": "/work/web/.claude/worktrees/sindri-x-1",
  "runBranchNamespace": "sindri/<instance>/<run>",
  "mounts": [ { "path": "/", "mode": "ro" }, { "path": "<worktree>", "mode": "rw" }, { "path": "$AW_STATE_DIR", "mode": "deny" } ],
  "writable": [ "<worktree>", "<run ref directory>", "<run directory>", "<scratch>" ],
  "network": { "mode": "enforce", "groups": [ "model", "packages" ], "proxyPort": 0, "sindriPort": 0 },
  "environment": [ "PATH", "HOME", "AW_SANDBOX", "AW_SINDRI_LAUNCH_SECRET" ],
  "limits": { "memoryMax": "12GiB", "cpuQuota": "400%", "tasksMax": 2048, "wall": "70m" },
  "credentials": { "github": "none" },
  "login": { "source": "owner-copy" },
  "inner": "on"
}
```

Environment entries are names, never values. The summary on `StartedLaunch` is this plan's `mechanism`, `planHash`, `writable`, group names,
network mode, `inner`, limits, `credentials` and `login`.

## The shims

The sandbox puts `dotnet`, `npm` and `npx` shims first on `PATH`. Each calls `heavy --expensive|--light /abs/tool` explicitly, because
`heavy` classifies by the literal command text. The table is `Golden/Sandbox/shims.json`; SB-3b checks it against the case list in
`bin-r1b/heavy`. A Step that already holds a heavy slot gets `AW_SANDBOX_SLOT_HELD=1` and the shims exec the real tool directly;
`AW_HEAVY_LOCK_HELD` is never in the session's environment.

| Shim | Subcommand | Route |
|---|---|---|
| `dotnet` | `build`, `publish`, `format`, `pack`, `restore`, `run`; `test` without `--no-build` | `heavy --expensive` |
| `dotnet` | `test --no-build`, `new`, `sln`, every other | `heavy --light` |
| `dotnet` | `--version`, `--info`, `--list-sdks`, `--list-runtimes` | direct |
| `npm` | `ci`, `install`, `test` | `heavy --expensive` |
| `npm` | `run`, `ls`, `view`, every other | `heavy --light` |
| `npm` | `--version` | direct |
| `npx` | `vitest`, `tsc` and any command `heavy` lists as expensive | `heavy --expensive` |
| `npx` | every other | `heavy --light` |

## What works inside a Step

Rows marked "probe" are confirmed by the spike (SB-S1 item 9) before SB-2 merges.

| Command | In a Step | If it fails |
|---|---|---|
| `git status`, `diff`, `log`, `show`, `add`, `commit` in the worktree | works; unsigned, identity from the sandbox's git settings | |
| `git checkout -b <other>`, `tag`, `stash`, `update-ref` on another ref | fails (`cannot lock ref`): only `refs/heads/sindri/<instance>/<run>/` is writable | commit on the run branch |
| `git branch -D`, any ref delete or rename | fails (`packed-refs.lock` is read-only) | Sindri removes the run's refs from outside |
| `git fetch`, `pull`, `push`, `gc`, `worktree add`, `config` | fails | Sindri sends the push and PR as effects |
| `dotnet build -warnaserror`, `dotnet test` (unit lane) | works (probe), through the shim and `heavy` | a new package needs the `packages` group |
| Integration tests that start Temporal | fail: `$AW_STATE_DIR/cache/bin/` is denied | run host-side as the gate |
| `dotnet format --verify-no-changes` | works (probe) | |
| `scripts/dotnet-verify-packages.sh`, `binaries-lock.sh fetch` | fail: signature and release hosts are not in a group | run host-side |
| `npm ci`, `npm test` in a TS package | `npm ci` fails (scripts off, network `packages` only); a repo that declares `sandbox.repos.<repo>.prepare.dirs` has `node_modules` installed on the host before the launch (SB-11, spec §3.8) | declare the directory, or run the package's tests host-side |
| `bash scripts/ci/bash-tests.sh`, `scripts/tests/*.test.sh` | mostly work; tests that mint tokens, run Docker or need `$HOME` state fail | run the suite host-side |
| `node scripts/check-deps.mjs`, `scripts/sync-rules.sh --check` | work | |
| `scripts/sindri-prove.sh`, anything starting Sindri | fail: no state dir | not a Step's job |
| `docker`, Playwright browsers, `serena` | fail: no socket, no display | ask a block |
| `sindri workflow complete`, `sindri sandbox explain`, MCP `sindri-step` | work through the Sindri forwarder | |
| Hooks (`block-destructive`, `detect-secrets`, `done-gate`, …) | run from the read-only agent-home settings; the meta hooks do nothing by design | the `hooks-run` canary proves it at every launch |

A missing path may be denied, not absent: run `sindri sandbox explain <path|host|command>` before working around it. Never touch the
`locks/` or `judge/` directories under the state directory.

## Owner-facing surfaces

The copy, the states (`Golden/Sandbox/states.json`), the accessibility rows (`accessibility.json`), the doctor lines (`doctor.json`), the
tmux status lines (`statusline.txt`, `statusline-attended.txt`, each under 80 characters with the prefix read from tmux), the takeover
banner (`takeover-banner.txt`) and the in-pane refusal (`refusal.txt`) are goldens under `tests/Aw.Sindri.Tests/Golden/Sandbox/`.
`Aw.Sindri.Ui.Tests` links the directory and reads it, so a change to any of them runs the UI lane (SB-6 builds the view models over it).

## The pure core (SB-1)

`Aw.Sindri.Core.Sandbox.Plan` builds the plan with no I/O: `SandboxPlanner.Build(mechanism, SandboxRequest)` returns a `SandboxPlan` or a refusal, and every adapter's
`ISandbox.Plan` calls it. The host gives it the box's facts (`SandboxHostFacts`: home, state directory, RAM, cores, the environment the allowlist copies from), so Core reads no
environment. `ISandbox` is `Probe`, `Plan`, `SelfCheck` and `Wrap`, all returning `Result`. The product registers `UnavailableSandbox` (every call is `-014 unsupported-platform`);
`PassThroughSandbox` and `RecordingSandbox` live in `Aw.Sindri.Testing` and only test hosts register them (`Product_host_registers_only_UnavailableSandbox`).

| Piece | Rule |
|---|---|
| Mount stack | Bottom to top, a later entry wins: root read-only, `/dev`, `/proc`, scratch as `/tmp`, an empty sized home, the home allowlist (defaults plus profile plus repo; `.dotnet`, `.cargo`, `.rustup`, `.local/bin` and the cache paths), the agent home from the prepared login, the permanent denies, the hook inventory, the per-launch files, the writable set, then `sandbox.fs.denyExtra` and the line's extra denies last |
| Agent home | The login's files are rewritten by rename, so they are bound by directory; a file bind of `.credentials.json`, `.claude.json` or `auth.json`, or a writable agent-home root, is `-016 login`. The owner's original `.credentials.json` and `auth.json` are masked inside the read-only bind |
| Git directory | Read-only, with `objects/`, `worktrees/<name>/`, `refs/heads/<ns>/` and `logs/refs/heads/<ns>/` writable for a WriteScope session. The branch is `<ns>/work` |
| Session home (rev 7) | The real `$AW_SINDRI_HOME` and `<real home>-sessions/` are denied, then masked with a small tmpfs so the mountpoints exist (also for a custom home outside `$HOME`). Bound after the masks, shallow to deep, each at its real path: the real `launches/` (rw), the real `approved/` (ro), the launch's session home `<real home>-sessions/<launch>/` (ro), its launcher-written `control.json` (ro) and `gh-config/` (rw). The plan names the paths; the launcher (SB-5a) makes the folder and writes the file. `SessionHomePaths` is the one place that builds the path. Test `Session_home_links_resolve_in_plan` (and one for a custom home) |
| C# binaries (rev 7) | The host facts carry the stamp directory `bin/cs/current` resolved to (`SandboxHostFacts.CsBinDir`; Core touches no file system). The plan binds it read-only at its own path and again at `$AW_STATE_DIR/bin/cs/current`, because the wrappers in `~/.local/bin` exec into `current`, a link the sandbox does not have. `DOTNET_ROOT` is bound read-only when it is not `~/.dotnet`. The `.nvm` bind, `NVM_DIR` and the toolkit checkout bind described the TS `judge` and are gone. Test `Wrapper_resolves_current_to_stamp` |
| Hook inventory | `HookInventory` lists the state paths the installed hooks use and each hook with its "answers permissions" and "rewrites" columns and its treatment (below); `Hook_inventory_matches_config_hooks` reads `config/hooks` so it cannot drift |
| Environment | `EnvironmentAllowlist` lists every name with a reason; the host environment is never copied wholesale; `AW_HEAVY_LOCK_HELD` is never set; `AW_SINDRI_HOME` is always the launch's session home and `GH_CONFIG_DIR` its `gh-config/` (rev 7); a secret-shaped name in the login's environment is `-016 env` |
| Git settings | `GIT_CONFIG_COUNT/KEY_n/VALUE_n` (command-line scope, so they beat the repo's config) and an empty `GIT_CONFIG_GLOBAL` |
| Limits | `LimitsResolver`: memory auto is a fifth of RAM up to 12 GiB, CPU is cores times the share, wall is timeout plus grace (4 h without a timeout, at most `longMax` plus grace), the loose ceiling adds `parkMax`; an attended session has neither unless `sandbox.attended.wall` is set |
| Host groups | `HostGroups`: `model` follows the target, `packages` for a session that may write, `github` only with `agent-app` and never attended, profile groups replace built-ins of the same name, the repo adds groups and hosts, a Step may remove a group |
| Login source | `LoginSourceResolver`: repo, then target, then profile; a Step's ask is the stricter of the two. `ModelCredentialSources` never falls back: a source that cannot deliver is `-017` with its id |
| `planHash` | 12 hex digits of SHA-256 over a canonical text of the plan with per-launch paths replaced by placeholders, so one policy hashes the same across launches; it includes the login source |
| Renderer | `SandboxPlanRenderer.Show` and `Explain` are pure over the plan (goldens in `tests/Aw.Sindri.Tests/Sandbox/Plan/Golden/`) |

**Fix round 2 (#355 run 2).** A narrow `Bash(<cmd>:*)` in a read-only Step stays narrow (only commands inside the read-only set survive); model API key values are never in the plan or the hash (the launcher supplies them at start, like the launch secret); a request that is both attended and carries a `StepPolicy` is an error; the agent-home write set also applies to `homeAllow` and `cachePaths`; Edit, Write and NotebookEdit patterns must be clean and inside a root; the git directory must be inside the repo; the run namespace is exactly three git-safe segments; `DISABLE_AUTOUPDATER=1` is set (spike item 11; the spec text still needs the line, with the next amendment); `TERM`, `LANG`, `USER` and `SHELL` stay out of the hash.

**Fix round 1 (#355 run 1).** The builder refuses, with `-015 worktree-outside-repo` (the closest closed reason; the detail says which), a ReadOnly or Plan Step that names a writable path, and
any path that is not absolute and normalized (no `..`, no `.`, no `//`, no trailing slash, not empty): the repo, the worktree (strictly inside the repo), the git directory, the run directory and
run area (inside the Sindri home), the home, the state directory, the Sindri home and the socket directory; the run namespace must be `sindri/<instance>/<run>`. A credential source may set
only `CLAUDE_CONFIG_DIR`, `CODEX_HOME` and the model key (`-016 env` otherwise), and a Codex home under `/tmp` is `-016 login`. A read-write bind inside the owner's `~/.claude` or `~/.codex` must be
in `AgentHomeWriteSet` (Claude: projects, sessions, session-env, shell-snapshots, file-history, todos, history.jsonl, backups, cache, state; Codex: sessions, log, memories, shell_snapshots, cache, tmp).
The credential masks follow what is bound: the owner's `.credentials.json` and `auth.json` are masked whenever the owner's real home covers them (a `homeAllow` entry included), and not when a
`separate-login` home from another host path is bound there. A `separate-login` home is outside the owner's `~/.claude` and `~/.codex`, so `AgentHomeWriteSet` does not apply to it: it is bound whole and read-write. `PATH` is the shims, then `$AW_STATE_DIR/bin-r1b` (so the shims find `heavy`), then the host tools outside the state directory.
The `-015 worktree-outside-repo` reason also covers a bad home, state directory, Sindri home, socket directory or C# binaries stamp (`bin/cs/<stamp>`, not a clean path inside `bin/cs`) (a Sindri fault either way; the detail names the path).
A repo declaration cannot add `github` or `packages` beyond what the session kind allows. The inner policy carries `DefaultMode` (`dontAsk` for a Step, `default` for an attended session) and
`DisableBypassPermissions`: **SB-5a's rendered settings must write `permissions.defaultMode` and `disableBypassPermissionsMode: "disable"` explicitly**, because the owner's own settings may say
`bypassPermissions` (spike items 1 and 6); an attended session gets `Ask` rules for Bash, Edit and Write and no Bash allow. SB-5a test names: `Rendered_settings_set_the_default_mode_explicitly`,
`Rendered_attended_settings_ask_for_bash`. Not in SB-1: the `control.json` directory bind for the `sindri` CLI (spike item 7; SB-5a). The `expiresAt` margin of `-017 expired` is in SB-2.

**Hooks inside the sandbox (§5; owner, 2026-10-18).** Hooks help performance and quality, so the default is to keep them. Only a hook that weakens the sandbox is treated:
it is dropped when it answers `allow` in a way that bypasses `dontAsk`, and a hook that rewrites commands is kept with every denied command also denied in its rewritten form.
`HookInventory.Scripts` holds each hook's row and `HookPermissionPolicy` applies it (`TreatmentOf`, `ForSession(installed)`); `DenyRuleExpansion.Expand(rules, hooks)` adds the rewritten
forms for each kept `KeepDenyRewrites` hook. The adapter passes the installed hooks in `SandboxHostFacts.InstalledHooks`; a hook the inventory doesn't list is kept and named in the plan's notes
(`PlanHooks.Notes`, printed by `sindri sandbox show` as `Note:` lines). The tests check the "answers" and "rewrites" columns against what each script prints.
**`rtk-rewrite` is strict in Steps (owner, 2026-10-18; enforced in SB-5a).** The hook still rewrites commands to their `rtk` form, but its `allow` grants no permission inside a Step:
a Step runs only what its own tool list names, so `npm test` or `cargo build` in their `rtk` form don't run in a read-only Step whose list doesn't name them. SB-1 records the rule;
SB-5a's rendered settings put it into effect.

| Hook | Runs on | Answers permissions | Rewrites | Treatment | Why |
|---|---|---|---|---|---|
| `block-destructive` | PreToolUse Bash | no | no | keep | denies only (exit 2): a guard that tightens |
| `block-push-main` | PreToolUse Bash | no | no | keep | denies only (exit 2): a guard that tightens |
| `context-guard` | PostToolUse * | no | no | keep | adds context and writes digests; answers nothing |
| `detect-secrets` | PreToolUse Bash | no | no | keep | denies only (exit 2): a guard that tightens |
| `done-gate` | Stop | no | no | keep | blocks a stop (exit 2) until the brief's proof is shown; answers no permission |
| `done-gate-annotate` | PostToolUse Agent | no | no | keep | annotates the result; never blocks or answers |
| `external-write-guard` | PreToolUse * | allow | no | **drop** | answers 'allow' to every tool call it doesn't match, so with it every tool would pass 'dontAsk'; its external writes stay refused by the deny rule 'gh:*' and by 'dontAsk' for tools outside the Step's list |
| `git-context` | SessionStart | no | no | keep | prints git context; answers nothing |
| `judge-health` | SessionStart | no | no | keep | warns when the judge is degraded; answers nothing |
| `meta-lane-gate` | UserPromptSubmit, SessionStart, PostToolUse, Stop (Codex and Cursor lanes) | no | no | keep | acts only in a Meta lane (does nothing in a Sindri launch); blocks a stop, answers no permission |
| `meta-outbox` | PostToolUse SendMessage | no | no | keep | records sent messages for the meta; answers nothing |
| `meta-stop-gate` | Stop | no | no | keep | blocks only the registered meta's stop; answers no permission |
| `outbox-flush` | TeammateIdle | no | no | keep | keeps a teammate working (exit 2); answers no permission |
| `prism-context` | SessionStart | no | no | keep | loads memory context; answers nothing |
| `probe-log` | every event, while the probe is on | no | no | keep | logs the hook input; prints nothing |
| `prompt-sort` | UserPromptSubmit | no | no | keep | may print a scaffold note; answers nothing |
| `record-teammate-name` | PreToolUse Agent | allow | no | **drop** | always answers 'allow' to Agent, so every dispatch would pass 'dontAsk'; without it subagent-start-map uses its dispatch-order fallback |
| `rtk-rewrite` | PreToolUse Bash | allow | yes | **keep, deny the rewritten forms; strict in Steps** | rewrites commands to their 'rtk' form for token savings (the owner keeps it); every denied command is also denied in its 'rtk' form, so 'git push' can't pass as 'rtk git push'; its 'allow' grants nothing inside a Step, which runs only what its own list names (owner, 2026-10-18; enforced in SB-5a) |
| `scope-gate` | PreToolUse Agent | allow | no | **drop** | answers 'allow' to an Agent dispatch with a good brief, so dispatches would pass 'dontAsk' |
| `send-gate` | PreToolUse SendMessage | allow | yes | **drop** | answers 'allow' to SendMessage (and rewrites the message), so messages would pass 'dontAsk' |
| `session-id-report` | SessionStart (Codex and Cursor) | no | no | keep | reports the session id to Sindri's launcher; answers nothing |
| `sindri-nudge` | SessionStart | no | no | keep | prints a reminder; answers nothing |
| `subagent-start-map` | SubagentStart | no | no | keep | maps a subagent to its brief; answers nothing |
| `turn-origin` | UserPromptSubmit | no | no | keep | marks the turn's origin; answers nothing |
| `winch` | PreToolUse *, Stop (inline) | no | no | keep | asks the owner's terminal to redraw; answers nothing |
| `aw-live` | in-process mod (the /live pane) | no | no | keep | draws this session's numbers; answers nothing |

## SB-2: the bubblewrap adapter and the owner-copy login

`Aw.Sindri.Infrastructure/Sandbox/` holds `BubblewrapSandbox` (Probe, Plan, Wrap, SelfCheck), `BubblewrapArgv` (plan to arguments), `OwnerCopyCredentialSource`, the canaries and the run-area preparer.
`AddSindriBubblewrapSandbox` registers them; **nothing calls it until SB-5a**, and a test pins that the product Host registers only `UnavailableSandbox`.

- **Argv.** `--die-with-parent --unshare-net --clearenv`, then the mount stack in plan order (`--ro-bind`, `--bind`, `--dev`, `--proc`, `--perms 0700 --size N --tmpfs` for the home and `/run`), then `--setenv` for every
  valued variable, `--chdir`. A denied path under a tmpfs, or whose link target is, adds nothing; a denied directory is an empty read-only tmpfs (writable while the hook inventory is bound into it);
  a denied file is an empty read-only file. A variable the plan leaves to the launcher (the launch secret, a model key) never reaches a command line: the launcher puts the per-launch values in `ILaunchSecrets`, `Wrap` writes them as NUL-separated `--setenv` pairs to a 0600 file under the run area and returns a `sh` wrapper that opens it on fd 3, deletes it and runs `bwrap --args 3` (so `ps` shows the file's path, not a value; bubblewrap closes the descriptor before the agent starts, and the real-bwrap test checks the agent has none; closing fds 3-9 in the forwarders is SB-4a's). A `Wrap` removes any older file in the launch's own directory, `secrets/<launch id>/`, and the launcher calls `ISandbox.Discard(plan)` on its failure path, so a file never outlives a launch whose wrapper did not run; one launch's `Wrap` or `Discard` never touches another's. `Discard` swallows an IO or access failure and reports it through the sandbox's warning sink, so it never replaces the launch's own error; the 0600 file then stays for the run area's cleanup. The self-check canaries are wrapped without any secret.
  No pid namespace (SB6). `PlanMount.Optional` marks the entries that use the `-try` forms (toolchain directories, the hook inventory).
- **The plan gained** a `/run` tmpfs (the net directory needs a place to bind on a read-only root), `AW_STATE_DIR` when the state directory is not `~/.agentic-workflow`, and this worktree's
  `projects/<key>` bound read-write from the owner's agent home into the config directory (its `memory/` read-only), so Sindri's readers and a resume find the transcripts.
- **Owner-copy login.** `launches/<launch>/login` (0700) gets `.credentials.json` and `.claude.json` (0600, temp file and rename) and is bound at `~/.claude-run` (`CLAUDE_CONFIG_DIR`); every other entry of
  `~/.claude` outside the write set is bound read-only into it; the original `.credentials.json` stays masked in the read-only `~/.claude` bind. The source reports `LoginStatus.ExpiresAt` and holds no policy: `ModelCredentialSources.Check`/`Prepare(needed)` turns "valid for less than the time the Step needs" into `-017 expired`
  (spike item 2: assume a refresh rotates the token); the caller always passes the time it needs (there is no default). A Claude `.claude.json` copy has every MCP server's `env` and `headers` values emptied. Codex gets `~/.codex-run`
  with `auth.json`, the config read-only and `sessions/` shared; its login is gated too (the earlier of the token's `exp` and `last_refresh` plus Codex's 8-day refresh age: Codex refreshes an older login at start whatever the token has left). The agent-home write set is the spike's, three files included
  (`mcp-needs-auth-cache.json`, `.last-update-result.json`, `.last-cleanup`). The run area and the rw state directories (`judge`, `locks`, `context-guard/state`) are made by the preparer; a missing worktree is `-015` and a missing run directory `-016`, never created; `SelfCheck` prepares the run area once before the first canary and returns those refusals unchanged.
  `SweepStale` is the crash sweep; `Release` removes a copy.
- **Separate-login (SB-8).** `SeparateLoginCredentialSource` (`Infrastructure/Sandbox/`, registered beside owner-copy) supports `claude-code` and `codex`. Its login is the target's own agent home,
  `AgentHomes.For(sindriHome, target)` = `$AW_SINDRI_HOME/agent-homes/<target>`. `Probe` reads `.credentials.json` (Claude Code) or `auth.json` (Codex) in it: no home or no file is `no-login`; an unreadable file is `no-login`;
  an access token past its expiry is still ready when the file holds a refresh token (a Step uses the home in place and may refresh it, spec §3.4, §15), and `expired` only when it has none. `Prepare` returns one read-write `LoginMount` that binds the home at its own path (host path = sandbox path) and `CLAUDE_CONFIG_DIR=<home>` or `CODEX_HOME=<home>`;
  nothing is copied and `Release` does nothing. The owner's `~/.claude` and `~/.codex` are not bound, so the plan binds no `projects/<key>` from them (the transcripts are in the agent home) and masks nothing. The home sits inside
  Sindri's home, which the plan empties with a tmpfs, so its bind is placed after that mask (`PlanMounts.AddLogin`, `inSindriHome`).
- **Self-check.** `fs-deny`, `fs-write`, `git-ref` (the sandbox's git settings win; two ref updates in the run's directory work; another ref does not; the `selfcheck` ref is removed from outside afterwards),
  `env`, `login`, the three-part `hooks-run` (under bash: `lib/failopen.sh` is bash, and Claude's `~/.claude/hooks` layout: Codex and Cursor lanes need their own directory in SB-5b; its guard checks derive from `plan.Hooks.Kept`, so a dropped hook is never exercised, and `rtk-rewrite` is never relied on: inside a Step its `allow` grants nothing, SB-5a enforces that) and the direct-connect half of `net-deny`. `net-forward` and the proxy half of `net-deny` need the forwarders (SB-4a, SB-5a wires them).
- **Contract.** `ISandboxContract` (in `Aw.Sindri.Testing`) runs on the pass-through (confinement cases skipped) and on the real bubblewrap; it skips cleanly where bwrap cannot make a user namespace. Its `SandboxBox` is a
  fixture repo, worktree, state directory and fake login under the test output directory, with the owner's real home (toolchains, hooks, the judge wrapper) and nothing of the owner's state or login.

## Egress (SB-4a)

`Aw.Sindri.Infrastructure/Sandbox/Egress*` is the outside half of spec §3.2 and the init script of the inside half. All types are internal until SB-5a wires the launcher
(three carry a `MA0182` pragma; **SB-5a removes them**).

**Rules the proxy enforces (enforce mode; learn mode is below):**

- Only ports **80 and 443**. A group names hosts, not ports; a repo that needs another port waits for a later additive `host:port` form.
- The host must be in a group of the plan (`HostGroups.Allowing`). A name that resolves to a loopback, link-local, private, shared (`100.64/10`), multicast or
  reserved address is refused unless a group lists that address; an address literal needs a group that lists it. The proxy dials the address it validated.
- Plain HTTP (an absolute URI, rewritten to origin form with `Connection: close`) and `CONNECT`; no TLS interception.
- A request head must arrive within `HeadTimeout` (30 s). When one side of a connection ends cleanly the other side is half-closed and the other direction gets `HalfCloseGrace` (30 s in total, counted from that moment and not reset by traffic) to finish; a byte-cap stop or a fault ends both at once.

**Caps** (fixed defaults; profile keys later if wanted): 64 concurrent connections and 2 GiB relayed, both **per run**; a **connection lifetime** of 2 h per connection.
The run's own length belongs to the wall watchdog (SB-3a), not the proxy. Over a cap the proxy answers 503 and counts the request as `Refused` under the host `(cap)`.

**`egress.log`** (in the run directory): one line per host, port and verdict, tab separated, sorted: `host<TAB>port<TAB>verdict<TAB>count`; verdict is `allowed`, `learned` (SB-4b), `denied`,
`refused` or `failed` (the host was allowed but the dial failed; the client got 502). No URLs. A malformed request is answered 400 and not counted. The file is
rewritten (temp file and rename) about once a second while anything changed, and at the end, so an open tunnel shows up.

**The init script** (`EgressInit.Render`) closes fds 3 to 9, starts both `socat` forwards with their output appended to a log file (not the pane), exits with
status **70** if either is not running 0.2 s later (the launcher reads that as `-016 net-forward`), exports `NO_PROXY=127.0.0.1,localhost,::1`, and runs the agent as
its **child**, with the init's standard input (held on fd 3, because a shell without job control gives a background command `/dev/null`): the sandbox has no PID namespace, so an `exec` would leave the forwarders running after the agent. A TERM, INT or HUP to the init reaches the agent; the
init waits for it, stops both forwarders and exits with the agent's status. The self-check of the spec's start order is a second run of the same script with a check argv. The script needs a POSIX `sh` (tested under `sh`, `dash` and `bash`); on Linux it reads `/proc/<pid>/stat` to tell a dead forwarder from a zombie, elsewhere `kill -0` alone (a zombie then looks alive, so a macOS profile that reuses the script needs a `ps` fallback; SB-9). The agent starts with SIGINT and SIGQUIT ignored, the POSIX rule for a background command; the launcher sends TERM. Without `ps`: nothing needs it.

### SB-4b: learn mode

- **Verdict.** In learn mode and before the plan's `LearnUntil`, a host no group lists, on port 80 or 443, that resolves to a public address gets `EgressVerdict.Learned`: the
  proxy dials it like an allowed host and `egress.log` counts it as `learned`, so the log is the list of hosts to add to `sandbox.repos.<repo>.hosts`. Private
  addresses stay `Refused` and other ports stay `Denied`. Learned requests are not denials and are not counted as such.
- **Window.** `LearnWindow.Decide(profileMode, history, now)` (pure, Core): learn while the profile says `learn`, the owner has not ended it, the repo has fewer than 10 learn
  runs and `now` is before `FirstLearnRun + 7 days` (the first run's own `now` when there is none). Otherwise enforce with no expiry. `PlanNetwork.LearnUntil` holds the
  instant (omitted from the plan file when null, so existing plans and their hashes are unchanged).
- **Expiry.** The proxy checks `LearnUntil` against its clock on every request, so a run that outlives the window stops learning mid-run without a restart. `sindri sandbox show` and
  `explain` name the date ("learning until 2026-11-03").
- **Seam (SB-5a).** `SandboxRequest.Learn` (`LearnContext`: the repo's `LearnHistory` and the launch time) is what the launcher fills from `ILearnHistory`; without it the planner uses the
  profile mode as before. `EgressService.Start` takes the clock as an optional fourth argument (after the denial counter).

### Learn off (SB-4b)

`sindri sandbox learn off [--repo <repo>] [--json]` ends network learn mode early for one repo, or for every repo without `--repo`. It goes through the running control plane (`POST /sindri/sandbox/learn-off`, body `{repo?}`; exit 0 done, 1 when Sindri isn't running, 2 on a bad repo name or a refusal) and appends `sandbox.learn-ended`. It only tightens, so it needs no approval. It is a desk command: a session Sindri launched for a Step is refused `SND-AUTH-002`; an owner-started session is not (owner decision q93). `LedgerLearnHistory` (the `ILearnHistory` adapter) reads the ledger, not `views.db`: the repo's items come from `item.created`, its learn runs are the `launch.started` rows of those items whose sandbox summary has `networkMode: learn` (`LearnRuns`, and the earliest as `FirstLearnRun`), and `EndedAt` is the latest `sandbox.learn-ended` for the repo or for every repo. A ledger that cannot be read answers with the run cap spent, so the window stays closed. The launcher passes the history into planning in SB-5a.

## SB-3a: wall and memory

`session wrap --launch <id> --limits <file> -- <agent cli>` holds a sandboxed session to its limits. Without `--limits` the wrap runs unwatched, as before; a limits file that cannot be read or has no unit refuses the start (exit 2), because a sandboxed agent must never run unwatched.

- **Limits file** (`WrapLimits`, JSON the launcher writes beside the plan): `unit` (the scope unit), `wallSeconds` (the Step's timeout plus the grace, absent for an attended session), `wallGraceSeconds`, `parkMaxSeconds`, `memoryMaxBytes`.
- **The scope.** `SystemdScopeArgv.Start` builds `systemd-run --user --scope --quiet --unit=sindri-<launch>.scope --slice=sindri-steps.slice` with `MemoryHigh`, `MemoryMax`, `MemorySwapMax=0`, `OOMPolicy=kill`, `CPUWeight=50`, `CPUQuota`, `TasksMax` and, for a Step, the loose `RuntimeMaxSec` (the wall plus `parkMax`). `SliceCap` puts the total memory on the parent slice. The launcher (SB-5a) wraps the bubblewrap command in it, writes `wrap-limits.json` (unit, wall, grace, `parkMax`, memory max) into the launch's run directory and passes `--limits <that file>` to `session wrap` for every sandboxed launch, so a memory or wall kill ends the run as `limit` (#1553). An unsandboxed launch keeps the bare `session wrap --launch <id> --`.
- **The watchdog** (`LimitWatchdog`, one tick every 5 s) is the only clock. It reads the scope's `memory.events`, `memory.current`, `memory.peak`, `pids.current` and `cpu.stat` (the files go with the scope, so it keeps the peaks), asks `GET /sindri/launches/{launch}/suspended` through the launch secret, and counts only unsuspended time (`WallBudget`; an interval counts when the run was unsuspended at both ends). A failed or unreachable answer counts as not suspended, so a runaway is still stopped while `serve` is down. When the wall is used up it sends SIGTERM to the scope (`systemctl --user kill --kill-whom=all`), waits 10 s, then SIGKILL. The watchdog reaches the scope through two ports (`WatchdogPorts`): `IScopeControl` (`SystemctlScopeControl`: `show -p ControlGroup|Result --value`, `kill --kill-whom=all --signal=<SIG>`, `set-property --runtime`, all through `ISandboxProcesses` with a 10 s timeout and plain `systemctl`) and `ICgroupStats` (`CgroupFiles`: the files under `/sys/fs/cgroup`; a missing file is a gone scope, answered as null, so the last sample stands). A memory kill whose poll was missed reports the limit as the peak. Where the ports come from is `WrapExtras` (with the hang-up signal); `--limits` without a watchdog refuses the start.
- **The loose ceiling.** When a suspension ends the watchdog runs `systemctl --user set-property --runtime <unit> RuntimeMaxSec=<elapsed + wall left + parkMax>` (`RuntimeCeiling.Next`; `wall` already holds the grace), so several long parks in a row never cost the run its scope. It is never the clock.
- **How a session ends.** A memory kill is the kernel's (`OOMPolicy=kill`): the watchdog sees `oom_kill` in `memory.events`, or, if the poll missed it, systemd's `Result=oom-kill` of the failed unit. Wrap then reports the exit through `POST /sindri/sessions/{launch}/exit` with `limit` (`kind`, `limit`, `peak` and the peaks of every limit): the service takes one final usage sample (`ISessionUsageSampler.Flush`), then appends `session.ended` with reason `limit`, never `lost`. An ordinary exit takes the same final sample.
- **Not in this phase.** `sandbox.limit-hit` (`TasksMax`, `MemoryHigh`, disk) needs a proven route and is SB-3b/SB-5a's; `GET /sindri/launches/{launch}/suspended` is the ledger read of #1383 (`IRunSuspensions`); the `step.failed(limit)` mapping is P8 U9's; the session cap and the shims are SB-3b.

## SB-4b: denials

`DenialCounter` (Infrastructure `Sandbox/`) is one run's count of what was denied. The launcher (SB-5a) creates one per launch, passes it to `EgressService.Start` (the proxy counts into it and `RunningEgress`'s one-second flush loop writes and publishes it, then once more on stop), sets its `Sink` to the ledger (`LaunchSessionEvents` implements the Core port `ISandboxDenials`) and, at session end, feeds it the transcript and `failopen.log` denials with `Record`. Until then nothing creates one.

- **`denials/<launch>.json`** in the launch directory (`LaunchPaths.Denials`; the directory is 0700; temp file and rename, written only when a count changed): `{launchId, updatedAt, entries: [{kind, target, rule, count, first, last}], overflow: [{kind, count}]}`, entries sorted by kind, target, rule. The API and the app read the true counts here, not from the events.
- **Targets** are a host, a path or a tool name, never a whole command line. Each is scrubbed with `ITextScrubber` first and then cut to 200 characters (a cut can split a token the scrubber would still know), and control characters become `_`.
- **The 200 cap**: at most 200 distinct (kind, target) in a run, counted across kinds. Past it a new target adds to its kind's `overflow` row instead; a target already counted keeps counting.
- **Cadence**: the 1st, 10th and 100th count of each (kind, target, rule) queues a `sandbox.denied`; the first overflow of each kind queues one with `overflow: true`, an empty target, rule `overflow` and count 1 (the running total is in the counter file). `Publish` sends the queue; one the ledger refused stays queued for the next tick, and the key makes a resend safe.
- **Key**: `sandbox.denied:<launch>:<kind>:<targetHash>:<count>`, the hash being the first 12 hex characters of the target's SHA-256. A repeat appends nothing.
- **Rule ids from the proxy**: `no-group` (verdict `denied`: no group lists the host, or the port is not 80 or 443), `private-address` (verdict `refused`: the host resolves to a loopback, link-local or private address), `cap` (a connection or byte cap; target `(cap)`). A learned host and a failed dial are not denials.
- Transcript and `failopen.log` denials (`inner-policy`, `deny-list`, `not-bound`, `hook-fail-open`) use the same counter through `Record`; their readers are SB-4b's other part.

## Reading denials (SB-4b)

Session end looks for denials the proxy can't see (spec §3.6). The pure readers are in `Aw.Sindri.Core/Sandbox/Denials/`; `TranscriptTail` (Infrastructure) reads the last **256 KiB** of a file as lines (a missing or unreadable file gives none; the cut first line is dropped).

- `ClaudeTranscriptDenials.Read(lines)`: a `user` record whose `tool_result` text starts `Permission to use <Tool> ... has been denied` (also the don't-ask-mode and "directory that is denied by your permission settings" forms) becomes `(Policy, target, inner-policy)`. The target is the tool, plus for a shell command only its first word and one subcommand word (`Bash: git push`), never the command line. Other `tool_result` text goes to path extraction.
- `CodexTranscriptDenials.Read(lines)`: Codex has no denied record; the error text of `function_call_output`, a failed `exec_command_end` and a failed `patch_apply_end` goes to path extraction.
- Path extraction keeps absolute paths on lines with `Read-only file system`, `No such file or directory`, `Permission denied` or `Operation not permitted` (`TranscriptDenials.FailedPaths`, at most 200).
- `PathDenials.Match(plan, path, home)` is the likely-denial rule: the last plan mount covering the path decides. A `Deny` mount gives `(Path, path, deny-list)`; a path under `$HOME` that no mount covers (the home tmpfs itself doesn't count) gives `not-bound`; anything else, such as a missing file outside `$HOME`, is not a denial (null).
- `FailOpenLog.Read(lines)`: each `HOOK-FAILOPEN` line of the per-launch `failopen.log` gives `(Policy, <hook>, hook-fail-open)`.

Bad JSON lines are skipped, never thrown; input is bounded (1 MiB per line, 20,000 lines, 500 observations). **Not wired yet:** SB-5a calls the tail reader and these parsers at session end and feeds the observations to the denial counter.

## SB-4b: explain and denials

- **`sindri sandbox explain <path|host|command> [--plan <file>] [--json]`** runs in process, before any service is built, and reads **only** the plan file `--plan` or `AW_SANDBOX_PLAN` names (`SandboxEnvironment.Plan`; the launcher sets it, SB-5a). It never opens `AW_SINDRI_HOME`, `AW_STATE_DIR`, the ledger or telemetry and needs no control plane, so it answers with the state directory denied (test `Explain_reads_only_the_plan_file`). The answer is `SandboxPlanRenderer.Explain`; `--json` is `{"query","answer"}`. Exit 0 answered, 2 usage, no plan file or an unreadable one (one `SND-CLI-003` line).
- **`sindri sandbox denials [--run <launch>] [--since <time|30m|6h|2d>] [--json]`** is a query: `GET /sindri/sandbox/denials?launch=&since=` (`ListDenials`; `IDenialFiles` is the Core port, `DiskDenialFiles` the adapter). It never starts the control plane (exit 1 `SND-API-001` when it is down). The text (`DenialsRenderer`) groups the counters of every launch by target and rule, the most counted first, with the fix by kind: a network host gets "allow `<host>` for this repo: add it to `sandbox.repos.<repo>.hosts`" (a private address and a hit cap get their reason, no fix); a not-bound home path gets `sandbox.repos.<repo>.homeAllow`; a path on the permanent deny list gets its reason and no fix; a tool the agent's own policy refused gets "push from your own terminal, or let Sindri send it" for a git push and "blocked `<tool>` by the agent's own policy" otherwise; the overflow is "+N more not counted by target". `--since` keeps the entries last seen at or after the time.
- **Where the counters are.** The counter flushes to `$AW_SINDRI_HOME/launch/denials/<launchId>.json` (`LaunchPaths.DenialsFile`, the one place the writers and readers take paths from; the first flush creates the folder, 0700), and `DiskDenialFiles` lists that folder, or reads the one file for `--run`. A launch whose counter never flushed, or whose file cannot be read, has no row: the ledger's `sandbox.denied` events are the history, these files the live count. The owner sees a route and a CLI; the item sheet's "Blocked" list is SB-6's.
- **`sindri sandbox exec <launch|run-area> [--json] [-- <command...>]`** (SB-5b) reproduces what a session saw. It is the human's: not an API route, and a CLI that is a session Sindri launched (`AW_SINDRI_LAUNCH` set) or has no readable per-start token gets `SND-AUTH-002`, the same test every human-only verb uses (`CallerHeaders.Resolve`). The target is a launch id (the stored plan is `<launch dir>/areas/<launch>/sandbox-plan.json`) or a run-area directory or plan file. `SandboxPlan.ForExec` (pure) turns that plan into the reproduction: the worktree, the run directory, the run's ref and reflog directories and the git object and worktree directories bind **read-only**; any host folder bound read-write from outside the run area (the owner's transcripts) turns read-only; scratch, spool and login directories move to a fresh `areas/exec-<12 hex>` run area (empty, not a copy, removed when the command ends); the session home, `AW_SINDRI_HOME`, `GH_CONFIG_DIR`, every variable the launcher supplies (the launch secret, a model key) and the GitHub credentials are gone; it has its own launch id and socket directory. `SandboxExec` (Infrastructure) then starts its own throw-away egress, wraps the command (`/bin/sh` without one) and runs it in the Steps' slice (`SystemdScopeArgv`, attended limits: no wall) inside `ISessionCap.Start`, so it counts against `sandbox.limits.sessions` and a full cap is `SND-LAUNCH-020`. Exit code is the command's; usage, a refusal or a sandbox that can't be made is 2; `--json` prints `{"launch","exitCode"}` after the command ends.
- **`sindri sandbox login <claude-code|codex>`** (SB-8) signs a target in to its own agent home, `AgentHomes.For($AW_SINDRI_HOME, target)` (`agent-homes/<target>`, 0700), the login Steps use when `sandbox.login.source` is `separate-login`. It is the human's: not an API route, and a session Sindri launched or a CLI with no readable per-start token gets `SND-AUTH-002`. It runs the target's hook installer with `--target-dir <home>` (`providers/<claude|codex>/install-hooks.sh` under `TOOLKIT_DIR`, default `$AW_STATE_DIR/toolkit`), then the sign-in on this terminal: `claude` with `CLAUDE_CONFIG_DIR=<home>`, or `codex login` with `CODEX_HOME=<home>`. For codex it then prints one line asking you to run `/hooks` in codex with that `CODEX_HOME` to trust the `aw:*` entries. Exit code is the sign-in's; an unknown target or a refusal is 2. The process work is behind `ISandboxLogin` (Core), implemented by `SandboxLoginRunner` (Infrastructure).

## SB-5a: Check again, `sandbox check` and `sandbox show`

- **`ISandboxChecks.Run`** (`Features/CheckSandbox/SandboxChecks.cs`) runs the box probe (`ISandbox.Probe`), the form check, the login probe and a throw-away self-check on a fixture request; every blocker lands on one `SandboxCheckReport`, and one failing check never hides another. The self-check runs only when the probe passed, and is skipped (not failed) while `ISandboxFixture.Request()` has no fixture (`NoSandboxFixture` until the switch registers a real one).
- **Form check** (`-015`): the agent Steps run (the approved profile's `workflow.agent.target`; no target, nothing to check), when installed and `StepOk`, and its policy form is `none` (`form-none`) or `cursor-config` (`form-reserved`). **Login probe** (`-017`): for that same agent (only the profile's `workflow.agent.target`) when its form is expressible, the source the profile picks (repo, then agent, then profile) must deliver the login; one blocker per distinct (agent, source, reason).
- **Resume list**: when nothing blocks, the instances parked on a sandbox code (`ParkReason.Launch`, or a no-runtime park whose question names an `SND-LAUNCH` code), oldest first, no more than the free session slots (`sandbox.limits.sessions`, default 3, less the live scopes). Read-only: Check again lists, the app's Resume sends the answers.
- **Words**: `SandboxCopy` carries the `copy.json` sentences (a test pins them together); a blocker's open values (`<which>`, `<target>`, `<tool>`, `<path>`) come from the refusal's extensions, the `-017` wording from its source. A blocker also carries the refusal's scrubbed `detail` (the canary's observed line, the failing tool line) when it has one: `detail` in the `GET /sindri/sandbox/check` blocker (omitted when null), `saw:` under the sentence in `sindri sandbox check`, in brackets after the sentence in the `sindri doctor` row.
- **`sindri sandbox check [--json]`**: `GET /sindri/sandbox/check` through the running control plane (never started by it); exit 0 ready, 1 blocked. **`sindri sandbox show [--plan <file>]`** reads only the plan file, like `explain`: four lines (Can write, Can read, Network, Limits), then the whole plan.
- **Doctor**: a `sandbox` row (`sandbox ready: <mechanism>, 4 checks passed in <ms> ms`, or one row per blocker with its command as the fix; `sandbox:login` for `-017`; the gray macOS sentence is information). The aot-smoke normalizer drops these rows because they depend on the box.

## GitHub (agent-app) (SB-7)

A Step with `sandbox.credentials.github: agent-app` (never an attended session) pushes and opens PRs as the agent GitHub App, with a token Sindri mints and the agent never holds in its environment.

- **Mint.** `GitHubAppTokenSource` (`Infrastructure/Sandbox/Launch/`) signs an RS256 JWT (`iat` now-60, `exp` now+540, `iss` the App id) with the App key and posts it to `<api>/app/installations/<installation>/access_tokens` with `{"repositories":["<repo>"]}`. The key is the PEM at `AGENT_APP_KEY_FILE`, else the macOS keychain item `agentic-workflow-agent-app` / `private-key` (hex-decoded when hex). `AGENT_APP_ID` (default 5259848), `AGENT_APP_INSTALLATION_ID` (default 169843852) and `AGENT_APP_API_URL` (`https://api.github.com`, or `http://127.0.0.1:<port>` / `http://localhost:<port>` for a test; anything else is refused before the key is read) are read from the environment. No proxy, 30 s per request. It does not shell out to `scripts/agent-token.sh`.
- **Where the token lives.** `<session home>/gh-token/token` (folder 0700, file 0600, one line), written by `AgentTokenRenewal` in `WritePolicy` through a temp file in the same folder renamed over it. The plan binds the folder (not the file) read-only at its own path, so a rename shows up inside the sandbox.
- **Who reads it.** The run area's `shims/gh` sets `GH_TOKEN` from the file for gh's own process only; `shims/git-credential-sindri` answers git's `get` for github.com from the same file. The pre-push hook allows a push only to `refs/heads/<run namespace>` (or under it) and never a delete. What that does and doesn't cover: the Step can read the token file (gh and git need it), so it is kept out of the agent's environment, argv and output, not out of its reach; and the branch rule is a `git push` hook, so `gh` or the REST API with the same token can still act on any branch of the repo the App's permissions allow (it guards against an honest wrong push, not a determined agent). A released launch's token is not revoked; it expires within the hour.
- **Renewal.** `sindri serve` re-mints every 45 minutes per live agent-app launch and renames the new token in; `Release` stops it. A failed renewal keeps the old token (valid 60 minutes), logs `SND-LAUNCH-018` with its reason on stderr, and tries again in 5 minutes.
- **What serve needs.** The key and the `AGENT_APP_*` variables are read from `sindri serve`'s own environment (on Linux there is no keychain, so `AGENT_APP_KEY_FILE` must be set there). The repo the token is narrowed to is the profile's repo `name`, so it must be the GitHub repository's name; another name is GitHub's `http-422`. A renewal timer lives in serve: after a serve restart a running Step keeps its last token until it expires (at most 60 minutes).
- **`SND-LAUNCH-018` reasons.** `no-key` (no key file or keychain item, an unusable key, or a non-numeric App/installation id), `http-<status>` (GitHub answered anything but 201), `no-network` (connect, DNS or the 30 s deadline; also a URL off the allowlist). A failure at launch is a Step failure; the token and the JWT are never in an error, a log or the plan.

## Phases

| Phase | Builds |
|---|---|
| SB-0 | this page, the types, codes, goldens, profile keys, `deps.yaml` and `.env.example` entries |
| SB-1 | `ISandbox`, the pure plan builder, the credential source registry and fakes, `UnavailableSandbox`, the test-only sandboxes |
| SB-2, SB-3a, SB-3b, SB-4a, SB-4b | bubblewrap and the `owner-copy` source; wall and memory; cap and heavy; static egress; denials |
| SB-5a, SB-5b | the launcher integration (ReadOnly first), then WriteScope and sessions you open yourself |
| SB-6 | the UI copy |
| SB-7, SB-8, SB-9, SB-10 | the GitHub App token; `separate-login`; macOS; the first unattended run |
| SB-11 | the per-repo prepare: the launcher hook, `BaseCommit` on the launch, the cache per lockfile hash (built; the onboarding offer is separate) |

## Environment

`AW_SANDBOX_BWRAP`, `AW_SANDBOX_SYSTEMD_RUN` and `AW_SANDBOX_SOCAT` redirect the three host binaries for a test. `AW_SANDBOX_PLAN` is set by the launcher and read only by `sandbox explain`. No variable switches the
sandbox off. `deps.yaml` declares the host-tool edges; `scripts/install-deps.sh` (which `setup.sh` runs) checks `bubblewrap` and `socat` (both soft until SB-5a wires the sandbox into a launch: a failed install warns and setup goes on), and warns when `systemd-run --user --scope` does not work.

## SB-6a: denials by item, and Allow

- **`GET /sindri/sandbox/denials?item=<item>`** adds an item filter: only the launches of that item (`launch.started` names the item, `item.created` its repo; `ILaunchOwners` is the Core port, `LedgerLaunchOwners` reads the ledger, never `views.db`). Every file now carries its `repo` and `item`, and every entry its `fix`: `allow` (a host, or a not-bound home path), `never` with a `reason` (a path on the permanent deny list, a private address, a hit cap) or `own-terminal` (a policy refusal). `DenialFixes.Decide` is the one place that decides it; the counter file on disk stays as it was (the new fields are left out when null).
- **`POST /sindri/sandbox/allow { launch, kind: network|path, target }`** (`AllowDenial`, catalog action `AllowDenial`, desk class: a launched session is refused `SND-AUTH-002`). It checks that the run was blocked on exactly that target and that its fix is `allow`, then edits the **live** profile file (the draft, not the approved snapshot) through `IProfileSandboxWriter` (`YamlProfileSandboxWriter`): `sandbox.repos.<repo>.hosts` gets the host, `sandbox.repos.<repo>.homeAllow` gets `{ path: <relative to $HOME>, mode: ro }`. The edit is line by line, so the rest of the file and its comments stay; a flow-style list with entries is a layout it leaves alone (`SND-PROFILE-002` names the lines to add by hand). Answer `{ state: "needs-approval" | "allowed", repo, entry }`: `allowed` only when the approved profile already lists it; otherwise the live profile now differs from the approved one and the usual drift and approval flow takes over. Allowing twice writes one entry. Refusals are `SND-SANDBOX-001`.
