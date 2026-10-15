---
name: parallel-agent-team
description: multi-stage /specToProvenPR epics run as an agent team — main session orchestrates, worker agents run stages in parallel lanes
metadata:
  type: feedback
---

For multi-stage epics (e.g. the Sindri app, 2026-10-11), plan and run the work as an **agent team**. The main session
orchestrates: it keeps lanes busy, sequences heavy gates, resolves cross-branch conflicts and holds the human gates.
Worker agents each run `/specToProvenPR` for one stage, in its own worktree, branch and PR. Stages whose contracts are
frozen start in parallel (stacked PRs where one builds on another) instead of waiting for the previous merge.

**Why:** the owner wants agents always working, with the main session focused on orchestration rather than writing
stage code itself.

**How to apply:** put a "Parallel execution" section in the epic's stages.md (lanes, what each waits on, kickoff). Cap
concurrent builds at the heavy-lock slots ([[heavy-lock-two-slots]]), check open branches pairwise with
`git merge-tree`, and never let a worker merge. Subagents share the session's scratchpad dir: give each lane its own
subdirectory (`scratchpad/<stage>-lane<X>/`) in its brief from the start. On 2026-10-11 lanes overwrote the orchestrator's
gate script and stopped each other's hand-test serve through a shared `scratchpad/hand`.

**Reinforced 2026-10-14 (owner):** a lane orchestrator does no stage work itself, including running state checks the
lead agents can run. Use one team per issue/phase: a lead agent runs `/specToProvenPR` with its own builders. A separate
meta orchestrator watches the GitHub PR queue, merges, and messages the lane. The lane orchestrator dispatches, unblocks
and relays "ready #N".
