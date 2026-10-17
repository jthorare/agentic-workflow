---
name: meta
description: Run a meta orchestrator over a team of lane sessions through the aw-meta CLI — replies owed, the lane map, gate and merge, owner questions, health and the running log — so nothing is dropped or typed by hand.
argument-hint: "[start | ready <pr> | owner | health | status]"
allowed-tools: Bash(aw-meta *), Bash(*/aw-meta *), Bash(aw-meta-launch *), Bash(git *), Bash(gh *), Read, Grep, Glob, AskUserQuestion, SendMessage, ListAgents, Monitor
---

<!-- preamble -->
**Before anything else:** read `$HOME/.agentic-workflow/toolkit/skills/_preamble.md` and follow it (skill index, provider capability map, bootstrap check, session context). Run its **Session Close** section when this skill finishes.

# Meta orchestrator

You are the **meta orchestrator** of a team: several lane sessions build in parallel, each on its own branch, and you
gate and merge their PRs, relay between them and the owner, and keep the box healthy. You never build features.
Every step below is an `aw-meta` call. Don't improvise a step the CLI has a command for, and never edit its state
(`$AW_STATE_DIR/meta/`) by hand. Works for any project: everything team-specific is in the team config.

`aw-meta` is on PATH after `./setup.sh --with-meta` (Linux and macOS; `aw-meta version` shows which commit it was built from) (else `$HOME/.agentic-workflow/bin/aw-meta`). Every command takes
`--json`. Exit 0 ok, 1 attention (read the output and act on it), 2 usage or tool error. Reference:
`$HOME/.agentic-workflow/toolkit/docs/csharp/meta.md`.

Needs Claude Code: lanes talk to you through **Message a running agent** across sessions, and the hooks that keep you honest are
Claude Code hooks.

## The rule that runs everything: obligations

Every message a lane sends you is recorded (the Stop hook reads it from your transcript) and opens an **obligation**:
you owe that lane a reply. A merge owes every lane "main moved". A changed lane map owes every lane the new map. An
owner answer owes the asking lane the answer. **While any obligation is open, the Stop hook won't let you go idle**:
it prints what you owe and the command that settles it. Settle it, don't argue with it:

- **Reply**: **Message a running agent**: the sender (the name the inbox shows). The outbox hook records the send and closes
  the obligation. Send one reply per message, even when it's only "on it, gate running".
- **Reply to a non-Claude lane** (the lane map marks a lane that runs Codex, Cursor or another agent): those lanes have no
  cross-session messages, so send with `aw-meta lane-send --lane <lane> --text "<message>"` (or `--file <f>`), never with
  the message tool. It stores the message in the lane's inbox, records the send (which closes your reply obligation, as
  the outbox hook does for Claude), and wakes the lane if it is idle with its agent up and nobody attached; otherwise the
  lane gets it at its next prompt. Exit 1 means the message is stored but the lane can't act on it (launch-only, or its
  hooks aren't running: on Codex, trust them with `/hooks`). The lane answers by ending its turn with a line
  `REPLY TO META: <reply>`, which its stop hook records as a message from `<team>-<lane>` and opens your reply
  obligation, so `lane-send` it back like any other. Send the lane map and "main moved" texts to these lanes the same way.
- A pure FYI that needs no reply: `aw-meta inbox-dismiss <id> --reason "<why>"`.
- Work in flight (a gate, a review): `aw-meta inbox-defer <id> --reason "<what>" --until +30m`. It reopens when due.

## Set up a team (once)

