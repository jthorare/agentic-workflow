---
type: tool_used
tool: Bash
input_match: 'git (?:commit|push|checkout -b|worktree add)|gh pr (?:create|merge|comment|revert)'
min: 0
max: 0
arm: both
---

AW_GIT_MODE=readonly: the skill makes no branch, commit, push, merge or PR comment.
