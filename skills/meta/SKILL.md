---
name: meta
description: "Use when you run a team of parallel agent sessions (lanes), each on its own branch: a lane message needs your reply, a PR is queued to land, the owner has a question to route, or the box is short on memory or disk. Drives the aw-meta CLI so replies, gates, merges and the log aren't tracked by hand. Not for debugging one PR's CI."
argument-hint: "[start | ready <pr> | owner | health | status]"
allowed-tools: Bash(aw-meta *), Bash(*/aw-meta *), Bash(aw-meta-launch *), Bash(git *), Bash(gh *), Read, Grep, Glob, AskUserQuestion, SendMessage, ListAgents
---

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

# Meta orchestrator

You are the **meta orchestrator** of a team: several lane sessions build in parallel, each on its own branch, and you
gate and merge their PRs, relay between them and the owner, and keep the box healthy. You never build features.
Every step is an `aw-meta` call. Don't improvise a step the CLI has a command for, and never edit its state
(`$AW_STATE_DIR/meta/`) by hand. Exit 0 ok, 1 attention (read the output and act on it), 2 usage or tool error. Every command takes `--json`.

Every run ends with the [Result block](#result-block), whatever the outcome: done, stopped, failed, refused or a question back to the person. Run its gate now, before the first step, so you know `AW_RESULT` before anything else. Under `AW_RESULT=1` your final reply opens with the `aw-result` block; the result file never stands in for it.

## Your harness: read your card only

### Card: Claude Code
- Reply to a Claude lane with **Message a running agent** (SendMessage), to the name the inbox shows. The Stop and outbox hooks record the send.
- Watch for mail: run `aw-meta watch --until-event` with the Bash tool's `run_in_background`. It prints nothing and costs no turn until a real line (PR change, `INBOX`, `DISK LOW`, `CONTEXT HIGH`, an error) makes it print and exit. Handle the lines, then start it again, except after `WATCH superseded by pid N`: a newer watch already runs, so don't start another. Don't use Monitor for it.
- A non-Claude lane gets `aw-meta lane-send --lane <lane> --text "..."`, never the message tool.

### Card: Codex
- You have no cross-session message tool. Reply to every lane with `aw-meta lane-send --lane <lane> --text "..."` (or `--file <f>`).
- Watch for mail in the **foreground**: `aw-meta watch --until-mail --for 29m`. It exits when mail waits (`INBOX <n> open message(s)`). Run `aw-meta inbox`, handle it, run the watch again. Background output is only shown when polled, so a background watch does not work.
- Trust the hooks with `/hooks` once.

### Card: Cursor
- Same as Codex: reply with `aw-meta lane-send`, watch in the foreground with `aw-meta watch --until-mail --for 29m`, handle `aw-meta inbox`, run it again.
- The hooks inject mail on session start and after tool calls; an empty `aw-meta inbox` is the settled case.

### Every harness
- **Trust the CLI.** Act on its output and trust the `FAIL` lines it prints as they stand. Don't open gate logs or step logs unless the CLI says a log is the only evidence, and don't open `docs/csharp/meta.md` for a step this skill already gives.
- Detail for a task that isn't a per-turn step (team setup, named teams, worktrees, health, resources, usage limits, routing, trains, the log) is in `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/docs/csharp/meta-skill-reference.md`. Open it for that task only. The full CLI reference is `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/docs/csharp/meta.md`.
- `aw-meta` is on PATH after `./setup.sh --with-meta` (else `$HOME/.agentic-workflow/bin/aw-meta`).

## The rule that runs everything: obligations

Every message a lane sends you opens an **obligation**: you owe that lane a reply. An owner answer owes the asking lane the answer. **While any obligation is open, the Stop hook won't let you go idle**: it prints what you owe and the command that settles it. Settle it, don't argue with it.

- **Reply** with your card's send method. The outbox hook records the send and closes the obligation.
- **Tag every message** you send on its first line, and put the same rule in every lane's kickoff prompt (`prompts/<lane>.md`): `STATUS:` for a line that needs nothing back (`STATUS: review clean on #123`), `ACTION:` for one that does (`ACTION: rebase #12 on main, it conflicts in EndpointTable`). A `STATUS:` line opens no reply even with a `?` in it, and the watch never wakes you for one: it prints `STATUS <n> new (lanes: a, b)` at most every 15 min, or adds it to the next `INBOX` line. Read them with `aw-meta inbox --status` (it marks them read). The protocol lines (`ready #`, `OK #`, `NOT OK #`, `HOLD #`, `DIGEST READY`, owner lines) are action with or without a tag, so `STATUS: ready #12 <sha>` still queues the pull request.
- **Notices** (untagged) open no reply and never block a lane's stop (`inbox --all` shows them `settled`). An untagged message is a notice when its first line starts `FYI:`, `NOTICE:`, `main moved` or `LANE MAP v<N>`, or it is a bare ack (`ack`, `thanks`, `noted`, `got it`, `roger`) under 240 bytes. It is never a notice when it is a protocol line or holds `please` or `?`.
- A non-Claude lane answers with a final line `REPLY TO META: <reply>`; that opens your reply obligation like any message. `lane-send` exit 1 means the message is stored but the lane can't act on it (launch-only, or its hooks aren't running).
- A pure FYI that needs no reply: `aw-meta inbox-dismiss <id> --reason "<why>"`.
- Work in flight (a gate, a review): `aw-meta inbox-defer <id> --reason "<what>" --until +30m`. It reopens when due.

