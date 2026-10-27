# Sindri sandbox phase: confining a Step session, and the sessions you open, on the host

**Status:** design, revision 7 (owner answers of 2026-10-25 on #1113 folded in; Steps still refuse with `-014` until SB-5a merges: the Linux track goes now, in parallel with a macOS track, with no stopgap; the per-launch session home and the human token, §3.1, §3.4; revision 6 was the owner's final read; after /autoplan rounds 1, 2 and 3 and the engine lane's three reviews, §14) · **Date:**
2026-10-18 · **Owner decisions:** answered 2026-10-18 and folded in as revision 5 (§13): a takeover stays sandboxed (Q1); the model login is an injectable credential source (Q2, SB18); sessions you open with `sindri open` are sandboxed too (Q6, SB19, §3.9), once SB-5b lands and unsandboxed as today until then (final read); the first unattended Step is read-only (Q14); every other default accepted. Inherited: the threat model is honest mistakes, not a
deliberate bad actor (README "Gates guard against…", A §17.28.3, `docs/csharp/bugfix.md` "Threat model"); a Step only
tightens its policy (owner, 2026-10-17; engine P8 D8); Steps run in tmux on the host (README X55) · **Closes:** README
X59(a) · **Builds on:** the session launcher spec `2026-10-15-sindri-session-launcher-design.md` (LCH), SL4, SL8, §3, L-6,
and `docs/csharp/sindri-launcher.md` "Model and policy forms" (the scoping-station spec's L-17); the scoping-station spec
`2026-10-15-sindri-scoping-station-design.md` (SC), W9 (the read-only capability) and W11 (the Step directory); the workflow
engine spec `2026-10-10-sindri-workflow-engine-design.md` (WF), §5.5, §6.5, P8; the blocks-over-MCP spec
`2026-10-18-sindri-blocks-over-mcp-design.md` (BM); the session collector spec `2026-10-16-sindri-session-collector-design.md`
(CO); the first Sindri design `2026-10-07-sindri-design.md` (D), §8.2 and §13.1, which this replaces for the host (§4); the
line spec `2026-10-15-sindri-spec-to-proven-pr-line-design.md` (LN), LN-WF3, LN-WF6, LN-LCH1; the engine lane's binding
constraints for P8 (cited **engine P8 D3, D8, D9, D10, D11**; D4, D7 and the 2026-10-18 `launch.outcome` update where named)
and its review of this spec (E1-…, §14) · **Lanes:** the API lane builds the sandbox inside the launcher (SB-1…SB-5b); the
ideas lane runs the spike and builds the macOS adapter, the GitHub credential path and the per-repo declaration; the
engine lane (P8) consumes the contract, owns the engine side of §6 and §7, and reviewed it; the UI lane builds the copy
(SB-6) · **Spike:** SB-S1 recorded in §15, 2026-10-18 (items 2 and 12 provisional on passive evidence; the live rotation check is an owner step) · **Next:** the owner's read of revision 7 (§13 has one open question, from rev 7), then /specToProvenPR takes each phase to PRs.

Co-written with the engine lane (P8 consumes it).

Today a Step's agent session is just a CLI in a tmux pane on the owner's box, as the owner. `ILauncher` (LCH §3) starts it
with `Attended: false`, in a per-run worktree, with a `StepPolicy` (L-17) that names a permission mode and a writable
path, and nothing stops the session from reading `~/.ssh`, writing the owner's other repos, running `git push`, or
starting a build storm. D §8.2 answered that with a container per session and a model proxy; the owner then chose tmux on
the host (X55) and the container's tmux "later". This spec is the "before any unattended Step runs on the host" phase
that X59(a) promised, and it is deliberately smaller than D §8.2: a thin, per-launch confinement of the agent's process
tree, built from tools the box already has, with the CLI's own policy inside it as a second layer.

## 1. Decisions

| # | Decision | Reason |
|---|---|---|
| SB1 | **Every session Sindri launches runs confined, or does not run.** Confinement keys on the **launch**, never on the `Attended` flag: a **Step run** (a launch or a resume that carries a `StepRef`, or reuses a launch that has a stored plan) and, **from SB-5b on, a session you open yourself with `sindri open`** (owner decision Q6, 2026-10-18; until SB-5b it opens unconfined as today, owner's final read; §3.9). A resume or an `OpenItem` of either reuses the stored plan `launches/<id>.sandbox.json` and is confined; if the plan is gone it is refused (`-015`, reason `no-stored-plan`). **Meta lane sessions are the one exception**: they are the meta team's own tmux sessions, started by `scripts/meta-launch.sh` and not through Sindri's launcher, and they stay as they are. A launch whose confinement can't be built or proven never falls back to unconfined (§6); for a one-off the owner's own terminal is outside Sindri and always available. The one loosening an owner can choose is a time-boxed network *learn mode* (Q13), which still confines the filesystem and the credentials | The owner wants Steps unattended and wants the sessions it opens itself confined too; one unconfined launch through Sindri is what the spec prevents. Keying on `Attended` would let a resumed Step come back unconfined (R1-02) |
| SB2 | **Sindri owns the boundary; the agent CLI's own policy is the second layer.** Outer: a Sindri-built sandbox around the CLI's whole process tree (bubblewrap on Linux, Seatbelt on macOS) plus a cgroup scope for limits. Inner: L-17's per-launch policy file (mode, allow and deny rules, Codex overrides), which gives the agent a clear refusal instead of `EACCES` and still helps on a target Sindri hasn't taught the outer layer (§5) | Cursor and added entries have no usable inner sandbox, and three CLIs' sandbox behaviour changes by version. But their rules are cheap and L-17 already needs them |
| SB3 | **Filesystem: read-only everywhere, writable in one small set.** Writable: the run's worktree, the run's own ref directory in the repo's git dir (SB5), the **run directory** (§3.1), a private scratch, and a short list of tool caches and hook spool directories. `$HOME` is an empty tmpfs rebuilt from an allowlist; Sindri's state and the secret stores are denied except named paths (§3.1). The worktree stays an ordinary host directory, readable by the engine after the session ends (engine P8 D10) | Matches `StepPolicy`: with `WriteScope` the worktree is the one writable path; without it nothing in the repo (engine P8 D8) |
| SB4 | **Network: default deny, one allowlisting proxy, loopback to Sindri.** The session has no network of its own. It reaches Sindri's API and `/sindri/mcp` and the egress proxy, both through unix-socket forwarders; `NO_PROXY` keeps the Sindri port off the proxy (§3.2) | The honest mistakes are `curl \| sh`, a typo'd package, a push to a real remote. A namespace plus a name-checking proxy works alike on Linux and (with Seatbelt's localhost rule) macOS |
| SB5 | **The git directory is read-only, with one writable directory: the run's ref directory.** The launcher creates the worktree and its branch `sindri/<instance>/<run>/work` (engine P8 D8: the launcher creates the worktree) and tells the sandbox the directory `<ns>` = `sindri/<instance>/<run>`. The sandbox binds `<repo>/.git` read-only and read-write only `objects/`, `worktrees/<name>/`, `refs/heads/<ns>/` and `logs/refs/heads/<ns>/`. The branch is one level inside its own directory because a ref update is a lock-file-and-rename in the ref's directory: with the branch directly under `refs/heads/`, either `refs/heads/` would have to be writable or the first commit would fail with `cannot lock ref`. `config`, `hooks/`, `packed-refs`, `refs/stash`, tags, other runs' directories and `refs/remotes` are read-only (E1-I6) | A commit needs the shared git dir; with the whole `.git` writable, one `git branch -D` rewrites the owner's branches. The self-check proves it with a real ref update (§6), not `commit --dry-run` |
| SB6 | **No process-ID namespace** (accepted limit, Q8 and Q9). The heavy-job wrapper finds a session by walking to the nearest `claude` ancestor (`~/.agentic-workflow/bin-r1b/heavy`) and the collector reads the process table (CO §4). The cost: a same-user `pkill`, and a shared `/proc` (§3.1, "what it can't stop"). The safety hooks and the inner deny rules cover the common forms | Honest-mistake model; the lock design is the owner's |
| SB7 | **Limits are a cgroup scope under one parent slice, plus one watchdog that owns the wall clock; a kill ends the session with a named reason.** CPU, memory and pids come from `systemd-run --user --scope` properties under a parent slice with its own total memory cap. Wall time has **one owner, the `session wrap` watchdog**, which asks `GET /sindri/launches/{launch}/suspended` (SB16) and counts only unsuspended time; when that answer is unavailable (the control plane is down) the run counts as **not suspended**, so a runaway is still stopped. The scope's `RuntimeMaxSec` is only a **loose last-resort ceiling** for when wrap or tmux died; it is never the clock. It is set at start to the Step's timeout plus `wallGrace` plus `sandbox.limits.parkMax` (default 24 h) and **extended on every resume**: when a park, answer or handback ends a suspension, `session wrap` runs `systemctl --user set-property --runtime <scope> RuntimeMaxSec=<real time elapsed + unsuspended time left + wallGrace + parkMax>`, so `parkMax` is the longest **single** suspension and several long parks in a row never cost the run its scope (R3-03). A kill ends in `session.ended` with the new reason `limit` and `kind` `memory` or `wall` (§3.3); the engine maps it to `step.failed(limit)` (a new engine rule, E1-I2). **The wall clock pauses whenever the run is suspended, a park or a takeover** (E1-B2); it resumes with what remained | A runaway session must hit its own limit, not take the box down. A park keeps its session (engine P8 D11): a fixed real-time limit set at start would kill the session an owner is about to answer (R2-01) |
| SB8 | **The engine passes a policy; the launcher maps it.** The engine gives `Attended: false`, `Mode: Open`, `Model: null`, the worktree as cwd, `StepRef` and a `StepPolicy` (`WriteScope` → the worktree is the only writable path, otherwise ReadOnly; `AllowedTools` empty = the CLI's defaults for the mode) and **never names a host, argv, bin, environment variable or mount** (engine P8 D8). The sandbox owns every other environment variable and the policy → confinement mapping (§5). `AW_SINDRI_LAUNCH_SECRET` is set by `session wrap` and survives the environment scrub | Keeps the engine out of per-CLI detail |
| SB9 | **Confinement is fixed at launch and does not change for the life of the session.** Takeover and handback neither loosen nor re-tighten it, and never relaunch (§7; engine P8 D11). **A takeover stays sandboxed** (owner decision Q1, 2026-10-18): there is no "continue unconfined" option, and a human who needs something the sandbox blocks does it in a window of their own | The session is one live process tree; a bubblewrap mount namespace can't be changed after the fact, and a relaunch may end the conversation the human joined and breaks engine P8 D11 |
| SB10 | **No owner credentials in the session, with one named exception: the target's own model login, delivered by a pluggable credential source (SB18).** The environment is built from an allowlist, not scrubbed from the owner's (§3.4). What the login looks like inside is the credential source's business (SB18; the default `owner-copy`: the agent home read-only with a named write set and a per-launch copy of the login, §3.1); the inner policy denies reading the credential files (§5). GitHub access is `none` by default; the 1 h agent GitHub App token (`scripts/agent-token.sh`) is minted by the launcher outside the session and delivered through a bound directory only with `agent-app` (SB-7) | "No owner tokens"; a leak into a transcript needs the token to be reachable first |
| SB11 | **Each refusal is its own `SND-LAUNCH` code with a kind, and the engine routes on the code** (engine P8 D8). Four kinds: **no runtime** (`-014`, `-017`: the Step has no agent runtime on this box right now; the rule is SB16's N1), **launch park** (`-015`, `-016`: a new engine park kind `launch` whose Resume starts a **new run**: a new run token, launch id and worktree), **step failure** (`-018`, `-019`, `-021`) and **resource wait** (`-020`: a queue condition, not a fault). A refusal is never auto-retried within the same run, except the wait (E1-B1, E1-I1, E1-I5) | The engine's park answers are fixed by kind; `spec-conflict` parks of other kinds have no resume, so a bare park could only be cancelled |
| SB12 | **The mechanism is a port, with two non-test-only-confusable implementations.** `ISandbox` in `Aw.Sindri.Core/Sandbox/`: `Probe()`, `Plan(SandboxRequest) → Result<SandboxPlan>` (pure), `SelfCheck(SandboxPlan)`, `Wrap(SandboxPlan, argv) → argv`. Adapters: `BubblewrapSandbox` (Linux), `SeatbeltSandbox` (macOS), the product default **`UnavailableSandbox`** (always `-014`, registered until SB-5a), and two test-only types in `Aw.Sindri.Testing`: **`PassThroughSandbox`** (`Wrap` returns the argv unchanged, `SelfCheck` passes) and `RecordingSandbox`. The pass-through is registered **only** in test hosts (`WebApplicationFactory` or the dev server a test starts), never in the published product Host and never through a switch of any kind (E1-B3, R2-17) | P8 must prove a real agent launch before the real sandbox exists, without any product switch that turns confinement off. The published Host cannot load a Testing type, so P8's proof runs in process (SB16) |
| SB13 | **`.devcontainer/` is not the sandbox** (docs/devcontainer.md, readiness spec §4d). Neither is Docker: its socket is denied inside the session. Containers stay an option for the "container's tmux later" step of WF §6.5, behind the same port (§4) | Repo rule; Docker-in-the-agent is root on the host |
| SB14 | **Work that needs the box's heavy capacity goes through Sindri's blocks.** BM's `sindri_execute_tool_call` runs heavy gates, evidence and browsers host-side under the Step's policy and the heavy slots. The session's own `dotnet build` and test run still work (§3.3, §3.5) but signature-verified restores, binary fetches, whole-repo gates and the bash-test suite are expected host-side (§3.5) | One owner of the box-wide cap; keeps Docker and browser sockets out of the session |
| SB15 | **Denials are visible and tunable.** Every denial class is counted per run and recorded; the agent is told it is confined and can ask why; the owner sees what was blocked on the item and in the failure row, and gets a one-line profile edit to allow it (§3.6). A denial that can't be seen cannot be fixed, and the first runs will need fixes | The allowlists are guesses; without this loop the first runs stall opaquely (R1-05) |
| SB16 | **The engine contract, agreed with the engine lane (round 2 recheck, 2026-10-18).** (1) **N1:** availability enters only through the launch result, never through the recorded agent map. A `FailedLaunch` with `-014` or `-017` closes the agent run. A `deskCapable` Step then takes the **`runtime.unavailable` transition**: a distinct transition reason, not `step.failed(launch)`, with the desk run opening **in the same frame** (the desk twin gets its new token in that one transition). **It counts no round, bypasses the loop rules (rounds, retries, the Step's failure budget) and is never shown as a Step failure** (R3-01; until SB-5a every launch fails `-014`, so every `deskCapable` Step would otherwise log a failure first, and a `rounds: 1` recipe would park before its desk twin ran). Any other Step parks `NoRuntime` with a sandbox marker (§6 names its fields), and that park's Resume opens a new agent run that tries the launch again. The `launch` park gets its own `ParkReason` and `ParkKind` members (§6), a `FitRow` row and a `Fitting` entry `[Resume]`. The engine builds the transition in P8 U9. (2) **The suspension read:** `GET /sindri/launches/{launch}/suspended` returns `{suspended: bool}`, backed by an engine-owned Core port `IRunSuspensions.IsSuspended(LaunchId, ct)` (a fake and a contract base ship with it) that reads the ledger's park, takeover, answer and handback events joined through `OpenRun.Launch`; never a Temporal query per beat, never `views.db`. `session wrap` calls it with the launch secret; unavailable means not suspended. (3) **`-020` is a wait:** the launch activity retries with backoff (30 s) until the state timeout, writes no `launch.outcome` meanwhile, and the live-session count is atomic (a lock under `$AW_SINDRI_HOME/locks`). (4) **The run ref** is `sindri/<instance>/<run>/work`, so two instances on one item never collide. (5) **P8 U9** (engine-owned) lands the engine Core work before SB-5a: the `launch` park, the N1 rule, the limit rule, the "lost while suspended → Rerun" override, `IRunSuspensions` and its endpoint, the `-020` wait. (6) **P8's real-launch proof runs in process** (dev server, the real launcher, `PassThroughSandbox`, a stub agent on a private tmux socket); the binary proof is SB-10; there is no Host switch. (7) The engine's D10 git hardening (E1-S2) is P8 U7's, not SB-5b's. (8) A suspended run whose session is lost, and is answered, reruns; the answer's own effect (extra tokens, a grant) still applies to the new run | The engine lane read the rev 2 text against `main` and #295; these are its proposals, written in as decisions |
| SB17 | **The run directory is a launcher folder per run, and the engine's "no writable path" rule is about the repository** (was owner question Q11 of rev 2; the engine accepted it). `$AW_SINDRI_HOME/workflow/runs/<instance>/<step>/<run>/` (0700), its path on `StepContext` (SC W11), created by the launcher; engine P8 D8 reads "ReadOnly = no writable path *in the repo*; the launcher adds the run directory". The inputs are written into it by the engine or SC-K **before** `Launch`. A report's named outputs must resolve inside it (checked in `CompleteStepService`, SC-K's part). Keyed per run, so a Rerun never sees a killed run's partial outputs; D10 is unaffected (the folder is outside the worktree) | Lets a read-only scoping Step hand back files without a repo write path |
| SB18 | **The model login comes from a pluggable credential source** (owner decision Q2, 2026-10-18). Port `IModelCredentialSource` in `Aw.Sindri.Core/Sandbox/`: `Id`, `Supports(target)`, `Probe(target) → LoginStatus` (`ready`, `missing`, `expired`, with detail; cheap, no side effect), `Prepare(target, LaunchId, ct) → Result<PreparedLogin>` and `Release(handle)`. `PreparedLogin` is plain data the pure plan builder consumes: the mounts that make up the agent home, the environment names and values to add (`CLAUDE_CONFIG_DIR`, `CODEX_HOME`), the paths the inner policy must deny reading, and a cleanup handle. One adapter per source: **`OwnerCopyCredentialSource`** (`owner-copy`, today's default: the owner's agent home read-only with a named write set and a per-launch 0600 copy of the login, §3.1) and **`SeparateLoginCredentialSource`** (`separate-login`: a Steps-only login the owner makes once with `sindri sandbox login <target>`, kept in its own agent home with the hooks installed into it; the owner's `~/.claude` and `~/.codex` are not bound at all; SB-8). `FakeModelCredentialSource` and the contract base `IModelCredentialSourceContract` ship in `Aw.Sindri.Testing`. Adapters are registered explicitly in `AddSandbox()` in a registry keyed by id; an unknown id is a profile error at approval. **The source is chosen by the profile with no code change:** `sandbox.login.source` (default `owner-copy`), overridable per target (`sandbox.login.targets.<target>`) and per repo (`sandbox.repos.<repo>.login.source`), the most specific winning (repo, then target, then profile). A recipe or Step may **ask for a stricter source** and never a looser one, so "a Step only tightens" holds (`owner-copy` < `separate-login`; §9). The resolved source is in the plan, its `planHash` and the `launch.started` summary, and is never switched mid-run (SB9). When the chosen source can't deliver, the launch is `-017` with the reason and the source id; nothing falls back to another source or to the owner's login (§3.4) | The login is the one owner credential in the session, and who holds it is the decision most likely to change; a port makes the next answer an adapter and a profile line, not a redesign |
| SB19 | **Sessions you open yourself are sandboxed too, from SB-5b on, with a short list of differences** (owner decision Q6, 2026-10-18; until SB-5b they open unconfined as today, owner's final read; the full table and reasons are §3.9). Same mechanism, plan builder, credential source, network groups, canaries and codes as a Step. Different: the inner mode asks instead of refusing (a human is there); **no wall-clock limit** while attached (memory, CPU and pids still apply); a full session cap is refused at once instead of waited on; no GitHub App token; no takeover banner (there is no run to take over), the tmux status line carries the escape instead; a failure is shown to the human, not parked. Meta lane sessions are not launched through Sindri and are unchanged | The mistakes the sandbox stops (a stray recursive delete, a leaked secret, a `curl \| sh`) do not need a Step to happen; the differences follow from a person being at the keyboard |

## 2. Terms

- **Real Sindri home / session home (rev 7):** the real home is `$AW_SINDRI_HOME` as the control plane has it (the ledger, `auth/human.token`, the run areas); a launch's session home is
  `<real home>-sessions/<launch>/`, the folder the launcher makes for it (§3.1). Inside a launch, `AW_SINDRI_HOME` names the session home; "`$AW_SINDRI_HOME`" in §3.1's deny list and everywhere
  else this spec means the real home unless it says "session home"; in the text of a hook or command that runs inside a launch, `$AW_SINDRI_HOME` is the session home.
- **Confined session:** an agent CLI process tree started by `session wrap` inside the sandbox of §3, inside a cgroup
  scope, in the tmux session `snd-<run>` (LCH SL4, WF §6.5). `session wrap`, tmux, the launcher and the egress proxy run
  outside it.
- **StepPolicy:** the engine's per-launch policy (L-17; SC W9): mode, allowed tools, writable paths. SB8 fixes the two
  values the engine sends and what each means in the sandbox.
- **Sandbox plan:** the data the pure builder produces: mounts (read-only, read-write, denied), network rules,
  environment allowlist, limits, credentials. Hashed (`planHash`) and summarised on `StartedLaunch` (§8).
- **Agent home:** where a CLI keeps its login, settings and session records: `~/.claude` and `~/.claude.json` for Claude
  Code, `~/.codex` for Codex. Which one the session gets comes from its credential source (SB18); by default the owner's own.
- **Run directory:** `$AW_SINDRI_HOME/workflow/runs/<instance>/<step>/<run>/` (0700; SC W11, `AW_SINDRI_RUN_DIR`), where a Step
  leaves its output files; its path is on `StepContext`. It is a launcher-provided area, **not** a writable path of the
  `StepPolicy`: the engine's rule is about the repository (SB17).
- **Suspension:** a park or a takeover of a run (WF §5.5). The session stays alive through both (engine P8 D11).
- **Credential source:** the adapter behind `IModelCredentialSource` that supplies the target's model login to a session
  (SB18): `owner-copy` (the default) or `separate-login`. Chosen by `sandbox.login.source`.
- **Attended session:** a session the owner opens with `sindri open`, with no Step run and a person at the keyboard
  (§3.9). Sandboxed; it differs from a Step session only where a person being there changes the answer.
- **Threat model.** The sandbox stops the honest mistakes: `rm -rf` or a redirect in the wrong directory, a `git push` or
  `gh pr create` from a session that shouldn't, a read of a secret by accident and its leak into a transcript, a
  `curl | sh`, a build storm or runaway test that takes the box down, an agent editing another repo or the owner's
  dotfiles. It does **not** try to contain an agent that probes for an escape: no seccomp filter, no pid or ipc isolation,
  nothing against a kernel exploit. A review finding that needs a crafted escape is answered here.

## 3. What the sandbox does

### 3.1 Filesystem

The plan is a stack of bind mounts over the host root, bottom to top:

1. **Root, read-only.** `/` is bound read-only, with a fresh `/dev` and `/proc`, and `/tmp` replaced by a private directory.
   The agent reads the system, the toolchains and the other repos; it can't write any of them.
2. **`$HOME` replaced.** A tmpfs over `$HOME`, **sized** (`sandbox.limits.homeTmpfs`, default 256 MiB, bubblewrap `--size`,
   confirmed by the spike; if the pinned bubblewrap lacks it, the home is backed by the disk scratch instead). It counts against the
   scope's memory, so it is small on purpose. The **home allowlist** is bound back, read-only unless noted
   (`sandbox.fs.homeAllow`, detected by `sindri sandbox check` and declared per repo, §3.7):

   | Entry | Mode | Why |
   |---|---|---|
   | `.dotnet`, `.nvm`, `.cargo`, `.rustup`, `.local/bin` | ro | Toolchains (`DOTNET_ROOT`, `NVM_DIR`); `.local/bin` also holds the `judge` wrapper |
   | The toolkit checkout (where `~/.local/bin/judge` points) | ro | `judge` is `exec node <toolkit>/judge/dist/cli.js`; the checkout sits under `$HOME`, so it must be bound |
   | `.nuget/packages` | rw | A restore writes new packages; shared with the owner's builds (Q4) |
   | `.npm/_cacache` | rw | Same, for npm; shared |
   | `.local/share/NuGet`, `.dotnet` first-run files | not bound for writing | Replaced by per-launch scratch paths: `NUGET_HTTP_CACHE_PATH`, `DOTNET_CLI_HOME` and `npm_config_logs_dir` point into the scratch (§3.4), because those tools write there on the first call |
   | The agent home of the target | **read-only with a named write set** (below) | The CLI needs it |
   | `.gitconfig` | **not bound** | Replaced by the sandbox's git settings (§3.4) |

   Everything else in `$HOME`, including `.ssh`, `.aws`, `.config`, `.gnupg`, shell histories, the browsers' profiles and the
   other repos that live under `$HOME`, doesn't exist for the session. An allowlist, not a deny list, is the default for
   `$HOME`: a deny list names only what someone thought of. A tool that writes an unbound path (`~/.cache`,
   `~/.config/<tool>`) writes into the small tmpfs and loses it with the run; an overflow is `ENOSPC`, and the launch brief and `explain` name the setting to raise, `sandbox.limits.homeTmpfs`. A *read* of such a path
   fails as absent, and §3.6 matches it against the not-bound set so it shows up as a likely denial.

   **The agent home is read-only with a named write set** (R2-10, inverted from rev 2). This paragraph and the login copy
   in it belong to the `owner-copy` credential source (SB18, Q2); the `separate-login` source binds a different home (§3.4). `~/.claude` is bound
   **read-only**, and only these are re-bound read-write: `projects/<this repo's key>/` (with `memory/` read-only inside it),
   `sessions/`, `todos/`, `session-env/`, `file-history/`, `shell-snapshots/`, `history.jsonl` and caches; Codex: `~/.codex`
   read-only with `sessions/`, `log/` and caches writable (SB-S1 item 2 records the exact Codex set). So `keybindings.json`,
   `rules/`, `output-styles/` and every unlisted file stay the owner's. Other repos' `projects/<key>/` stay readable
   (read-only); the `separate-login` trigger below covers that. **The login is a per-launch copy:** the launcher copies the target's login
   file(s) and `~/.claude.json` into a per-launch directory bound inside the home, copy in and never synced back, and points
   Claude Code at it with `CLAUDE_CONFIG_DIR` where the CLI supports it (`~/.claude.json` at the root of a tmpfs `$HOME`
   cannot be bound as a directory, which is why it lives in the copy's directory; SB-S1 item 2 decides the exact mechanism).
   The CLI process reads its own copy (it needs it), and the outer layer cannot hide it from a same-user process; the **agent's tools** are denied it by the inner policy (`Read`, `Grep` and `cat`-style reads, §5), so a debugging session does not print it into a transcript. **The copy is mode 0600, removed when the run closes (`session wrap`) and by the crash sweep** (the launcher removes the copies of launches with no live scope at `serve` start and on Check again). **Caveat the spike must settle:** if a
   token refresh *rotates* the refresh token, a copy would invalidate the owner's original login; in that case the session
   uses the owner's own login file read-write and the spec says so plainly (it is the owner's login, shared, and `sindri doctor` says so; `separate-login` is the way out). **The trigger for
   `separate-login` (SB-8):** the first time `sindri sandbox denials` shows a Step reading another session's transcript, or a second person uses
   the box, whichever is first; the owner decides then and switches with one profile key (`sandbox.login.source`, SB18), with no code change.
3. **Outside `$HOME`, a short deny list** over the read-only root:

   | Denied | Why |
   |---|---|
   | `$AW_STATE_DIR` (default `~/.agentic-workflow`), the real `$AW_SINDRI_HOME` and `<real home>-sessions/` (other launches' session homes), **except** the entries below | The ledger, the profile, the human token (`auth/human.token`), every launch secret, judge and scorer stores, other runs' areas |
   | `/var/run/docker.sock`, `/run/podman`, `/run/user/<uid>` (ssh-agent, gpg-agent and dbus sockets) | The Docker socket is root on the host; the agent sockets sign as the owner |
   | `/etc/ssh` host keys, `/root`, other users' homes | Not the session's |
   | The holdout store and other runs' worktrees (LN-LCH1) | Added by the line's capability policy through the same plan |
   | `~/Library/Keychains` (macOS) | The agent App's key and the owner's logins |

   **The exceptions inside the state directories** are a closed inventory, taken from the hooks that are actually installed
   (`~/.claude/hooks` and `config/hooks`, with `lib/failopen.sh` and `lib/locks.sh`, `~/.local/bin/judge`, and
   `bin-r1b/heavy`; read again 2026-10-18). They exist so the safety hooks run in a Step as they do in a lane; a hook that
   can't run **fails open**, so a missing path would silently switch a guard off (R1-01, R2-04). *Rev 7: the rows naming `node`, `judge/dist`, `better-sqlite3` and `.nvm` describe the TS `judge`; the installed one is the C# NativeAOT `judge` under `bin/cs/<stamp>/` with `e_sqlite3` beside it, so those rows are superseded, part (b) of the canary runs that binary, and SB-1 re-reads this table against it.*:

   | Path | Mode | Used by |
   |---|---|---|
   | `~/.claude/hooks/` and `lib/` (inside the read-only agent home) | ro | every hook script, `lib/failopen.sh`, `lib/locks.sh` |
   | `~/.local/bin/judge` (a two-line wrapper), `node` (through `.nvm`), the toolkit checkout's `judge/dist` and `judge/node_modules` | ro | `done-gate`, `done-gate-annotate`, `scope-gate`, `send-gate`, `subagent-start-map`, `prompt-sort`, `judge-health`: they call `judge`, which opens `judge/decisions.sqlite` |
   | `$AW_STATE_DIR/judge/` as a directory | rw, `config.json` re-bound ro | `decisions.sqlite` (the directory is needed for its `-wal` and `-shm`), `sessions/`, `outbox/`, `teammate-names.jsonl` (`done-gate`, `external-write-guard`, `turn-origin`, `send-gate`, `outbox-flush`, `record-teammate-name`, `subagent-start-map`). Wider than the three subpaths a file bind could give; accepted (§11) |
   | `$AW_STATE_DIR/logs/` | **a per-launch directory** bound at that path, rw | `lib/failopen.sh` appends `failopen.log`. The launcher reads the per-launch log into the run's denial summary, so a hook that fails open is seen, and the owner's own log is untouched |
   | `$AW_STATE_DIR/probe/` and `digests/` | **per-launch directories** bound at those paths, rw | `probe-log` and `context-guard` write them; a Step cannot read other sessions' prompts or excerpts (R2-13) |
   | `$AW_STATE_DIR/context-guard/` | ro, `state/` rw | `context-guard` reads its config and writes per-session state |
   | `$AW_STATE_DIR/locks/` | rw | `heavy`'s `mkdir` mutexes (`heavy-job.lock*`, `heavy-session/`). A finer bind is impossible for `mkdir` mutexes; accepted with the mitigations of §3.3 rule 6 |
   | `$AW_STATE_DIR/heavy-slots-cap`, `heavy-meta-reserve`, `bin-r1b/` | ro | `heavy` |
   | `$AW_STATE_DIR/bin/aw-meta` and `$AW_STATE_DIR/meta/` | **not bound** (absent) | `meta-outbox` and `meta-stop-gate` exit at once when `aw-meta` is not executable, and `meta-lane-gate` passes without lane variables (LCH §10.2), so the meta hooks do nothing in a Step, by design; the canary proves it |
   | `$AW_SINDRI_HOME/launches/` (the real folder, at its real path) | rw | The session home's `launches` link resolves here (below). `session-id-report` writes `launches/<id>.session-id`, the Step policy's turn-ended hook touches `launches/<id>.turn-ended`, and `session wrap` reads `launches/<id>.capture.json` (outside the sandbox). The idle rule needs the turn-ended marker to reach the folder the control plane watches, so rev 6's thrown-away per-launch directory would have switched idle detection off. The GitHub token directory (SB-7) is **not** under this folder any more: it is the session home's `gh-token/` (below) |
   | `$AW_SINDRI_HOME/approved/` (the real path) | ro | The session home's `approved` link resolves here: the CLI finds its repo in the approved profile snapshot. It should hold no token or secret (a forge `credential` is a pointer, `env:NAME` or `command:<argv>`; a literal secret written into an argv would be readable, which the profile's approval does not check today) |
   | The session home `<real home>-sessions/<launch>/` (the same path) | **ro**, with `gh-config/` rw | The launcher makes it before the sandbox (0700) and removes it at run close and in the crash sweep. It holds the Step's policy file, the MCP config and the `control.json` below. `session wrap` runs outside the sandbox and reads and deletes the launch secret file before it starts the agent, so the file needs no bind and the agent's environment loses `AW_SINDRI_LAUNCH_SECRET_FILE` (only `AW_SINDRI_LAUNCH_SECRET` is on the §3.4 allowlist). The delete is skipped if `AW_SINDRI_LAUNCH_SECRET` was inherited into wrap's environment; a test `Agent_env_has_no_LAUNCH_SECRET_FILE` and a canary check that the file is gone after wrap starts guard it. The agent can read its own launch secret from its environment; it cannot rewrite its own policy |
   | `<session home>/control.json` | ro, **a file the launcher writes** | A copy of the endpoint record with the real `Url`, pid and epoch (SB-S1 item 7; the inside forwarder listens on the same port number as Sindri's real one, §3.2). The real file is not bound: the state directory is absent, and a single-file bind would pin an old inode after `serve` restarts (R1-14). The launcher writes it before the inside init; the CLI's liveness probe (`GET /sindri/ping`, pid and epoch must equal the record) then passes through the forwarder. **It follows a `serve` restart:** `session wrap` (outside the sandbox, rw on the dir) rewrites it by rename when the epoch changes (case `Control_json_follows_serve_restart`), so a running Step does not see `not the recorded control plane`. Outside a sandbox (macOS before Seatbelt, a Step run without one) the session home's `control.json` is a link to the real file instead |
   | The net directory (§3.2), the token directory (SB-7, the session home's `gh-token/`, ro) and the plan summary | ro (net: sockets) | §3.2, §3.4 |
   | The run directory | rw | The Step's outputs |
   | `$AW_STATE_DIR/bin/cs/<stamp>/` (the published C# `sindri` and `judge`; `~/.local/bin/sindri` execs into `bin/cs/current`, a sibling link that is absent in the sandbox, so the plan builder resolves `current` to the stamp at launch and binds the wrapper to that path; golden case `Wrapper_resolves_current_to_stamp`) and `DOTNET_ROOT` | ro | The wrappers below resolve here, and the state directory is otherwise absent, so without this bind `sindri status` fails at exec. The inventory above was read against the TS `judge` and `sindri`; SB-1 re-reads it against the C# binaries (rev 7) |
   | `~/.local/bin/sindri` | ro | `sindri-nudge`, `workflow complete`, `sandbox explain`; the discovery file it reads is the session home's `control.json` above (SB-S1 item 7) |

   **The launch's session home (rev 7; #1099, built on `bugfix/gh-1099-a1`).** The launcher gives each launch a folder of its own, `<real home>-sessions/<launch>/` (0700, beside the
   real home and not inside it), and sets `AW_SINDRI_HOME` and `AW_SINDRI_LAUNCH_SECRET_FILE` to paths in it, so no variable and no argv entry names a path under the real home, where
   `auth/human.token` is. It holds three links (`control.json`, `launches/` and `approved/`, each to the real one), the launch's one-read secret file, and the Step's policy file, MCP
   config empty `gh-config/` folder (the GitHub CLI's `GH_CONFIG_DIR`) and, for `agent-app`, the `gh-token/` folder; the whole home, those two included, is removed at run close and in the crash sweep. The CLI, the turn-ended hook and `session wrap` run unchanged against it. **Under the sandbox** it is bound as the rows above say: the real
   `launches/` and `approved/` at their real paths, so the links resolve (the plan builder emits the binds in depth order, with the links absolute; the plan golden has a case
   `Session_home_links_resolve_in_plan`), and a launcher-written `control.json` in place of the link. The `net-forward` canary (`-016`) gains the end-to-end check: SB-4a asserts that a request through the forwarder reaches a stub `/sindri/ping` with the record's pid and epoch; **SB-5a**, where the launcher exists, runs `sindri status` inside the sandbox through the launcher-written file and must answer. The binds are the plan's (SB-1: the plan emits them, shallow to deep, after a tmpfs mask of the real home and of `<real home>-sessions/` so the mountpoints exist even for a custom home outside `$HOME`; golden cases `Session_home_links_resolve_in_plan` and one for a custom home); the launcher (SB-5a) makes the session home and writes the file; SB-4a owns the listener side only. Honest about what hides the token: the real home's **path is not secret** (`readlink` on a link, or
   `/proc/self/mountinfo`, names it); what protects `auth/human.token` is that no mount of it exists in the session.

   **What blocks `human.token`, and when, said plainly (#1099).** Today nothing blocks the file for a launched agent: the session home only hides the path, and the path is findable (the default home, or `readlink`). It is blocked per launch type once the sandbox is in force: Steps at SB-5a, `sindri open` at SB-5b, macOS at SB-9; SB-2 alone only builds the `bwrap` argv. Inside the sandbox the file does not exist: the state directory and the real home are absent and the plan binds only the
   entries above. The `fs-deny` canary (SB-2) asserts `[ ! -e <real home>/auth/human.token ]` inside (on Seatbelt, where a denied file still exists, "stat and read both fail") and that `ls <real home>` shows only `launches` and `approved`, so this is a tested fact once it runs, not a plan-derived claim. **Still open:**
   (1) `/proc/<pid>/root` and `/proc/<pid>/environ` of a process outside the sandbox, which the "what it can't stop" paragraph below accepts because `/proc` is shared; **Assumed open until the probe says otherwise:** for a same-uid target the kernel's ptrace check passes when the target is dumpable, and the plan shares the pid namespace. so SB-2 adds the probe (`cat /proc/<serve pid>/root/...` from inside). If the probe reads, SB-2's done-when fails and SB-5a does not start until a pid namespace with a fresh `/proc` is in the plan (which reopens SB6 and the heavy wrapper's ancestor walk); if it is denied, the spec is amended to say so;
   (2) every session that runs outside the sandbox: attended `sindri open` sessions until SB-5b (and on macOS, where no sandbox exists yet, until SB-9, or refused: §12), a session someone starts by hand in tmux (it has neither the session home nor the sandbox, and its
   only control is #1098's rule); before SB-5a no Step runs at all, since the product refuses with `-014` (SB16);
   (3) the same OS user: a process the sandbox did not start can read the file. #1098's server rule (a call carrying a launch is that launch's session, never the human) is the other layer;
   with a sandbox in force the API call from inside is still allowed, and #1098 is what makes it non-human. The two do not replace each other. Owner-accepted, #1113 answer 2: there is no interim control before this.

   No ledger, token or secret file is ever among them. Two hooks degrade by design: `prism-context` calls `localhost` (no
   network: it prints its own warning, it guards nothing) and `rtk-rewrite` uses `rtk` only when it is on `PATH`. The **hook
   canary** (canary `hooks-run`, §6) has three parts, because plain-bash hooks proving themselves says nothing about the
   `judge`-backed ones: (a) `block-destructive.sh` with an `rm -rf /` payload and `detect-secrets.sh` with a planted key must
   both deny; (b) a `judge` round trip, `judge brief get task:selfcheck`, must exit 1 with its "no brief" answer (not a crash),
   which proves `node`, `judge/dist`, `better-sqlite3` and the database, plus a write and removal of a probe file in
   `judge/sessions/`; (c) `aw_failopen` writes a line that shows up in the per-launch `logs/failopen.log`, and `meta-outbox.sh`
   with a sample payload exits 0 without running `aw-meta`. Any part failing fails the launch (`-016`). `sindri doctor` repeats
   it and says what it exercised, not a hook count. The test `Hook_inventory_matches_config_hooks` greps the hook scripts for
   `AW_STATE_DIR` paths so this table cannot drift.
   it fails the launch (`-016`) if any hook can't run, and `sindri doctor` repeats it.
4. **Writable.**
   - The **worktree** (`<repo>/.claude/worktrees/sindri-<instance>-<run>`, the cwd; LN-WF2's phase worktree when the line
     owns one) with `WriteScope`; read-only otherwise (engine P8 D8).
   - The **run ref directory** (SB5), same condition.
   - The **run directory**, always (SB17).
   - A **private scratch** directory, `$run-area/tmp`, mounted as `/tmp` and `$TMPDIR` and holding the per-launch cache
     paths; removed when the run closes. Not a RAM tmpfs: it would count against the cgroup and fill silently.
   - The tool caches and hook spool directories above.
5. **Not unshared:** user, pid and ipc namespaces (SB6; the user namespace is bubblewrap's own). The session runs as the
   owner, so files it creates in the worktree are the owner's, and the engine reads the worktree's `HEAD` and
   `git status` from outside after the session ends with no permission change (engine P8 D10). The sandbox never rewrites
   ownership, overlays or copies the worktree; the end of a session removes only the scratch. The engine's own git calls on
   the worktree should run with `-c core.hooksPath=/dev/null -c core.fsmonitor= -c safe.directory=<worktree>` and check that
   the worktree's `.git` file still points at `<repo>/.git/worktrees/<name>`, since the `.git` file and
   `worktrees/<name>/config.worktree` are writable to the session (E1-S2).

**What it can't stop, said plainly:** reading a repo's `.env` outside `$HOME` (the root is readable; repos under `$HOME` are not bound); `pkill` (SB6); a write through
a tool cache that poisons a later build; filling the disk (a growth check on the worktree and scratch runs on the
watchdog's tick and records `sandbox.limit-hit {kind: disk}` as a warning, it does not kill); and, because `/proc` is
shared, a same-user process can read `/proc/<pid>/environ` or walk `/proc/<pid>/root` around the denials (assumed open until SB-2's probe says otherwise; see the session-home paragraph above). The state
directory is hidden from honest access only, not from someone looking for a way around (R1-22). `sandbox.fs.denyExtra` takes
more paths. Tidying `locks/` or `judge/` (writable for the hooks and `heavy`) can also damage the owner's box-wide mutex and judge records; the launch brief says not to, and `heavy` prints the holder.

### 3.2 Network

On Linux the session runs in its own **network namespace** with only a loopback interface (verified on this box: `lo` is
up with `--unshare-net`). Two forwarders connect it to two services, **end to end**:

| | Outside (Sindri host service, per launch) | The path | Inside (started before the agent, in the sandbox) |
|---|---|---|---|
| Sindri | A listener on a unix socket `<net>/sindri.sock`, where `<net>` is a short per-launch directory `$XDG_RUNTIME_DIR/sindri/<8 hex>/` (fallback `/tmp/sindri-<uid>/<8 hex>/`, 0700) so the path stays far under the 104-byte macOS and 108-byte Linux limit (R2-20), that relays to `127.0.0.1:<sindri port>` | The net directory is bound into the sandbox at `/run/sindri/net` (the exception row of §3.1) | `socat TCP-LISTEN:<sindri port>,bind=127.0.0.1,fork UNIX-CONNECT:/run/sindri/net/sindri.sock` on the **same port number** the per-launch MCP config and `AW_SINDRI_*` already name |
| Egress | A listener on `<net>/proxy.sock` running the CONNECT proxy | same directory | `socat TCP-LISTEN:<proxy port>,bind=127.0.0.1,fork UNIX-CONNECT:/run/sindri/net/proxy.sock` |

The outside half is an `EgressService` inside the Sindri host (not in `session wrap`), so the launcher can check the whole
path before starting the agent; the inside half is a small init shell in the sandbox that starts both `socat`s and then
`exec`s the agent. Both die with the run. **Start order:** outside listeners, then the inside init, then the self-check
(which exercises both forwarders from inside the namespace), then the agent. The environment sets `HTTPS_PROXY`,
`HTTP_PROXY`, `ALL_PROXY` (and lower-case) to the proxy port, and **`NO_PROXY=127.0.0.1,localhost,::1`** so the MCP and
API calls to the Sindri port go straight to the forwarder, not through the proxy, which would refuse them. A
`socat` that is missing is `-014` (`no-forwarder`); a forwarder that fails to start or doesn't carry a call is `-016`
(canary `net-forward`). SB-S1 may replace `socat` with a small helper; the code stays.

The proxy does no TLS interception: it checks the requested host name and port, then relays bytes; it resolves names itself
(the session has no DNS) and refuses a target that resolves to loopback, link-local or a private address unless a group lists
that address. It dials the address it validated, and caps each run's concurrent connections, bytes and duration (it runs inside the Sindri host, outside the cgroup, so an install retry loop must not starve the control plane). Every request's verdict is counted per host in a per-run `egress.log` (host, port, verdict, count; no URLs) in
the run area, and §3.6 shows it. The allowlist is a set of named **host groups** in the approved profile
(`sandbox.network.groups`) and per repo (§3.7):

| Group | Hosts | Given to |
|---|---|---|
| `model` | `api.anthropic.com` for `claude-code`; `api.openai.com`, `chatgpt.com` for `codex`; an entry names its own | every Step |
| `packages` | `api.nuget.org`, `*.nuget.org`, `registry.npmjs.org` | `WriteScope` Steps (a restore on a changed lock file) |
| `signing` | the certificate-revocation and timestamp hosts the signed restore contacts; **filled by SB-S1 item 9**, which runs `scripts/dotnet-verify-packages.sh` in the sandbox | none by default (§3.5: the verify step is expected host-side) |
| `releases` | the hosts named in `binaries.lock` | none by default (`binaries-lock.sh fetch` is host-side) |
| `github` | `github.com`, `api.github.com` | only with `credentials.github: agent-app` (SB-7) |

Telemetry and update checks are turned off in the inner policy (`DISABLE_TELEMETRY`,
`CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC`, Codex's equivalents). A tool that honours only a config file for its proxy has
no network, which is the intended failure, and §3.6 makes it visible. In **learn mode** (Q13) the proxy allows every host
and records it; the filesystem and credentials stay confined.

On macOS there is no network namespace: Seatbelt denies all network and allows `network-outbound` only to
`localhost:<proxy port>` and `localhost:<sindri port>`; the same proxy and `egress.log` apply (SB-9).

### 3.3 Process limits, and how they compose with the heavy-job wrapper

`session wrap` starts the agent as `systemd-run --user --scope -p … --slice sindri-steps.slice -- bwrap … -- <agent argv>`; the
tty is the tmux pane's, so attach works as before (WF §6.5). Limits, defaults changeable in the profile (`sandbox.limits.*`,
Q5):

| Limit | Mechanism | Default | When hit |
|---|---|---|---|
| Memory (per session) | `MemoryHigh` (80% of max), `MemoryMax`, `MemorySwapMax=0`, `OOMPolicy=kill` | `auto`: 20% of RAM, at most 12 GiB | The scope is killed; `session.ended reason: limit kind: memory` |
| Memory (all Step sessions) | the parent `sindri-steps.slice` with its own `MemoryMax` | 60% of RAM | The kernel bounds the sum, so the arithmetic can't be wrong; a kill inside it ends one session as above |
| Concurrent sessions | the launcher counts live scopes in the slice **under a lock** (count and start are one atomic step; an `flock` on a file under `<real home>/locks`, which the kernel releases when the holder dies; the count is read from the live scopes, never stored, so a crashed launcher leaves nothing counted and no stale lock to clear) | `sandbox.limits.sessions` = 3 | The launch **waits** (SB16; an attended open is refused at once instead, §3.9): the launch activity retries with backoff (30 s) until the Step's state timeout; no `launch.outcome` meanwhile; the run reads "Waiting for a free session slot"; `-020` names the wait |
| CPU | `CPUWeight=50` and `CPUQuota` = (cores × 50)% (systemd's quota is per CPU: 8 cores = 400%) | half of all cores | Throttled only; never a kill |
| Processes | `TasksMax` | 2048 | New forks fail with `EAGAIN`; recorded once as `sandbox.limit-hit {kind: pids}`; no kill |
| Wall time | the `session wrap` watchdog is the **one clock** (SIGTERM, 10 s, then `systemctl --user kill --signal=SIGKILL --kill-whom=all`); the scope's `RuntimeMaxSec` is only a loose ceiling (Step timeout + `wallGrace` + `parkMax`) for when wrap or tmux died | the Step's `timeout` plus 10 minutes; 4 h when it has none; at most `workflow.steps.longMax` plus 10 minutes (LN-WF7) | `session.ended reason: limit kind: wall`. **The clock pauses while the run is suspended** (SB7, SB16). An attended session has no wall limit (§3.9) |
| Disk | a growth check on worktree and scratch at the watchdog tick | warn at 10 GiB | `sandbox.limit-hit {kind: disk}`; no kill |

**The heavy-job wrapper keeps box-wide concurrency; the cgroup only caps one session's size.** Rules:

1. A session is not a heavy job: it mostly waits on a model and takes no slot.
2. A command the agent runs through `heavy` (`dotnet build`, tests) takes slots as today: the locks are on
   `$AW_STATE_DIR/locks/` (rw), the cap files are read (ro), and the wrapper finds the session by walking to the `claude`
   ancestor (SB6). Its children are in the scope, so they count against the memory ceiling. **A raw `dotnet test` or
   `npm test` would bypass `heavy`, so the sandbox's shim directory (first on `PATH`) holds `dotnet` and `npm` shims.** Each
   shim has its own table of subcommands and calls `exec heavy --expensive /abs/real-tool "$@"` or `--light` **explicitly**:
   `heavy` classifies by the literal command text, so a bare absolute path would count as light and a bare `npm` would loop
   back into the shim. Expensive are the ones `heavy` lists itself; pure queries (`--version`, `--info`) skip `heavy` so they never queue behind a
   build. There are three shims, `dotnet`, `npm` and **`npx`** (heavy counts `npx vitest` and `npx tsc` as expensive, so a
   bare `npx` would bypass the cap; R3-08). The table is a **golden** (`tests/…/Golden/Sandbox/shims.json`) checked by
   `Shim_table_matches_heavy_cases` against the case list in `bin-r1b/heavy`, so drift is caught:

   | Shim | Subcommand | Route |
   |---|---|---|
   | `dotnet` | `build`, `publish`, `format`, `pack`, `restore`, `run`; `test` without `--no-build` | `heavy --expensive /abs/dotnet …` |
   | `dotnet` | `test --no-build`, `new`, `sln` and every other subcommand | `heavy --light` |
   | `dotnet` | `--version`, `--info`, `--list-sdks`, `--list-runtimes` | direct, no `heavy` |
   | `npm` | `ci`, `install`, `test` | `heavy --expensive /abs/npm …` |
   | `npm` | `run`, `ls`, `view` and every other subcommand | `heavy --light` |
   | `npm` | `--version` | direct |
   | `npx` | `vitest`, `tsc` (and any command `heavy` lists as expensive) | `heavy --expensive /abs/npx …` |
   | `npx` | every other command | `heavy --light` |

   The shims are documented in SB-0 and printed by `sandbox show`; SB-S1 item 8 confirms the table against the real wrapper.
3. A Step that already holds a heavy slot (LN-WF3's `waitFor: { resource: heavy }`) gets `AW_SANDBOX_SLOT_HELD=1`, read by the
   shims only: they then exec the real tool directly. **`AW_HEAVY_LOCK_HELD` is never in the session's environment**, since it
   would make the agent's own `heavy` calls skip the box-wide cap. A Step that holds no slot gets nothing.
4. `heavy` sets its resource environment only after its held-slot exit, so the sandbox sets it always:
   `DOTNET_PROCESSOR_COUNT` (from `AW_HEAVY_CPUS`, default 4), `MSBUILDDISABLENODEREUSE=1` and
   `DOTNET_CLI_USE_MSBUILD_SERVER=0`. Otherwise a held-slot build would run at full core count with resident MSBuild nodes.
5. The doctor line prints `slots × ceiling` against RAM and warns over 100%; the ceiling is not a reservation, and the
   wrapper's memory guard still gates the third slot.
6. A SIGKILLed scope skips the wrapper's `trap`. The session-end sweep removes `locks/heavy-session/<pid>.lock` of the dead
   scope's ancestor, **after SB-3b reads what the wrapper's own stale check already tolerates** (it tests `kill -0` on the
   holder) and keeps only what the wrapper doesn't clear; SB-3b has the test. A Codex Step has no `claude` ancestor, so the
   wrapper falls back to the parent shell and per-session serialisation is lost for it: accepted, listed in §11.
7. `locks/` is writable because the mutexes are `mkdir`s, which invites a careless "clear the stale lock". The launch brief
   says never to touch `locks/`; `heavy` prints the holder while it waits, **with a "do not delete this" line** (a one-line
   change to the wrapper's waiting message, in SB-3b); and the **inner policy denies `rm` and `mv` of `locks/` and `judge/`**
   (§5; R3-09). The inner policy is advice, not the boundary, so the writability itself is accepted in §11.

### 3.4 Credentials and environment

**Environment from an allowlist**, one list in code with a reason per name, tested against a known-good set taken from
CLAUDE.md's own commands. `session wrap` builds it: `PATH` (the toolchain dirs behind the shim dir), `HOME`, `LANG`, `TERM`,
`TMPDIR`, `USER`, `SHELL`, `DOTNET_ROOT`, `DOTNET_CLI_HOME` (scratch), `DOTNET_SKIP_FIRST_TIME_EXPERIENCE`,
`DOTNET_CLI_TELEMETRY_OPTOUT`, `DOTNET_NOLOGO`, `NUGET_PACKAGES`, `NUGET_HTTP_CACHE_PATH` (scratch), `NVM_DIR`,
`npm_config_cache`, `npm_config_logs_dir` (scratch), `npm_config_ignore_scripts=true`, `NODE_OPTIONS` only if the repo
sets it, `GIT_CONFIG_COUNT`/`GIT_CONFIG_KEY_<n>`/`GIT_CONFIG_VALUE_<n>` and `GIT_CONFIG_GLOBAL` (an empty file; the sandbox's git settings, §3.4), `CLAUDE_CONFIG_DIR` (when the CLI supports it), `DOTNET_PROCESSOR_COUNT`, `MSBUILDDISABLENODEREUSE`, `DOTNET_CLI_USE_MSBUILD_SERVER`, the proxy variables and `NO_PROXY`, `AW_SANDBOX=1`,
`AW_SANDBOX_PLAN` (the value-free summary, §3.6), `AW_SINDRI_INSTANCE`, `AW_SINDRI_RUN_DIR`, `AW_SINDRI_WORKTREE`,
`GH_CONFIG_DIR` (the session home's empty `gh-config/`, so `gh` finds no login), `AW_SINDRI_BRANCH` (the run ref), `AW_SINDRI_CHILD`, `AW_SINDRI_ITEM`, `AW_SINDRI_LAUNCH`, `AW_SINDRI_HOME` (rev 7: always the launch's session home, §3.1, never the real home), `AW_SANDBOX_SLOT_HELD` under rule 3 of §3.3 (never `AW_HEAVY_LOCK_HELD`), and `AW_SINDRI_LAUNCH_SECRET` (engine P8 D8). A test fails on
any name matching `*_TOKEN`, `*_KEY`, `*_SECRET`, `*PASSWORD*`, `AWS_*`, `GH_*`, `GITHUB_*` or `AGENT_APP_*` unless it
is on the list with a reason (the launch secret, the target's model credential). LN-LCH1's scrub is subsumed.

**The sandbox's git settings** (R1-15, R2-11) are delivered as command-line-scope config: `GIT_CONFIG_COUNT`,
`GIT_CONFIG_KEY_<n>` and `GIT_CONFIG_VALUE_<n>` in the environment, which beat the repo's own `.git/config` (a per-launch
global file would lose to a local `commit.gpgsign`, `core.hooksPath` or credential helper, and the repo's `core.hooksPath`
would replace the refusing `pre-push`). The values: a fixed identity (`user.name`, `user.email` of the Step),
`commit.gpgsign=false`, `tag.gpgsign=false`, an empty `credential.helper`, `safe.directory=<worktree>`, `gc.auto=0`,
`gc.pruneExpire=never`, and a `core.hooksPath` to a per-launch directory holding a refusing `pre-push` and links to the repo's
own hooks (so `.git/hooks`, shared with the owner's checkout, stays untouched). `GIT_CONFIG_GLOBAL` points to an empty file,
so the owner's `.gitconfig` is absent. The owner's identity, signing key and credential helpers never reach the session, and
a signed-commit setup can't break an unattended commit. The `git-ref` canary runs against the real worktree and checks the
effective values (§6).

**Model login.** The one owner credential in the session is the target's own: Claude Code's login file or the macOS
keychain item it reads, Codex's `auth.json`, or `ANTHROPIC_API_KEY`/`OPENAI_API_KEY` in API-key mode (the model
credential's name is on the allowlist with that reason). The CLI cannot run without it. **Where it comes from is the
Step's credential source** (SB18, owner decision Q2, 2026-10-18), chosen by `sandbox.login.source` and nothing in code:

| | `owner-copy` (default, built in SB-2) | `separate-login` (an adapter, SB-8) |
|---|---|---|
| Whose login | The owner's own | A login only Steps use, made once with `sindri sandbox login <target>` |
| What it binds | The owner's agent home read-only with the named write set, plus a per-launch 0600 copy of the login (copy in, never synced back; removed at run close and by the crash sweep) and `CLAUDE_CONFIG_DIR`/`CODEX_HOME` pointing at it (§3.1). The owner's settings, hooks and skills apply, read-only | Its own agent home under `$AW_SINDRI_HOME/agent-homes/<target>/`, with the hooks installed into it, bound as the session's agent home; the login is used in place (it is Steps-only, so a refresh may rewrite it). **Nothing of the owner's `~/.claude` or `~/.codex` is bound**: their history and settings stay out |
| Inner policy denies reading | The login copy | The login file in that home |
| Doctor shows | `ok  login: Claude Code signed in for Steps (source: owner-copy)` | `ok  login: Claude Code signed in for Steps (source: separate-login)` |
| Self-check | the `login` canary (§6): the copy is readable at its planned path with mode 0600, the owner's original is not writable, the inner policy denies reading the copy | the `login` canary: the login is readable at its planned path and the owner's whole agent home is absent |
| Unavailable when | no login in the owner's home, or it expired: `-017` `no-login`/`expired`, source `owner-copy` | no separate login made yet, or it expired: `-017` `no-login`/`expired`, source `separate-login` |
| Known limit | A session can read other repos' transcripts in the owner's `projects/` (read-only); if a refresh *rotates* the refresh token, the spike's fallback is the owner's login file read-write and doctor says so (§3.1) | The second sign-in; hooks must be installed into that home (SB-8's installer option); readers and the collector follow the directory |

**Choosing and overriding.** `sandbox.login.source` is the profile default; `sandbox.login.targets.<target>` overrides it
for one agent and `sandbox.repos.<repo>.login.source` for one repo, the most specific winning. A recipe or Step may
**ask for a stricter source** than the profile gives (it names one in its policy: `separate-login` over `owner-copy`),
never a looser one; moving to a stricter source needs no profile re-approval, a looser one does (§9). The same
resolution runs at every launch and the result is in the plan, so a replayed or resumed session keeps the source it
started with. **Unavailable is a refusal, never a fallback:** `-017` with the reason and the source id (`no-login`,
`expired`, `source-unsupported` for a target the source has no adapter for), shown in the card's words for that source
(§8); the launch does not try another source or the owner's own login. **Adding a source** (SB-8 is the first such case)
is one new adapter that passes `IModelCredentialSourceContract`, one registration in `AddSandbox()`, one set of `-017`
copy goldens and one id in the profile schema; the plan builder, the canaries and the profile code do not change. The
login is checked at launch only; an expiry mid-run shows as a model error in the session and is accepted (§11).

**GitHub.** `credentials.github: none` is the default and the only value built before SB-7. A Step that needs a push or PR
emits an effect (WF §5.7, P6) that Sindri sends host-side, matching LN-WF6's `build` capability. With `agent-app`
(SB-7):

1. The launcher, as the owner outside the session, runs `scripts/agent-token.sh --repo <name>` ("owner/session launcher
   only", spec §4d) and writes the token to `<session home>/gh-token/token` (0600) inside a **directory** bound read-only at a fixed
   path (`session wrap`, outside the sandbox, writes it; the agent only reads; rev 7 moved it out of `launches/`); the shims read `<dir>/token`. A single-file bind would keep the old inode after an atomic rename (R1-14).
2. Two shims first on `PATH` (`gh` and a git credential helper) read that file at each call. `session wrap` mints a fresh
   token every 45 minutes and renames it inside the directory.
3. The `github` host group opens in the proxy; the pre-push refuser is replaced by one that allows only the run's branch.
4. A mint failure (key missing, no network, App uninstalled) is `-018`, a step failure.

The App's private key never enters the session; it lives in the keychain or `AGENT_APP_KEY_FILE`, outside the home
allowlist.

### 3.5 What works inside, and what to use instead

The commands in CLAUDE.md, against the mount stack. Rows marked "probe" are SB-S1 item 9 and must be confirmed there before
SB-2 is merged, from a **clean worktree** (a fresh worktree has no `node_modules`, here or in `scripts/`); the rest follow from the plan.

| Command | In a Step | If it fails or is refused |
|---|---|---|
| `git status`, `diff`, `log`, `show`, `add`, `commit` in the worktree | Works. Identity from the sandbox's git settings, unsigned | |
| `git checkout -b <anything>`, `git tag`, `git stash`, `git update-ref` on another ref | Fails (`cannot lock ref`): only `refs/heads/sindri/<instance>/<run>/` is writable. The agent is told its branch (`AW_SINDRI_BRANCH`) | Commit on the run branch; a sub-branch `sindri/<instance>/<run>/try-2` can be created |
| `git branch -D`, `git branch -m`, any ref **delete or rename**, even under the run's own directory | Fails: a delete or rename takes `packed-refs.lock`, which sits in the read-only git root (verified in round 2) | Leave the branch; Sindri removes the run's refs from outside |
| `git fetch`, `git pull`, `git push`, `git gc`, `git worktree add`, `git config` (repo) | Fails: no network to the remote, `FETCH_HEAD`, `packed-refs`, `config` read-only | Sindri sends the push and PR as effects; ask the block for a fetch |
| `dotnet build -warnaserror`, `dotnet test` (unit lane) | Works (probe): `~/.nuget/packages` rw, http-cache and CLI home in scratch, through the `dotnet` shim and `heavy` | A restore needing a new package needs the `packages` group (`WriteScope` Steps have it) |
| `dotnet test` of an integration project that starts Temporal | Fails: it reads `$AW_STATE_DIR/cache/bin/`, which is denied | Run the integration lane host-side as the gate |
| `dotnet format --verify-no-changes` | Works (probe) | |
| `scripts/dotnet-verify-packages.sh` | Works (probe, §15 item 9): locked restore and `dotnet nuget verify --all` pass with only the `packages` group; the `signing` group is empty | |
| `scripts/binaries-lock.sh fetch` | Fails: release hosts aren't in a group (not probed) | Run host-side (SB14); the `releases` group exists if the owner wants it in |
| `npm ci`, `npm test` in a TS package | `npm ci` works (probe, §15 item 9): 119 packages in `sindri/` with `npm_config_ignore_scripts=true`, needing `registry.npmjs.org` (the `packages` group); `npm test` passes except `sindri/tests/real.test.ts` (root-ownership check, see below). A native-build package that ships no usable binary would still need the SB-11 prepare | The SB-11 prepare still saves a network fetch per run; run the root-ownership test host-side |
| `bash scripts/ci/bash-tests.sh`, `bash scripts/tests/*.test.sh` | Mostly works; tests that start tmux use a private socket; those that mint tokens, run Docker or need `$HOME` state fail | Run the whole suite host-side as the gate; a Step runs the one test it touches |
| `node scripts/check-deps.mjs`, `scripts/sync-rules.sh --check` | Work (read-only repo scripts, local imports only) | |
| `node scripts/check-launcher.mjs`, `check-fleet.mjs` | Fail until the prepare has run `npm ci` in `scripts/` (they need Ajv from `scripts/node_modules`) | Same prepare (SB-11) |
| `scripts/sindri-prove.sh`, anything starting Sindri | Fails: no state dir | Not a Step's job |
| `docker`, `playwright` browsers, `serena` | Fail: no socket, no display | Ask a block (SB14) |
| `sindri workflow complete`, `sindri sandbox explain`, MCP `sindri-step` | Works through the Sindri forwarder (`explain` reads only the plan file) | |
| Hooks (`block-destructive`, `detect-secrets`, `done-gate`, …) | Run, from the read-only agent-home settings, with the paths of §3.1; the meta hooks do nothing by design | The hook canary (§6) proves it at every launch |

### 3.6 Seeing and fixing denials

The agent can't see why something is missing (a denied path doesn't exist; a denied host times out). The loop (SB15):

- **The agent knows.** Every sandboxed session has `AW_SANDBOX=1` and `AW_SANDBOX_PLAN`, a value-free summary file; the
  Step's launch brief (the seed's fixed template, LCH §3) gains one paragraph: "You are in a sandbox. A missing path may be
  denied, not absent. Run `sindri sandbox explain <path|host|command>` before working around it. Never touch `locks/` or
  `judge/`." `explain` is a pure lookup in the plan file: it opens **only** `AW_SANDBOX_PLAN`, never the state directory, the
  ledger or telemetry (a test runs it with the state directory denied), offline, and answers "denied by rule X, because Y; the
  owner can allow it by Z".
- **Every denial is counted** in a per-run counter file (`denials.json` in the run area), which the API and the app read at
  render, so a count is always the true count; the ledger events (§8) are only a cadence for history. Network: per host in
  `egress.log`. Inner policy: the CLI's own refusals (read from the session's transcript tail, best effort; the supported
  transcript formats per CLI are an SB-S1 item). Filesystem: bubblewrap reports nothing, so a failed tool call in the
  transcript tail whose path is on the deny list **or under `$HOME` and not bound** (`~/.cache`, `~/.config/<tool>`, a
  toolchain directory: the commonest first-run miss) is recorded as a *likely* denial of `kind: path`. The hooks' own
  fail-opens come from the per-launch `failopen.log`. At most 200 distinct targets are counted per run; the rest are one
  overflow count.
- **The owner sees them.** `sindri sandbox denials [--run <r>] [--since <t>] [--json]` lists them grouped by target and rule.
  The item sheet and the Step's failure or park row show "Blocked: github.com × 3 (the `github` group is off for this Step)";
  a Step that parks or fails with denials on record cites the top ones in that row. **A repeated denial does not raise Needs
  you by itself** (default, R2-09): the Step is still working around it, and a Needs-you row for every honest probe would
  drown the real ones; it is reconsidered from the first runs' data. Each line carries its fix **by kind** (R3-07): *network* gets "allow `<host>` for this repo" (below); *path* gets "allow
  `<path>` for this repo" only when the path is a not-bound home entry, and a path on the permanent deny list (the state
  directory, the Docker socket, keys) gets its reason and **no button**; *policy* (a tool the inner policy refused, such as
  `git push`) gets "blocked `git push`; push from your own terminal, or let Sindri send it", no allow button. **Allow is a draft profile edit plus the approve flow:**
  the button starts the typed approval (a session can't approve; the human does), and its states are Sending, "Needs your
  approval" (the approval opened), allowed, refused (SB-6 pins them). A weekly roll-up (the weekly job, ledger-only) says
  "14 refusals, 9 to the same host" so the owner can turn repeats into decisions.
- **Learn mode** (Q13): the network may run unenforced for a bounded first period, recording instead of blocking; the proxy
  itself enforces the expiry. **A one-line switch ends it early:** `sindri sandbox learn off [--repo <repo>]` (or
  `sandbox.network.mode: enforce` in the profile). It is a tightening, so it needs no re-approval (R3-06).
- **Reproduce it.** `sindri sandbox exec <launch> [-- <cmd>]` is a **human-only CLI path** (not an API route, so a session
  cannot reach it through its forwarder). It starts a shell (or the command) in a fresh sandbox built from that launch's stored
  plan, with the worktree, the run's ref directory and the run directory bound **read-only** (the live agent may still be
  using them), a fresh empty scratch (not a copy), no launch secret, no GitHub token directory, and its own throw-away egress
  listener (the ended launch's is gone). It runs inside the same slice and counts against the session cap. It is not the
  agent's session and cannot touch it.

### 3.7 Toolchains and other products

The defaults assume dotnet, node and rust. For any other repo the allowlists are a per-repo declaration, not a machine-wide
edit: `sandbox.repos.<repo>` in the profile (`homeAllow`, network groups and extra hosts, cache paths). `sindri repo
onboard` runs `sindri sandbox check` against the repo, detects its toolchains (project files), and offers the entries; the
owner approves them with the profile. A repo without a declaration gets the built-in defaults.

### 3.8 The host-side prepare (SB-11; R3-08)

A fresh worktree has no `node_modules`, and the session cannot run install scripts (`npm_config_ignore_scripts=true`). The
repo declares what to prepare, and the launcher does it **on the host, as the owner, before the launch**:

- **Declaration:** `sandbox.repos.<repo>.prepare.dirs`, the package directories to install (for this repo `sindri`, `judge`,
  `scorer`, `scripts`, `skills/ui-evidence`, `skills/bugFixOrchestrator`), each with its tool (`npm ci`) and, for install
  scripts, the packages the repo's own `allowScripts` entry names. Nothing else is installed with scripts.
- **Cached per lockfile hash.** The key is the hash of each declared directory's lockfile **read from the run's base commit**
  (`git show <base>:<dir>/package-lock.json`), never from the worktree, so a resumed run whose agent edited the lockfile does
  not feed it to install scripts running as the owner. A hit copies the cached `node_modules` (reflink where the filesystem has
  it) into the worktree; a miss runs the install and fills the cache under `<real home>/cache/prepare/<hash>/`. A Rerun of
  the same base is a hit. If the agent later changes a lockfile in its worktree, the new packages are the session's `packages`
  group business, with scripts off.
- **Through `heavy`.** The install runs as `heavy --expensive npm ci`, so it takes a box-wide slot and queues like any other
  expensive job; the wait shows as "Preparing the workspace".
- **Failure is `SND-LAUNCH-021`, a step failure** (`step.failed(launch)`; HTTP class Unavailable, 503), reasons
  `install-failed` (the tool's exit and the tail of its output), `lockfile-missing` (a declared directory has none at the base
  commit) and `timeout` (the prepare's own limit, default 10 minutes). The owner retries (a new run) or uses the desk twin. A
  repo with no declaration has no prepare and no `-021`.
- **Tests:** `Prepare_cache_key_reads_the_base_commit`, `Prepare_runs_through_heavy`, `Prepare_failure_is_021_with_its_reason`,
  `Prepare_hit_copies_without_running_install`, `Prepare_does_not_run_scripts_outside_allowScripts`.

### 3.9 Sessions you open yourself (SB19)

Owner decision Q6, 2026-10-18: a session started with `sindri open` (or the app's Open) on an item, with no Step run, is a
Sindri launch and is sandboxed like a Step's. It has no `StepPolicy`, so the launcher builds an **attended plan** from the
same builder with a fixed attended policy (Mode Default, the item's worktree writable). Everything not listed below is
the Step answer unchanged: the mount stack, the git directory rules, the environment allowlist, the credential source,
the hook inventory, the shims, the canaries, the denial loop and `sandbox exec`.

| Aspect | Attended session | Reason |
|---|---|---|
| Writable | The item's worktree (created as a Step's is when the item has none, on `sindri/open/<launch>/work`), the run ref directory, the private scratch, the tool caches and hook spool paths, and a run directory for the plan, `denials.json` and `egress.log`. Never the owner's main checkout | A person editing code needs a worktree; the git-directory rules (SB5) keep one mistaken `git branch -D` away from the owner's branches, and work reaches `main` through a PR as a Step's does |
| Inner policy | The CLI's normal asking mode (Claude Code `default`, Codex's on-request approval) with the **same deny rules**; no `dontAsk` | A person is there to answer a question, and a refusal with no way to say yes is the wrong default at the keyboard. Q12 concerns unattended sessions only |
| Wall clock | **Not applied.** No `session wrap` wall watchdog and no `RuntimeMaxSec` on the scope; memory, CPU, pids and the parent slice's total cap apply. `sandbox.attended.wall` can set a ceiling (default `off`) | A conversation left open overnight is normal; a person's time is not a runaway. The limits that protect the box (memory, pids) are not about time |
| Session cap | Counts against `sandbox.limits.sessions`. When the cap is full the open is **refused at once** with the `-020` words and the count; it does not wait | The person can close a session; a silent wait would look like a hang |
| Network | The groups of a `WriteScope` Step (`model`, `packages`); `github` never. The repo's learn mode applies as it does to Steps | A `curl \| sh` or a typo'd package is as likely at the keyboard |
| GitHub | `none`. `agent-app` is not available to an attended session | The person has a terminal outside the sandbox and can push from it; the App token is a Step credential (SB-7) |
| Takeover, handback, banner | Not applicable: there is no run to take over and no suspension to read. The escape is carried by the tmux status line (the golden of §7 with "attended" in it) and by the first screen of `sindri open` | Nothing to hand back to |
| Credential source | The same as Steps (SB18) | One login story |
| A refusal | Not a park (there is no engine run). `sindri open` prints the §8 sentence for the code in the attended wording and exits 1; the app shows the same card with Check again. Check again resumes nothing for an attended open; the person presses Open again | The park machinery belongs to the engine's runs |
| Ledger | `launch.started`'s `sandbox` summary gains `attended: true`; `sandbox.denied`, `sandbox.limit-hit` and `session.ended` (`limit/memory`) as for a Step; no `step.failed` | The same record for the same mechanism |
| Resume or reopen | Reuses the stored plan; a missing plan is `-015 no-stored-plan` | SB1 |

**Meta lane sessions are unchanged.** The meta team's own sessions are started by `scripts/meta-launch.sh` in tmux, not
through Sindri's launcher; they are not sandboxed, their hooks and statusline are untouched, and Sindri neither lists
nor limits them.

**Until SB-5b merges, a `sindri open` is not sandboxed** (owner, final read, 2026-10-18): the launcher opens it as
#295 does today, unconfined, so the app can open sessions from P10a on. The sandbox port is not consulted for an attended
open (`UnavailableSandbox` refuses Steps only), and its `launch.started` carries no `sandbox` summary. SB-5b switches it:
from that merge on every attended open goes through the attended plan above, and there is no setting that keeps the old
path. Steps are unaffected: until SB-5a every Step launch is refused with `-014` (SB16).
- **Tests (SB-5b):** `Attended_open_before_the_switch_is_unconfined_and_has_no_sandbox_summary` (removed by SB-5b),
  `Attended_open_uses_the_attended_plan`, `Attended_open_has_no_unconfined_fallback`.

## 4. Mechanism options

| Option | Filesystem | Network | Limits | Verdict |
|---|---|---|---|---|
| **bubblewrap** (`bwrap`; installed here, 0.9.0; verified 2026-10-18 under the AppArmor user-namespace restriction) | Full: read-only root, tmpfs home, per-path binds | Own namespace plus unix-socket forwarders | None; pair with a cgroup scope | **Linux primary.** The only unprivileged tool that does read-only root with a rebuilt home and a per-session network namespace; Claude Code's own Linux sandbox uses it |
| **systemd-run `--user --scope`** (cgroup v2; `cpu memory pids` delegated here; linger on) | None | None (`IPAddressDeny` needs BPF a user manager can't load) | Memory, CPU, tasks, kill, `RuntimeMaxSec` | **Linux limits**, with bubblewrap. No user manager is `-014` |
| **Landlock** (in the kernel's LSM list) | Allowlist only: can't carve a deny out of a granted tree, can't hide a path | TCP port rules only | None | **Fallback adapter, not built in v1.** Fits where `bwrap` is blocked; Codex uses it internally |
| **Containers** (rootless podman, Docker) | Full; the worktree needs a UID mapping, so files the engine reads would be owned by a subuid, against engine P8 D10 | Full | Full | **Not now.** D §8.2's design and the "container's tmux later" of WF §6.5; same port, new adapter |
| **Devcontainer** | n/a | n/a | n/a | **No** (docs/devcontainer.md, SB13) |
| **Firejail** and setuid sandboxes | Good | Good | Some | **No.** Not installed, setuid, no gain over `bwrap` |
| **sandbox-exec** (Seatbelt, macOS) | Full by path; can name the keychain files | Outbound by `localhost:port` or IP, no names | None (the watchdog polls memory) | **macOS primary** (SB-9). Deprecated but what Claude Code and Codex use there |

**Lean on, wrap, or both: both, with Sindri's layer the guarantee (SB2).**

| Question | Claude Code's built-in sandbox | Codex's built-in sandbox | Recommendation |
|---|---|---|---|
| What it gives | Settings `sandbox` key (bubblewrap/Seatbelt): Bash-tool commands confined, a domain allowlist, `autoAllowBashIfSandboxed` | `--sandbox read-only\|workspace-write\|danger-full-access`, `sandbox_workspace_write.*`, `--ask-for-approval` | Real, but each covers the CLI's tool calls only, differs by version, and is absent in Cursor |
| Whole session tree? | Only Bash-tool children | Only model-run commands | No: the outer layer wraps the CLI itself |
| Nested in Sindri's bwrap? | Needs a nested user namespace, which AppArmor may refuse | Same | **Off by default** (`sandbox.inner: auto`): the self-check tries the inner one once per box and keeps it only if it starts; the result is per pinned version (SB-S1) |
| Use it for | Permission rules, mode, and the inner copy of §3.2 | `approval_policy="never"` plus the overrides | Always: the inner policy file is L-17's `policy` form (§5). Advice with a good error, not the boundary |

## 5. Per-target forms

The engine's `StepPolicy` is mapped in two places: the **outer plan** (§3, the same for every target) and the **inner
policy** (L-17's `policy` form). With no human attached (a Step), the inner policy never relies on a prompt: a call outside it is
refused, not asked about. An attended session asks instead (§3.9), with the same deny rules. **The keys in this table are provisional until SB-S1 item 6 records which keys each pinned CLI
honours**; the built-in forms are tested against that record.

| Engine input | Outer plan | Claude Code (`claude-settings-file`, `--settings <path>`) | Codex (`codex-overrides`, `-c`) | Cursor |
|---|---|---|---|---|
| `Mode: Default`, `WriteScope` | worktree and run ref directory writable, run directory writable | `permissions.defaultMode: "dontAsk"`; `allow` `Read`, `Edit`, `Write` under the worktree and run directory, `Bash`, `mcp__sindri-step__*`; `deny` `Read` of the denied paths **and the login copies** (the per-launch login directory of §3.1, in its `Read`, `Grep` and `cat`-style `Bash` forms), `Bash(env)`, `Bash(printenv*)`, `Bash(git push:*)`, `Bash(gh:*)`, `Bash(rm -rf /*)`-style rules, and `Bash` rules refusing `rm` and `mv` on `$AW_STATE_DIR/locks` and `$AW_STATE_DIR/judge` (R3-09) | `sandbox_mode="workspace-write"`, `sandbox_workspace_write.writable_roots=[<worktree>, <run dir>]`, `network_access=false`, `approval_policy="never"`; or, if the inner sandbox can't start (§4), `danger-full-access` with the outer layer as the only sandbox | **Reserved**: parks `-015` until a box with the CLI verifies a config argument |
| `ReadOnly` (no `WriteScope`) | only the run directory writable | `dontAsk`; `allow` `Read`, `Glob`, `Grep`, `Edit`/`Write` under the run directory only, `mcp__sindri-step__*`, a read-only `Bash` set (`git status`, `diff`, `log`, `show`, `sindri`); `deny` `Edit`/`Write` elsewhere | `sandbox_mode="read-only"`; the run directory is permitted **by the outer layer only** (Codex's `writable_roots` applies under `workspace-write`, so it is not claimed here; SB-S1 item 6 records whether the CLI tolerates the outer write) | Reserved |
| `AllowedTools` non-empty | no change | the list replaces `allow`'s tool entries; denies stay | the nearest `-c` set; a tool Codex can't express is `-015` | Reserved |
| A Step only tightens (owner, 2026-10-17) | the profile's deny list and groups are the floor; a Step may remove a group from itself, never add one | same for rules | same | same |

The Claude settings file also sets `sandbox.enabled: false` unless the box's `sandbox.inner` is on, sets
`disableBypassPermissionsMode: "disable"`, and relies on the owner's hooks in the read-only agent-home `settings.json`. Owner
decision Q12: a Step uses `dontAsk` (refuse what is not allowed), not `bypassPermissions`.

**Targets that can't be confined.** An entry whose `policy` form is `none` or reserved, whose `open` template the plan can't
wrap (a GUI app that owns its process, the `none` host), or whose `bin` the plan can't make runnable inside the sandbox (a
path under a denied tree) parks with `-015`. `-013` is "the entry has no form for what the Step asked"; `-015` is "the
entry has the form but the sandbox can't confine what the form lets through". `argvHash` (SL8) is unchanged: the plan and
policy file are per-launch values, so one approval holds across runs. The wrapper argv (`systemd-run … bwrap …`) is Sindri's,
not part of any entry.

## 6. Launch flow, refusals and codes

`ILauncher.Launch` for a Step run becomes (LCH §3's order, new steps in bold): resolve the agent → SL8 (`-012`) → L-17's form
check (`-013`) → **build the plan from the StepPolicy (pure; `-015`)** → **probe the mechanism (cached per box, refreshed by
Check again; `-014`)** → **session cap (`-020`: the atomic count; a wait, not a failure)** → **prepare credentials from the Step's credential source (`-017`, `-018`)** → budget (E-28) → **write the
plan file; start the outside half of the network (§3.2); run the self-check (`-016`)** → start in the host → return. The
self-check sits inside the 4 s host-start stage of LCH §3.

**Who writes what** (E1-I4): `ILauncher.Launch` appends **nothing**. The engine's launch activity writes
`launch.outcome:<launch>` (one fact key for `launch.started` and `launch.failed`, P8's 2026-10-18 update). The sandbox
summary (mechanism, `planHash`, writable paths, network groups, inner on/off, limits, credentials) is a field of
`StartedLaunch` (an SB-0 type), which the engine copies onto the `launch.started` payload; a refusal is `FailedLaunch`
with its code and reason.

The **self-check** proves the plan, not the box. Inside the planned sandbox, a canary for each denial must fail and one
for each grant must succeed:

| Canary | Proves |
|---|---|
| `fs-deny` | a denied path (`~/.ssh`, the state dir, and `<real home>/auth/human.token` itself) is absent; write outside the set fails |
| `fs-write` | a write in the worktree, run directory and scratch succeeds |
| `git-ref` | **in the real worktree and its real repo** (not a scratch one): the effective settings are the sandbox's (`git config --get core.hooksPath` is the per-launch directory, `commit.gpgsign` is `false`); an unsigned `git commit-tree` of `HEAD`'s tree then `git update-ref refs/heads/<ns>/selfcheck <that commit>`, and a second `update-ref` that moves it, both work; `git update-ref refs/heads/other-selfcheck HEAD` fails. **The canary never deletes a ref** (a delete needs `packed-refs.lock`, which is read-only): the launcher removes `selfcheck` from outside afterwards and at run end |
| `env` | no secret-pattern name in the environment except the allowlist |
| `login` | the credential source delivered what it promises (§3.4): the login is readable at its planned path (a copy has mode 0600), the owner's original login is not writable and, under `separate-login`, the owner's whole agent home is absent, and the inner policy denies reading it |
| `hooks-run` | the three-part hook canary of §3.1: plain hooks deny, a `judge` round trip works, the fail-open log is written and the meta hooks do nothing |
| `net-forward` | a call through the Sindri forwarder reaches the API, stub ping in SB-4a, then `sindri status` through the session home's launcher-written `control.json` in SB-5a (rev 7); the proxy forwarder answers |
| `net-deny` | a connect to a non-allowed host through the proxy is refused; a direct connect fails |

| Code | Kind | Meaning | `reason` / detail | Engine route |
|---|---|---|---|---|
| `SND-LAUNCH-014` | **no runtime** | The sandbox isn't available on this box | `no-bwrap`, `no-user-namespace`, `no-user-manager`, `no-cgroup-controllers` (which), `no-forwarder` (`socat`), `unsupported-platform` | **N1 (SB16):** the launch result closes the agent run. A `deskCapable` Step takes the **`runtime.unavailable` transition** and the desk run opens in the same frame: no round is counted, the loop rules are bypassed, and it is not presented as a Step failure (R3-01); any other Step parks NoRuntime with a sandbox marker, whose Resume opens a **new agent run** that tries again. Availability never enters the recorded agent map |
| `SND-LAUNCH-015` | **launch park** | The target or its form can't be confined to this policy | `form-none`, `form-reserved`, `tool-not-expressible` (the tool), `bin-denied` (its path), `host-not-tmux`, `worktree-outside-repo`, `no-stored-plan` | New park kind `launch`; Resume starts a **new run** (new token, launch id, worktree `sindri-<instance>-<run+1>`) |
| `SND-LAUNCH-016` | **launch park** | The self-check failed | `canary`, `expected`, `observed`, the failing argv and scrubbed stderr | Same `launch` park |
| `SND-LAUNCH-017` | **no runtime** | The target's model login is not available from the chosen credential source for this session | `no-login`, `expired`, `source-unsupported` (the target has no adapter for the source), each with the source id (`owner-copy`, `separate-login`) | Same as `-014` for a Step. An attended open has no run to park: `sindri open` shows the card (§3.9). Never falls back to another source |
| `SND-LAUNCH-018` | **step failure** (`step.failed(launch)`) | The agent GitHub App token couldn't be minted (`agent-app` only) | `no-key`, `http-<status>`, `no-network` | The owner retries (a new run) or uses the desk twin |
| `SND-LAUNCH-019` | **step failure** (`step.failed(limit)`) | A sandbox limit ended the session | `kind` `memory` or `wall`, the limit, the peak | **Never auto-retried in the same run.** The owner retries (a new run with a new worktree; the killed run's branch and commits are kept and named in the failure row) or uses the desk twin |
| `SND-LAUNCH-021` | **step failure** (`step.failed(launch)`) | The host-side prepare of §3.8 failed | `install-failed`, `lockfile-missing`, `timeout` | The owner retries (a new run) or uses the desk twin; never auto-retried |
| `SND-LAUNCH-020` | **resource wait** | The cap on concurrent sandboxed sessions is reached: a queue condition, not a fault | `sessions`, the cap, the number running | The launch activity retries with backoff (30 s) until the Step's state timeout, which then parks `timeout` as usual; **no `launch.outcome` meanwhile** and the wall clock has not started; the run shows "Waiting for a free session slot". The count and start are one atomic step |

`-019` rides on `session.ended` and `step.failed`; it is **catalogue-only** and has no HTTP class (E1-S3). `-020` is a wait the
launch activity absorbs; its HTTP class (Unavailable, 503, retryable) exists for the launch call. The others have HTTP
classes: `-014` Unavailable (503), `-015`, `-016`, `-017` Conflict (409), `-018` and `-021` Unavailable (503). Each gets a `SindriErrors`
row with a golden per `(code, reason)`, in the owner's words (§8). Check again (§8) answers a `launch` or sandbox-marked
NoRuntime park when every probe passes, **up to the free session slots, oldest first, after showing which**. A viewer failure
never changes any of this (LCH SL5). **An attended open has no engine run:** `sindri open` and the app show the same sentences in the attended wording (§8); nothing parks, and Check again resumes nothing for it (§3.9).

**Engine side** (the engine lane's **P8 U9**, before SB-5a; SB16). **Park names (R3-02; the engine lane may rename them in U9, the shape is fixed):** `-015` and `-016` use a **new `ParkReason.Launch`** (the member after `NoRuntime`), and `ParkKind` splits the rows inside it: `ParkKind.Unconfinable` (`-015`) and `ParkKind.SelfCheck` (`-016`), each with a `FitRow` row and `Fitting → [Resume]` with `ResumeKind.Rerun` (a new run). The sandbox case of `-014`/`-017` is **not a new kind**: it is the existing `ParkReason.NoRuntime` with a **detail field `sandbox`** (`{code: "SND-LAUNCH-014"|"SND-LAUNCH-017", reason}`); when the field is present the park's owner text is the §8 golden for that `(code, reason)` instead of NoRuntime's "no runtime in this build", and its resume kind is **`ResumeKind.Rerun`** (open a new agent run and try the launch again), not the build case's `Restart`. Also in U9: the `runtime.unavailable` transition of SB16 (desk run in the same frame, no round, no Step failure); the `-020` wait in the launch activity; the rule
`session.ended(limit)` → `step.failed(limit)` carrying `kind`, the limit and the peak (not `lost`); the rule for a session
that ends while the run is suspended (§7); `IRunSuspensions` and `GET /sindri/launches/{launch}/suspended`. The `Limit` member
of `SessionEndReason` and `SessionEnded`'s `kind`, `limit` and `peak` fields are **SB-0 types**: #295's `SessionEndReason` has
only `Exit` and `Lost` today (E1-I2). Before it sends `session.ended`, `session wrap` (or the launcher) takes **a final
usage sample** for every reason, so a kill doesn't lose the heaviest minute against the budget (E1-I3).

## 7. Takeover and handback

**SB9 (owner decision Q1, 2026-10-18): confinement stays exactly as launched.** The engine's behaviour is fixed (engine P8 D11): a park or
takeover suspends the run and keeps the session; the human joins the same tmux session; while taken over only a human
report (`via: desk`) is accepted and the agent's launch secret is refused; handback resumes the watch on the same launch id;
no second session is ever started. The sandbox adds:

| Moment | Confinement | Limits | The session |
|---|---|---|---|
| Takeover (typing in the pane, or `OpenItem` on a live instance) | **Unchanged** | Memory, CPU, pids keep applying. **The wall clock pauses** | Kept |
| A park (a loop cap, a gate refusal, a timeout, a question) | **Unchanged** | The wall clock pauses | Kept (engine P8 D11) |
| Handback or a park answered | **Unchanged**: nothing was loosened | The wall clock resumes with what remained | Same launch id |
| The human needs something the agent can't do (read a secret, push) | Do it in a window of your own: a new tmux window's shell is outside the sandbox and you are the owner. `sindri sandbox exec` reproduces what the agent saw | n/a | Kept |
| The scope is killed or lost while the run is suspended | | | The engine **records it on the suspension**; the answer or handback then becomes a **Rerun** (a new run), because there is no session to hand back to. The answer's own effect (extra tokens, a grant) still applies to the new run |
| A suspended run's session hits a memory limit | Killed as for any session | | As the row above |

**Who knows the run is suspended** (E1-S1, R2-01, SB16): the source of truth is the engine's suspension state, read through
the engine-owned port `IRunSuspensions.IsSuspended(LaunchId, ct)`, which reads the ledger's park, takeover, answer and handback
events joined through `OpenRun.Launch`. It is exposed as `GET /sindri/launches/{launch}/suspended` → `{suspended: bool}`.
`session wrap` polls it (outside the sandbox, with the launch secret) on its watchdog tick and counts only unsuspended time
against the wall limit; the API lane never reads `views.db`. **If the answer is unavailable (the control plane is down) the
run counts as not suspended**: a runaway is still stopped, at the price that a parked session can reach its wall limit while
`serve` is down. The scope's `RuntimeMaxSec` is only the loose ceiling of SB7.

Why not relax on takeover: a live bubblewrap namespace can't be changed, so "loosen" would mean a relaunch, which may end the
conversation the human joined and breaks engine P8 D11. Why not re-tighten on handback: nothing was loosened. The cost: a
human who joins and says "go ahead, push it" sees the agent refuse; they push from their own terminal. The owner chose this
(Q1); an unconfined relaunch is not part of the design.

**What the human sees and says** (R1-10, R2-08, R3-07): a banner on the Step sheet, with the full text "Sandboxed session.
Pushes and secrets are blocked. The clock is paused while you're here. To push or read a secret, open a new window: it is
outside the sandbox. `sindri sandbox exec` shows what the agent saw." The pane's tmux status line carries a **short golden
under 80 characters** (`Golden/Sandbox/statusline.txt`): `Sandboxed: push and secrets blocked. Outside it: <prefix> c`,
where `<prefix>` is read from the session's own tmux prefix (`tmux show-options -gv prefix`, shown as "Ctrl-b" for `C-b`),
so a remapped prefix is right. The human who takes over by **typing in the pane** never sees the app, so the status line
carries the escape too, and `sindri sandbox exec` is named in the first screen of the other takeover paths and in the refusal text. The refusal wording the inner rules return is addressed to whoever is typing and has no rule id (the id stays in the
denial record): "Blocked by the sandbox. You can do this yourself in a new terminal window." Each takeover path has its
surface: typing in the pane uses the status line; `OpenItem` and `sindri open` of a Step run use the takeover flow's first
screen, which shows the same escape, not a footnote. A session you open yourself (§3.9) has no takeover; its status line reads `Sandboxed (attended): push and secrets blocked. Outside it: <prefix> c` (also under 80 characters, `Golden/Sandbox/statusline-attended.txt`) and the first screen of `sindri open` carries the same escape.

When a run closes (report decided, `step.failed`, cancel, instance ends; engine P8 D11) the host stops the tmux session, the
scope ends, the proxy and forwarders stop, and `session wrap` removes the plan file, token directory and scratch.
**The worktree is left as it was, readable by the engine's process** (engine P8 D10); purging it is the instance's cleanup
(LN-WF2).

## 8. What the owner sees

**Ledger events (additive; API §9 rules).**

| Event | When | Fields |
|---|---|---|
| `launch.started` gains `sandbox` (copied from `StartedLaunch`) | every Step start | `{mechanism, planHash, writable: [path…], network: groups, networkMode: enforce\|learn, inner: on\|off, limits: {memoryMax, cpuQuota, tasksMax, wall}, credentials: {github}}`. Paths are listed, contents never |
| `sandbox.denied` (public) | the 1st, 10th and 100th denial of each (kind, target) per run; at most 200 distinct targets per run, then one overflow event | `{launchId, kind: network\|policy\|path, target, rule, count}`. The target is a host, a path or a tool name; a denied command line is never recorded whole (it can hold a token): rule id and tool only, redacted and truncated. These events are the ledger's cadence; **the counts the app shows come from the run's denial counter** (§3.6) |
| `sandbox.limit-hit` (public) | `TasksMax` reached (once), `MemoryHigh` crossed, disk growth | `{launchId, kind, peak}` |
| `session.ended` gains `reason: limit` | a limit kill | `{kind: memory\|wall, limit, peak}` plus the peaks of every limit (so Q5 is tuned from data); the final usage sample precedes it |

**Owner-voice copy.** One sentence per `(code, reason)`, in the SB-0 goldens (`tests/…/Golden/Sandbox/copy.json`), with the
command as a copyable second line. The card's fix is the same sentence `sindri doctor` prints (sentences below are the
goldens' text):

| Code · reason | What the card says |
|---|---|
| 014 · no-bwrap | "This machine has no sandbox tool, so this Step can't run by itself. Install bubblewrap (`sudo apt install bubblewrap`), then press Check again." |
| 014 · no-user-namespace | "The system blocks the sandbox tool. Allow bubblewrap in AppArmor, then press Check again." Second line, copyable: `printf 'abi <abi/4.0>,\ninclude <tunables/global>\nprofile bwrap /usr/bin/bwrap flags=(unconfined) {\n  userns,\n  include if exists <local/bwrap>\n}\n' \| sudo tee /etc/apparmor.d/bwrap && sudo apparmor_parser -r /etc/apparmor.d/bwrap`; `sandbox check` prints the same |
| 014 · no-user-manager | "The sandbox needs your user session manager, which isn't running. Run `loginctl enable-linger $USER`, then press Check again." |
| 014 · no-cgroup-controllers | "The system doesn't give your account memory or CPU limits. Enable the missing limit (named here) in systemd, then press Check again." |
| 014 · no-forwarder | "The sandbox needs `socat` to reach Sindri. Install it, then press Check again." |
| 014 · unsupported-platform | "Steps can't run by themselves on macOS yet." Shown gray, as "not yet supported", never red (R1-20). The Step goes to your desk |
| 015 · form-none / form-reserved | "This agent hasn't been taught how to follow a Step's rules, so it can't run by itself. Use Claude Code or Codex, or add its policy setting." |
| 015 · tool-not-expressible | "This Step allows `<tool>`, which this agent can't restrict. Change the Step's tools, or use another agent." |
| 015 · bin-denied | "The agent program lives somewhere the sandbox hides (`<path>`). Move or reinstall it somewhere under your home or `/usr`." |
| 015 · host-not-tmux / worktree-outside-repo | "The Step's workspace isn't set up the way the sandbox needs. This is a Sindri fault: press Copy details and report it, then Resume." |
| 015 · no-stored-plan | "This session's sandbox record is gone, so it can't be reopened safely. Start the Step again." |
| 016 · fs-deny | "The sandbox could not hide your private files (SSH keys, Sindri's data) from the Step. Open the details, fix what they name, then Check again." (Copy details) |
| 016 · fs-write | "The sandbox could not give the Step its workspace to write in." |
| 016 · git-ref | "The sandbox could not let the Step commit on its own branch only." |
| 016 · env | "The sandbox found a secret in the Step's environment." |
| 016 · hooks-run | "Your safety hooks did not run inside the sandbox (`<which>`)." |
| 016 · net-forward | "The sandbox could not reach Sindri from inside." |
| 016 · net-deny | "The sandbox did not stop a connection it should block." |
| 016 · login | "The sandbox could not give the Step its sign-in without exposing yours." |
| 017 · no-login / expired · `owner-copy` | "Claude Code (or Codex) isn't signed in for Steps. Sign in as yourself, then press Check again." |
| 017 · no-login / expired · `separate-login` | "Steps have their own Claude Code (or Codex) sign-in, and it's missing or has expired. Run `sindri sandbox login <target>`, then press Check again." |
| 017 · source-unsupported | "The sign-in method chosen for Steps (`<source>`) isn't available for `<target>`. Change `sandbox.login.source` in the profile, then press Check again." |
| 018 · no-key / http-`<status>` / no-network | "Sindri couldn't get a short-lived GitHub token for this Step: the agent GitHub key is missing / GitHub refused the request (`<status>`) / there is no network to GitHub. Retry the Step, or do it at your desk." |
| 021 · install-failed / lockfile-missing / timeout | "Sindri couldn't prepare this repo's packages for the Step (the install failed / a package list is missing / it took too long). Open the details, fix what they name, then run the Step again; or do it at your desk." |
| 019 · memory | "This Step ran out of memory (`<limit>`) after `<time>`. Its work so far is kept on branch `<name>`. Raise `sandbox.limits.memory` in the profile (now `<limit>`; for example `<1.5 ×>`; a re-approval), then run the Step again; or do it at your desk." |
| 019 · wall | "This Step ran out of time after `<time>`. Its work so far is kept on branch `<name>`. Run it again, or do it at your desk." |
| 020 | "Waiting for a free session slot (`<n>` of `<cap>` running). This Step starts by itself." Shown as a waiting state, never red |

**For a session you open yourself** (§3.9) the card and `sindri open` use the same goldens keyed with `attended`: "this Step
can't run by itself" becomes "this session can't be opened", "press Check again" stays, and "or do it at your desk" is
never added. `-020` reads "No free session slot (`<n>` of `<cap>` running). Close one, then open again." (a refusal, not a
wait). The attended status line and start line are goldens too.

A card for a `deskCapable` Step adds "or do it at your desk" to every `014` and `017` sentence. **The app's words** are
"sandbox", "limit" and "blocked"; "scope", "plan", "canary" and "confined" never appear in an owner string (a golden test
checks every string against the word list).
`-019` says plainly that limits are **profile-wide**: raising one is a profile edit and a re-approval, not a per-Step knob.

**Check again runs every probe and shows every blocker on one card** (R1-09), so the first run is one visit, not four. It
re-runs the box probe, the form check and the login check, then a throw-away self-check on a fixture plan. When all pass,
it **lists the Steps it would resume before it acts**: parked instances on a sandbox code, **oldest first and no more than
the free session slots**: "Ready. Resume these 2 Steps? [Resume] [Not now]". Resume sends the answers (as the human; a
service never answers). A result can be partial ("Resumed 1 of 2: `<item>` has moved on"), and the rest stay parked for the
next press. `sindri sandbox check` is the same thing from the terminal and is the **pre-flight**: `sindri doctor` and `sindri
repo onboard` run it and print its remediation block in one list.

**Surfaces.**
- `sindri doctor`, a `sandbox` section in plain terms, **generated from the same goldens as the card** (one sentence for both): `ok  sandbox ready: bubblewrap 0.9.0, limits available, 8 checks passed in 180 ms`, `ok  login: Claude Code signed in for Steps (source: owner-copy)`, `fail login: Steps have their own Claude Code sign-in, and it's missing. Run sindri sandbox login claude-code` (source `separate-login`), `ok  limits: 3 sessions × 12 GiB = 36 GiB of 62 GiB`, `warn limits: 3 × 12 GiB is more than this machine's memory (the heavy wrapper still gates slot 3)`, `fail sandbox: The sandbox needs your user session manager, which isn't running. Run loginctl enable-linger $USER`, `ok  hooks: 3 kinds verified inside a Step (plain, judge-backed, meta skipped)`, `info  agents' own sandboxes: off (not needed; Sindri's covers it)` (not a fault), `info  macOS: Steps not yet supported` (gray), `info  sessions you open: sandboxed too, no time limit while you're attached`. A failing line is a `-014`/`-016`/`-017` reason, so the card and the line say the same sentence.
- `sindri sandbox check|show|denials|explain|exec`: all read-only except `exec` (human-only, read-only mounts, §3.6). `show` leads with four lines (Can write / Can read / Network / Limits), then the mount table, the groups, the environment names (never values), the limits and the shims.
- **Health and Settings**: the sandbox state is a row on Health ("Sandbox: ready" or the blocker) and a section in Settings → Agents (the mechanism, the groups, the limits, learn mode with its expiry). While learn mode is on, Health shows a **warning row** ("Sandbox: learning, network open for `<repo>`, ends in 6 days"). The vocabulary is "sandbox", "limit" and "blocked"; "scope", "plan", "canary" and "confined" don't appear in the app.
- `sindri status` and `agents`: a Step session reads "sandboxed · mem 2.1/12 GiB"; a session you open yourself reads "sandboxed · attended · mem 2.1/12 GiB". Meta lane sessions are not Sindri's and are not listed.
- **The app** (SB-6): the Agents sheet "Sandbox" line (writable folder, groups); the park card with the sentences above and a **Check again** button; the "Blocked" list on the item sheet; the waiting state; the limit ending; the takeover banner (§7).

**States** (pinned as SB-0 goldens; `Aw.Sindri.Ui.Tests` reads them): *Check again*: idle → in flight (label kept with a spinner, "Sending…" announced after 300 ms, a second click does nothing) → still failing (the card lists every blocker, not the first) → all clear with the list ("Ready. Resume these 2 Steps?") → resumed, or partial ("Resumed 1 of 2"). *Park card*: never checked, checked stale (shows the age), checked fresh. *Sandbox line*: sandboxed (Step), sandboxed (attended), plan file unreadable ("details unavailable"), serve down (dimmed). *Blocked list*: none, some, many (top 5 and a link); *Allow* button: idle, Sending, "Needs your approval", allowed, refused. *Waiting*: "Waiting for a free session slot (3 of 3 running)", then started. *Limit ending*: memory, wall, with and without a kept branch. *Health row*: ready, blocked (the reason), learning (warning), serve down, never checked. *Settings → Agents section*: loaded, learn mode on (expiry shown), unreadable plan, serve down.

**Accessibility** (pinned in `docs/csharp/sindri-ui.md`'s terms; SB-6's done-when includes each row):

| Control | Keyboard | Screen reader | Focus | Text scale 1.3 · never colour-only |
|---|---|---|---|---|
| Sandbox line / headroom | in the sheet's tab order | name "Sandbox: on, writes only to `<folder>`" ("Sandbox: on, attended, writes only to `<folder>`" for a session you open); headroom as text "memory 2.1 of 12 GiB" | n/a | wraps; state as a word |
| Park card | a `focus-region` group (`ControlTypeOverride=Group`) with a named peer; its fix text before its buttons | name "Needs you: Step `<x>` can't start, `<first clause of the golden sentence>`" (for example "This machine has no sandbox tool"), never the raw reason code | when it appears from a user action, focus goes to its heading; **when it is answered and goes, focus moves to its neighbour (the Health row or Park section heading it came from; the `KeepFocus` case) after the live region has spoken "Ready. Resuming 2 Steps"** | wraps; severity as a word and an icon |
| **Check again** | Enter and Space; the 2 px focus ring | a zero-width transparent `TextBlock` with `LiveSetting="Polite"` already in the tree before its state; its `Text` changes (never set to `""`); "Sending…" after 300 ms, then the result | stays on the button (enabled for the keyboard) while checking; a second click does nothing; the region **rests on the label before a repeat outcome**, so a repeated "still failing" is a change | the result is text |
| Resume list `[Resume] [Not now]` | both reachable, Resume first | the list read before the buttons | on Not now, back to Check again | text |
| Blocked list | a list; each row's fix button is reachable | each row reads by kind: "Blocked host `<target>` `<n>` times; allow it for this repo", "Blocked path `<target>` `<n>` times; allow it for this repo" (a not-bound home path only), "Blocked path `<target>`: never allowed, `<reason>`" (permanent deny list, no button), "Blocked tool `<name>` `<n>` times; push from your own terminal" (policy, no allow button) | the Allow button keeps focus through Sending and "Needs your approval" | n/a |
| Waiting for a slot (`-020`) | in the run's row, not a trap | a polite zero-width live `TextBlock`, present before its state: "Waiting for a free session slot, `<n>` of `<cap>` running" once, then "Started" when it starts by itself (R3-07) | does not take focus | a word, never red |
| Allow, "Needs your approval" | the Allow button stays reachable | the same polite live `TextBlock`: "Sending", then "Needs your approval: open the approval to allow `<target>`", then "Allowed" or "Refused" (R3-07) | stays on the Allow button through every state | text |
| Limit ending | in the failure row | the whole sentence | n/a | n/a |
| Takeover banner | not a trap; dismissible once, back after reattach | a polite zero-width live `TextBlock`, read once on appearance | does not take focus | text, not colour |
| Health row, Settings section | in the page's tab order | row name "Sandbox: `<state>`"; learn mode "learning, ends in 6 days" | n/a | wraps; state as a word |

## 9. Profile keys and environment

Under `sandbox:` in the profile (S1's schema; one section in `docs/csharp/sindri-profile.md`, checked by
`scripts/check-profile-doc.mjs`). A key can only tighten a Step; the profile is the owner's. **An approved profile with no
`sandbox:` section is valid**: it gets the built-in defaults below; tightening needs no re-approval, loosening (a longer
limit, a new group or host, learn mode, `agent-app`, a looser login source) does; `sindri doctor` prints "using the built-in sandbox defaults" and
the profile preview shows the new section on the next approval.

| Key | Default | Meaning |
|---|---|---|
| `sandbox.limits.memory` / `.cpu` / `.tasks` / `.sessions` | `auto` (20% RAM, max 12 GiB) / 50% / 2048 / 3 | Per-session memory, CPU share, processes, concurrent sessions |
| `sandbox.limits.total` | 60% of RAM | The parent slice's `MemoryMax` |
| `sandbox.limits.wallGrace` | `10m` | Added to the Step's `timeout` |
| `sandbox.limits.homeTmpfs` / `.parkMax` | 256 MiB / `24h` | The size of the `$HOME` tmpfs (counts against the scope's memory); the longest **single** suspension the scope's `RuntimeMaxSec` ceiling allows beyond the Step's timeout, renewed on every resume (SB7) |
| `sandbox.fs.homeAllow` / `.denyExtra` | detected / `[]` | Home entries bound in (each `ro` or `rw`); more denied paths |
| `sandbox.network.groups` / `.mode` | §3.2's table / `enforce` | Named host groups; `enforce` or `learn` (Q13); setting `enforce` ends learn mode early (also `sindri sandbox learn off`) |
| `sandbox.repos.<repo>.…` | none | Per-repo `homeAllow`, groups and hosts (§3.7) |
| `sandbox.inner` | `auto` | `auto`, `on`, `off` |
| `sandbox.credentials.github` | `none` | `none` or `agent-app` (SB-7) |
| `sandbox.login.source` | `owner-copy` | The model credential source (SB18): `owner-copy` or `separate-login`. Retires the earlier `sandbox.agentHome` key |
| `sandbox.login.targets.<target>` / `sandbox.repos.<repo>.login.source` | none | Per-agent and per-repo overrides; the most specific wins (repo, then target, then profile). A recipe or Step may ask for a stricter source and never a looser one (`owner-copy` < `separate-login`); stricter needs no re-approval, looser does |
| `sandbox.attended.wall` | `off` | A wall-clock ceiling for sessions you open yourself; `off` or a duration (§3.9). Setting one is a tightening |

No environment variable switches the sandbox off. `.env.example` gets only paths a test redirects (`AW_SANDBOX_BWRAP`,
`AW_SANDBOX_SYSTEMD_RUN`, `AW_SANDBOX_SOCAT`), checked by `scripts/check-env-example.sh`. `deps.yaml` declares the new `cli`
edges (`bwrap`, `systemd-run`, `socat`) in SB-0. **Docs the phases update:** `docs/csharp/sindri-sandbox.md` (new, with a
glossary: plan, `planHash`, park, step failure, Check again), `docs/csharp/sindri-launcher.md` (the launch flow gains steps),
`docs/csharp/sindri-profile.md`, `docs/csharp/sindri.md` (codes), `AGENTS.md` and `CLAUDE.md` (Directory Structure, Commands:
`sindri sandbox check|show|denials|explain|exec`), `docs/sindri/README.md`, `.agents/rules` (a note for agents: "what works
inside a Step", §3.5) and `setup.sh`/`scripts/install-deps.sh` (the prerequisite list and the `AW_NO_SYSTEM_INSTALL`
behaviour for `bubblewrap` and `socat`).

## 10. Testing

Per the C# rules (TDD, lanes, fakes with contract tests). Red first; builders on Sonnet.

| What | Cases | Lane |
|---|---|---|
| Plan builder (pure) | `ReadOnly_makes_only_the_run_directory_writable`, `WriteScope_adds_the_worktree_and_the_run_ref_directory`, `Run_branch_is_inside_its_own_directory`, `Home_is_empty_unless_allowlisted`, `Agent_home_config_is_read_only`, `State_dir_is_denied_except_the_hook_inventory`, `Environment_has_no_secret_name_except_the_list`, `A_step_cannot_widen_the_profile_floor`, `Docker_socket_is_always_denied`, `Cpu_quota_is_cores_times_share`, `Resume_of_a_step_session_is_confined`, `Missing_sandbox_section_uses_defaults`; mount-table goldens | unit |
| Contract base | `ISandboxContract` on `PassThroughSandbox`'s documented behaviour and the real `BubblewrapSandbox`: write outside the set fails, denied path absent, `git commit` and `update-ref` on the run ref work, `branch -D other`, `tag`, `stash` and `push` fail, the hook canary passes, `dotnet restore` of a fixture works with the cache paths, a connect to the host's other loopback port fails, the Sindri forwarder works, a non-allowed host is refused, an allowed one relays (local stub) | integration |
| Limits | a memory hog ends `limit/memory`; a sleeper ends `limit/wall`; the wall clock stops while suspended (park and takeover); `TasksMax` gives `EAGAIN` and one event; the stale heavy lock is swept; `RuntimeMaxSec` kills a scope whose wrap was killed | integration |
| Heavy | inside the sandbox the real `heavy` takes and releases a `locks/` slot; `AW_HEAVY_LOCK_HELD=1` runs through; the `dotnet` shim goes through `heavy` | integration |
| Launcher and codes | each `(code, reason)` from a fake with its kind; `Code_kinds_match_the_engine_table` reads one table both sides use; the copy goldens; `Final_usage_sample_precedes_session_ended` | unit |
| Denials | a planted token in a denied command isn't in the event; `egress.log` counts; `explain` answers offline | unit, integration |
| Per target | built-in forms against the SB-S1 record; `cursor-config` parks `-015` | unit |
| UI | view-model tests over the SB-0 state goldens; the accessibility rows | unit |
| Round 2 additions | `Park_longer_than_wallGrace_keeps_the_scope`; `Unavailable_suspension_read_counts_as_not_suspended`; `Git_canary_never_deletes_a_ref`; `Git_settings_beat_the_repo_config`; `Hook_inventory_matches_config_hooks`; `Judge_hook_runs_inside_the_sandbox` and the fail-open log is visible; `Shim_takes_the_box_wide_slot`, `Shim_inside_a_held_slot_does_not_loop`, `Heavy_lock_held_is_never_in_the_environment`; `Cap_count_and_start_are_atomic`; `Check_again_resumes_no_more_than_free_slots`; `Explain_reads_only_the_plan_file` (state directory denied); `Home_tmpfs_is_sized`; `Socket_path_is_under_the_limit`; `Sandbox_exec_mounts_read_only_and_is_not_an_api_route`; `Failed_path_under_home_is_matched_as_not_bound`; `Owner_strings_use_the_sandbox_vocabulary`; `Agent_home_write_set_is_exactly_the_named_one` | unit, integration |
| Credential source and attended sessions (rev 5) | `Credential_source_contract_holds_for_every_adapter` (`IModelCredentialSourceContract`, the fake and `OwnerCopyCredentialSource`; `SeparateLoginCredentialSource` joins in SB-8); `Unknown_source_id_is_a_profile_error`; `Repo_override_beats_target_beats_profile`; `Step_cannot_ask_for_a_looser_source`; `Unavailable_source_is_017_with_its_source_id_and_never_falls_back`; `Login_canary_fails_when_the_owner_login_is_writable`; `Source_is_in_the_plan_hash_and_survives_resume`; `Open_of_an_item_is_sandboxed`; `Attended_session_has_no_wall_watchdog_and_no_runtime_max`; `Attended_session_refuses_at_once_when_the_cap_is_full`; `Attended_session_never_gets_agent_app_or_the_github_group`; `Attended_inner_mode_asks_with_the_same_deny_rules`; `Resume_of_an_attended_session_is_confined`; `Attended_refusal_is_shown_not_parked`; the attended goldens | unit, integration |
| Proof | §12, SB-5a, SB-5b, SB-10 | heavy |

Proofs that start tmux use a private socket and unset `TMUX` (`scripts/test-tmux.sh`); no proof touches the real
`$AW_SINDRI_HOME`, state directory or agent home (a canary file under a fixture `HOME` stands in for a secret); none uses a
temp HOME for dotnet (a NuGet cache leaks per run).

## 11. What stays out, and what is accepted

Out: Meta lane sessions, which Sindri's launcher does not start (Q6); containers, a pid namespace, seccomp and anything aimed at a deliberate escape
(§2); a model proxy with a dummy credential (D §8.2's W1); per-session bandwidth and content inspection; Windows; Cursor
(reserved, `-015`); bounded tool-less jobs (`claude -p`, X56).

Accepted (the LOW findings, recorded rather than fixed): disk is warned, not enforced; a session you open yourself has no wall limit (§3.9); a Codex Step loses per-session `heavy`
serialisation; the login is checked at launch only; shared tool caches are shared state (Q4); install scripts are off by
default but a package that needs one fails loudly (SB-11's host-side prepare is the fix for the repo's own); the shared `/proc` (probe pending, SB-2)
(§3.1); `locks/` and `judge/` are writable for the hooks and the heavy wrapper, so a careless cleanup inside them can hurt the
owner's box-wide mutex or judge records (the launch brief says not to, `heavy` prints the holder with a "do not delete" line, and the inner policy denies `rm` and `mv` there); a session can read other
repos' session history and its own login copy (under `owner-copy`, SB18, SB-8); a parked session can hit its wall limit while `serve` is down (SB7);
a replayed plan uses the profile it was built with, so a later tightening applies to new runs only; the real `launches/` folder is writable from the session so the idle rule works, which lets a Step read or touch another launch's session ids, capture rules and markers, and `approved/` shows the whole approved profile snapshot (rev 7, §3.1); `human.token` stays reachable by the same-OS-user routes listed in §3.1 and by every session that runs outside the sandbox (rev 7); there is no interim control before the sandbox (owner-accepted, #1113 answer 2, §12). **Multiplayer** (README X61) changes the agent home (per user), the credentials (per user), the proofs (owner-run)
and shares the profile; `sandbox.login.source: separate-login` is the multi-user path.

## 12. Phases

**In four lines (rev 7):** Linux sandbox now. macOS Seatbelt in parallel, runner to be assigned by the owner. No stopgap before the sandbox. One open question (§13: macOS `sindri open` between SB-5b and SB-9).

**Order (rev 7, owner answers 2026-10-25 on #1113).** Two tracks run at once. **Linux, now**, in this merge order: SB-0, SB-1, then SB-2 and SB-4a (built in parallel, either may merge first; they share no files), then SB-3a (needs P10a's `session wrap`) and SB-5a (needs SB-2, SB-3a, SB-4a and P8 U9). Each parent PR is restacked onto `main` before its child. **macOS Seatbelt, in parallel:** SB-9, from the same plan SB-1 builds, with its runner the owner assigns
later (**macOS: owner-assigned runner**; the spec does not name a lane or a Mac). The macOS unit work does not wait for the Linux track; its end-to-end proof needs SB-5a, so the tracks join there, and a Linux merge does not mean the macOS box is
covered. **On a Mac** the product's sandbox is unavailable until SB-9: Steps park without a runtime (`-014` `unsupported-platform`) from SB-5a, and from SB-5b `sindri open` is refused the same way (SB1: no setting keeps the old path). Whether macOS owners keep an unconfined `sindri open` until SB-9 is an open owner question (§13, rev 7). **There is no stopgap** (owner answer 2): no interim token ACL, no process-ancestry check, no keychain trick; until a sandbox is in force the controls are the two that
already merged, #1098's server rule (a call carrying a launch is its launch's session) and #1099's session home (§3.1), and `UnavailableSandbox` keeps refusing every Step launch with `-014` (SB16).

**Why the Linux track can go now (box facts, 2026-10-25):** bubblewrap 0.9.0 is installed; `kernel.unprivileged_userns_clone` is 1 and `user.max_user_namespaces` is
127216; `kernel.apparmor_restrict_unprivileged_userns` is 1 (Ubuntu 24.04's restriction, which stops an unprofiled program from making a user namespace); what lets `bwrap` through is the AppArmor profile named `bwrap` that Ubuntu ships in `/etc/apparmor.d`, so no root step is needed per box (a box without that profile needs one, and `sindri sandbox check` reports `no-user-namespace`). Re-run for this amendment on 2026-10-25: `bwrap --ro-bind / / --unshare-user --dev /dev --proc /proc true` exits 0. #366 (SB-2) reports its contract cases passing under the same setup (a claim of that PR, not re-run here). The spike's item 1 and 4 answers (§15) stand for the same tools.

Each phase is one PR, driven by /specToProvenPR in its lane, red first. Sizes: S a few files, M one subsystem slice, L more.
**(owner)** steps run on the owner's box. **The critical path to the first agent-run item:** P10a → DT-J2 (#306) → P8 (with
**P8 U9**, the engine-owned Core work of SB16, landed before SB-5a) → the first unattended Step, with the sandbox merged before
the owner's first run. P8 builds on DT-J2 and needs no P7 (E1-I7). SB-10 needs #295 (P10a's contract) and P8's U8, not P7.

| Phase | What | Files | Size | Depends on | Done when | Lane |
|---|---|---|---|---|---|---|
| **SB-S1** | **Spike (no product code).** On the owner's box, throw-away scripts outside the repo: (1) interactive Claude Code and Codex under `bwrap` (with and without `--new-session`) in tmux: attach, type, detach, and as an attended `sindri open` session (the asking mode works in the pane); (2) the agent homes, `~/.claude.json` (atomic rename), the per-launch login copy with `CLAUDE_CONFIG_DIR`, whether a token refresh **rotates** the refresh token, which paths must be writable, and the Codex write set; (3) the forwarder plumbing of §3.2 carries Sindri's MCP and the CLI's HTTPS, and `NO_PROXY` works; (4) `systemd-run --user --scope` keeps the tty, and wrap reads `memory.events` and the scope's `Result` after an OOM kill; (5) nested inner sandboxes per pinned CLI; (6) which settings and `-c` keys each pinned CLI honours (§5); (7) which discovery file `sindri` reads; (8) the nested-heavy rule with the real wrapper; (9) **the real commands of §3.5 in the sandbox, from a clean worktree**: `dotnet build -warnaserror`, the unit lane, `dotnet format`, `npm ci && npm test` in `sindri/`, `scripts/dotnet-verify-packages.sh`, one bash test; the writable list is derived from it; (10) *informational only:* does a relaunch on the pre-chosen session id keep the conversation (`claude --resume`, `codex resume`)? Nothing depends on the answer (the unconfined relaunch was dropped, Q1); it is recorded in §15; (11) the real hooks inside the sandbox, including the `judge`-backed ones, `prism-context` and `rtk-rewrite`, with `aw-meta` absent; bubblewrap's `--size` on the pinned version; the supported transcript formats for denial reading; that the whole self-check fits the 4 s host-start stage; (12) the credential sources of SB18 on the pinned CLIs: `CLAUDE_CONFIG_DIR`/`CODEX_HOME` pointing at a separate home, and whether sessions sharing one separate login race on a token refresh | scratch dir; findings into §15 and `docs/csharp/sindri-sandbox.md` | S | none | Each of the twelve answered with command and version **written into §15**; SB-2 to SB-4b start only when §15 holds the answers they use. A "no" on (1) or (3) goes to the owner first | ideas |
| **SB-0** | Contracts: `docs/csharp/sindri-sandbox.md` and the docs list of §9 (with the shims); the plan-file schema; **the types**: `SessionEndReason.Limit` and `SessionEnded`'s `kind`, `limit`, `peak`, the `StartedLaunch` sandbox summary, `FailedLaunch` reasons; codes `-014` to `-021` with kinds and the engine table; **the copy goldens, the states and the accessibility rows** of §8; profile keys (incl. `sandbox.login.*` and `sandbox.attended.wall`); the `IModelCredentialSource` port types and the `-017` source copy goldens; `deps.yaml` edges; `.env.example`; the README row | `docs/csharp/*`, `SindriErrors.cs`, the launch and session types, `SindriProfile` schema and `ProfileRules`, `deps.yaml`, `tests/…/Golden/Sandbox/*` | M | none (beside the spike) | `check-profile-doc`, `check-deps`, `check-env-example` pass; goldens pin every `(code, reason)`; the engine lane reads the kind table and the types | api |
| **SB-1** | The pure core: `ISandbox` and types, the plan builder, mount stack and deny lists, the hook inventory, the environment allowlist, the git settings, the limits parsing, host-group rules, the per-repo declaration schema, `planHash`, the renderer for `show` and `explain`, `UnavailableSandbox`, `PassThroughSandbox` and `RecordingSandbox` (test-only ones in `Aw.Sindri.Testing`); the `IModelCredentialSource` port, its registry, `FakeModelCredentialSource` and `IModelCredentialSourceContract` | `…Core/Sandbox/*`, `…Features/Sandbox/`, `…Testing/*`, tests | M | SB-0 | The plan-builder cases pass; the environment test fails on a planted `GH_TOKEN`; the product Host registers only `UnavailableSandbox` | api |
| **SB-2** | `BubblewrapSandbox`: argv from the plan, the run ref directory, the sized tmpfs home, the `OwnerCopyCredentialSource` (agent-home write set and login copy), the `login` canary, the cache paths, the hook binds, the canaries incl. `git-ref` and the three-part `hooks-run` | `…Infrastructure/Sandbox/Bubblewrap*`, `…Testing/ISandboxContract` | M | SB-1, SB-S1 (1, 2, 9, 11) | The contract base passes on real `bwrap`; a `dotnet restore` fixture works; the hook canary denies `rm -rf /` and the judge round trip passes; `fs-deny` asserts `<real home>/auth/human.token` is absent and `ls <real home>` shows only `launches` and `approved`; the `/proc` probe runs and the spec is amended with its outcome (§3.1, rev 7) | api |
| **SB-3a** | **Wall and memory:** the scope and slice properties, the loose `RuntimeMaxSec` ceiling, the watchdog on the engine's suspension read (SB16), `memory.events` polling, `limit` and `-019`, the final usage sample | `…Infrastructure/Sandbox/SystemdScope*`, `…Host/Commands/SessionWrap*`, the heartbeat | M | SB-1, SB-S1 (4); **SL-2/P10a's `session wrap`**; P8 U9's suspension endpoint | The limits cases pass; `session.ended` carries `reason: limit` and the kind, never `lost`; the clock stops during a park **and a park longer than `wallGrace` keeps the scope** | api |
| **SB-3b** | **Cap and heavy:** the atomic session count and the `-020` wait signal, the `TasksMax` event, the `dotnet`, `npm` and `npx` shims (explicit `heavy --expensive\|--light`, the golden shim table, the held-slot variable, heavy's resource environment), the stale-lock sweep | the same, and the waiting message in `bin-r1b/heavy` | M | SB-3a, SB-S1 (8) | The heavy cases pass (slot taken, no loop, no `AW_HEAVY_LOCK_HELD`); the cap test races two launches | api |
| **SB-4a** | **Static egress:** the outside `EgressService`, the proxy with its static host groups and per-run caps, the short socket directory, `egress.log`, the inside init and forwarders, `NO_PROXY` | `…Infrastructure/Sandbox/Egress*` | M | SB-1, SB-S1 (3) | The network cases pass on stubs; the Sindri MCP call works through the forward and not through the proxy; a request through the forwarder reaches a stub `/sindri/ping` that answers the record's pid and epoch (the `net-forward` canary's SB-4a half) | api |
| **SB-4b** | **Denials:** learn mode (the proxy enforces its expiry), the denial counter, `sandbox.denied` events, `denials` and `explain` | the same | M | SB-4a | The denial cases pass; `explain` answers with the state directory denied | api |
| **SB-5a** | **Launcher integration, ReadOnly first, thin:** the launch flow, probe and Check again (all blockers on one card, the resume list), the `ReadOnly` policy files for `claude-settings-file` and `codex-overrides`, `-014` to `-017`, the `StartedLaunch` summary, `sandbox check\|show`, doctor lines. It needs only the model-host egress (no learn mode, no `exec`), a scope with `MemoryMax` and the loose ceiling, the watchdog and the run directory; the session cap is the parent slice alone until SB-3b. **Merging this puts a real scoping or review Step on the factory** | `…Features/Launch*`, `…Infrastructure/Launch/*`, `…Host/Commands/Sandbox*`, events, goldens (the gate runs `Aw.Sindri.Ui.Tests`) | M | SB-2 (read-only parts), SB-3a, SB-4a, **P8 U9**, P10a with SL-2 and L-17 | **Proof:** `sindri-prove.sh` starts a ReadOnly fixture Step under a private tmux socket with a stub CLI that tries each forbidden action: every one is refused, the allowed ones work; with `bwrap` renamed away a deskCapable Step runs at the desk and another parks (NoRuntime) and resumes into a **new agent run** after Check again; the launcher makes the session home and writes its `control.json`, and `sindri status` answers inside the sandbox through it (the `net-forward` canary's end-to-end half, rev 7); **the engine side of `limit` (rev 7):** `src/Sindri/Aw.Sindri.Infrastructure/Workflow/LedgerAgentSessions.cs:43` maps only `Exit`, `Lost` and `Idle` and **throws** `InvalidOperationException` on any other `session.ended` reason, so a session a sandbox limit ended would crash the read; add `AgentSessionEnd.Limit` (`Core/Workflow/Engine/Runtime/AgentSessionEnd.cs`), map `SessionEndReason.Limit` to it with the kind, limit and peak, and route it to `step.failed(limit)` (SB16, engine P8 U9); a test `Ledger_session_ended_limit_reads_as_limit` fails first | api |
| **SB-5b** | **WriteScope:** the worktree and run-ref-directory binds, the `packages` group, the git canary on the real worktree, the git settings, the `WriteScope` policy files, `denials` and `explain` wired, `sandbox exec`, `-018` stub, **sessions you open yourself**, switched from today's unconfined open (§3.9: the attended plan, no wall watchdog, the cap refusal, the attended copy and status line) | the same | M | SB-5a, SB-3b, SB-4b, SB-2 | **Proof:** a WriteScope fixture Step commits on its branch, can't update another ref or push, `dotnet build` works, the engine reads its diff after the session; a `sindri open` fixture session is sandboxed the same way, reads "sandboxed · attended", has no wall limit and is refused at once when the cap is full | api |
| **SB-11** | The per-repo declaration and **prepare**: `sindri repo onboard` runs `sandbox check`, detects toolchains, offers the `sandbox.repos.<repo>` entries (incl. the host-side prepare of §3.8: cached per lockfile hash, through `heavy`, `-021` on failure); the "allow this for this repo" fix | `…Features/Onboard*`, `…Features/Prepare*`, `sandbox` commands | S | SB-5b | Onboarding a Python and a Go fixture repo offers their entries; this repo's `sindri/` tests pass in a Step after the prepare; the prepare cases of §3.8 pass | ideas |
| **SB-6** | UI and status copy: the Sandbox line, the park card, Check again and the resume list (every state), the Blocked list and Allow, the waiting state, the takeover banner and tmux status line (and the attended ones), the limit ending, Health and Settings rows, the fixture sections | `src/Sindri/Aw.Sindri.Ui/Views/Agents*`, `Views/Launch*`, `Views/Health*`, `Settings`, `scripts/sindri-ui-fixture.sh`, `docs/csharp/sindri-ui.md` | **M** | SB-0 (goldens), SB-5a for live data | View-model tests over the SB-0 goldens; the accessibility rows of §8 each have a test; the fixture shows every state | ui |
| **SB-7** | The GitHub credential: `agent-app`, the mint, the token directory, the shims, the 45 min refresh, the `github` group, the branch-only pre-push refuser, `-018` | `…Infrastructure/Sandbox/AgentToken*`, shims, tests with a fake `AGENT_APP_API_URL` | M | SB-5b | With the fake API a Step pushes its run branch, not another; the token is never in `env`, `ps`, a transcript or the plan; the refresh is seen through the directory bind | ideas |
| **SB-8** | `SeparateLoginCredentialSource` (`separate-login`), an adapter behind the port of SB18 and nothing else changes: `sindri sandbox login <target>`, the agent home under `$AW_SINDRI_HOME/agent-homes/<target>/`, hooks installed into it, `CLAUDE_CONFIG_DIR`/`CODEX_HOME`, readers and the collector following the directory, its `-017` copy. Selected with `sandbox.login.source` (no code change); built when its trigger fires or the owner asks | `…Infrastructure/Sandbox/SeparateLogin*`, `providers/*/install-hooks.sh` (a target-dir option), readers | M | SB-2 (the port, the contract base and `OwnerCopyCredentialSource`), SB-5a | The adapter passes `IModelCredentialSourceContract`; a Step runs with none of the owner's `~/.claude` bound; its transcript is found by the usage reader and the collector; switching the profile key back and forth changes no other code | api, ideas |
| **SB-9** | macOS: `SeatbeltSandbox` from the same plan, the keychain rule, the polling watchdog. **Starts with SB-1** (it needs only the plan, not SB-2 or SB-5a), in parallel with the Linux track (rev 7) | `…Infrastructure/Sandbox/Seatbelt*` | M | SB-1 to start; for the proof SB-2's `ISandboxContract` base, SB-4a's proxy, SB-3a's `session wrap` watchdog and SB-5a; a Mac | **(owner, on a Mac)** the SB-5a proof passes; a memory `limit` fires within 5 s of the ceiling; the session home's links resolve under the Seatbelt profile (Seatbelt has no bind mounts and no network namespace: the same real paths of §3.1 become path allow rules plus the session home, the session home's `control.json` is the link to the real file, and the SBPL rule order and `/var` to `/private/var` canonicalisation are part of done) | owner-assigned (macOS runner) |
| **SB-10** | **(owner) The first unattended run**, the published binary, real sandbox, on P8's signal: `workflow.step` on a scratch item launches Claude Code confined, commits on its branch, cannot read `~/.ssh` or push, reports; a park keeps the session and pauses the clock (a park longer than `wallGrace` included); a takeover keeps the same confined session; handback resumes; a `sindri open` session on the scratch item is sandboxed and reads "sandboxed · attended"; the worktree is read by the engine afterward; a planted memory hog ends `step.failed(limit)` with its branch kept | owner's box, a scratch repo | S | SB-5b; P8's U8 and #295 | The owner records the run (events, `sandbox show`, the engine's rows) | owner |

### 12.1 Parallelization plan

```
now ──► SB-S1 (ideas) ───────────────────────────────┐
now ──► SB-0 (api) ──► SB-1 (api) ──┬──► SB-2 ───────┼──► SB-5a ──► SB-5b ──► SB-11 ──► SB-7 (ideas)
                                    ├──► SB-4a ──────┤       │         │└──► SB-10 (owner, with P8)
P10a with SL-2 ──► SB-3a (api) ─────┘        P8 U9 ──┘       │       SB-3b, SB-4b feed SB-5b
                                                              └──► SB-8 (if triggered)
SB-1 ──► SB-9 (macOS Seatbelt, owner-assigned runner), in parallel with SB-2 / SB-4a / SB-3a; its proof needs SB-5a
SB-0 goldens ──► SB-6 (ui) ◄────────────── live data after SB-5a
P8 (engine) proves in process on PassThroughSandbox (tests only); the product refuses with -014 via UnavailableSandbox until SB-5a
```

| Wave | API lane | Ideas lane | Engine lane | UI lane | Owner |
|---|---|---|---|---|---|
| 0 | SB-0, then SB-1 | SB-S1 | reads SB-0's table and types; **P8 U9** (park kind, N1 rule, limit rule, suspension read and endpoint, `-020` wait); P8 on `PassThroughSandbox`, in process | — | nothing to answer: the decisions are in §13 |
| 1 | SB-2 and SB-4a (no common files), SB-3a once `session wrap` is on `main`; SB-9's unit work (macOS; the runner is whoever the owner assigns, not the API lane) | SB-7 tests against a fake token API | P8 eval cases per code kind | SB-6 on the goldens | the spike's "no" answers |
| 2 | SB-3b and SB-4b beside SB-5a, then SB-5b | SB-11, then SB-7 | P8 U7 (the D10 git hardening) | SB-6 live | — |
| 3 | — | SB-8 if triggered | P8's binary proof is SB-10 | — | SB-10 |

- **What runs in parallel so a team stays busy:** the spike with SB-0/SB-1; SB-2, SB-3a and SB-4a are independent files; SB-3b
  and SB-4b run beside SB-5a; the ideas lane writes SB-7 against a fake while SB-5 lands; the UI lane needs only goldens; the
  engine lane never waits for the real sandbox until its owner proof.
- **ReadOnly first** (SB-5a, deliberately thin): a scoping or review Step runs sandboxed before WriteScope plumbing, the git
  binds, the `packages` group, the shims and learn mode exist. Owner decision Q14: the first Step is a read-only one (scoping or review).
- Hotspots (`SindriErrors.cs`, `EventCatalog.cs`, the command and endpoint tables, the JSON context, `Add…()`, `deps.yaml`):
  each phase appends at its anchor; the second to merge rebases. SB-0, SB-5a, SB-5b and SB-6 change goldens the UI tests
  read, so their gates run `Aw.Sindri.Ui.Tests`.
- Every proof uses `scripts/sindri-prove.sh`, a private tmux socket and a guarded temp `AW_SINDRI_HOME`.
- **P8 is not blocked and no Step runs unconfined in the product:** P8 merges with the product Host registering only
  `UnavailableSandbox`; its launcher tests and its real-launch proof run in process on `PassThroughSandbox`, which only test
  hosts register (SB12, SB16). In the product every Step has no agent runtime until SB-5a: `deskCapable` Steps run at the
  desk, others park NoRuntime. SB1 is true by construction for Steps; a `sindri open` opens unconfined as today until SB-5b switches it (owner's final read, §3.9).

## 13. Owner decisions (2026-10-18)

**Open question (rev 7).** Between SB-5b and SB-9 a macOS owner cannot `sindri open` (no Seatbelt adapter, and SB1 allows no unconfined path). Keep macOS opens unconfined, with a visible "not sandboxed" line, until SB-9 lands, or accept the refusal? Until answered the spec's text is the refusal.

**Wire decisions (rev 7, from SB-1's restack; the code fixes land in the stack, not in this amendment).**
(a) **One reason, in `Detail`.** `launch.failed` has main's `Detail` (`LaunchFailureDetail`: `Stage`, `Reason`, `Current`, `Approved`), and `LaunchFailureDetail.Of` already copies any error's `reason` extension into `Detail.Reason`, so a sandbox `-014` to `-021` refusal's reason lands there. The sandbox's top-level `Reason` is folded into it and goes away; the credential source of `-017` becomes `Detail.Source`, read from the error's `source` extension the same way. The launch row and the launch view read the detail. One place for "why".
(b) **The wire name is `launchId`.** `sandbox.denied` and `sandbox.limit-hit` carry `{launchId, ...}` as §8 already says and as main's launch events do; SB-0's `SandboxDenied` and `SandboxLimitHit` serialize `launch` today and follow the spec (rename, and the three goldens under `Golden/Sandbox/events/`).

The owner answered the fourteen questions of revision 4 on 2026-10-18. None of these fourteen is open; the one question from revision 7, above, is the only open one. The numbers Q1..Q14 are the
ones the rest of the spec cites. "Accepted" means the default of revision 4 stands as written.

| # | Decision | What it means |
|---|---|---|
| Q1 | **A takeover stays sandboxed** (option A). The unconfined relaunch (option B) is dropped | SB9 and §7 say it plainly; there is no per-takeover "continue unconfined"; a human who needs more does it in a window of their own. SB-S1 item 10 stays only as an informational measurement |
| Q2 | **The model login is injectable**: a credential source per Step, chosen by the profile with no code change | SB18 and §3.4: port `IModelCredentialSource`, adapters `owner-copy` (today's default) and `separate-login`; `sandbox.login.source` per profile, target and repo; a Step may only ask for a stricter source; unavailable is `-017` with the source id; SB-8 is "add an adapter" |
| Q3 | Accepted | Every Step reaches its model provider; `WriteScope` Steps also reach the package servers; signature and release hosts stay host-side |
| Q4 | Accepted | Steps may write the shared NuGet and npm caches; install scripts off |
| Q5 | Accepted | 3 sessions, 20% of RAM (at most 12 GiB) each, half the CPU, 60% in total; peaks recorded |
| Q6 | **Sessions you open with `sindri open` are sandboxed too, from SB-5b on**; until then they stay unsandboxed as today (final read) | SB1, SB19 and §3.9: the same sandbox, with each difference written down with its reason (asking mode, no wall limit, refused-at-once cap, no App token, a status line instead of a banner, shown not parked). Meta lane sessions are not launched through Sindri and stay as they are |
| Q7 | Accepted | No GitHub for Steps in the first release; Sindri pushes host-side; `agent-app` is SB-7 |
| Q8 | Accepted | A Step can `pkill` (no pid namespace) |
| Q9 | Accepted | A Step can read other projects, `.env` included, read-only |
| Q10 | Accepted | Disk growth warns at 10 GiB and does not kill |
| Q11 | Accepted | Linux first, Mac second |
| Q12 | Accepted | A Step refuses what is not allowed (`dontAsk`); an attended session asks instead, because a person is there (§3.9) |
| Q13 | Accepted | Network learn mode for a repo's first ten runs or seven days; one-line switch off |
| Q14 | **The first unattended Step is read-only** (scoping or review) | It needs only the model's site and its run directory, so SB-5a is enough to run it; code-changing Steps wait for SB-5b |

### Owner notes, settled at the final read (2026-10-18)

The owner read revision 5 and approved it, with one change (note 3). Notes 1, 2 and 5 are accepted as written; note 4 is
the engine lane's agreement.

1. **Attended sessions have no wall-clock limit** (memory, CPU and pids still apply). `sandbox.attended.wall` can set one.
2. **An attended session works in a worktree, never in your main checkout** (SB5 keeps the git directory protected). If you want `sindri open` to write in the main checkout, that needs a different git-directory rule.
3. **Until SB-5b merges, `sindri open` stays unsandboxed, as today** (owner's change at the final read; revision 5 had it refused with `-014`). #295's launcher opens sessions from the app now; SB-5b switches attended opens to the sandbox (§3.9).
4. **A Step asking for a stricter login source** (agreed with the engine lane, 2026-10-18): `StepPolicy.LoginSource`, type `ModelLoginSource?` (`OwnerCopy` < `SeparateLogin`; null = no ask, omitted from JSON when null), added in SB-0. The engine adds the optional recipe/descriptor field and passes only the Step's ask (P8 U9); the launcher takes the stricter of the ask and the profile/repo/target value, so a looser ask never loosens.
5. **Attended sessions never get the GitHub App token**, even once SB-7 exists; push from your own terminal.

## 14. Revision 2 (autoplan round 1), Revision 3 (autoplan round 2) and review record

Round 1 (2026-10-18): verdicts, product ITERATE, architecture NEEDS_WORK, design BLOCKED (accessibility rule), devex
NEEDS_WORK, security NEEDS_WORK; no CRITICAL. The engine lane's review of rev 1: 3 blocking, 7 issues, 3 suggestions.
(Question numbers in this table are rev 2's; Q11 has since become decision SB17 and Q12 is now Q11.)

| id | What changed |
|---|---|
| R1-01 | §3.1 inventory of the hook state paths, taken from `~/.claude/hooks` and `config/hooks`; exceptions for read-only `hooks/ bin/ bin-r1b/ toolkit` and writable `judge/ context-guard/ digests/ probe/`; the `hooks-run` canary (§6) and a doctor line |
| R1-02 | SB1 and §6: confinement keys on the Step run, not `Attended`; resume reuses the stored plan, `-015 no-stored-plan`; test `Resume_of_a_step_session_is_confined` |
| R1-03 | §3.1, Q2: agent home writable only where the CLI writes, the owner's settings, hooks, skills read-only; credential reads denied in the inner policy; the SB-8 trigger named |
| R1-04 | §3.2 end to end: both halves, net directory exception, `NO_PROXY`, start order before the self-check, `-014 no-forwarder`, `-016 net-forward` |
| R1-05 | SB15 and §3.6: counts per class, `egress.log`, `denials`, `explain`, `exec`, `AW_SANDBOX`, launch-brief paragraph, per-repo allow edit, weekly roll-up; learn mode as Q10 |
| R1-06 | §3.5 works/fails table; cache paths into scratch, `.local/share/NuGet` and `.npm` logs; the env list extended (§3.4); `signing` and `releases` groups; the run branch told to the agent (`AW_SINDRI_BRANCH`); SB-S1 item 9 |
| R1-07 | §8 accessibility table per control; SB-6 done-when includes it |
| R1-08 | §8 owner-voice sentence per `(code, reason)` as SB-0 goldens; `-019` says limits are profile-wide |
| R1-09 | §8 Check again runs every probe, one card of all blockers, `sandbox check` as a pre-flight from doctor and onboarding |
| R1-10 | §7 banner and tmux status line, the defined refusal wording, the escape in the first screen |
| R1-11 | SB7, §3.3: parent slice with total cap, `sandbox.limits.sessions` and `-020`, `RuntimeMaxSec`, the `dotnet`/`npm` shims through `heavy` |
| R1-12 | Header and §2 refs fixed (SC W9 and W11, not WF); the run directory reconciled with engine P8 D8 as Q11 |
| R1-13 | §5 Codex ReadOnly row corrected; table marked provisional pending SB-S1 item 6 |
| R1-14 | §3.4 token in a bound directory, shims read `<dir>/token` |
| R1-15 | §3.4 per-launch gitconfig; the canary does a real commit |
| R1-16 | SB-5 split into SB-5a (ReadOnly) and SB-5b (WriteScope); Q12 |
| R1-17 | §3.7 and SB-11: per-repo declaration at `repo onboard` |
| R1-18 | `sindri sandbox exec` (§3.6, §8) |
| R1-19 | §9 docs list incl. AGENTS.md, CLAUDE.md, launcher doc; §15 is filled by SB-S1 and gates SB-2 to SB-4; a missing `sandbox:` section uses defaults |
| R1-20 | §8 neutral wording for attended, macOS gray "not yet supported", inner "off" is an info line, Health and Settings placement, vocabulary |
| R1-21 | Accepted or fixed in passing (§3.1, §3.3, §3.4, §11): per-run ref directory, disk warning, install scripts off, redacted events, private-address refusal, `CPUQuota` per CPU, the lock sweep, Codex `heavy` accepted, login check accepted, multiplayer line, peaks recorded |
| R1-22 | §3.1 "what it can't stop": the shared `/proc` |
| E1-B1 | SB11, §6: `-014`/`-017` use the NoRuntime park; `-015`/`-016` a new `launch` park kind whose Resume starts a new run; Check again answers the park when every probe passes |
| E1-B2 | SB7, §7: the wall clock pauses on any suspension; a session lost during a suspension makes the answer a Rerun |
| E1-B3 | SB12, §12: `UnavailableSandbox` is the product default; `PassThroughSandbox` and `RecordingSandbox` are test-only (tests and the proof fixture); P8 proves on the pass-through; the real proof is SB-10 |
| E1-I1 | §6, SB11: a limit kill is never auto-retried in the run; the owner retries (new run, branch kept and named) or uses the desk twin |
| E1-I2 | §6, SB-0: `Limit` and the `session.ended` fields are SB-0 types; the mapping is a new engine rule; citations fixed |
| E1-I3 | §6: a final usage sample precedes `session.ended` for every reason |
| E1-I4 | §6: the engine writes `launch.outcome:<launch>`; the sandbox summary is on `StartedLaunch` |
| E1-I5 | SB11, §6, Q8: an unavailable sandbox is "no agent runtime": `deskCapable` to the desk, otherwise NoRuntime |
| E1-I6 | SB5, §3.1, §6: the run ref `sindri/<item>/<run>/work` in its own writable directory; the canary is a real ref update |
| E1-I7 | §12: P8 builds on DT-J2 (#306), no P7; SB-10 needs #295 and P8's U8 |
| E1-S1 | §7: the engine adds the "is the run suspended" read the heartbeat calls |
| E1-S2 | §3.1 item 5: the engine's git calls use `-c core.hooksPath=/dev/null -c core.fsmonitor= -c safe.directory=` and check the `.git` file; SB-5b |
| E1-S3 | §6: `-019` is catalogue-only, no HTTP class |

### Revision 3 (autoplan round 2) and the engine recheck

Round 2 (2026-10-18): verdicts, product ITERATE, architecture NEEDS_WORK, design NEEDS_WORK (accessibility 3/5), devex
NEEDS_WORK, security NEEDS_WORK; no CRITICAL, nine HIGH (R2-01..R2-09). The engine lane's recheck of rev 2: 8 of 10 rev-1 items
resolved, B1 and B2 partial, one new blocking (N1). Its proposals for N1, N2, N3/B2, N4, N5 and Q11 are written in as decisions
SB16 and SB17; **Q11 stopped being an owner question** (the engine accepted it) and the questions are renumbered.

| id | What changed |
|---|---|
| R2-01 | SB7, §3.3, §7: one clock owner, the `session wrap` watchdog, driven by `GET /sindri/launches/{launch}/suspended` (SB16, `IRunSuspensions`; unavailable = not suspended); `RuntimeMaxSec` is only a loose ceiling (timeout + `wallGrace` + `parkMax`); test `Park_longer_than_wallGrace_keeps_the_scope` |
| R2-02 | SB11, SB16, §6: N1 (a `FailedLaunch` `-014`/`-017` closes the agent run; desk twin or NoRuntime with a sandbox marker; Resume opens a new agent run); the `launch` park has its own `ParkReason` and `FitRow` row; the engine work is P8 U9, before SB-5a |
| R2-03 | §6 `git-ref`: the canary creates and updates `selfcheck` and never deletes; the launcher removes it from outside; §3.5: branch delete and rename don't work in the session |
| R2-04 | (`launches/` superseded by O7-5) §3.1: the inventory rebuilt from the real hooks: `judge` (wrapper, node, toolkit checkout, `judge/` directory), a per-launch `logs/`, `probe/`, `digests/` and `launches/`, `aw-meta` and `meta/` absent so the meta hooks do nothing; a three-part `hooks-run` canary incl. a judge round trip; the doctor line states what was exercised; `Hook_inventory_matches_config_hooks` |
| R2-05 | §3.3: shims call `heavy --expensive\|--light /abs/tool` explicitly, with their own subcommand table; `AW_SANDBOX_SLOT_HELD` for the shims, `AW_HEAVY_LOCK_HELD` never exposed; heavy's resource environment set in the sandbox; tests |
| R2-06 | §3.5, SB-11: `npm ci` as a host-side declared prepare; rows corrected (TS packages, `check-launcher`/`check-fleet`, Temporal tests); SB-S1 item 9 runs from a clean worktree |
| R2-07 | SB11, SB16, §3.3, §6, §8: `-020` is a resource wait (backoff to the state timeout, no `launch.outcome`, atomic count under a lock); Check again lists and resumes only up to the free slots, oldest first |
| R2-08 | §7: the tmux status line carries the escape; the refusal wording is addressed to whoever is typing and has no rule id; each takeover path names its surface |
| R2-09 | §3.6, §8: counts from the run's denial counter, events only as the ledger cadence; Allow is a draft profile edit plus the approve flow with named states; fix text for *policy* and *path*; **default: a repeated denial does not raise Needs you** |
| R2-10 | §3.1, §3.4, Q2: the agent home is read-only with a named write set; a per-launch login copy (the spike checks token rotation); `~/.claude.json` in the copy's directory with `CLAUDE_CONFIG_DIR`; the inner policy denies reading the copy |
| R2-11 | §3.4, §6: git settings through `GIT_CONFIG_COUNT/KEY/VALUE` (beat the repo's config); the `git-ref` canary runs on the real worktree and checks the effective values |
| R2-12 | §3.6: `sandbox exec` is human-only, mounts read-only, has no launch secret or token, runs inside the slice and cap, has its own throw-away egress |
| R2-13 | (`launches/` superseded by O7-5) §3.1: `probe/`, `digests/`, `logs/` and `launches/` are per-launch; `locks/` stays rw for the `mkdir` mutexes with the brief, the holder print and §11 as mitigations |
| R2-14 | §3.6: failed paths under `$HOME` are matched against the not-bound set; `explain` opens only the plan file (test with the state directory denied) |
| R2-15 | SB-S1 item 10 and Q1 option B ("continue unconfined", a relaunch, per takeover, only if the conversation survives) |
| R2-16 | §12: a thin SB-5a (model-host egress, memory and wall, run directory); SB-3 and SB-4 split into a/b; SB-11 right after SB-5b |
| R2-17 | SB5, SB12, SB16, SB17, §12: the run ref `sindri/<instance>/<run>/work`; P8's real-launch proof runs in process with no Host switch, the binary proof is SB-10; P8 U9 is engine-owned; the run folder is `runs/<instance>/<step>/<run>/` with its path on `StepContext`; Q11 became a decision |
| R2-18 | §7, §8: vocabulary "sandbox" everywhere; the accessibility rows in `sindri-ui.md`'s terms (polite zero-width live `TextBlock`, `KeepFocus`, rests before a repeat outcome); a sentence per canary; `-018` reasons in words; the AppArmor command; `-020` uses the cap; `-019` names `sandbox.limits.memory`; states and rows for Health and Settings |
| R2-19 | §3.1, §9: the `$HOME` tmpfs is sized (`sandbox.limits.homeTmpfs`, 256 MiB) |
| R2-20 | §3.2: sockets in a short per-launch directory (`$XDG_RUNTIME_DIR/sindri/<8 hex>/`) |
| R2-21 | In passing: atomic cap count; per-run proxy caps and the proxy-enforced learn expiry; at most 200 distinct denial targets; doctor counts 7 checks and has a `login:` line; the answer's own effect still applies to a rerun; the D10 git hardening moved to P8 U7. Accepted: Q3 and Q7 stay bundled, a replayed plan keeps its original profile (§11) |

### Revision 4 (autoplan round 3), the final revision

Round 3 (2026-10-18): all five lenses pass (product SHIP, architecture SOUND, design 4/4/4/4, devex PASS, security PASS); no
CRITICAL, no HIGH, nine MEDIUM and LOW findings (R3-01..R3-09). The engine lane's final recheck: 0 blocking, one issue
(R3-01), agreed and written in as a decision. **The owner questions were rewritten in plain words and renumbered Q1..Q14**;
earlier question numbers in this section are rev 2's and rev 3's.

| id | What changed |
|---|---|
| R3-01 | SB11, SB16(1), §6: the desk fallback for `-014`/`-017` is a distinct **`runtime.unavailable` transition** with the desk run opening in the same frame; it counts no round, bypasses the loop rules and is not a Step failure; built in P8 U9 |
| R3-02 | §6 engine side: `-015`/`-016` use a new `ParkReason.Launch` with `ParkKind.Unconfinable` and `ParkKind.SelfCheck`; the sandbox NoRuntime marker is a detail field `sandbox` on the existing `NoRuntime` park, its owner text is the §8 golden and its resume kind is `Rerun` |
| R3-03 | SB7, §3.3, §9: `RuntimeMaxSec` is extended on every resume with `systemctl --user set-property --runtime`, so `parkMax` is per suspension; the cap-count lock is an `flock` released by the kernel and the count is read from live scopes, so there is no stale lock |
| R3-04 | §13 rewritten in plain words with no phase ids or mode names; Q3 split (sites, caches) and Q7 split (processes, other repos, disk); Q1 option C dropped and Q1 marked provisional until the early experiment; a top line says Q1, Q2, Q6 and Q14 block the first build wave and the other ten have safe defaults; renumbered Q1..Q14 and all references updated |
| R3-05 | §3.1, Q2: the CLI process reads its own login copy and the agent's tools are denied it; the copy is 0600, removed at run close and in the crash sweep; Q2 says a rotating refresh makes Steps share the owner's login |
| R3-06 | §3.6, §9, Q13: `sindri sandbox learn off` (or `sandbox.network.mode: enforce`) ends learn mode early, with no re-approval |
| R3-07 | §7: a pinned status-line golden under 80 characters that reads the tmux prefix; §8: live-region rows for Waiting and Allow's "Needs your approval", the park card's screen-reader name uses the golden sentence's first clause, per-kind Blocked actions and row names |
| R3-08 | New §3.8: the host-side prepare is cached per lockfile hash read from the base commit, runs through `heavy --expensive`, declares package directories, and fails as `SND-LAUNCH-021`; an `npx` shim; the shim table written out as a golden checked against heavy's case list; ENOSPC names `sandbox.limits.homeTmpfs` |
| R3-09 | §3.3 rule 7, §5, §11: the inner policy denies `rm` and `mv` of `locks/` and `judge/`; heavy's waiting message gets a "do not delete" line (SB-3b) |

### Revision 5 (owner answers)

The owner's answers of 2026-10-18 (§13), folded in. No review round; the changes are the answers and what follows from them.

| id | What changed |
|---|---|
| O-Q1 | SB9, §7, §12 (SB-S1 item 10 informational), §13: a takeover stays sandboxed; the unconfined relaunch is dropped |
| O-Q2 | New SB18, terms, §3.1, §3.4 (source table, choosing and overriding, adding a source), §6 (`login` canary, `-017` reasons and source id), §8 (per-source `-017` copy, `016 · login`, doctor lines, 8 checks), §9 (`sandbox.login.*`, `sandbox.agentHome` retired), §10, §11, §12 (SB-0, SB-1, SB-2, SB-8 as an adapter, spike item 12) |
| O-Q6 | SB1 rewritten, new SB19 and §3.9 (the differences and reasons), §3.3 (wall and cap rows), §5 (asking mode), §6 and §7 (attended refusal and status line), §8 (status, states, accessibility name, attended goldens, doctor line), §9 (`sandbox.attended.wall`), §10, §11, §12 (SB-5b, SB-6, SB-10, the not-blocked bullet); meta lanes explicitly unchanged |
| O-Q14 | §12: the first Step is read-only; wave 0 owner column |
| O-rest | Q3..Q5, Q7..Q13 accepted as written; §13 rewritten as "Owner decisions" with five owner notes |

### Revision 6 (owner's final read)

| id | What changed |
|---|---|
| O-N3 | `sindri open` stays unsandboxed until SB-5b, then switches (no setting keeps the old path): status line, SB1, SB19, §3.9 (the paragraph and its tests), §12 (the not-blocked bullet), §13 (Q6, note 3) |
| O-N1/2/5 | Accepted as written: no wall limit for attended sessions, always a worktree, never the GitHub App token |
| O-N4 | `StepPolicy.LoginSource` as agreed with the engine lane; fallback per profile, repo and target accepted |

### Revision 7 (owner answers 2026-10-25, #1113)

| id | What changed |
|---|---|
| O7-1 | The sandbox phase ships for launched sessions: #352 (SB-0), #355 (SB-1), #366 (SB-2) and #363 (SB-4a) are restacked onto `main` and landed in order; status line, §12 |
| O7-2 | **No stopgap.** No interim token ACL, process-ancestry check or keychain control before the sandbox; #1098's server rule and #1099's session home are the controls until it is in force: §12, §11 |
| O7-3 | **Parallel tracks.** Linux now (SB-0, SB-1, then SB-2 and SB-4a in parallel, then SB-3a and SB-5a) in parallel with a macOS Seatbelt track (SB-9) whose runner the owner assigns later; SB-9 now starts with SB-1 instead of waiting for SB-5a: §12, §12.1 |
| O7-4 | Box facts that make the Linux track possible (bubblewrap 0.9.0, unprivileged user namespaces on, Ubuntu's AppArmor userns restriction on, #366 passing under it): §12 |
| O7-5 | #1098 and #1099 reconciled with §3.1 and §3.4: the launch's session home (ro, `gh-config/` rw), the real `launches/` (rw) and `approved/` (ro) binds its links need, a launcher-written `control.json` with the forwarder URL under the sandbox (SB-S1 item 7 stands), `AW_SINDRI_HOME` always the session home, the `net-forward` and `fs-deny` canaries gain the `sindri status` and `human.token` checks, and what still blocks `human.token` once SB-2 runs. Supersedes R2-04 and R2-13 on `launches/` being per-launch |
| O7-6 | Items from SB-1's restack: `launch.failed` keeps one reason, in `Detail` (§13); the sandbox events' wire name is `launchId` (§13); SB-5a gets the explicit `LedgerAgentSessions.cs:43` item for `SessionEndReason.Limit` (§12) |

## 15. Spike record

Filled in by SB-S1 (ideas lane, 2026-10-18). SB-2 to SB-4 start only when this section holds the answers they use.
**Items 1, 2, 3, 9, 11** (they gate SB-2 and SB-4a) come first; 4, 5, 6, 7, 8, 10 and 12 follow. Items 2 and 12 (rotation) are provisional.

**Box and versions.** Ubuntu, kernel 6.17.0-40-generic; bubblewrap 0.9.0 (`/usr/bin/bwrap`); systemd 255 (`systemd-run --user --scope`, cgroup v2, `cpu memory pids` delegated to `user@1000.service`); socat 1.8.0.0; tmux 3.4;
Claude Code 2.1.302; Codex CLI 0.139.0 (0.141.0 is available); .NET SDK 10.0.401; node v24.14.1;
`kernel.apparmor_restrict_unprivileged_userns=1`, `kernel.unprivileged_userns_clone=1`, `user.max_user_namespaces=127216` (bwrap works under the restriction; nested bwrap works inside a bwrap, see item 5).
No `sqlite3` binary on the box (not needed); `rtk` is installed (`~/.local/bin/rtk`).

**Harness.** Throw-away scripts in `/tmp/sbs1.9nlh/scripts/` (not in the repo): `sbx.sh` builds the §3.1 stack (`--clearenv`, `--ro-bind / /`, fresh `/dev` and `/proc`, `/tmp` replaced by a scratch dir, `--perms 0700 --size 268435456 --tmpfs $HOME`, the allowlist binds, the agent home read-only with the named write set as scratch directories, `--tmpfs /run` plus the net directory, `--unshare-net`);
`net-up.sh` and `init.sh` are the §3.2 forwarders (a Python CONNECT proxy on `proxy.sock`, a fake `sindri-step` MCP server behind `sindri.sock`, two `socat`s inside). Every interactive run was in a private tmux server (`tmux -L sbs1-<pid>`, `TMUX` and `TMUX_PANE` unset).
Nothing wrote the owner's real `~/.claude`, `~/.claude.json`, `~/.codex` or `~/.agentic-workflow`: every writable bind was a scratch directory, and the login files were 0600 copies, deleted at the end.

### Item 1. Interactive Claude Code and Codex under `bwrap` in tmux: **yes** (Claude end to end; Codex to the sign-in screen)

| Check | Result |
|---|---|
| Claude Code, `bwrap … -- claude`, in a tmux pane, one prompt (`say ok`) | **Yes.** The TUI, the owner's statusline and the hooks start; the model answers; the transcript is written (`projects/<key>/<id>.jsonl`) |
| With and without `--new-session` | **Both work** (attach, type, detach, re-attach via a pty client; the pane keeps running). `--new-session` is therefore optional; leaving it off keeps the tty the controlling terminal |
| Attended `sindri open`: the asking mode in the pane | **Yes**, with a per-launch settings file that sets `permissions.defaultMode: "default"` and an `ask` rule (`--settings`): the pane shows "Do you want to proceed? 1. Yes 2. No", Enter approves, the command then runs. **Caution:** `--permission-mode default` alone did not ask here, because the owner's `settings.json` has `defaultMode: bypassPermissions` and `skipDangerousModePermissionPrompt`; the attended policy file must set the mode and its `ask`/`allow` rules itself |
| Codex, `bwrap … -- codex`, in tmux, with and without `--new-session` | **TUI runs**: update prompt, sign-in screen, typing, detach and re-attach all work. **A live model turn was not run** (see "Codex login" below), so Codex is "yes to the TUI, model path pending the owner's throwaway-login step" |
| Codex nested in the same sandbox | `codex sandbox -- sh -c …` (Codex's own sandbox) starts inside our bwrap (item 5) |

Commands (abridged): `SBX_NET=ns SBX_NEWSESSION=1 sbx.sh i4 -- /run/init.sh claude --mcp-config /tmp/mcp.json --settings /tmp/attended.json` inside `tmux -L sbs1-<pid> new-session -d`; attach test `( sleep 2; printf x; sleep 1; printf '\002d' ) | script -qfc "tmux -L … attach -t i4" /dev/null`.

**Things the plan builder and launcher must do (found by running it):**
1. `--clearenv` removes every export: `CLAUDE_CONFIG_DIR` must be a `--setenv`. Without it Claude reads `~/.claude.json`, finds none and shows the login screen.
2. An inherited `CLAUDE_CODE_CHILD_SESSION` marker turned transcript saving off ("Transcript saving is off"); the §3.4 allowlist already drops it, and the denial reader depends on the transcript.
3. With a read-only root, a bind destination whose parent does not exist fails ("Can't mkdir parents … Read-only file system"). `/run/sindri/net` needs `--tmpfs /run` (which also hides `/run/user/<uid>` and the Docker socket, as §3.1 wants). `--tmpfs /run` also removes the resolver stub, so there is **no DNS** in the net-namespace case, as intended.
4. **The inside `socat`s outlive the agent.** There is no pid namespace (SB6), so `--die-with-parent` only kills bwrap's direct child; the two forwarders were still running after the session ended and held the caller's stdout pipe open (a hang). §3.2's "both die with the run" holds only if the scope is stopped (`systemd-run --scope` kills everything in it) or the init stays as parent and kills them on exit instead of `exec`ing. Their stderr must go to a file: connection-reset lines printed into the tty corrupted the pane.
5. The first Enter typed within a few seconds of start was sometimes swallowed by a start-up screen; a launcher that types into the pane (the launch brief) should wait for the prompt, not a fixed delay.
6. The CLI's own start-up traffic with no network opt-outs: `downloads.claude.ai` (auto-update), `http-intake.logs.us5.datadoghq.com` (telemetry), `github.com`. With `DISABLE_TELEMETRY=1 CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC=1 DISABLE_AUTOUPDATER=1` those three disappear; what remains denied is the owner's MCP servers (`stitch.googleapis.com`, `registry.npmjs.org` for `npx` servers) and `mcp-proxy.anthropic.com` (claude.ai connectors). Add `DISABLE_AUTOUPDATER=1` to the §3.2/§3.4 telemetry list.

**Codex login: a flag for the owner.** With a copy of the owner's `~/.codex/auth.json` (`last_refresh` 2026-06-15, older than Codex's 8-day refresh age) Codex contacted `auth.openai.com` once at start-up; with no `auth.json` it never did. That is very probably a token-refresh attempt from the owner's refresh token. The copy was not modified afterwards (byte-identical) and the owner's file was not touched (mtime unchanged), so the refresh most likely did not complete, but the outcome was not recorded. The session was killed at once and no Codex session was started with the real login again. **If the owner's Codex asks to sign in again, that was this; nothing else in the spike used the Codex login.**

### Item 2. The agent homes, the login copy, rotation, the write sets: **answered, rotation provisional**

**Rotation: PROVISIONAL, from passive evidence only. The definitive test is an owner step with a throwaway login (procedure at the end of this item).** No token refresh was forced with a copy of the owner's login; the owner's `.credentials.json` was not written by this spike (mtime 2026-10-17 21:57) and every copy stayed byte-identical to it (their access token had hours left, so no copy refreshed).

Passive evidence, Claude Code 2.1.302:
- The credential record carries `accessToken`, `refreshToken`, `expiresAt` (8 h lifetime) and `refreshTokenExpiresAt` (about 30 days out): the server issues a refresh token with its own expiry.
- The binary has cross-process rotation handling: `tengu_oauth_token_refresh_lock_acquiring`, `…_race_recovered`, `tengu_oauth_401_recovered_from_rotation`, `tengu_oauth_refresh_token_marked_dead_invalid_grant`, `tengu_oauth_refresh_token_cleared_on_disk`. Code written to recover from "another process rotated the token" and to clear a dead refresh token from disk means the refresh **does rotate**.
- Observed with a **bogus** refresh token (a fake login in a scratch home, never the owner's): Claude contacted `platform.claude.com/v1/oauth/token` once, then rewrote `.credentials.json` with empty tokens and `expiresAt: 0` ("dead token cleared on disk"). So a copy whose refresh token was rotated away by the original would be cleared, and so would the original if the copy rotated it first.
- Codex: `auth.json` has `last_refresh` and a refresh age of about 8 days; its rotation behaviour was not examined (no passive source on the box other than the owner's file).

**Answer for SB-2 to build on (provisional): assume refresh rotates, and design `owner-copy` so a session never refreshes.** The copy is safe only while the access token outlives the run. So `OwnerCopyCredentialSource` should read `expiresAt` from the copy and refuse the launch with `-017 expired` (reason "login too close to expiry; open Claude Code once to refresh it") when `expiresAt - now` is under the run's planned wall time plus a margin (with the 8 h token and a 4 h default wall limit that is mostly satisfied; a `longMax` Step is not). The §3.1 fallback (the owner's login file read-write) is **not needed for the first build**; if the owner's test shows no rotation, the margin rule can be dropped. If the test shows rotation and the margin rule is too strict in practice, `separate-login` (SB-8) is the way out, as the spec already says.

**Mechanics (verified):**
- `CLAUDE_CONFIG_DIR=<dir>` works: Claude reads `<dir>/.claude.json` and `<dir>/.credentials.json`, writes its own state there, and does not touch `~/.claude.json`. With `--clearenv` it must be `--setenv`.
- **Both files are written by temp file + rename inside the config directory** (strace: `.credentials.json.tmp.<hex>` then `rename` onto `.credentials.json`; `.claude.json.tmp.<pid>.<hex>` then `rename`). A **single-file bind of either is impossible** (the rename onto a mount point fails). They must live in a bound **directory**, which is what the per-launch `CLAUDE_CONFIG_DIR` directory is. Keep that.
- `~/.claude` is bound read-only at its own path so the hook scripts named in `settings.json` run; **the original `.credentials.json` inside it must be masked** (`--ro-bind /dev/null ~/.claude/.credentials.json`, a read then gives EACCES) or the whole-directory read-only bind exposes the owner's login to the session. The per-launch config directory is bound at a different path (`~/.claude-run` in the spike) with `settings.json`, `settings.local.json`, `statusline.sh`, `mcp.json`, `skills/`, `plugins/` bound read-only into it and the write set below read-write.
- **Claude Code write set** (what a first-run session created in the scratch config directory; the rest of the directory can stay read-only): `.claude.json`, `.credentials.json` (both rename-written, so the directory), `projects/<key>/` (transcripts, `memory/`), `sessions/`, `session-env/<session>/`, `shell-snapshots/`, `file-history/`, `todos/`, `history.jsonl`, `backups/` (`.claude.json.backup.<ms>`), `cache/` (`model-catalog`), `state/`, `mcp-needs-auth-cache.json`, `.last-update-result.json`, `.last-cleanup`. Outside the config directory: `/tmp/claude-<uid>/`, `/tmp/cc-socks` and `/tmp/node-compile-cache` (all land in the scratch `/tmp`).
- **Codex write set** (`$CODEX_HOME`, observed from a start-up in an empty home; `CODEX_HOME` is honoured, checked with `codex debug prompt-input`): `config.toml` (read), `auth.json` (read; rename-written on refresh), `installation_id`, `models_cache.json`, `.personality_migration`, `rules/default.rules`, `goals_1.sqlite`, `logs_2.sqlite`, `memories_1.sqlite`, `state_5.sqlite` (each with `-wal` and `-shm`, so directory-level binds, not file binds), `sessions/`, `log/`, `memories/`, `shell_snapshots/`, `cache/`, `tmp/`. Codex refuses to create its helper binaries under `/tmp` ("Refusing to create helper binaries under temporary dir"), so a `CODEX_HOME` under the scratch `/tmp` degrades; the per-launch Codex home must not be under `/tmp`.
- `~/.claude.json` (atomic rename) answered above. `projects/<key>/` for the worktree is created by the CLI; with the read-only agent home it must be pre-created and bound read-write by the launcher.

**Owner step (needs a separate throwaway login): the definitive rotation test.** Do not use the owner's real account; use a throwaway Claude (and a throwaway ChatGPT/Codex) login.
1. `A=$(mktemp -d); CLAUDE_CONFIG_DIR=$A claude` and sign in with the throwaway account; exit. `B=$(mktemp -d); cp -a $A/. $B/`.
2. Note `sha256sum` of the `refreshToken` field in `$A/.credentials.json` (`jq -r .claudeAiOauth.refreshToken $A/.credentials.json | sha256sum`).
3. Force a refresh in A: set `expiresAt` to `1` in `$A/.credentials.json`, then `CLAUDE_CONFIG_DIR=$A claude -p "say ok"`. Compare the refresh token hash: **changed = rotates**.
4. Run the same in B: `CLAUDE_CONFIG_DIR=$B claude -p "say ok"` (with B's `expiresAt` also set to `1`). If A rotated, B's old refresh token is dead: expect an `invalid_grant`, a login prompt, or B's tokens cleared. **That outcome is the answer to "does a copy invalidate the original".**
5. Race (item 12): copy A to C, set `expiresAt` to `1`, start two `CLAUDE_CONFIG_DIR=$C claude -p "say ok" &` at once; both should answer and `$C/.credentials.json` should be valid after (the binary's refresh lock).
6. Codex: `CODEX_HOME=$A2 codex login` (throwaway), copy to `$B2`, set `last_refresh` in `auth.json` to a date 30 days ago, run `CODEX_HOME=$A2 codex exec "say ok"` and compare the `refresh_token` hash, then `CODEX_HOME=$B2 codex exec "say ok"`.
Record the three outcomes (rotates? copy invalidated? concurrent refresh safe?) in this section.

### Item 3. The forwarder plumbing carries Sindri's MCP and the CLI's HTTPS, and `NO_PROXY` works: **yes**

Setup: the §3.2 design with stand-ins. Outside: a fake `sindri-step` MCP server (Streamable HTTP, JSON replies) on `127.0.0.1:<port>`, a `socat UNIX-LISTEN:<net>/sindri.sock,fork TCP:127.0.0.1:<port>`, and a Python CONNECT proxy on `<net>/proxy.sock` (host-group allowlist, resolves names itself, refuses private addresses). The net directory (`$XDG_RUNTIME_DIR/sindri/<8 hex>/`) is bound at `/run/sindri/net`; inside, `--unshare-net` leaves only `lo`, and the init starts `socat TCP-LISTEN:<proxy port>,bind=127.0.0.1,fork,reuseaddr UNIX-CONNECT:/run/sindri/net/proxy.sock` and the same for the MCP port. Environment: `HTTPS_PROXY`/`HTTP_PROXY`/`ALL_PROXY` (both cases) to `http://127.0.0.1:3128`, `NO_PROXY=127.0.0.1,localhost,::1`.

| Check | Result |
|---|---|
| Interactive Claude with `--mcp-config` naming `http://127.0.0.1:<port>/mcp` | **Yes.** The fake server logged `server/discover`, `initialize`, `notifications/initialized`, `tools/list` and `tools/call` from `claude-code/2.1.302`; the pane showed "Called sindri-step" and the tool's marker string |
| The MCP calls did not go through the proxy (`NO_PROXY`) | **Yes.** `egress.log` has no loopback entry; `NO_PROXY=127.0.0.1,localhost,::1` is honoured by Claude's MCP client |
| The CLI's HTTPS (model API) through the proxy | **Yes.** `api.anthropic.com:443` counted `allowed` many times; the model answered |
| A host outside the group | `403` from the proxy, counted `denied` per host (`curl`: exit 56/000); the owner-MCP and start-up hosts of item 1 showed up exactly this way |
| A direct connection to an IP | Fails (no route), as designed |
| `curl` through the forwarders from the init (the self-check) | MCP `ping` round trip and an HTTPS request to `api.anthropic.com` through the proxy: listeners up in 2 to 24 ms, whole forwarder check **122 to 135 ms** |

Notes for SB-4a: (a) HTTP (not CONNECT) proxying is needed for plain-`http://` hosts (certificate-revocation and OCSP lookups are plain HTTP): the proxy must handle absolute-URI requests, not only `CONNECT`; the spike's proxy forwards them. (b) The inside `socat` lifetime and stderr, item 1 notes 4. (c) `socat` can stay; nothing in the spike argued for a helper.

### Item 9. The real §3.5 commands in the sandbox, from a clean worktree: **answered**

Worktree: `git worktree add --detach … origin/main` (`d59bf0e25`, no `node_modules`, no `artifacts/`). Each command ran inside the §3.1 stack (read-only root, 256 MiB tmpfs home, scratch `/tmp`, `~/.nuget/packages` and `~/.npm/_cacache` read-write, `DOTNET_CLI_HOME`, `NUGET_HTTP_CACHE_PATH` and `npm_config_logs_dir` in the scratch, `npm_config_ignore_scripts=true`, net namespace with the proxy in **learn mode** so every host it contacted was recorded), launched from outside as `flock <lane lock> heavy --expensive env … bwrap …` (the lane's heavy wrapper held the slot; the sandbox environment does not carry `AW_HEAVY_LOCK_HELD`).

| §3.5 row | Command | Result | Time |
|---|---|---|---|
| `dotnet build -warnaserror` | whole solution, empty `artifacts/` | **Works**: 0 warnings, 0 errors | 2 m 46 s |
| unit lane | `dotnet test --solution AgenticWorkflow.slnx --filter-trait "Lane=unit" --ignore-exit-code 8` | **Works**: 11 544 tests, 11 543 passed, 1 skipped, 0 failed | 24 s wall (11.7 s of tests) |
| `dotnet format --verify-no-changes` | | **Works**, exit 0 | 2 m 33 s |
| `scripts/dotnet-verify-packages.sh` | locked restore + `dotnet nuget verify --all` | **Works, contrary to §3.5**: "139 packages locked, restored and signature-verified", exit 0 | 13 s |
| `npm ci && npm test` in `sindri/` | fresh worktree | `npm ci` **works** (119 packages, 1 s from the shared cache); `npm test`: 1 357 passed, **1 failed** | 3 m 01 s |
| one bash test | `bash scripts/tests/sync-rules.test.sh` | **Works**, 33 passed | < 1 s |

Corrections the table makes to §3.5:
- **`scripts/dotnet-verify-packages.sh` works in the sandbox**, so the "fails: certificate and release hosts aren't in a group" row is wrong for the signed restore. The proxy recorded only `api.nuget.org:443`: **the `signing` group is empty** (no certificate-revocation, OCSP or timestamp host was contacted; the signature check runs against the locally trusted roots). The `signing` row can be dropped, or kept empty; `scripts/binaries-lock.sh fetch` (the `releases` group) was not run.
- **`npm ci` does not fail on `better-sqlite3`** here: with `npm_config_ignore_scripts=true` it still installs and the native tests pass, so the "fails until the repo declares a prepare" row does not hold on this box and package set (`better-sqlite3` resolved a usable binary). The SB-11 prepare is still the right way to avoid a network fetch per run, but it is not a precondition for `npm test` working. `npm ci` needed `registry.npmjs.org:443` (10 requests).
- **The one `npm test` failure is a sandbox artefact:** `sindri/tests/real.test.ts` > `real index I/O (smoke)` > `hasBinary takes an absolute path only when it is a root-owned executable file` (line 97, `expect(hasBinary("/bin/sh")).toBe(true)`; SB-2 adds a skip-with-reason when `AW_SANDBOX=1`) expects `/bin/sh` to be owned by root; in bubblewrap's user namespace only the owner's uid is mapped, so every host file owned by root reads as `nobody` (`stat -c %U /bin/sh` prints `root` outside; inside, the root-owned parents show `nobody nogroup`). Any test that checks root ownership fails in a Step. The gate must run it host-side.
- **A restore needs `api.nuget.org` even when everything is cached:** `dotnet build -warnaserror` without network stops with `NU1900` (the NuGet audit cannot fetch vulnerability data and the warning is an error). So `packages` is needed for **every** `-warnaserror` build, not only for a new package; a ReadOnly Step cannot build.

**Hosts contacted** (learn mode): `api.nuget.org:443`, `registry.npmjs.org:443`, plus `api.anthropic.com:443` from the agent. Nothing on port 80.

**Writable list derived from the runs** (everything else could stay read-only):
- the worktree: `artifacts/` (build output; the repo's `.gitignore` covers it) and `sindri/node_modules/` (ignored). `git status` was clean after all six commands;
- the scratch `/tmp`: about **370 MiB** after build + unit lane + format + npm test (CLR debug pipes and `clr-debug-pipe-*`, `.dotnet/` bundle extraction, test temp directories, `dotnet-home`, `nuget-http`, `npm-logs`): the disk growth check has to count it, and it must not be a RAM tmpfs;
- `~/.nuget/packages` (read-mostly; written only for a package not yet in the shared cache) and `~/.npm/_cacache`;
- nothing in the tmpfs `$HOME`: no `ENOSPC` against the 256 MiB, with `DOTNET_CLI_HOME`, `NUGET_HTTP_CACHE_PATH` and `npm_config_logs_dir` pointed into the scratch;
- the per-launch hook directories of item 11 (`logs/`, `probe/`, `digests/`, `judge/`, `locks/`, `context-guard/`).

**Lesson for SB-2 and SB-4a: the forwarders must not inherit the caller's lock descriptors.** The lane runs these jobs as `flock <lock> heavy … bwrap …`. The inside `socat`s were started by the init in the background, outlived the session (no pid namespace), and **inherited the open file description of the `flock`ed lock file**: the lane's `ideas-lane-heavy.lock` stayed held for over an hour after the build finished and blocked every builder in the lane until the strays were killed. Rules for the launcher: start every long-lived helper (`socat`, the proxy, any `systemd-run` unit, tmux) with the inherited descriptors closed (`3>&- 4>&- …`, or a `closefrom`, or `flock -o`); kill the forwarders when the session ends (stop the scope, or keep the init as the parent and `kill` them in a trap); and stop everything started for a check as soon as it is answered. The spike's `init.sh` now closes fds 3 to 9 for its `socat`s; a general `closefrom` is cleaner.

### Item 11. The real hooks inside the sandbox, `--size`, transcript formats, and the 4 s self-check: **yes**

Command: `scripts/canary.sh` run inside the stack (`/run/init.sh /run/canary.sh`), with `~/.claude` read-only at its own path (credentials masked), `$AW_STATE_DIR/judge`, `locks`, `logs`, `probe`, `digests`, `context-guard` as per-launch (scratch) directories and `aw-meta` absent.

- **Three-part canary:** (a) `block-destructive.sh` with a destructive-`rm` payload exits **2**; `detect-secrets.sh` with a planted AWS key exits **2**. (b) `judge brief get task:selfcheck` prints `no brief for tool_use_id: task:selfcheck` and exits **1** (not a crash: `node` through `.nvm`, `judge/dist`, `better-sqlite3` and a fresh `decisions.sqlite` all work); a probe file in `judge/sessions/` is written and removed. (c) `aw_failopen` appends its line to the per-launch `logs/failopen.log`; `meta-outbox.sh` with a sample payload exits **0** with `aw-meta` absent. The three parts took **about 100 ms** together.
- **Every other installed hook**, run with a sample payload: every installed hook script ran and returned 0 (`send-gate` returned a `deny` decision for the sample payload, as it would in a lane); `prism-context` prints its "dashboard unreachable" line (no network; guards nothing); `meta-stop-gate`, `meta-outbox`, `meta-lane-gate` and `session-id-report` do nothing without `aw-meta` or lane variables; `done-gate` and `scope-gate` wrote `transcript-unreadable` and `field-missing` fail-open lines for the empty sample payload (so the `failopen.log` part of the canary sees real lines). **All of them together took 1.1 s.**
- **`rtk-rewrite` is live**, not degraded: `rtk` is installed in `~/.local/bin` and the hook rewrites commands such as `git push` into `rtk git push`. See the §5 finding in item 6 below: the rewrite happens before permission rules are matched.
- **`bwrap --size`:** supported on 0.9.0. `--perms 0700 --size 268435456 --tmpfs $HOME` gave a 256 MiB tmpfs home (`df` shows `256M`); no disk-backed fallback is needed on this box.
- **The whole self-check fits the 4 s host-start stage:** bwrap start plus init forwarders and their check (about 0.15 s), the hook canary (about 0.1 s) and the full hook sweep (1.1 s) total **well under 2 s** here. A `git-ref` canary on a worktree was not part of the spike; it is a few `git` calls.
- **Transcript formats for denial reading.**
  - Claude Code: `<config dir>/projects/<key>/<session-id>.jsonl`, one JSON object per line. A refused tool call is a `user` record with `message.content[].type == "tool_result"` whose text starts `Permission to use <Tool> … has been denied` (or `… denied because Claude Code is running in don't ask mode`, or `File is in a directory that is denied by your permission settings`), plus top-level `toolUseResult` ("Error: …"), `toolDenialKind`, `permissionDecision` and `sourceToolAssistantUUID`. `claude -p --output-format json` also lists the refusals in `permission_denials[]` (tool, input). A write blocked by the outer layer shows as the shell's own error text (`Read-only file system`, `No such file or directory`) in a `tool_result`, with no denial record: that is the "likely denial" path of §3.6.
  - Codex: `~/.codex/sessions/YYYY/MM/DD/rollout-<ts>-<id>.jsonl`, lines `{timestamp, type, payload}`; the useful records are `response_item/function_call_output` (`output` text: "Process exited with code N … Output: …"), `event_msg/exec_command_end` (command, exit code, stderr/aggregated output) and `event_msg/patch_apply_end` (`success`, stdout/stderr). A sandbox refusal appears as the shell error text in those; there is no separate "denied" record, so the same "likely denial" matching applies.

### Item 6 (partial, needed by SB-5a): which settings and `-c` keys are honoured
Claude Code 2.1.302 (`--settings <file>`, tested with `claude -p --output-format json` inside the sandbox): honoured: `permissions.defaultMode: "dontAsk"` (unlisted tools refused with "denied because Claude Code is running in don't ask mode"), `permissions.allow`, `permissions.deny` (`Bash(<prefix>:*)`, `Read(//abs/**)` and the whole-directory form), `permissions.ask`, `sandbox.enabled` (item 5), `disableAllHooks`. Codex 0.139.0 (`codex debug prompt-input -c …`, no model call): honoured: `sandbox_mode` (`read-only`, `workspace-write`, `danger-full-access`), `approval_policy="never"`, `sandbox_workspace_write.writable_roots` (the model-visible text lists the worktree, the roots and `/tmp` as writable: **`/tmp` is writable by default**), `sandbox_workspace_write.network_access=false` ("Network access is restricted").
**Finding that changes §5 (for the owner and the API lane):** with the owner's hooks active, `dontAsk` is **not** a refuse-by-default guard. In one run with `allow: [Read, Bash(git status:*)]` and `deny: [Bash(git push:*)]`, an unlisted `touch /tmp/not-allowed` ran, and `git push origin HEAD` ran too (it failed only on the network, `CONNECT tunnel failed, 403` from our proxy). Two causes, both checked: (1) the PreToolUse hooks (`external-write-guard`, `scope-gate`, `rtk-rewrite`, …) answer `permissionDecision: "allow"`, which bypasses `dontAsk`'s refusal of unlisted commands (a `deny` rule still wins: `deny: [Bash(touch:*)]` refused `touch`); (2) `rtk-rewrite` rewrites `git push` to `rtk git push` before the permission check, so `Bash(git push:*)` no longer matches. With `disableAllHooks: true` the same rules were all honoured. So the hook-based safety net and the allowlist interact: SB-5a's policy file must either deny by command patterns that survive the rewrite (`Bash(rtk git push:*)`, `Bash(*git push*)`), or exclude `rtk-rewrite` from the Step's hook set, and must not rely on `dontAsk` alone to refuse unlisted commands while allow-emitting hooks run. The sandbox boundary (no network to GitHub, read-only refs) held in this run regardless.

### Item 5 (partial): nested inner sandboxes
Both start inside Sindri's bwrap on this box (AppArmor restriction on): Claude Code with `sandbox.enabled: true` ran Bash with a seccomp filter (`Seccomp: 2`, `NoNewPrivs: 1`) and a read-only `$HOME`; Codex's `codex sandbox -- sh -c …` ran and refused a write ("Read-only file system"). A plain `bwrap --unshare-user --unshare-net` and `--unshare-pid` inside the outer sandbox also work. So `sandbox.inner: auto` can keep the inner sandbox on this box for both pinned versions.

### Item 7 (answered, needed by the §3.1 table): the discovery file
The C# `sindri` client verbs read **`$AW_SINDRI_HOME/control.json`** (default `$AW_STATE_DIR/sindri-app/control.json`, 0600; `ControlEndpoint {Url, Pid, Epoch, Temporal?}`; `src/Sindri/Aw.Sindri.Infrastructure/Control/FileEndpoint.cs`), and refuse a stale record by pid and epoch. `AW_SINDRI_PORT` is the serve-side port variable only. The record is replaced by temp + rename, so bind its **directory form**: a per-launch `AW_SINDRI_HOME` containing a `control.json` whose `Url` names the forwarder's port (same pid and epoch as the real one), no `auth/`. The TS `sindri` (`~/.local/bin/sindri`) uses the ledger directly and no endpoint file. *(Rev 7: inside a launch the file is the session home's `control.json`, §3.1.)*

### Item 4. `systemd-run --user --scope`, the tty, and the OOM kill: **yes**
Command: `systemd-run --user --scope --unit=<u> --slice=<s>.slice -p MemoryMax=256M -p MemorySwapMax=0 -p OOMPolicy=kill -p TasksMax=512 -- bwrap … -- python3 alloc.py` inside a tmux pane (systemd 255).
- **The tty is kept**: `isatty` is true inside (shown as `/dev/console` in the fresh `/dev`), and the interactive Claude/Codex runs of item 1 were attachable; with `SBX_SYSTEMD=1` there was no change in behaviour.
- **After an OOM kill** the shell saw exit **137**; `systemctl --user show <scope> -p ActiveState,Result` still answers (`ActiveState=failed`, `Result=oom-kill`) until `reset-failed`, and the journal says "A process of this unit has been killed by the OOM killer." So `session wrap` reads `Result` from the failed scope. **`memory.events` is readable only while the scope is alive** (the cgroup is removed at death, and `MemoryPeak` afterwards is meaningless): poll `memory.events`/`memory.peak` on the tick and keep the last sample.
- **`MemoryHigh` throttles instead of killing**: with `MemoryHigh=200M` the allocator crawled (the `high` counter climbed about 100 per second) and was not killed in 90 s; the kill came at `MemoryMax`. The 80 %-of-max `MemoryHigh` of §3.3 therefore makes a runaway session slow before it dies; say so in the `limit` copy, or set `MemoryHigh` closer to the max.
- A slice named `sindri-steps.slice` nests as `sindri.slice/sindri-steps.slice` under `user@<uid>.service` (the dash is a hierarchy); the parent-slice cap in §3.3 must be set on the right unit.

### Item 8. The nested-heavy rule with the real wrapper: **confirmed, with two corrections**
Command: the real `~/.agentic-workflow/bin-r1b/heavy` run inside the stack against a scratch `locks/` directory and fake `dotnet`/`npm`/`npx` that print the wrapper's environment.
- A bare absolute path with no flag is classified **light** (`mode=light`, no box-wide slot): the §3.3 note is right. `heavy --expensive /abs/tool` takes `heavy-job.lock` (`AW_HEAVY_SLOT` set); `--light` takes only the session slot.
- With `AW_HEAVY_LOCK_HELD=1` the wrapper `exec`s the command straight away and sets **no** resource environment (`DOTNET_PROCESSOR_COUNT`, `MSBUILDDISABLENODEREUSE`, `DOTNET_CLI_USE_MSBUILD_SERVER` all unset): rule 4 is right, the sandbox must always set them. `heavy` inside `heavy` runs straight through (no deadlock). The sandbox environment (`--clearenv`) does not carry `AW_HEAVY_LOCK_HELD`.
- **Correction 1:** the wrapper puts `$HOME/.dotnet` first on `PATH`, so `heavy dotnet build` (a bare name) resolves the **real** `dotnet`, not the shim; it is still classified expensive by its text, so nothing is bypassed, but the "bare name loops back into the shim" statement holds only for `npm` and `npx`.
- **Correction 2 (session key):** the session slot key is the nearest `claude` ancestor, else the parent; for a process orphaned to the user manager that was `1781`, the systemd `--user` pid. In a Step the scope's `claude` is an ancestor, so this only matters for Codex (already in §11).
- Stale slots: after `kill -9` of the holder the next `heavy` reclaimed the box-wide and session slots at once (`kill -0` on the recorded owner pid, which is a host pid because there is no pid namespace). A leftover `heavy-session/<pid>.lock` of a different key stays listed until reused: the sweep only has litter to remove, no lock to unwedge.
- The cap files (`heavy-slots-cap` = 3, `heavy-meta-reserve` = 0) are readable through the read-only binds.

### Item 10 (informational). A relaunch on the pre-chosen session id: **Claude yes; Codex not run**
`claude -p "remember PINEAPPLE-42" --session-id <uuid>` and then, in a second sandbox launch on the same config directory, `claude -p "what was the word?" --resume <uuid>` answered `PINEAPPLE-42` with the same session id: the conversation survives a relaunch as long as `projects/<key>/<id>.jsonl` is in the bound config directory. `codex resume` was not run (it needs a live Codex login, see items 1 and 2).

### Item 12 (passive only): the credential sources on the pinned CLIs
`CLAUDE_CONFIG_DIR` pointing at a directory other than `~/.claude` works end to end (every run in this section used one). `CODEX_HOME` pointing at a separate home is honoured by Codex 0.139.0 (`codex debug prompt-input`, `codex` TUI). Whether sessions sharing one separate login race on a refresh: Claude Code 2.1.302 carries a cross-process refresh lock and race-recovery code (the `tengu_oauth_token_refresh_lock_*` and `…_race_recovered` strings of item 2), which suggests the shared login is safe for Claude; Codex was not examined. **Provisional, same as item 2; the definitive check is steps 5 and 6 of the owner procedure in item 2.**

### Item 5 and item 6: what is still open
Item 5 is answered for both pinned CLIs on this box (above). Item 6 is answered for the keys in the "partial" paragraph above; still open: `AllowedTools`-style narrowing for Codex, whether Codex tolerates an outer-layer-only write to the run directory under `sandbox_mode="read-only"` (needs a live Codex turn, owner step with the throwaway login), and Cursor (reserved).
