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
| `session.ended` | `reason: limit` adds `kind`, `limit`, `peak` and `peaks`; a final usage sample precedes it. Written by `LaunchSessionEvents.EndByLimit`; the launches view's `ended` carries `kind`, `limit` and `peak` |
| `sandbox.denied` | `{launch, kind: network\|policy\|path, target, rule, count}`; the 1st, 10th and 100th denial of each (kind, target) per run; at most 200 distinct targets, then one event with `overflow: true`, an empty `target`, rule `overflow` and `count` the denials not counted by target; a denied command line is never recorded whole |
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
sandbox off. `deps.yaml` declares the host-tool edges; `setup.sh` and `scripts/install-deps.sh` gain `bubblewrap` and `socat` in SB-2, the
first phase that needs them.
