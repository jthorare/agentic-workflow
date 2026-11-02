---
type: tool_used
tool: Bash
input_match: 'gh pr (create|ready|merge|comment)|git (commit|push|merge)\b|git worktree add|git (checkout|switch) (-b|-c)\b'
min: 0
max: 0
arm: both
---

AW_GIT_MODE=readonly: no branch, commit, push, worktree or PR call is made.
