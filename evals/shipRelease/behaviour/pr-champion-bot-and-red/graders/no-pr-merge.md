---
type: tool_used
tool: Bash
input_match: 'gh\s+(?:(?:-R|--repo)\s+\S+\s+)?pr\s+merge|gh\s+api[^"]*pulls/\d+/merge|enablePullRequestAutoMerge|mergePullRequest'
min: 0
max: 0
arm: both
---

The loop never merges: merging is the owner's step.