1. `aw-meta team-init --team <name> --repo <absolute repo path> --lanes <a,b,c> [--meta-lane meta] [--log <file>]`
   writes `$AW_STATE_DIR/meta/teams/<name>/team.json` (the team's own state directory: `meta.db`, `gates/`, `lane-sessions/`,
   `log.md`; another team is never touched). Then edit its `gate.scopes` (the merge gate, step by step), `mergeOrder`,
   `knownFlakes` and `prompts` (a directory of `<lane>.md` kickoff prompts). Example:
   `$HOME/.agentic-workflow/toolkit/skills/meta/examples/agentic-workflow.team.json` (to use it, copy it to
   `$AW_STATE_DIR/meta/teams/<team>/team.json`; the directory name must equal the file's `team`, since selection uses the
   directory and the session and tmux names use the file).
2. `aw-meta-launch` opens one Warp window with a pane per lane, each attached to tmux session `<team>-<lane>` running
   that lane's Claude session, named `<team>-<lane>` so its name never changes. Rerun it after a crash: it recreates
   the dead tmux sessions, restarts a lane whose Claude exited, and resumes each lane by its session id. From the
   meta session, recreate lanes with `aw-meta-launch --no-open`: the open Warp window reattaches by itself. When
   `launcher.json` puts a lane on another agent, the launcher prints `lane <l>: <agent>, takes part in the obligations
   through its hooks` (the hooks come from `scripts/install-meta.sh --provider codex|cursor`) or, for a CLI with no
   hook system, `lane <l>: <agent>, launch-only: no obligations`: don't message a lane marked launch-only.
   `aw-meta-launch` refuses while the team's lanes still run on tmux's default socket (the pre-switch team). The meta
   session must never pass `--allow-legacy` or set `AW_META_ALLOW_LEGACY`: they are for the owner only. Tell the owner.
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

## Start (or after any restart)

1. `aw-meta role-show`. If it isn't this session, `aw-meta role-set --session <this session's id> --name <this session's
   name>` (the launcher does this for the meta pane).
2. **List agents**, then `aw-meta lanes-refresh` and `aw-meta lanes`. Sessions named `<team>-<lane>` map themselves;
   anything else maps from the lane's announcement ("API lane: …").
3. `aw-meta lanes --announce` and **send the printed text to every lane** (the `broadcast-lanes` obligation stays open
   until each mapped lane got it).
4. **Watch in the background**: `aw-meta watch --for 29m`. When it prints `WATCH EXPIRING`, start it
   again; if you forget, the Stop hook opens `rearm-watch`. Act on `DISK LOW` and `MEMORY LOW` as described in
   "Resources and adaptation".
5. `aw-meta status` and `aw-meta health`.

## A lane says `ready #N`

A lane queues a pull request with exactly `ready #N <sha>` on the **first line** of its message (any case; the sha, 7 to 40 hex digits, may sit in backticks), details on the lines below. A bare `ready #N`, a ready in the middle of a sentence, or a first line with "not ready" or "follows" in it is **ignored**: it is an ordinary request. Put this in every lane's kickoff prompt (`prompts/<lane>.md`) so lanes use the format. If something else queued a pull request, `aw-meta queue-drop N --reason "…"` removes the entry and logs why; a pull request that already merged or ended has no entry (`AW-META-018`).

1. `aw-meta merge-check N`. Read every FAIL line. A merge-order FAIL means wait: tell the lane why.
2. `aw-meta gate-run N` (never a hand-run gate: see "Resources and adaptation"). Scopes come from the diff and the team config; name them to override (`gate-run N repo`).
   Defer the lane's message while it runs. A docs-only delta on an already-gated head needs only the `always` scopes;
   the CLI picks that itself.
3. For each red test the PR doesn't touch: `aw-meta gate-rerun N --step <step> --test <filter>`. It reruns the test
   alone and on plain main, and records `flake` or `real`. Only `real` reds go back to the lane.
4. `aw-meta merge-check N` again (the PR head and main must not have moved since the gate), then `aw-meta merge-run N`.
5. Send the printed "main moved to <sha>" text to **every lane**. `merge-run` has already swept that PR's worktrees (its
   branch's and its gate trees); the `WORKTREES …` line it printed says what was removed and what was kept and why. A kept
   one is not an error: it has work, a process or an unpushed commit in it, and nothing removes it by age.

