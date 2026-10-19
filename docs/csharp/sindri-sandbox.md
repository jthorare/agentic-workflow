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
| **Plan** | The data the pure builder produces for one launch: mounts (read-only, read-write, denied), network rules, environment allowlist, limits, credentials. Written to `launches/<id>.sandbox.json`; hashed; summarised on `StartedLaunch`. |
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

`FailedLaunch` carries the error. The reason and source also reach the ledger and the launches view: `launch.failed` gains `reason` and `source` (left out when null, written from the error by the launcher), and `GET /sindri/launches/{id}` serves them, so the engine's NoRuntime park and the app pick the sentence by `(code, reason)` without the in-process error. The named details stay on the in-process error until SB-5a needs them on the card. Build the error with `SandboxRefusal.For(definition, reason, detail, source)`, which refuses a reason the code does
not define. The sentence the owner sees for each `(code, reason)` is in `Golden/Sandbox/copy.json` (a Step wording and an attended wording;
the `deskCapable` suffix is added to every `-014` and `-017` sentence). A golden test checks every owner string against the word list: the
words "scope", "plan", "canary" and "confined" never appear.

## Events

| Event | Fields |
|---|---|
| `launch.started` | gains `sandbox` (the `SandboxSummary`) |
| `session.ended` | `reason: limit` adds `kind`, `limit`, `peak` and `peaks`; a final usage sample precedes it. Written by `LaunchSessionEvents.EndByLimit`; the launches view's `ended` carries `kind`, `limit` and `peak` only: the tripped limit is what the limit ending shows, so `peaks` stays on the event |
| `sandbox.denied` | `{launch, kind: network\|policy\|path, target, rule, count}`; the 1st, 10th and 100th denial of each (kind, target) per run; at most 200 distinct targets, then one event per `kind` (the key includes the kind, so up to three) with `overflow: true`, an empty `target`, rule `overflow` and `count` the denials not counted by target; a denied command line is never recorded whole |
| `sandbox.limit-hit` | `{launch, kind: pids\|memory\|disk, peak}` |

Wire shapes are goldens under `Golden/Sandbox/events/`. The spec writes `launchId` in these payloads; on the wire the field is `launch`, as in every other launch event.

## Profile keys

The `sandbox:` section is documented key by key in `docs/csharp/sindri-profile.md` (checked by `scripts/check-profile-doc.mjs`). A profile
with no `sandbox:` section is valid and gets `SandboxDefaults`; `sindri doctor` prints "using the built-in sandbox defaults". The retired key
`sandbox.agentHome` is not a key.

## The plan file

`$AW_SINDRI_HOME/launches/<launchId>.sandbox.json` (0600), written by the launcher before the agent starts and read by a resume, which is
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
  "inner": "on",
  "denials": { "counterFile": "denials.json", "egressLog": "egress.log" }
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
| `npm ci`, `npm test` in a TS package | fail until the repo declares a host-side prepare (SB-11) | run the package's tests host-side |
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
| Mount stack | Bottom to top, a later entry wins: root read-only, `/dev`, `/proc`, scratch as `/tmp`, an empty sized home, the home allowlist (defaults plus profile plus repo), the agent home from the prepared login, the permanent denies, the hook inventory, the per-launch files, the writable set, then `sandbox.fs.denyExtra` and the line's extra denies last |
| Agent home | The login's files are rewritten by rename, so they are bound by directory; a file bind of `.credentials.json`, `.claude.json` or `auth.json`, or a writable agent-home root, is `-016 login`. The owner's original `.credentials.json` and `auth.json` are masked inside the read-only bind |
| Git directory | Read-only, with `objects/`, `worktrees/<name>/`, `refs/heads/<ns>/` and `logs/refs/heads/<ns>/` writable for a WriteScope session. The branch is `<ns>/work` |
| Hook inventory | `HookInventory` lists the state paths the installed hooks use and each hook with its "answers permissions" and "rewrites" columns and its treatment (below); `Hook_inventory_matches_config_hooks` reads `config/hooks` so it cannot drift |
| Environment | `EnvironmentAllowlist` lists every name with a reason; the host environment is never copied wholesale; `AW_HEAVY_LOCK_HELD` is never set; a secret-shaped name in the login's environment is `-016 env` |
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
`separate-login` home from another host path is bound there. `PATH` is the shims, then `$AW_STATE_DIR/bin-r1b` (so the shims find `heavy`), then the host tools outside the state directory.
The `-015 worktree-outside-repo` reason also covers a bad home, state directory, Sindri home or socket directory (a Sindri fault either way; the detail names the path).
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
- **Self-check.** `fs-deny`, `fs-write`, `git-ref` (the sandbox's git settings win; two ref updates in the run's directory work; another ref does not; the `selfcheck` ref is removed from outside afterwards),
  `env`, `login`, the three-part `hooks-run` (under bash: `lib/failopen.sh` is bash, and Claude's `~/.claude/hooks` layout: Codex and Cursor lanes need their own directory in SB-5b; its guard checks derive from `plan.Hooks.Kept`, so a dropped hook is never exercised, and `rtk-rewrite` is never relied on: inside a Step its `allow` grants nothing, SB-5a enforces that) and the direct-connect half of `net-deny`. `net-forward` and the proxy half of `net-deny` need the forwarders (SB-4a, SB-5a wires them).
- **Contract.** `ISandboxContract` (in `Aw.Sindri.Testing`) runs on the pass-through (confinement cases skipped) and on the real bubblewrap; it skips cleanly where bwrap cannot make a user namespace. Its `SandboxBox` is a
  fixture repo, worktree, state directory and fake login under the test output directory, with the owner's real home (toolchains, hooks, the judge wrapper) and nothing of the owner's state or login.

## Phases

| Phase | Builds |
|---|---|
| SB-0 | this page, the types, codes, goldens, profile keys, `deps.yaml` and `.env.example` entries |
| SB-1 | `ISandbox`, the pure plan builder, the credential source registry and fakes, `UnavailableSandbox`, the test-only sandboxes |
| SB-2, SB-3a, SB-3b, SB-4a, SB-4b | bubblewrap and the `owner-copy` source; wall and memory; cap and heavy; static egress; denials |
| SB-5a, SB-5b | the launcher integration (ReadOnly first), then WriteScope and sessions you open yourself |
| SB-6 | the UI copy |
| SB-7, SB-8, SB-9, SB-10, SB-11 | the GitHub App token; `separate-login`; macOS; the first unattended run; the per-repo prepare |

## Environment

`AW_SANDBOX_BWRAP`, `AW_SANDBOX_SYSTEMD_RUN` and `AW_SANDBOX_SOCAT` redirect the three host binaries for a test. No variable switches the
sandbox off. `deps.yaml` declares the host-tool edges; `scripts/install-deps.sh` (which `setup.sh` runs) checks `bubblewrap` and `socat` (both soft until SB-5a wires the sandbox into a launch: a failed install warns and setup goes on), and warns when `systemd-run --user --scope` does not work.