## Start (or after any restart)

1. `aw-meta role-show`. If it isn't this session, `aw-meta role-set --session <this session's id> --name <this session's name>` (the launcher does this for the meta pane).
2. List agents, then `aw-meta lanes-refresh` and `aw-meta lanes`. Sessions named `<team>-<lane>` map themselves; anything else maps from the lane's announcement ("API lane: ...").
3. Start the watch from your card. If you forget, the Stop hook opens `rearm-watch`, and `aw-meta status` and `aw-meta health` print a `consumer:` line that says "no active coordinator consumer" while mail waits and no watch beats.
4. `aw-meta status` and `aw-meta health`.

## Watch events

Act on each line when it prints:
- `DISK LOW`: tell every lane to stop starting new builders; run `aw-meta worktree-sweep`; if disk is still low, ask the owner before deleting anything else (`aw-meta owner-ask`). `MEMORY LOW`: tell every lane to pause builders and heavy jobs. `DISK OK` / `MEMORY OK`: tell the lanes to resume.
- `CONTEXT HIGH <lane> <N>k`: ask the lane to write a digest at a safe point and reply `DIGEST READY <path>`, then `aw-meta lane-recycle <lane> --digest <path>`. For `META RECYCLE <N>k` (your own context) write a digest and run `aw-meta lane-recycle <meta lane> --digest <abs path> --detach`.
- `USAGE LIMITED <lane|meta> ...`: don't retry in a loop; run `aw-meta usage-wait` in the background. `USAGE OK <lane>` needs nothing from you; on `USAGE OK meta` resume your subagents, restart the watch and run `aw-meta usage`.
- `ROUTE SUGGEST ...`: run `aw-meta route` and follow the reference.
- `builders: N per lane` changes: tell the lanes. Run gates only through `aw-meta gate-run`, one at a time, and before each one run `meta-gate-reserve A` with the argument `aw-meta status` prints.

## A lane says `ready #N`

A lane queues a pull request with exactly `ready #N <sha>` on the **first line** of its message (any case; the sha, 7 to 40 hex digits, may sit in backticks), details below. A bare `ready #N`, a ready in the middle of a sentence, or a first line with "not ready" or "follows" in it is **ignored**: it is an ordinary request. Put this in every lane's kickoff prompt (`prompts/<lane>.md`). To drop a wrongly queued pull request: `aw-meta queue-drop N --reason "..."`.

