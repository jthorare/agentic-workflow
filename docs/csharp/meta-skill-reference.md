# Meta skill reference

The long-form detail that used to sit in `skills/meta/SKILL.md`. The skill is read every meta turn and stays short; open this file only when the task is the topic of a section below. The full CLI reference is `docs/csharp/meta.md`.

Not per-turn reading: trust the CLI's `FAIL`, `LOW` and `ok` lines and do not open gate logs.

## Set up a team (once)

1. `aw-meta team-init --team <name> --repo <absolute repo path> --lanes <a,b,c> [--meta-lane meta] [--log <file>]`
   writes `$AW_STATE_DIR/meta/teams/<name>/team.json` (the team's own state directory: `meta.db`, `gates/`, `lane-sessions/`,
   `log.md`; another team is never touched). Then edit its `gate.scopes` (the merge gate, step by step), `mergeOrder`,
   `knownFlakes` and `prompts` (a directory of `<lane>.md` kickoff prompts). Example:
   `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/meta/examples/agentic-workflow.team.json` (to use it, copy it to
   `$AW_STATE_DIR/meta/teams/<team>/team.json`; the directory name must equal the file's `team`, since selection uses the
   directory and the session and tmux names use the file).
2. `aw-meta-launch` opens one Warp window with a pane per lane, each attached to tmux session `<team>-<lane>` running
   that lane's session, named `<team>-<lane>` so its name never changes. The agent is Claude Code unless `team.json`
   has `agent` set to a catalog id (`cursor`, `codex`, or `claude-code`). That one field is every lane, including the
   meta lane, and it wins over `launcher.json`'s `meta.lanesAgent` and `meta.lanes`. Without it, `launcher.json` still
   decides, and a file that sets only `lanesAgent` still leaves the meta lane on Claude Code. Rerun it after a crash: it recreates
   the dead tmux sessions, restarts a lane whose Claude exited, and resumes each lane by its session id. From the
   meta session, recreate lanes with `aw-meta-launch --no-open`: the open Warp window reattaches by itself. When
   `team.json` `agent` or `launcher.json` puts a lane on another agent, the launcher prints `lane <l>: <agent>, takes part in the obligations
   through its hooks` (the hooks come from `scripts/install-meta.sh --provider codex|cursor`) or, for a CLI with no
   hook system, `lane <l>: <agent>, launch-only: no obligations`: don't message a lane marked launch-only.
   Lanes run in your login shell (`$SHELL -lc`, else bash) with the `PATH` of the shell that ran `aw-meta-launch`, even on a tmux server that was already up.
   `aw-meta-launch` refuses while the team's lanes still run on tmux's default socket (the pre-switch team). The meta
   session must never pass `--allow-legacy` or set `AW_META_ALLOW_LEGACY`: they skip the check that stops a second agent on the same transcripts. Tell the owner.
   The team runs on its own tmux socket, named after the team, so attach with `tmux -L <team> attach -t =<team>-<lane>`
   (for example `tmux -L sindri attach -t sindri-ideas`), and list its sessions with `tmux -L <team> ls`. A bare
   `tmux attach` looks at tmux's default socket, which the team no longer uses. A team launched before this change
   stays on the default socket until it is relaunched.

### Named teams: several meta sessions at once

Every team has its own config and state, so one meta session per team can run at the same time without seeing the
others' messages, obligations, role, gates or lane sessions. `aw-meta team-list` shows the teams (layout, repo, lanes,
the registered meta session and whether it is running). Which team a command acts on:

- `--team <name>` (any command) or `AW_META_TEAM=<name>`; the flag wins. `aw-meta-launch --team <name>` starts that team.
- `aw-meta-launch` exports `AW_META_TEAM` (and `AW_META_DIR`, the team's state directory) into every pane it opens, so a
  lane or meta session and its hooks need nothing more. The Claude Stop and outbox hooks also take the team from the
  tmux socket they run on (the launcher names it after the team) when `AW_META_TEAM` is unset, but only for a team that
  exists on disk.
- With one team on disk none needs selecting. With several and none selected a command exits 2 with `AW-META-019`
  listing them: pick one, never guess. The hooks (`stop-check`, `outbox-record`) pass straight through instead, so an
  unrelated session is never blocked.
- `AW_META_CONFIG` still overrides the config file path.

An older install has the flat layout (`$AW_STATE_DIR/meta/team.json` and `meta.db` directly under `meta/`). It keeps
working unchanged while no team is selected (or when the selected name is that config's own team). Moving it into
`teams/<its team>/` is opt-in: first run `worktree-sweep` for the old `meta/gates` trees (after the migration the sweep
roots are `teams/<team>/gates`, so the old directory is never swept again), stop every session of the team (the meta and
the lanes: `team-migrate` refuses while one runs), run `aw-meta team-migrate --dry-run`, then `aw-meta team-migrate`, then
relaunch with `aw-meta-launch`. `gates/` stays where it is. Never run it from the meta session of a live team.

Teams on the same repo share the default worktree roots (`~/repos/aw-wt`, `<repo>/.claude/worktrees`), so one team's
`worktree-sweep` or merge trigger may remove another team's finished worktrees (the other rules still apply: an ended pull
request, nothing unpushed, no process inside). Give each team its own `worktrees.roots` in its `team.json` to keep them apart.

## Worktrees

Nobody removes worktrees by hand. `aw-meta` does it on **triggers**, never on a timer (work builds up over time, and a
worktree that has sat untouched may still be someone's work in progress): `merge-run N` sweeps PR N's worktrees, the watch
sweeps a PR it sees merged or closed (a `WORKTREES …` line), and **you** run `aw-meta worktree-sweep` when a lane reports a
builder done or finished. It removes only a worktree whose branch is merged into `origin/main` (or whose PR is closed with
everything pushed) and that has no uncommitted or untracked file (build output aside), no unpushed commit and no process in
it, and is not the main worktree, the shared checkout or locked. It never forces, never deletes a branch, and prints each
kept worktree with every reason.

- `aw-meta worktree-list`: every worktree with its verdict and reasons (read-only; `--no-fetch` skips the git fetch).
- `aw-meta worktree-sweep [--dry-run] [--pr N]`: remove the removable ones. `--dry-run` first when unsure.
- `aw-meta worktree-sweep --path <dir> --done`: you are finished with a detached leftover tree (kept otherwise as "detached, not a gate tree"). It goes when its HEAD is in origin/main or on a pushed branch, it is clean and idle, and a sweep ran since it last changed; otherwise the output says why it stayed. Never forced.
- A lane that finds its worktree kept for a reason it can clear (stop the process, commit and push) can ask you for a sweep.

## Which PRs the watch reports

`aw-meta watch` reports only the team's PRs. A PR is the team's when any of these holds:

1. It is in the merge queue (`merge.queued`).
2. A lane sent `ready #N` for it.
3. Its head branch starts with a prefix in `team.json` `watch.prBranchPrefixes`. With no prefixes set (the default), its head branch is checked out in a worktree under the team's worktree roots instead.
4. It is stacked on a team PR, however deep the stack goes.

`aw-meta watch --all-prs` restores the old output: every open PR in the repo.

## Health

`aw-meta health` groups memory (Claude sessions, builds, Temporal, MCP servers, Docker, other), finds orphaned Temporal
dev servers, duplicate MCP server copies, lanes with no live session and heavy-lock holders, and reports the heavy reservation aw-meta
computed from the merge queue (sized against `heavy.cap`; not what the wrapper's gates actually hold) and the builders-per-lane advice (see Resources and adaptation). `aw-meta health
--kill-orphans` kills only orphans that match the rule (parent `systemd --user` or init, `--db-filename` under
`/tmp/aw-sindri-*`, older than 5 min), by pid. Never `pkill -f` a pattern.

## Resources and adaptation

The box has a fixed amount of memory, disk and heavy-job slots. Watch them and adapt, so lanes never find out by crashing the
machine. The thresholds are the team config's `resources`, `builders` and `heavy` keys (defaults: disk floor 30 GB, memory floor
8 GB, 4 builders per lane of 4 GB each; see the reference doc). Nothing below is specific to one project.

**Gates.**
- Run gates **only through `aw-meta gate-run`** (and reruns through `gate-rerun`). They carry `AW_HEAVY_PRIORITY=meta`, so the
  heavy-job wrapper lets them use any box-wide slot. aw-meta computes the slot reservation from the merge-queue depth (the more
  ready pull requests wait, the more slots the gates should keep) and prints it as `heavy reservation: N of C slots (queue depth
  D) - set it with meta-gate-reserve A` in `status`, `health` and `gate-run`. **Before each gate, run `meta-gate-reserve A`** with
  the argument `aw-meta status` just printed (not the queue depth: the script's tiers are fixed, so aw-meta prints the argument that gives its computed slots; `gate-run` prints the line after its steps, so read it from `status` first); aw-meta never writes the wrapper's files itself. When the queue empties (or the line says
  `[stale: ...]`) run `status` again and apply it, or `meta-gate-reserve --clear`. A gate you run by hand has no priority and
  waits behind that reservation: don't.
- Gates run **one at a time** (serial): start the next `gate-run` only when the previous one is recorded. A `queue-gate` train is one run: do not start another gate beside it.
- **Re-gate after any merge**: main moved, so every queued pull request's gate is stale (`merge-check` says so).
- A gate failure whose log shows only a **heavy lock wait** (the step never started its build or test, it sat waiting for a
  slot, or timed out waiting) is **infrastructure, not code**: don't send it back to the lane, re-run the gate.

**Watch events** (from `aw-meta watch`; act on each when it prints):
- `DISK LOW`: tell every lane to **stop starting new builders**; run `aw-meta worktree-sweep` (the safe cleanup: only finished
  worktrees); then if disk is still low, **ask the owner before deleting anything else**: list what is rebuildable (build output,
  package caches, proof binaries, old gate trees) with sizes via `aw-meta owner-ask`, and delete only what the owner approves,
  never with a recursive delete you were not told to run.
- `DISK LOW` / `MEMORY LOW` print only after two polls agree, and a re-armed watch does not repeat a state it already reported.
- `MEMORY LOW`: tell every lane to **pause starting builders and heavy jobs** until it clears; jobs already running finish.
- `DISK OK` / `MEMORY OK`: tell the lanes to **resume**.

**Builders per lane.** `aw-meta health` prints `builders: N per lane (...)` (and `status` the same line): the number of builder
subagents each lane may run at once now, never below 1. It falls as free disk gets close to the floor plus every lane's builder
worktrees, and is 1 while memory is low (the same rule as `MEMORY LOW`: available memory under its floor; swap use alone never counts). Check it after `DISK`/`MEMORY` events and at each `health`, and **tell
the lanes when it changes** (one message to every lane: "builders per lane: N"). Every builder gets its **own worktree and
branch**; never two builders on one branch or in one worktree. The disk floor is a rule, not a hint: below it, no new builder
starts whatever the advice says for a lane that is idle.

**Worktrees are part of the disk story.** `merge-run` and the watch already sweep a pull request's worktrees when it merges or
closes (see Worktrees); a kept worktree is still holding disk, so ask its lane to clear the reason (commit and push, stop the
process) and sweep again.

**Board.** Lanes learn that main moved and that the lane map changed from `aw-meta board-line` in their prompt hook: one line, once per change. You don't send either, and neither opens an obligation.

## Lane context

Keep each Claude lane's context window small, or the lanes burn usage. `aw-meta lanes-context` lists each lane's size and a verdict
(`ok`, `recycle` at `context.recycleAt`, default 300k, `unknown`); `health` and `status` print it as `context: api 487k (recycle), ...`,
and `watch` prints `CONTEXT HIGH <lane> <N>k` once per crossing and `CONTEXT OK <lane>` after a recycle.

On `CONTEXT HIGH` (or a `recycle` verdict), message the lane (SendMessage): at a safe point write a digest to
`~/.agentic-workflow/digests/<team>-<lane>-<date>.md` and reply `DIGEST READY <path>`. A safe point means no builder subagent, review run or
background job is running (`/clear` cuts them off) and everything is pushed. When the reply arrives, run
`aw-meta lane-recycle <lane> --digest <path>` (`--dry-run` shows the steps). It clears the lane, waits for the new session, records it so a
relaunch resumes it, and has the lane read the digest and continue. Exit 1 means it refused (stale or empty digest, or no live Claude session in the lane's pane) or the new session never
appeared (nothing more was sent; the error prints the command to send the line by hand). Never recycle the meta lane this way: its own context shows as `META RECYCLE <N>k`; write a digest, then run `aw-meta lane-recycle <meta lane> --digest <abs path> --detach`, which waits for your turn to end before it clears. If the clear never comes, look for a NOTE in the running log.

## Usage limits

A Claude usage limit stops every lane and your own subagents at once ("You've hit your session limit · resets 2:50am",
"weekly limit · resets Oct 19, 10pm"). `aw-meta usage` shows who is `limited` (kind and reset) and `watch` prints
`USAGE LIMITED <lane|meta> <kind> resets <time>` once per stop.

- On `USAGE LIMITED`: don't fight it and don't retry in a loop; the lanes cannot work and neither can you. Note it in the digest
  (which lanes, which limit, the reset time) and run `aw-meta usage-wait` in the background (`run_in_background`): it blocks
  until the reset plus a margin and its exit wakes you. If you are the one limited, the watch's `USAGE OK meta` line wakes you too.
- On `USAGE OK <lane>`: the watch types the resume line into the idle lanes itself (`usage.autoResume`); a busy lane is
  never typed into. You don't message them.
- On `USAGE OK meta` (or the `usage-wait` exit): resume your own subagents with SendMessage (their ids are in your digest), re-arm
  `aw-meta watch`, then run `aw-meta usage` and confirm every lane is `ok`. A lane still `limited` after the reset
  (`USAGE RESUME FAILED`, or it was busy or had a draft in its box) needs a nudge: SendMessage it (`aw-meta lane-send` only for a non-Claude lane; it refuses a Claude lane, AW-META-017).

## Usage across providers

`aw-meta usage` also shows, per lane, the agent and model it is assigned and what its provider says is left (Codex: 5h and weekly percent used; Claude Code: only the limit stop; Cursor: nothing, shown as unknown). `aw-meta route` turns that into moves, using `routing.equivalents` in the team config (for example opus to gpt-5.5, haiku to gpt-5.4-mini).

- Run `aw-meta route` when `watch` prints `ROUTE SUGGEST <lane> ...`, when `USAGE LIMITED` hits and the reset is hours away, and before you hand a lane new work.
- It only prints. Read each `switch` line and each `keep` line (`keep` says why a limited lane cannot move: no equivalent model, the target provider is limited too, a relaunch is pending).
- Switching a lane is your action. Run `aw-meta route --apply` (add `--lane <l>` for one lane), then log the decision: `aw-meta log-append --kind decision --text "route: moved <lane> from <from> to <to> because <reason>"`.
- `--apply` writes `routing.lanes.<lane>` and starts nothing. The lane moves at its next start: for a Claude lane run `aw-meta lane-recycle <lane> --digest <path>` (it exits the Claude and relaunches the lane on the routed agent and model instead of clearing), then `aw-meta lane-send --lane <lane>` the digest line it prints (the new agent starts from its kickoff prompt). A lane already on Codex or Cursor: exit it and run `aw-meta-launch --lanes <lane>`. Do not move a lane whose provider shows `usage unknown` without saying so in the digest.

## The running log

Merges, owner decisions, lane maps and kills are logged by the CLI. For anything else worth keeping:
`aw-meta log-append --kind incident|note --text "…"`.

## CI gate mode

When `gate.mode` is `ci` in the team config, step 2 changes: `aw-meta gate-run N` runs nothing on this box. It reads the pull request's required checks, review decision and merge state from GitHub and records the answer against the head sha. It passes only when every required check is green, the review is `APPROVED` and the merge state is `CLEAN`; a pending check is a fail (`pending`), so run it again once CI finishes. **Skip every `meta-gate-reserve` step**: no gate tree is built and no heavy slot is taken. Skip step 3 (there are no local test reds to rerun; a red required check goes back to the lane). Then `aw-meta merge-check N` and `aw-meta merge-run N` as before: `merge-check` needs a passing CI record at the head the pull request has now, so a push after `gate-run` means run it again. If `gate-run` refuses with `AW-META-024` (`no required checks on <base>`), the base has no branch protection: ask the owner, never merge on it. `queue-gate` (the train) is a local gate and does not apply.

## Gating several ready pull requests as a train

**With several ready pull requests, gate them as a train** instead of one `gate-run` at a time:

1. `aw-meta queue-gate [--max K]` takes the first K ready pull requests in merge-queue order (K defaults to the team's heavy cap; `queue-gate 12 13` names them). The first gates on `origin/main`; each later one on `origin/main` with the heads ahead of it merged in order, so one round proves them all in the order they will merge. The gates run side by side and share the heavy slots.
2. Read the `TRAIN` table at the end (position, PR, base, RESULT). `DROPPED #N: conflicts with #M at <sha>` means that head conflicts with the train so far: tell its lane to rebase; the rest of the train is unaffected. A `CANCELLED` gate was stopped because a gate below it failed and was not recorded; it stays queued for the next round.
3. Red steps: `gate-rerun` as in step 3 above (it works on a train gate), then merge in **train order**: `aw-meta merge-check N`, `aw-meta merge-run N` for position 1, then position 2, and so on. After each merge, main is the previous gate's tested tree, so `merge-check` accepts the next gate (it compares the merge result's tree with the tree the gate tested, whatever merge style gh used). A gate that fails ends the train there: later positions need a new `queue-gate` once it is fixed.
4. `aw-meta status` shows the running train (position, PR, state).