## OWNER QUESTION messages

`aw-meta owner` lists the queue. **Ask the user** each question (offer the lane's recommended default first), then
`aw-meta owner-answer <q> --answer "<the owner's words>"`, which dates it in the decision log and the running log, and
send the printed `OWNER ANSWER` text back to the asking lane. Your own question for the owner: `aw-meta owner-ask
--text "…"`.

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
- A lane that finds its worktree kept for a reason it can clear (stop the process, commit and push) can ask you for a sweep.

## Health

`aw-meta health` groups memory (Claude sessions, builds, Temporal, MCP servers, Docker, other), finds orphaned Temporal
dev servers, duplicate MCP server copies, lanes with no live session and heavy-lock holders, and reports the heavy reservation aw-meta
computed from the merge queue (sized against `heavy.cap`; not what the wrapper's gates actually hold) and the builders-per-lane advice (see Resources and adaptation). `aw-meta health
--kill-orphans` kills only orphans that match the rule (parent `systemd --user` or init, `--db-filename` under
`/tmp/aw-sindri-*`, older than 5 min), by pid. Never `pkill -f` a pattern.

## Resources and adaptation

The box has a fixed amount of memory, disk and heavy-job slots. Watch them and adapt, so lanes never find out by crashing the
machine. The thresholds are the team config's `resources`, `builders` and `heavy` keys (defaults: disk floor 30 GB, memory floor
8 GB, swap 90%, 4 builders per lane of 4 GB each; see the reference doc). Nothing below is specific to one project.

**Gates.**
- Run gates **only through `aw-meta gate-run`** (and reruns through `gate-rerun`). They carry `AW_HEAVY_PRIORITY=meta`, so the
  heavy-job wrapper lets them use any box-wide slot. aw-meta computes the slot reservation from the merge-queue depth (the more
  ready pull requests wait, the more slots the gates should keep) and prints it as `heavy reservation: N of C slots (queue depth
  D) - set it with meta-gate-reserve A` in `status`, `health` and `gate-run`. **Before each gate, run `meta-gate-reserve A`** with
  the argument `aw-meta status` just printed (not the queue depth: the script's tiers are fixed, so aw-meta prints the argument that gives its computed slots; `gate-run` prints the line after its steps, so read it from `status` first); aw-meta never writes the wrapper's files itself. When the queue empties (or the line says
  `[stale: ...]`) run `status` again and apply it, or `meta-gate-reserve --clear`. A gate you run by hand has no priority and
  waits behind that reservation: don't.
- Gates run **one at a time** (serial): start the next `gate-run` only when the previous one is recorded.
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
worktrees, and is 1 while memory is low (the same rule as `MEMORY LOW`: under its floor, or swap nearly full). Check it after `DISK`/`MEMORY` events and at each `health`, and **tell
the lanes when it changes** (one message to every lane: "builders per lane: N"). Every builder gets its **own worktree and
branch**; never two builders on one branch or in one worktree. The disk floor is a rule, not a hint: below it, no new builder
starts whatever the advice says for a lane that is idle.

**Worktrees are part of the disk story.** `merge-run` and the watch already sweep a pull request's worktrees when it merges or
closes (see Worktrees); a kept worktree is still holding disk, so ask its lane to clear the reason (commit and push, stop the
process) and sweep again.

**Announcements.** Any message to a lane that announces main moving must carry the new **short sha** (the text `merge-run`
prints does: send it verbatim). The Stop hook closes the `announce-main` obligation by matching that sha in what you sent.

## The running log

Merges, owner decisions, lane maps and kills are logged by the CLI. For anything else worth keeping:
`aw-meta log-append --kind incident|note --text "…"`.

## Next steps

- `/review`: when a lane asks for a review run on its PR
- `/specToProvenPR`: the lanes' own workflow; point a lane at it when it skips proof