1. `aw-meta merge-check N`. Read every FAIL line. A merge-order FAIL means wait: tell the lane why. With `review.lane` set in the team config, `merge-check` is READY on the reviewer's `OK #N at <sha>` at the head (not taken back by a later `HOLD #N`; on a UI pull request the line after it says `qa passed <sha>` at the head) plus the lane's ready message with a passing `N passed, M skipped, 0 failed` line, with no gate run: then `queue-gate` is optional, skip to step 4, and gate main periodically instead.
2. `aw-meta gate-run N` (never a hand-run gate). Scopes come from the diff and the team config; name them to override (`gate-run N repo`). Defer the lane's message while it runs. A docs-only delta on an already-gated head needs only the `always` scopes; the CLI picks that itself.
3. For each red test the PR doesn't touch: `aw-meta gate-rerun N --step <step> --test <filter>`. It reruns the test alone and on plain main, and records `flake` or `real`. Only `real` reds go back to the lane. A gate failure that only waited for a heavy lock is infrastructure: re-run the gate.
4. `aw-meta merge-check N` again (head and main must not have moved since the gate), then `aw-meta merge-run N`.
5. Send nothing about main moving: `merge-run` recorded it on the board, and each lane's prompt hook shows it once. Re-gate every other queued pull request after a merge. The `WORKTREES ...` line `merge-run` printed says what was removed and what was kept; a kept worktree is not an error.

**CI gate (`gate.mode` is `ci` in the team config).** Step 2 changes: `aw-meta gate-run N` runs nothing on this box. It reads the pull request's required checks, review decision and merge state from GitHub and records the answer against the head sha. It passes only when every required check is green, the review is `APPROVED` and the merge state is `CLEAN`; a pending check is a fail (`pending`), so run it again once CI finishes. **Skip every `meta-gate-reserve` step**: no gate tree is built and no heavy slot is taken. Skip step 3 (there are no local test reds to rerun; a red required check goes back to the lane). Then `aw-meta merge-check N` and `aw-meta merge-run N` as before: `merge-check` needs a passing CI record at the head the pull request has now, so a push after `gate-run` means run it again. If `gate-run` refuses with `AW-META-024` (`no required checks on <base>`), the base has no branch protection: ask the owner, never merge on it. `queue-gate` (the train) is a local gate and does not apply.

With several ready pull requests use `aw-meta queue-gate` (a train); the steps are in the reference.

## OWNER QUESTION messages

`aw-meta owner` lists the queue. Ask the user each question (the lane's recommended default first), then `aw-meta owner-answer <q> --answer "<the owner's words>"`, and send the printed `OWNER ANSWER` text back to the asking lane. Your own question for the owner: `aw-meta owner-ask --text "..."`.

## Toolkit defects

Whenever you'd write a NOTE that says you did something by hand, or you hit an aw-meta or harness defect, run `aw-meta report-defect --code <code> --title <title> --what <what happened> --error <exact error line> [--path <path>]` instead of only logging it. Lanes send you a `DEFECT:` line for aw-meta and harness defects; run `report-defect` for them. Lanes still file their own product bugs and UX issues; this command is only for toolkit defects.

The CLI dedups against open issues (and ones closed in the last 14 days), comments instead of filing a twin, files at most 3 new issues a UTC day, writes the NOTE and queues the owner FYI. Exit 1 means the cap held it back: the defect is in the log, and `aw-meta status` shows `issue cap hit`.

## Next steps

- `/review`: when a lane asks for a review run on its PR
- `/specToProvenPR`: the lanes' own workflow; point a lane at it when it skips proof

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
f="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/repo-slug.sh"; [ -f "$f" ] && . "$f" 2>/dev/null; echo "AW_DIR=${AW_DIR:-}"
[ "${AW_RESULT:-0}" = 1 ] && [ -z "${AW_DIR:-}" ] && echo "AW_DIR is empty: source skills/_shared/repo-slug.sh, then write the result file; never skip it" || true
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/meta-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) first, along with every other file the run writes; then open your final reply (the message after the last tool call) with the same JSON as a fenced `aw-result` block, before any prose and before `## Next steps`.

Keys: `skill` (`meta`), `artifacts` (absolute paths: the files under the team's running log it appended to), `summary` (one line),
plus `team` (team name); `actions` (count of aw-meta actions taken this turn); `merged` (array of PR numbers `aw-meta merge-run` reported merged, `[]` when none); `open_obligations` (count `aw-meta status` still shows).

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
