# Meta — `aw-meta`

`aw-meta` is the helper CLI the `/meta` skill drives a **meta orchestrator** through: the session that gates and merges
a team's PRs, relays between lane sessions and the owner, and keeps the box healthy. Its job is to make that role
impossible to fail by forgetting. Every inbound lane message is recorded and owes a reply, the Stop hook won't let the
session idle while one is owed, and the lane map, merge gate, owner queue, health check and running log are commands,
not habits.

It is a portable harness for any project. Everything team-specific (lanes, repo, gate scopes, merge order, known
flakes, kickoff prompts, log file) lives in a team config. It runs before the C# switch as an opt-in standalone
NativeAOT CLI (`./setup.sh --with-meta`; the meta itself is Claude Code only, lanes can run other agent CLIs and take part in its obligations, see [Launcher](#launcher) and [Non-Claude lanes](#non-claude-lanes); Linux and macOS, see [OS support](#os-support)) that keeps its state under `$AW_STATE_DIR/meta/` (per team: `teams/<team>/`, see [Named teams](#named-teams)) and
appends to the configured running log. Its gate and merge commands act on the team's checkout and PRs: `git fetch`, a
gate worktree registered in the repo, the configured gate steps, `gh pr merge`, and the removal of finished worktrees (`git worktree remove`, never forced; see "Worktree cleanup"). Sindri absorbs it later (see the
last section).

Source: `src/Meta` (full shape: `Core`, `Features`, `Infrastructure`, `Host`; NativeAOT binary `aw-meta`).
Tests: `tests/Aw.Meta.Tests` (rules, slices, contract bindings) and `tests/Aw.Meta.Testing` (fakes and contract bases).
AOT smoke: `bash src/Meta/aot-smoke/run.sh`. Shell side: `config/hooks/meta-stop-gate.sh`, `config/hooks/meta-outbox.sh` (Claude), `config/hooks/meta-lane-gate.sh`, `config/hooks/session-id-report.sh` (Codex, Cursor),
`scripts/meta-launch.sh`, `scripts/meta-lane.sh`, `scripts/install-meta.sh`; skill `skills/meta/SKILL.md`.

## Pieces

| Piece | What it does |
|-------|--------------|
| `aw-meta` | State in `$AW_STATE_DIR/meta/teams/<team>/meta.db` (or the legacy flat `$AW_STATE_DIR/meta/meta.db`; SQLite, WAL): messages, sends, lane map versions, gates, merges, owner questions |
| Stop hook `aw:meta-stop-gate` | Runs `aw-meta stop-check` with the session id and transcript path; exit 1 from it blocks the stop (hook exit 2, the list on stderr) |
| PostToolUse hook `aw:meta-outbox` (matcher `SendMessage`) | Pipes the hook payload to `aw-meta outbox-record`, which records the send and closes what it settles |
| `aw-meta-launch` (`scripts/meta-launch.sh`) | By default one Warp window, a pane per lane, each attached to tmux session `<team>-<lane>` (on the team's own socket, `tmux -L <team>`) running `scripts/meta-lane.sh`; `launcher.json` can pick another viewer and other lane agents ([Launcher](#launcher)) |
| `scripts/meta-lane.sh` | Runs the lane's agent (Claude Code unless `launcher.json` says otherwise) as `claude -n <team>-<lane>`, resumed by its recorded session id, or fresh with `<prompts>/<lane>.md` |

## Why the transcript

No Claude Code hook sees an inbound cross-session message, so `stop-check` reads them from the meta session's transcript
(Claude Code 2.1.300). A message delivered while the session is idle is a `type:"user"` record with
`origin.kind == "peer"` (`from`, `name`, `msg_id`, `body`). A message that arrives mid-turn is a `type:"attachment"`
record with `attachment.type == "queued_command"` and the same `attachment.origin`. Both are recorded once, keyed on
`msg_id` (else the attachment's `delivery_id`). `stop-check` reads from a stored byte offset, so each stop costs only
the new lines.

Sends are seen by the PostToolUse hook: `tool_input.to`, `tool_input.message`, and `msg_id` in the tool response.

## Obligations

An obligation is something the meta owes. They are derived on each `stop-check` from the recorded messages, sends,
merges, lane map versions and owner answers, not stored as state.

| Kind | Opened by | Closed by |
|------|-----------|-----------|
| `reply` | every inbound lane message | a later send to that peer; `inbox-dismiss <id> --reason`; `inbox-defer <id> --until` (reopens when due) |
| `broadcast-lanes` | a new lane map version | a send of the map text to every mapped lane |
| `announce-main` | `merge-run` | a send containing the new main sha to every mapped lane |
| `relay-owner` | `owner-answer` | a send of the `OWNER ANSWER` text to the asking lane |
| `rearm-watch` | a started watch whose heartbeat is older than two intervals | `watch` running again |

`stop-check` exits 0 silently for any session that isn't the registered meta session (`role-set`), so the hook is safe
to install globally. Loop guard: a stop-check without `stop_hook_active` starts a new turn (it blocks and resets the count); each
continuation with `stop_hook_active` and the same open set counts one block, and after `AW_META_STOP_MAX` (default 3) of
them the next continuation is let through with a one-line warning instead of looping the session. Every turn gets the
full allowance again.

`/clear` in the meta pane gives it a new session id under the same `-n` name. `stop-check` and `outbox-record` treat a
session as the meta when its id is the registered one, or when the live session with that id in the sessions directory
(`AW_CLAUDE_SESSIONS_DIR`, `<pid>.json`) carries the registered name; the new id is then registered (as `role-set` would)
and the hook carries on.

## The ready message

A lane queues a pull request for merge by sending a message whose **first line** is `ready #<n> <sha>`: the pull request number and a commit of 7 to 40 hex digits (the head the lane means). "Ready" and the hex digits are case-insensitive, and the sha may be wrapped in a pair of backticks (both or neither). The words "not ready" and "follows" disqualify the message only when they are on that first line. aw-meta checks the shape, not the head: it has no head lookup where the queue is read, and `merge-check` already refuses a pull request whose head moved after the gate, so a stale sha can't merge. Nothing else queues a pull request: a status line that merely mentions a later ready ("...then ready #250 <sha>", "ready #264 follows once the re-review is clean"), a ready without a sha, or any message containing `not ready` or `follows` is an ordinary request. This changes behaviour only for false positives and for ready messages that omit the sha; lanes now add it (`ready #269 079c2541` and details below). The rule applies to messages synced after the upgrade; entries queued under the old rule stay until a merge, the watch seeing the pull request end, or `queue-drop` removes them. `queue-drop` takes the same view as `status`: a pull request with a recorded merge, or one in `watch.ended-prs`, has no entry to drop (`AW-META-018`, exit 2). Tell every lane the format in its kickoff prompt (`prompts/<lane>.md`): `ready #N <sha>` on the first line, details below; a bare `ready #N` is ignored.

## Commands

Every command takes `--json`. Exit 0 ok, 1 attention (read the output and act), 2 usage or tool error; an error is one
stderr line `AW-META-NNN: <reason>`. Every command but `team-init`, `team-list`, `team-migrate`, `role-*`, `health`, `stop-check`, `outbox-record` and the lane-side `lane-inbox`, `lane-stop-check` and `lane-reply` needs
the team config (`AW-META-002` otherwise); the hook commands must let any session through when no team is set up. Those that need no config still need a *selected* team when several exist (`role-*`, `health`: `AW-META-019`), because they read or write the team's `meta.db`; only `team-list` and `team-migrate` work with none, and the hooks pass through.
`stop-check` prints what is owed on stdout (the hook moves it to stderr for Claude Code).

| Command | What it does |
|---------|--------------|
| `team-init --team <t> --repo <abs dir> --lanes a,b [--meta-lane meta] [--log <file>] [--force]` | Writes a starter team config to `teams/<t>/team.json` (`--force` to overwrite that team's; no other team is touched). A name that is the legacy config's own team resolves to the flat layout, as before |
| `team-show` | Prints the team config |
| `team-list` | Every team: name, layout (`named` or `legacy`), repo, lanes, the registered meta session and whether it runs; `*` marks the selected one. Works with no team selected |
| `team-migrate [--dry-run]` | Opt-in, never automatic: moves the legacy flat layout (everything directly under `meta/` but `teams/` and `gates/`) into `teams/<its team name>/`, putting entries back if a move fails. Refuses (`AW-META-020`) while the team's meta or a lane session runs; checkpoints the WAL, moves `meta.db*` first and `team.json` last. `gates/` stays (gate trees are registered with git by absolute path), so sweep it with `worktree-sweep` first: afterwards the sweep roots are `teams/<team>/gates`. Exit 2 with `AW-META-002` when there is no legacy config, `AW-META-020` when the team name is unusable as a directory or `teams/<name>` exists. Stop the team's sessions first (meta and lanes) |
| `role-set --session <id> [--name <n>]` / `role-show` | Registers the meta session (the launcher does this for the meta pane) |
| `inbox-sync --session <id> --transcript <path>` | Records new peer messages from the transcript |
| `inbox [--all]` | Open reply obligations (or every message) |
| `inbox-dismiss <id> --reason <r>` / `inbox-defer <id> --reason <r> --until <iso8601\|+30m>` | Settles a reply without sending one |
| `outbox-record` | Stdin: the PostToolUse payload. Records a send and closes what it settles |
| `lane-send --lane <l> (--text <t> \| --file <f>)` | The meta's send to a **non-Claude** lane (Codex, Cursor, an added agent): stores the message in the lane's inbox, records it as a send to `<team>-<lane>` (which closes the meta's reply obligation as `SendMessage` does), and wakes the lane when all three guards hold (see "Non-Claude lanes"). Exit 0 sent; 1 sent, but the lane is launch-only or its hooks haven't run since launch; 2 unknown lane (`AW-META-016`), a Claude lane (`AW-META-017`: use `SendMessage`) or usage |
| `lane-inbox` | A lane's hook call (lane from `AW_META_TEAM` and `AW_META_LANE`): prints the meta's messages the lane hasn't seen (marks them delivered; they stay owed a reply) and records the heartbeat. Exit 0 (silent when empty or when no team is set up); 2 when the lane variables are unset (`AW-META-014`) |
| `lane-stop-check [--stop-hook-active]` | A lane's stop hook call: exit 1 while the lane owes the meta a reply (prints the owed messages and how to reply), with the loop guard of `stop-check` (`AW_META_STOP_MAX`, default 3); exit 0 otherwise, or with one warning line once the cap lets the stop through. Records the heartbeat as a stop. Exit 2 when the lane variables are unset (`AW-META-014`) |
| `lane-reply (--text <t> \| --file <f> \| --stdin)` | A lane's answer to the meta: records an inbound message `lane:<lane>:<n>` from `<team>-<lane>` (classified like any inbound message), clears what the lane owed, and logs team, lane, provider and session. Exit 0 (silent when no team is set up, or the reply is blank); 2 when the variables are unset (`AW-META-014`) or `lane-sessions/<lane>` has no record (`AW-META-015`) |
| `stop-check --session <id> --transcript <path> [--hook-active]` | Syncs, refreshes the lane map, prints every open obligation with the command that settles it; exit 1 if any |
| `lanes-refresh` / `lanes [--announce]` | Rebuilds the lane map from live sessions named `<team>-<lane>` (`~/.claude/sessions`), then lane announcements ("API lane: …"); `lanes` refreshes first; `--announce` prints the `LANE MAP v<n>:` text |
| `gate-run <pr> [scope...]` | Runs the merge gate in a gate tree (PR head merged onto `origin/main`); scopes come from the changed paths and the team config. Every step runs with `AW_HEAVY_PRIORITY=meta` (the heavy-job wrapper lets such a job use any box-wide slot), and before the run it computes the heavy reservation and keeps the heavy reservation (computed before the steps run) and prints it after the steps, just before `RESULT`, as `heavy reservation: N of C slots (queue depth D) - set it with meta-gate-reserve A` (see "Resources and adaptation") |
| `queue-gate [pr...] [--max K]` | Gates the first K ready pull requests in merge-queue order (K defaults to the team's `heavy.cap`; named pull requests are taken in the order given) as a train: see "Merge queue". Prints each gate as `gate-run` does, then the `TRAIN` table (position, PR, base, RESULT) in train order; `DROPPED #N: conflicts with #M at <sha>` lines name the pull requests left out. Exit 0 when every pull request in the train passes, 1 otherwise |
| `gate-rerun <pr> --step <s> --test <filter>` | Reruns one red test alone and on plain main (the main tree is built first: `--no-build` is dropped there); records `flake` or `real` for that test. A filter that matches no test on the gate tree is an error (AW-META-001), whatever `--ignore-exit-code` the step carries; on main it means the test is not there (a test the PR adds), so a test that fails alone is `real` |
| `merge-check <pr>` | The merge preconditions: gate green (or every failing test of every red step, read from the step log's `failed <name>` lines, or from the bash runner's `FAIL  par|ser  <suite>  ...  exit N` lines (suite paths, deduplicated), was rerun and classified a flake; a log that can't be read, or whose summary `failed: N` differs from the number of `failed <name>` lines (a crash or timeout names no test), refuses), the gate ran every scope its diff needs (every gate run of the PR at the same head and the same main counts: a scope is judged by its newest such run, so `gate-run <pr> <scope>` after a run with one flaky step completes it; a scope whose newest run failed stays red even if an earlier run passed it; runs at another head or main never count; the output names the run each scope came from, e.g. `repo (run 1), dotnet (run 2)`), head unmoved since the gate (a docs-only delta needs only the `always` scopes), main unmoved or still giving the tested tree (an unstacked gate on the same main is valid by the fast path; a stacked gate, or one whose main moved, is valid only if `git merge-tree --write-tree <origin/main> <gated head>` equals the tree the gate recorded, `gates.tree`, with the pulls merged under it kept in `gates.stack`; a gate recorded before migration 0004 has no tree and keeps the same-main rule), merge order, not a draft |
| `merge-run <pr>` | `merge-check`, then `gh pr merge <pr> --merge`, logs it and opens `announce-main` with the "main moved to <sha>" text. The merge is recorded as soon as gh succeeds: main's sha comes from a fetch, else from `gh pr view --json mergeCommit`, else it stays unknown ("main moved (#N merged)"). Running it again on a pull request that is already merged with no merge recorded records it and opens `announce-main`. Once the merge is recorded it computes the heavy reservation again from the shorter queue (0 when the queue is empty), even when the log line could not be written |
| `owner [--all]` / `owner-answer <q> --answer <t>` / `owner-ask --text <t>` | The owner question queue (inbound messages starting `OWNER QUESTION`); an answer is dated in the decision log and owed back to the lane |
| `health [--kill-orphans]` | Memory by group, orphaned Temporal dev servers, duplicate MCP server copies, lanes with no live session, heavy-lock holders, the last computed heavy reservation (with a `[stale: ...]` note when nobody recomputed it for 30 minutes), and the `builders-per-lane` advice (see "Resources and adaptation"); `--kill-orphans` kills only orphans by the rule below, by pid |
| `watch [--interval <s>] [--for <dur>]` | Prints one line per settled change (PR opened, merged, ready, head moved, mergeable after two equal polls and never `UNKNOWN`, main moved, `DISK LOW` / `DISK OK`, `MEMORY LOW` / `MEMORY OK`, each after two polls agree) and keeps a heartbeat; every poll also recomputes the heavy reservation |
| `worktree-list [--no-fetch]` | Every worktree of the team repo (`git worktree list --porcelain`) with its branch, head, pull request (number and state) and verdict `removable` or `kept` with every reason; changes nothing (it fetches origin first unless `--no-fetch`). Exit 0 |
| `worktree-sweep [--dry-run] [--pr <n>]` | Removes the removable worktrees (see "Worktree cleanup"), all or just pull request n's (its head branch and its gate trees); prints each removed and each kept one with its reasons. `--dry-run` removes nothing. Exit 0, 1 when git refused a removal, 2 when nothing could be judged (git, gh or the process scan failed: nothing is removed) |
| `version` | The commit the binary was built from, its build time, the OS and the runtime identifier (`aw-meta commit <commit>, built <UTC time>, <OS> <rid>`; `--json`: `commit`, `builtAt`, `os`, `rid`). `scripts/install-meta.sh` stamps them at publish (`-p:SourceRevisionId`, `-p:AwBuildTime`); a build that was not stamped reads `unknown`. Needs no team. Exit 0 |
| `log-append --kind merge\|decision\|lanes\|incident\|note --text <t>` | Appends a dated line to the running log |
| `status` | Obligations, merge queue (ready pull requests; one leaves it when any merge is recorded for it, or when the watch saw it merged or closed on GitHub, kept under `watch.ended-prs`), owner queue, lanes, and one line each for the heavy reservation and the builders advice |
| `queue-drop <pr> --reason <text>` | Removes the pull request's merge-queue entry and appends a `NOTE` line (`queue-drop #N: <reason>`) to the running log. The inbox message stays; the drop is kept with its time under `queue.dropped`, so only a ready message sent after it queues the pull request again. Exit 0 dropped; 2 when the pull request has no queue entry (`AW-META-018`), the reason is blank or usage |

The orphan rule: the command line has `temporal` and `server start-dev`, its `--db-filename` is under `/tmp/aw-sindri-`,
its parent is `systemd --user` or init, and it is older than five minutes.

## Merge queue

`queue-gate` gates a train of ready pull requests in one round. The queue is the pull requests lanes said are ready, lowest number first, reordered only by the team's merge-order rules (a pull request waits behind the one the rules say merges before it).

- **Stacking.** Position 1 gates on `origin/main`. Position k gates in its own gate tree (`gates/<pr>/tree`) on the same pinned `origin/main` with the heads of positions 1..k-1 merged in order, then its own head. Nothing is pushed. The record keeps the stack (the pull request and head of every position under it) and the tested tree; `merge-check` accepts a stacked gate when the merge result of the current `origin/main` with the gated head has the tree the gate tested, which holds once the pull requests under it are merged, in any merge style.
- **Conflicts.** A head that conflicts with main or with the heads ahead of it is dropped from the round: `DROPPED #N: conflicts with #M at <sha>`. The later pull requests stack without it.
- **One slot pool.** Every step of every gate takes its heavy slots from one pool of `gate.parallelSteps` (default `heavy.cap`) slots; a free slot goes to the lowest train position, then the earliest step. Steps that are not heavy start as soon as they are free.
- **Failure.** When a step of position j fails, the gates above j that are still running are cancelled: each step's own process tree is killed by the PIDs aw-meta started (never by name). A cancelled gate is not recorded and its pull request stays queued for the next round. Gates above j that had already finished keep their record, but their stack holds j, which is not merged, so `merge-check` refuses them until a new train is run.
- **Status.** The train is kept in the key-value store (`train.state`) and `status` prints it: position, PR, base, state (`GATING`, `PASS`, `FAIL`, `CANCELLED`, `DROPPED`, `SKIPPED`, `ERROR`). Drafts are `SKIPPED`.
- `gate-rerun <pr>` works on a train gate: it reruns in the tree the train built for that pull request.

## Resources and adaptation

The meta watches memory and disk and adapts, so a team never has to be told by hand. All thresholds live in `team.json`
(`resources`, `builders`, `heavy`) and have defaults; nothing is specific to one repo.

**Events** (`watch`, one line when the state flips, never repeated): `DISK LOW` / `DISK OK` (state-disk free space against
`resources.diskFloorGb`), `MEMORY LOW` / `MEMORY OK` (available memory (`MemAvailable` from `/proc/meminfo` on Linux, vm_stat on macOS) against `resources.memFloorGb`, or swap
used at or above `resources.swapHighPct`). A flip is reported only after two consecutive polls agree on it (the rule `mergeable`
already follows), so a host sitting at its floor does not flap LOW, OK, LOW and make the meta message every lane each time. The
last reported state is kept in the team's store (`watch.resources`), so a re-armed watch does not announce a standing LOW again.
`health` judges memory by the same rule (`ResourceRules.MemoryLow`), so the two never disagree. How each OS reads these figures is under [OS support](#os-support).

**Gate priority and the reservation.** The heavy-job wrapper (`AW_HEAVY` or `$AW_STATE_DIR/bin-r1b/heavy`) shares a
box-wide number of slots between every expensive job. aw-meta stays inside its own state directory: it never writes the
wrapper's files (`heavy-meta-reserve`) and never reads its cap file. It speaks to the wrapper in one way and advises about
the other:

- `gate-run` and `gate-rerun` export `AW_HEAVY_PRIORITY=meta` to every command they start. A job with that priority may take any slot.
- aw-meta **computes** the reservation from the **merge-queue depth**: the pull requests lanes said are `ready` that are not yet
  merged, ended or dropped (what `status` lists as the merge queue), sized against the team's `heavy.cap` (default 3; keep it equal
  to the wrapper's own cap). The tier with the highest `minDepth` the depth reaches decides; with the defaults a depth of 3 or 4
  reserves 1 slot, 5 or 6 reserves 2, 7 or more reserves all of them (`"all"` is `heavy.cap`), and 0 to 2 reserves none. It
  keeps the result, the depth behind it and the time in `heavy-reserve` in the team's state directory (`meta/teams/<team>/`, or
  `meta/` for a legacy team), and recomputes it wherever the queue changes: before each `gate-run`, after each `merge-run` (even
  when its log line fails), after each `queue-drop`, on every `watch` poll (a ready message, a drop, or a pull request seen
  merged or closed on GitHub) and on each `status`. Each change of the reservation is one `NOTE` line in the running log
  (`heavy reservation 0 -> 1 of 3 slots (merge queue depth 3); set it with meta-gate-reserve 3`; the argument is explained below). Computing it is advisory: if the
  file can't be written the gate or merge still runs. `heavy.reserve: false` makes aw-meta compute nothing.
- **Applying it is the meta's step**: `gate-run`, `status` and `health` print `heavy reservation: N of C slots (queue depth D) - set
  it with meta-gate-reserve A`, and the meta runs `meta-gate-reserve A` (the installed bash script, the only writer of the
  wrapper's file) before each gate. `A` is **not the queue depth**: the script has its own fixed tiers (depth 0-2 reserves 0, 3-4
  reserves 1, 5-6 reserves 2, 7 or more reserves its cap), which differ from a team's custom `heavy.reserve` tiers or `heavy.cap`.
  aw-meta prints the argument that makes the script keep the slots it computed: 0 slots prints 0, 1 prints 3, 2 prints 5, and all
  of them (N = C) prints 7. A count between 2 and the cap has no argument; it prints 5 (the script keeps 2, never more than computed).
  `gate-run` computes before its steps and prints the line after them, so to apply the reservation before a gate read it from
  `status` (which recomputes it). The wrapper then gives every expensive job *without* the priority only the
  slots above the reservation, and does not count a wait for the reservation toward its timeout. A line ending `[stale: computed
  HH:MMZ; ...]` means nothing recomputed it for 30 minutes (no watch is running and no gate, merge, drop or status has run): run
  `status` and apply the new depth, or `meta-gate-reserve --clear` when the queue is empty.
- The reservation assumes every gate runs through `aw-meta gate-run`. A gate run by hand carries no priority and would wait behind
  the reservation it is meant to share; nothing running is ever pre-empted.

**Builders per lane.** `health` (and `status`, one line) advises how many builder subagents each lane should run now, from
`builders.maxPerLane`: it is reduced while free disk is under `resources.diskFloorGb + lanes x builders x builders.worktreeGb`
(lanes are the configured lanes but the meta lane), and is 1 while memory is low (the watch's `MEMORY LOW` rule: under `resources.memFloorGb` available, or swap at `resources.swapHighPct`); it is never
below 1. The line reads `builders: 4 per lane (disk 132 GB free, mem 12 GB available)`, or `builders: 2 per lane (disk below
floor+headroom)` / `builders: 1 per lane (memory low)` when something cuts it; `--json` carries `builders` (`perLane`,
`maxPerLane`, `diskFreeGb`, `memAvailableGb`, `limit`) and `heavy` (`cap`, `reserve`, `depth`, `computedAt`, `stale`; absent when no reservation was computed or `heavy.reserve` is false). Without a team config the defaults and one lane apply, and no reservation.

## OS support

aw-meta runs on **Linux** and **macOS** (`scripts/install-meta.sh`, `./setup.sh --with-meta`; the host RID is whatever
`dotnet --info` reports: `linux-x64`, `linux-arm64`, `osx-arm64` or `osx-x64`; NativeAOT needs the platform's C toolchain, Xcode's
command line tools on macOS). On any other OS the installer exits 1 and says so; `setup.sh --with-meta` finishes the rest of setup, then exits non-zero with that message. Nothing is
skipped quietly, so a reinstall never leaves an old binary in place without a signal: the installer prints the installed
version, and the launcher warns when the binary is older than the Meta sources it runs from (below).

What reads the host differs; everything else (the SQLite stores, the team config, the CLI, gate and merge, the launcher) is the same
code. One internal function (`AddHostAdapters`, over a `HostPlatform`) picks the adapters for the process table, host resources, process liveness and process locations when the host composes (`AddMetaInfrastructure`); on an OS that is neither Linux nor macOS those four answer `AW-META-010` "unavailable on <OS>", and `lsof` and the tools below are never run. Nothing sniffs `/proc` at run time to pick one.

| Need | Linux | macOS |
|------|-------|-------|
| Memory total and available (`health`, `status`, `watch`'s `MEMORY LOW`) | `/proc/meminfo` (`MemAvailable`) | `sysctl -n hw.memsize`; available is the free, inactive and speculative pages of `vm_stat` times its page size |
| Swap used and total | `/proc/meminfo` | `sysctl -n vm.swapusage` |
| The process table (orphans, duplicate MCP servers, process groups, `health`'s counts) | `/proc/<pid>/stat` and `cmdline`; age from the process start time | `ps -axww -o pid=,ppid=,etime=,rss=,stat=,args=`; age from `etime`, so no time-zone or boot-time arithmetic. Zombies are left out on both |
| Is this pid running, and is it the process the file names (live sessions, heavy-lock holders; `IProcessLiveness`) | `/proc/<pid>/stat` start time against `/proc/uptime` | `ps -o etime= -p <pid>`: the start time is now (read before `ps` runs) minus the elapsed time. Many pids at once (`IProcessLiveness.Running`: the session list, the heavy-lock holders) go through one `ps -axo pid=,etime=` |
| What a process holds (worktree cleanup's idle test) | `/proc/<pid>/cwd` and `fd` | `lsof -w -n -F pcnR` (cwd, open files, executables, parent pid), run like the other tools. The scanning process and its ancestors (the shell that ran it) are marked as invokers, from lsof's parent field here and the `/proc` stat there, so a wrapper shell is never counted as a running gate |
| Termination (`health`'s orphan kill) | `kill -TERM` | `kill -TERM` |
| Disk free space, the heavy-job lock directories | the same on both | the same on both |

**A reused pid is not the session.** A session record (`~/.claude/sessions/<pid>.json`) or a heavy-lock owner file names a pid, and the OS hands a
pid to a new process once the old one exits. Liveness therefore also compares the process's start time with the file's last write: a
process that started more than 2 seconds after the file was written (`ProcessStart.Tolerance`; `ps` and `/proc` count whole seconds) is
another process, and the session or lock is not live. The check is as good as the file's modified time: a record that is rewritten
later by something other than its own process would hide a reuse, and a start time that cannot be read (a failed `ps`) makes that
pid unknown, not dead: the session or lock is kept (one pid the host can't be asked about never fails the whole listing), and on Linux a `/proc/uptime` that is unreadable or earlier than the process's start (inconsistent clocks) is `AW-META-010`, never "not running" or a reused pid.

A macOS tool that cannot run, times out or prints something unreadable makes the figure unavailable: the command fails with
`AW-META-010` and a reason that starts "unavailable on macOS: <tool> ..." (for example `/usr/sbin/sysctl -n vm.swapusage exited 1: ...`; at most 300 characters of the tool's stderr are quoted). A number too large for a 64-bit integer in a tool's output is the same error, never an exception. Each tool runs by the absolute path macOS ships it at (`/usr/sbin/sysctl`, `/usr/bin/vm_stat`, `/bin/ps`, `/bin/kill`, `/usr/sbin/lsof`) so a PATH that something else controls can't substitute another program; a tool missing at its path fails to run, which is the `AW-META-010` above, never a lookup by bare name. It is
never replaced by a made-up value, and `health` does not guess from the rest. The tools' output is read up to 16 MiB (`ps` lists far
more than the command runner's default 64 KB tail; 16 MiB is about thirty times a busy machine's list, and past it the start is dropped). Symlinked paths: git reports a worktree's physical path (`/private/var/...`),
while a team config may name it through a symlink (`/var/...`); put worktree roots in `worktrees.roots` by their physical path on
macOS.

**The launcher on macOS.** `scripts/meta-launch.sh`, `meta-lane.sh` and `launcher-lib.sh` run on macOS's bash 3.2 and BSD tools: no `flock`,
`exec {fd}>`, `BASHPID`, `mapfile`, associative arrays or `readlink -f`. The team start lock is the symlink `lane-sessions/.reported.lock`
(`ln -s` is atomic and records the owner pid in one step): a lock whose owner is dead is taken over, a live holder is waited for. macOS
`uuidgen` ids are lowercased. `scripts/tests/meta-launch.test.sh` runs on both OSes (it builds a one-line C `sleep` with `cc` on macOS: a
copy of the system `sleep` is killed there). It was run on macOS for this section (a throwaway team on a private tmux server, one Claude lane and
two lanes whose agent reports its session id, started one at a time); it has not run on a real Warp window here, and `aw-meta-launch` on
macOS opens Warp with `open warp://launch/<team>%20team`. `scripts/tests/meta-version.test.sh` covers the staleness check on both.

**Version and staleness.** `aw-meta version` is how a stale install shows. `scripts/install-meta.sh` stamps the publish with the tree's
`git rev-parse HEAD` (`<HEAD>-dirty` when a file under a build path has uncommitted or untracked changes) and a UTC time and prints `aw-meta commit <commit>, built ..., <os> <rid>` when it finishes (`... predates 'aw-meta
version'` for an older binary). `scripts/meta-launch.sh` runs `aw-meta version --json` at each launch and prints one stderr line, never
failing the launch, when the binary is older than the sources of this checkout: the last commit that touched a build path (`scripts/meta-build-paths.txt`, the one list: `src/Meta`, the Kernel, Redaction and Analyzers projects and the root build files; the installer, the staleness check and the NativeAOT smoke all read it) is not an ancestor of, or
equal to, the binary's commit (`git merge-base --is-ancestor`); an unstamped binary, one with no `version` command, and a commit this
checkout lacks, and a `-dirty` stamp warn too. After copying the binary the installer runs `aw-meta version --json` and fails (exit 1, "install didn't take") unless its commit is the one stamped, and fails when the publish produced no `aw-meta`. Docs-only commits after the binary do not. `AW_META_NO_STALE_CHECK=1` turns it off; `AW_META_BIN` names the
binary (default `$AW_STATE_DIR/bin/aw-meta`).

## Worktree cleanup

Worktrees pile up (a gate tree per pull request, a worktree per lane or builder). `aw-meta` removes the finished ones
itself, on **triggers**, never on a timer: work builds up over time, so how long a worktree has sat there is no reason to
remove it, and no rule reads a commit date or an age. The one time rule is a change trigger, not an age timer (rule 7).

**Triggers.** Each one sweeps only the worktrees that belong to the pull requests it concerns (their head branches and
their gate trees `gates/<pr>/tree` and `gates/<pr>/main-tree`), then logs one `NOTE` line to the running log ("worktrees
merge-run #7: removed 2 (...); kept 1 (path: reasons)") and keeps it as `worktrees.last`.

| Trigger | When |
|---------|------|
| `merge-run <pr>` | after a successful merge (also when it records a merge that already happened); the line is part of its output. A sweep that cannot run never fails the merge |
| the watch | when a poll sees a pull request merged or closed that is not yet in `watch.ended-prs`; one `WORKTREES watch #n: ...` line. A sweep that could not run (GitHub or git unreachable) leaves the pull request in `worktrees.pending-prs` and the next poll tries again |
| `worktree-sweep` | by hand, e.g. when a lane reports a builder done; also the way to retry a worktree that was kept for a reason that has since gone (a process exited, a commit was pushed) |

With `worktrees.enabled: false` the two automatic triggers only report ("would remove"); `worktree-sweep` still removes.

**The rules.** A worktree is `removable` only when all of these hold; any other is `kept` with every failing reason:

1. Finished: the work is proven finished by an ended pull request, not by reachability (a branch just cut from `origin/main` is reachable too: the 2026-10-16 incident removed a live worktree on such a branch, with no commits and no pull request yet). A worktree on a branch is removable only when that branch's pull request is MERGED (its HEAD is also reachable from `origin/main`; a fetch precedes every sweep) or CLOSED (and nothing is unpushed). A branch with no pull request is always kept ("no pull request: work not finished"; the main checkout says "main checkout (never swept)" there instead), and one whose head is already in `origin/main` with no ended pull request is kept as a "fresh branch, no commits". A branch whose pull request is still open is kept even when its head is merged. A detached worktree that is not a gate tree is always kept ("detached, not a gate tree").
2. Clean: `git status --porcelain --untracked-files=all` is empty, ignoring only untracked files under a directory named like a build output (`worktrees.buildOutputs`, default `bin`, `obj`, `artifacts`, `node_modules`, `dist`). Any other untracked file, and any change to a tracked file, keeps it.
3. Nothing unpushed: every commit of its HEAD is reachable from a remote branch (`git rev-list HEAD --not --remotes` is empty).
4. Idle: no live process has its working directory or an open file inside it (`/proc/<pid>/cwd` and `fd` on Linux, `lsof` where there is no `/proc`). This covers a lane or builder still sitting in it. A scan that cannot run fails the sweep: nothing is removed.
5. Not protected: not the main worktree, not the team's shared checkout (`repo`), not locked (`git worktree lock`), its directory not missing.
6. Under an allowed root: `worktrees.roots` (default `~/repos/aw-wt`, `<repo>/.claude/worktrees`), plus the selected team's gate directory (`$AW_STATE_DIR/meta/teams/<team>/gates`, or the flat `meta/gates` on the legacy layout) always. After `team-migrate` the old flat `meta/gates` is no longer a root, so sweep it first (`worktree-sweep` with the legacy layout) or remove those throwaway gate trees by hand. The other roots (`worktrees.roots`) default to the same directories for every team: teams on one repo share them, so a team's sweep may remove another team's finished worktrees (rules 1 to 5 still apply); set `worktrees.roots` per team to keep them apart. A path outside every root, a root itself, or a sibling that shares a root's name prefix is kept.

7. Not changed since the last sweep: the directory, or any file in its top two levels (build output and `.git` left out), has a modification time later than the start of the previous removing sweep (`worktrees.sweep-start` in aw-meta state; a sweep records its start after it removes, and `--dry-run` and `worktree-list` record nothing). This is a change trigger, not an age timer: it protects only trees that changed after the previous sweep, so a tree untouched since then is judged by rules 1 to 6. With no recorded sweep it has nothing to compare with. A tree whose files can't be read is kept. Gate trees are exempt (they are aw-meta's own and rule 1 for them already needs an ended pull request).

A **gate tree** (`gates/<pr>/tree`, `gates/<pr>/main-tree`) is aw-meta's own throwaway checkout, so rule 1 reads: its pull request is merged or closed (looked up on GitHub; unknown or open keeps it), and its HEAD is reachable from `origin/main` or is the merge commit the gate made itself (committer `aw-meta@localhost`); rule 3 does not apply. It is also kept while a `gate-run`, `gate-rerun` or `merge-run` of that pull request runs (found by command line, leaving out `aw-meta` itself and its parent shells).

**How it removes.** One worktree at a time, judged again just before its removal (a process may have started since), with
`git worktree remove <path>`: never forced and never a recursive delete, so git itself refuses a worktree that is not clean
(an untracked `obj/` that is not in `.gitignore`, for one); that refusal keeps the worktree and is reported (`git refused to
remove it: ...`, exit 1 for an explicit sweep). `git worktree prune` runs after any removal. **Branches are never deleted.**
A worktree git cannot read is kept with the reason.

## Named teams

Every team has its own config and state, so several meta sessions (one per team) run at once and never see each
other's messages, obligations, role, gates or lane sessions. A team name is `[a-z0-9][a-z0-9-]*` (it is a directory
name, a tmux socket name and the prefix of the session names).

**Selecting a team**, for every command: the global `--team <name>` option (it wins), else `AW_META_TEAM`. The
launcher exports `AW_META_TEAM` and `AW_META_DIR` (the team's state directory) into every lane it starts, the meta pane
included, so lane commands, the lane hooks and the Claude Stop and outbox hooks need nothing more; the two Claude hooks
also fall back to the tmux socket name (the launcher runs the team on `tmux -L <team>`) when the variable is unset,
and only for a team that exists on disk. `aw-meta-launch --team <name>` takes the same option.

**Resolution** (`TeamResolver`, mirrored by `scripts/meta-team-lib.sh`):

| Selected | On disk | Acts on |
|----------|---------|---------|
| `X` | `teams/X/` | `teams/X` |
| `X` | no `teams/X`, legacy `team.json` whose `team` is `X` | the legacy flat layout |
| `X` | neither | `teams/X` (`team-init` creates it; others report `AW-META-002`) |
| none | a legacy `team.json` | the legacy flat layout, exactly as before |
| none | one `teams/<t>/team.json` | `teams/<t>` |
| none | several | error: `AW-META-019`, exit 2, the teams listed; `stop-check` and `outbox-record` pass through (exit 0) so a hook never blocks an unrelated session; `team-list` and `team-migrate` need no team |
| none | none | the flat paths (no config: `AW-META-002`) |

A name that is not `[a-z0-9][a-z0-9-]*` is a usage error before anything is read or created (except `stop-check` and `outbox-record`, which pass through with exit 0 like they do on ambiguity). `AW_META_CONFIG` still
overrides the config file path (the state directory is still the team's), unless a team is selected and the file's `team` names another one: a lane's own `AW_META_CONFIG` is then ignored, so `aw-meta --team B` in team A's pane acts on B.

**Legacy layout.** `$AW_STATE_DIR/meta/team.json` with `meta.db`, `lane-sessions/` and `gates/` beside it keeps
working untouched. `team-migrate` moves it into `teams/<its team name>/` on request only.

## Team config

`$AW_STATE_DIR/meta/teams/<team>/team.json` (the legacy flat `$AW_STATE_DIR/meta/team.json`, or `AW_META_CONFIG`). Unknown fields are ignored. Example:
`skills/meta/examples/agentic-workflow.team.json` (its `repo`, `prompts` and `log` are placeholders: set absolute paths, since they are not expanded).

| Field | Meaning |
|-------|---------|
| `team`, `metaLane`, `lanes` | Session and tmux names are `<team>-<lane>` |
| `repo` | Absolute path of the shared checkout (Warp needs an absolute cwd) |
| `prompts` | Directory of `<lane>.md` kickoff prompts for fresh lane sessions |
| `log` | The running log (default `$AW_STATE_DIR/meta/teams/<team>/log.md`) |
| `gate.always`, `gate.docsOnly`, `gate.scopes.<name>.{paths,steps}` | The merge gate. A step is `{name, run: argv, heavy, rerun?}`; `heavy` runs it through the heavy-job wrapper (`AW_HEAVY`, used when the file exists); a `run[0]` with a `/` that is not absolute (`scripts/check.sh`) is resolved against the gate tree, not aw-meta's own directory (a bare name goes through PATH); `rerun` is the argv `gate-rerun` uses, with a `{test}` placeholder (default for a `dotnet test` step: its argv plus `--filter-method *<test>*`). Step names are unique across scopes. **Parallel steps:** a step may carry `after: [names]` and the gate carries `parallelSteps`. Steps run concurrently once what they wait for has finished; heavy steps are limited to `parallelSteps` at once (default: `heavy.cap` from team.json, else 3; 1 = serial). Without `after`, dependencies are inferred: a step that builds (`dotnet build`, or `dotnet test`/`publish` without `--no-build`) waits for the previous builder, so builders chain; any other heavy step, and any step with a word naming `dotnet` (so `scripts/dotnet-verify-packages.sh`, which restores the solution, waits like `dotnet format` does), waits for every builder in the selection; the rest run at once. `after: []` is how a heavy step opts out. Every gate step runs with `AW_SESSION_HEAVY_SLOTS` set to the pool limit (the heavy-job wrapper otherwise serialises all steps of the meta pane on its per-session lock), and a wrapper exit 75 (lock timeout) is an infrastructure error (`AW-META-009`), never a red gate. A cancelled step gets SIGTERM to its process tree (pids aw-meta started, found by parent, never by name), a 10 s grace, then a kill; `queue-gate` cancels its steps on SIGINT/SIGTERM and writes `TRAIN pid <n> started <time>` first and `TRAIN ended <time>` last on its board `after: []` waits for nothing; a name that is configured but not selected is ignored; an unknown name, a self-wait or a cycle is AW-META-003. Results, the RESULT output and the record keep config order and the same log files. A failed step does not skip its dependents. `dotnet test --no-build` steps only read `bin`/`obj`, so they may run side by side |
| `mergeOrder` | `[{before, after}]`: `after` can't merge while `before` is open |
| `worktrees` | `{roots?, buildOutputs?, enabled?}` for the worktree cleanup (see that section): the directories whose worktrees may be removed (`~` is the home directory), the directory names of untracked build output that don't keep a worktree, and `enabled` (default true; false makes the merge-run and watch triggers only report) |
| `resources` | `{diskFloorGb?, memFloorGb?, swapHighPct?}`, defaults 30, 8 and 90 (floors 0 or more, `swapHighPct` 1 to 100; anything else is AW-META-003 at load): the watch reports `DISK LOW` below `diskFloorGb` free on the state disk and `MEMORY LOW` below `memFloorGb` available or at `swapHighPct` percent of swap in use (a host with no swap never trips the swap rule). See "Resources and adaptation" |
| `builders` | `{maxPerLane?, worktreeGb?}`, defaults 4 and 4 (`maxPerLane` 1 or more, `worktreeGb` 0 or more; anything else is AW-META-003 at load): the most builder subagents a lane runs at once, and the disk one builder's worktree and build output take (the `health` advice) |
| `heavy` | `{reserve?, cap?}`: `reserve` is an array of `{minDepth, slots}` tiers (slots is a whole number or `"all"`), `true` or absent for the defaults `[{"minDepth":3,"slots":1},{"minDepth":5,"slots":2},{"minDepth":7,"slots":"all"}]`, or `false` to compute no reservation; `cap` (default 3, at least 1) is the slot count the reservation is sized against, to keep equal to the wrapper's own |
| `knownFlakes` | Test names (or Class.Method, or a class) a red is checked against before it counts as real; matched as whole dot-separated segments, never as a substring |

## Files

| Path | Written by |
|------|-----------|
| `$AW_STATE_DIR/meta/teams/<team>/meta.db` | `aw-meta` |
| `$AW_STATE_DIR/meta/teams/<team>/team.json` | `team-init` (then the owner) |
| `$AW_STATE_DIR/meta/meta.db`, `team.json` | the legacy flat layout of a team set up before named teams (still read when no team is selected; `team-migrate` moves it) |
| `$AW_STATE_DIR/meta/teams/<team>/gates/<pr>/` | `gate-run`, `gate-rerun`: `tree` (the gate tree, reused per PR), `main-tree` (plain main for reruns), `<run>/<step>.log` |
| `$AW_STATE_DIR/meta/teams/<team>/lane-sessions/<lane>` | `scripts/meta-lane.sh` (the lane's Claude session id), or `config/hooks/session-id-report.sh` at the SessionStart of a Codex or Cursor lane (its own session id, atomic, 0600) |
| `$AW_STATE_DIR/meta/teams/<team>/lane-sessions/<lane>.replied`, `.owed` | `config/hooks/meta-lane-gate.sh`: the checksum of the last reply it recorded and of the owed list the last block showed (cleared by a new delivery, a relaunch, a new session) |
| `$AW_STATE_DIR/meta/teams/<team>/lane-sessions/<lane>.agent` | `scripts/meta-lane.sh` (JSON: `agent`, `processName`, `hooks`, `transcripts`) for a lane whose agent isn't Claude Code; read by the lane commands and the lane source |
| `$AW_STATE_DIR/meta/teams/<team>/lane-sessions/<lane>.exited` | `scripts/meta-lane.sh` when the lane's claude exited and the pane fell back to its shell; `meta-launch.sh` restarts such a pane unless a claude runs in it again |
| `$AW_STATE_DIR/meta/teams/<team>/heavy-reserve` | `aw-meta` (`gate-run`, `merge-run`, `queue-drop`, `watch`, `status`): the computed heavy reservation, `<slots> <queue depth> <time>`; the heavy-job wrapper never reads it (the meta applies it with `meta-gate-reserve`) |
| the running log | `aw-meta` (merges, owner decisions, lane maps, kills, worktree sweeps, `log-append`) |
| `$XDG_DATA_HOME/warp-terminal/launch_configurations/<team>-team.yaml` | `scripts/meta-launch.sh` (the `warp` viewer only) |
| `$AW_STATE_DIR/launcher.json` | the owner, or `aw-meta-launch --init-launcher` (read by `scripts/launcher-lib.sh`) |
| `$AW_STATE_DIR/meta/teams/<team>/lane-sessions/<lane>.ambiguous` | `scripts/meta-lane.sh` when an agent that reports its own session id left more than one candidate session file |
| `$AW_STATE_DIR/meta/teams/<team>/lane-sessions/<lane>.settled` | `scripts/meta-lane.sh` for a lane whose agent reports its own session id: left once the id is recorded or refused; `meta-launch.sh` waits for it before starting the next lane |
| `$AW_STATE_DIR/meta/teams/<team>/lane-sessions/.reported.lock` | `scripts/meta-lane.sh`: the team start lock (a symlink whose target is the owner's pid; a dead owner's lock is taken over) that serialises the start of lanes whose agent reports its own session id |

## Launcher

`aw-meta-launch` reads `launcher.json` (`$AW_LAUNCHER_CONFIG`, else `$AW_STATE_DIR/launcher.json`; the contract is
`docs/csharp/sindri-launcher.md`, the design `docs/superpowers/specs/2026-10-15-sindri-session-launcher-design.md`, phase
SL-1). With no file the launcher is what it was apart from the team's own tmux socket (`-L <team>`, below): the Warp layout, Claude Code in every lane (a golden in
`scripts/tests/meta-launch.test.sh` pins the Warp YAML, every tmux command and every claude command line).

| Setting | Effect on `aw-meta-launch` |
|---|---|
| `viewer.kind: warp` (the default with no file) | The multi-pane Warp layout, written to `AW_WARP_LAUNCH_DIR` and opened |
| `viewer.kind: exec-template` | One window per lane (the lanes of this run, so `--lanes` opens only those) running `viewer.argv` with `{title}` and `{name}` = `<team>-<lane>`, `{cwd}` = the repo, `{cmd...}` = `tmux -L <team> attach -t =<team>-<lane>`; `--no-open` opens none, `--dry-run` prints each argv. A terminal that is not installed never stops the sessions: the attach command is printed |
| `viewer.kind: print` | Prints `tmux -L <team> attach -t =<team>-<lane>` per lane |
| `host` | Checked, not used: lanes always run in tmux |
| `meta.lanesAgent`, `meta.lanes.<lane>` | The catalog agent that runs the lane (the meta lane is always Claude Code). The catalog is `config/launcher/builtins.json` plus the file's `agents` |
| `agents.<id>` | `open`/`resume` argv templates (`{bin}`, `{cwd}`, `{name}`, `{seed}`, `{sessionId}`), `processName` (the pane check that restarts a lane whose agent exited) and `transcripts` (the glob that decides resume) |

**Non-Claude lanes.** A lane on `codex`, `cursor` or an added agent starts and resumes. `aw-meta-launch` prints
`lane <l>: <agent>, takes part in the obligations through its hooks (scripts/install-meta.sh installs them)` for an agent whose
catalog entry has `reportedId.hook: true`, and `lane <l>: <agent>, launch-only: no obligations` for one without a hook system. A
lane takes part once its hooks are installed and trusted (see [Non-Claude lanes](#non-claude-lanes)); a launch-only lane is not
messaged (`lane-send` stores the message and exits 1). An agent with `sessionId: reported`
chooses its own id: lanes of that kind start one at a time, each waiting up to `AW_META_ID_WAIT` seconds (30) for its id
(the `lane-sessions/<lane>` file a hook writes, else the single new file the catalog's `newestFile` glob finds that no other
lane has recorded; two candidates are refused and leave `<lane>.ambiguous`). "New" means a path that did not exist when
the lane started (a snapshot taken before the agent runs), not a recently modified one. The one-at-a-time start is the
lane's own: `meta-lane.sh` takes a team start lock (the symlink `lane-sessions/.reported.lock`, no `flock`: macOS has none) before the agent runs and releases it
when the id is recorded or refused, or after a timeout plus a grace window (`AW_META_ID_GRACE` seconds, default 10, during which the lane still watches for its late file and keeps it if it arrives, so the next lane cannot take it), so lanes created together (a Warp pane that creates its session, a
reboot, `--lanes`) are safe too. A lane that cannot start leaves `<lane>.failed` (the reason, the line the launcher prints as `lane <l> failed to start: ...`) and `<lane>.settled`, so the launcher reports it at once instead of waiting out the timeout. A lane refused by the launcher reader (a kickoff prompt starting with `-` once a
`launcher.json` exists) starts no agent and records no id.

**The team's own tmux socket.** `aw-meta-launch` runs every tmux call as `tmux -L <team>`, where `<team>` is `team.json`'s `team` (for example `-L sindri`): the has-session, new-session, respawn-pane and display-message calls, the `tmux -L <team> new -A` lines of the Warp layout, the printed attach lines and the exec-template viewer's attach argv. The team's server is therefore no longer tmux's default one, and a bare `tmux ls` or `tmux kill-server` in another terminal doesn't touch it. Attach by hand with `tmux -L sindri attach -t sindri-ideas`. The switch takes effect when the team is next relaunched: a server already running on the default socket is left alone (no migration, nothing kills it), but `aw-meta-launch` looks, read-only, for the lanes' `<team>-<lane>` sessions there before it creates anything and refuses (exit 1, naming them) while any exist, because starting them again would run a second agent on the same transcripts: stop the old team, then relaunch. `--allow-legacy` (or `AW_META_ALLOW_LEGACY=1`) skips the check; it is for the owner only, and the meta session never passes it; `AW_META_LEGACY_TMUX_SOCKET` names a `-L` socket to check instead of the default one (tests). Tests never run tmux directly: they source `scripts/test-tmux.sh`, which unsets `TMUX`, uses a temp `TMUX_TMPDIR` and a `-L` socket of its own (`ttmux`), and refuses a socket outside its temp dir.

**Precedence** (environment, then the file, then the defaults): `AW_META_CLAUDE` replaces the `claude-code` entry's `bin`
and `AW_META_CLAUDE_ARGS` is inserted after `{bin}`; both stay for tests and are deprecated for users in favour of an
`"override": true` entry for `claude-code`. `AW_WARP_LAUNCH_DIR` is where the `warp` viewer writes its YAML (the file has no
such field). `AW_LAUNCHER_CONFIG` is passed on to every lane's tmux session.

**Lane variables.** Every lane's environment has `AW_SINDRI_CHILD=1`, `AW_META_TEAM`, `AW_META_LANE` and `AW_META_DIR` (the team's state directory, which the lane hooks write `lane-sessions/` under). `meta-lane.sh`
exports them (L-12, SL-1 section 10.2) instead of passing `-e` to `new-session`, so tmux's global environment, the Warp
layout and every tmux command stay as they are with no file, `-L <team>` aside (the owner's condition). The effect is the
same: a `sindri` run inside the lane is a session caller, whatever the agent. A shell left in the pane after the agent
exits still carries them, so run the human's `sindri` approvals from another terminal.

**Writing the file.** `aw-meta-launch --init-launcher [--viewer warp|print|<terminal id>] [--force] [--stdout]` detects the
terminal (no GUI session: `print`; Warp installed: `warp`; `$TERM_PROGRAM` naming a terminal in
`config/launcher/terminals.json`; else the first of those on PATH; else `print`) and writes `launcher.json` (0600, never over
an existing file without `--force`; exit 0 ok, 1 refused, 2 usage). The reader refuses unknown keys, unknown placeholders
and wrong JSON types with `SND-LAUNCH-011` before any session is created; `sindri launch show` is the full check.

## Non-Claude lanes

A lane on Codex, Cursor or an added agent has no `SendMessage` and no transcript the meta can read, so its obligations go through
aw-meta's own files (SL-1b). The shell half calls them from the CLI's hooks: `scripts/install-meta.sh --provider codex|cursor`
installs `config/hooks/session-id-report.sh` and `config/hooks/meta-lane-gate.sh` user-wide through the provider adapters, and
`scripts/meta-lane.sh` writes the `.agent` record and exports `AW_STATE_DIR` and `AW_META_CONFIG` into the lane's environment.

- **Install.** `install-meta.sh` takes any provider (the Claude-only check is gone) and publishes and links `aw-meta` and `aw-meta-launch` for each. Claude Code gets `meta-stop-gate.sh` and `meta-outbox.sh`, unchanged. Codex gets `session-id-report.sh` at SessionStart and `meta-lane-gate.sh` at UserPromptSubmit and Stop; Cursor gets `session-id-report.sh` and `meta-lane-gate.sh` at `sessionStart`, `meta-lane-gate.sh` at `postToolUse` and `stop`. The hook configs are `~/.codex/hooks.json` and `~/.cursor/hooks.json`, the scripts and adapters are staged in `$AW_STATE_DIR/hooks`. Codex records trust against each hook's hash, so every install prints "open `codex` and run `/hooks` to trust the `aw:*` hooks"; until then the lane shows "hooks not running" in `health`. The hooks are user-wide, so both pass straight through (exit 0, no work) for every session that is not a lane: `meta-lane-gate.sh` unless `AW_META_TEAM` and `AW_META_LANE` are set, `session-id-report.sh` unless those or `AW_SINDRI_LAUNCH` are. Both skip `AW_JUDGE_CHILD` and run fail-open (`HOOK-FAILOPEN`, `meta-lane-gate` and `session-id-report` causes). The lane never installs aw-meta itself.
- **Session id.** `session-id-report.sh` writes the hook's `session_id` to `lane-sessions/<lane>` (and, for a Sindri launch, to `$AW_SINDRI_HOME/launches/<launchId>.session-id`), keyed on the variables the launcher set on that one session, so lanes started together record their own ids. `meta-lane.sh` waits for it as before and falls back to the newest-file glob only when the hook didn't fire.
- **Delivery and stop.** `meta-lane-gate.sh` runs `lane-inbox` at Codex's UserPromptSubmit (plain stdout is developer context) and at Cursor's `sessionStart` and `postToolUse` (`additional_context`). At Stop it takes the turn's last assistant text (`last_assistant_message`; the Cursor adapter reads it from the transcript), pipes the text after the last line that starts `REPLY TO META:` to `lane-reply --stdin`, then runs `lane-stop-check` (`--stop-hook-active` on a continuation). Only `lane-stop-check` exit 1 blocks (Codex: the stop is blocked with the reason; Cursor: `followup_message`, capped by `loop_limit`); exit 0, exit 2 and anything else pass. The hook runs outside the agent's sandbox, so a Codex lane in `workspace-write` never needs to write the state directory. A reply is recorded once: the hook keeps a checksum of the last recorded reply in `lane-sessions/<lane>.replied` and skips a Stop whose reply text matches it (a continuation, a turn with no new text, Cursor's transcript tail). Any delivery makes the same words recordable again: mail that `lane-inbox` prints clears the marker, and so does a `lane-stop-check` block that shows a new owed list (checksummed in `<lane>.owed`; the same list blocked again, as in the stop-cap loop, does not), which is how a Codex lane busy mid-turn gets mail. A relaunch (`meta-lane.sh`) and a new session (`session-id-report.sh`) clear both files. The Cursor adapter reads the last assistant text of the transcript, so a lane that answers a new message without any new text re-sends its previous reply: the hook cannot tell, and `health` does not flag it.
- **Only the lane's own agent acts.** A `codex exec` or `cursor-agent -p` that the lane's agent starts inherits the lane variables, and a Claude lane that runs one must not have it write the Claude lane's session id. `meta-lane.sh` exports `AW_META_LANE_PID` (its own pid), `AW_META_LANE_AGENT` (the catalog id) and `AW_META_LANE_PROC` (the agent's process name) in **every** lane, a Claude lane included (additive: nothing on the Claude path reads them). Both hooks pass straight through unless they run under the lane's own agent:
  - no `AW_META_LANE_PID` (not started by a lane script) or `AW_META_LANE_AGENT=claude-code` (a Claude lane's hooks are Claude's) means not a lane;
  - the lane's agent **root** is the ancestor of the hook whose parent is `AW_META_LANE_PID`, whatever it is called: a native binary (`lane script -> codex -> hook`) or the node wrapper of an npm install (`lane script -> node -> codex -> hook`);
  - a **nested instance** is a second stretch of ancestors named like the agent (`AW_META_LANE_PROC`, by command name or argv[0]) below the first, behind a tool process such as a shell (`codex exec` run from the agent's shell tool); it passes straight through, so it neither takes the lane's session id nor consumes its inbox nor blocks at Stop. Forks of one instance share its command line and count as one stretch;
  - no lane script among the ancestors passes through; a codex a human starts after the pane fell back to `exec bash -i` keeps the lane script's pid and counts as the lane's own; if `ps` can't answer, the hook acts.
- **Pane name.** `lane-send`'s guard compares `#{pane_current_command}` with the agent's `processName`, and tmux reports the foreground process group's leader. `meta-lane.sh` therefore runs a non-Claude agent under job control (`set -m`), so the agent leads its own group and the pane reads the agent, not `bash`; a Claude lane runs exactly as before. The name the pane shows is not always the catalog's: the npm install of Codex is a node wrapper that spawns the native binary, so the pane reads `node`. Once the agent leads the foreground group, `meta-lane.sh` reads the leader's name the way tmux does and records it as `processName` in the lane's `.agent` record (never a shell's name, which is what a downed lane's fallback shell shows), so the guard compares against what tmux shows. The lane's id watcher polls the lane script's pid and ends when the script is gone (the pane closed: the kernel hangs up the agent, and the watcher stops on its own), so it never holds the team start lock for a lane that is gone; no signal traps are involved.
- **Kickoff.** A non-Claude lane with hooks and a kickoff prompt file gets one extra paragraph at the end of it: end a turn that answers the meta with a `REPLY TO META:` line. The message `lane-inbox` shows says the same.

- **Lane identity** is `AW_META_TEAM` and `AW_META_LANE` and nothing else: no lane command on the lane side has a `--lane` flag.
- **Lane source.** The lane map and `health` see a non-Claude lane as `<team>-<lane>` when `lane-sessions/<lane>` holds a session id, `lane-sessions/<lane>.agent` names the agent, and the lane's tmux pane (`tmux display-message -p -t =<team>-<lane>:`) runs the agent's `processName`. With no `.agent` record nothing changes: a team of Claude lanes makes no tmux call and prints what it printed before.
- **State** is in `meta.db` (`lane_mail`, `lane_replies`, and the replies as ordinary `messages` rows) and the key-value store (`lane.<lane>.beat`, `.woke`, `.stop-fingerprint`, `.stop-count`, `.stop-cap`). A reply is an inbound message of the meta with id `lane:<lane>:<n>` from `<team>-<lane>`, so the meta's `stop-check` opens a `reply` obligation from it; the next `lane-send` to that lane closes it. Its fix text is `aw-meta lane-send --lane <lane> ...`, not `SendMessage`.
- **Wake.** `lane-send` types one fixed line into an idle lane only when all of: the lane is idle (its last hook was `lane-stop-check` and it hasn't been woken since), its pane runs the agent's `processName` (a bare shell is "lane down"), and no tmux client is attached. It sends `tmux send-keys -t =<team>-<lane>: -l '<line>'`, then `Enter` (literal mode, exact session; the trailing colon is what lets tmux resolve the pane). Otherwise the message waits for the lane's next hook. `AW_TMUX` names the tmux program. Every call runs on the team's own socket, `tmux -L <team>` (the same server the launcher starts the lanes on); `AW_META_TMUX_SOCKET` names another socket instead (tests and proofs). With no team config and no override there is no socket to look on, so a lane probes as down. Every aw-meta test and the AOT smoke set `AW_TMUX` to a fail-closed stub, never the real tmux.
- **Stop gate.** `lane-stop-check` blocks a stop while the lane owes a reply. A check without `--stop-hook-active` starts a new turn (blocks, resets the count); each continuation with the same owed set counts one block; after `AW_META_STOP_MAX` (default 3) the next is let through with a warning, an incident in the running log and the mark `lane.<lane>.stop-cap`. Only exit 1 blocks; exit 2 (variables unset, a tool error) must pass.
- **Health.** `health` and `lanes` show, for non-Claude lanes only: `taking part`; `launch-only: no obligations` (the CLI has no hook support, so `.agent` says `hooks: false`); `hooks not running: trust them with /hooks in codex` (no hook has run since the session id was recorded); `obligation not delivered (stop cap)`; `obligation not delivered (Cursor loop_limit)` (a Cursor lane blocked as many times as Cursor's default `loop_limit`, 5); `ambiguous session id`. A lane that needs attention makes `health` exit 1.
- **Migration 0003 is one-way.** The first time a build with this change opens `meta.db` it applies migration 0003 (`lane_mail`, `lane_replies`; additive, in a transaction, after copying the database to `meta.db.bak-v2`). From then on an `aw-meta` that doesn't know 0003 refuses the database with `AW-DATA-007`, so the installed binary and the database move together: install the new build before anything else opens the live `meta.db` (run proofs and `dotnet run` against a temp `AW_STATE_DIR`). To roll back, stop the team, restore `meta.db.bak-v2` over `meta.db` and reinstall the old binary; the lane mail written since is lost.

## Threat model

`aw-meta` guards against an honest orchestrator forgetting, not against a deliberately hostile one: the session can
always dismiss an obligation, and the loop guard lets a stuck stop through. The hooks fail open (`HOOK-FAILOPEN`,
`.agents/rules/hooks.md`): a missing or broken `aw-meta` never blocks a session. Inbound message bodies are lane text,
stored and printed but never executed. `health --kill-orphans` kills by pid only after the full rule matches; it never
matches a pattern against every process.

## Sindri absorbs it

| aw-meta | Sindri (planned) |
|---------|------------------|
| `meta-launch.sh`, `meta-lane.sh` | `ILauncher` (API P10a/b, engine P8) |
| obligations, owner queue | needs-you (API P4, P11) |
| `watch`, `gate-run`, `merge-check`, `merge-run` | the PR babysitter (factory addendum §17.9–17.10) |

`gh` is called as `AW_GH` (default `gh`; tests point it at a stub).

Until then this package stays thin: no HTTP, no daemon, one SQLite file.
