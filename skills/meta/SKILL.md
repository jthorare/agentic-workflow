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

`aw-meta` is on PATH after `./setup.sh --with-meta` (Linux only for now: aw-meta reads /proc; on macOS setup skips it) (else `$HOME/.agentic-workflow/bin/aw-meta`). Every command takes
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
- A pure FYI that needs no reply: `aw-meta inbox-dismiss <id> --reason "<why>"`.
- Work in flight (a gate, a review): `aw-meta inbox-defer <id> --reason "<what>" --until +30m`. It reopens when due.

## Set up a team (once)

1. `aw-meta team-init --team <name> --repo <absolute repo path> --lanes <a,b,c> [--meta-lane meta] [--log <file>]`
   writes `$AW_STATE_DIR/meta/team.json`. Then edit its `gate.scopes` (the merge gate, step by step), `mergeOrder`,
   `knownFlakes` and `prompts` (a directory of `<lane>.md` kickoff prompts). Example:
   `$HOME/.agentic-workflow/toolkit/skills/meta/examples/agentic-workflow.team.json`.
2. `aw-meta-launch` opens one Warp window with a pane per lane, each attached to tmux session `<team>-<lane>` running
   that lane's Claude session, named `<team>-<lane>` so its name never changes. Rerun it after a crash: it recreates
   the dead tmux sessions, restarts a lane whose Claude exited, and resumes each lane by its session id. From the
   meta session, recreate lanes with `aw-meta-launch --no-open`: the open Warp window reattaches by itself. When
   `launcher.json` puts a lane on another agent, the launcher prints `lane <l>: <agent>, launch-only for now: no
   obligations`: don't message a lane marked launch-only.
   `aw-meta-launch` refuses while the team's lanes still run on tmux's default socket (the pre-switch team). The meta
   session must never pass `--allow-legacy` or set `AW_META_ALLOW_LEGACY`: they are for the owner only. Tell the owner.
   The team runs on its own tmux socket, named after the team, so attach with `tmux -L <team> attach -t =<team>-<lane>`
   (for example `tmux -L sindri attach -t sindri-ideas`), and list its sessions with `tmux -L <team> ls`. A bare
   `tmux attach` looks at tmux's default socket, which the team no longer uses. A team launched before this change
   stays on the default socket until it is relaunched.

## Start (or after any restart)

1. `aw-meta role-show`. If it isn't this session, `aw-meta role-set --session <this session's id> --name <this session's
   name>` (the launcher does this for the meta pane).
2. **List agents**, then `aw-meta lanes-refresh` and `aw-meta lanes`. Sessions named `<team>-<lane>` map themselves;
   anything else maps from the lane's announcement ("API lane: …").
3. `aw-meta lanes --announce` and **send the printed text to every lane** (the `broadcast-lanes` obligation stays open
   until each mapped lane got it).
4. **Watch in the background**: `aw-meta watch --for 29m`. When it prints `WATCH EXPIRING`, start it
   again; if you forget, the Stop hook opens `rearm-watch`.
5. `aw-meta status` and `aw-meta health`.

## A lane says `ready #N`

1. `aw-meta merge-check N`. Read every FAIL line. A merge-order FAIL means wait: tell the lane why.
2. `aw-meta gate-run N`. Scopes come from the diff and the team config; name them to override (`gate-run N repo`).
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
dev servers, duplicate MCP server copies, lanes with no live session and heavy-lock holders. `aw-meta health
--kill-orphans` kills only orphans that match the rule (parent `systemd --user` or init, `--db-filename` under
`/tmp/aw-sindri-*`, older than 5 min), by pid. Never `pkill -f` a pattern.

## The running log

Merges, owner decisions, lane maps and kills are logged by the CLI. For anything else worth keeping:
`aw-meta log-append --kind incident|note --text "…"`.

## Next steps

- `/review`: when a lane asks for a review run on its PR
- `/specToProvenPR`: the lanes' own workflow; point a lane at it when it skips proof
