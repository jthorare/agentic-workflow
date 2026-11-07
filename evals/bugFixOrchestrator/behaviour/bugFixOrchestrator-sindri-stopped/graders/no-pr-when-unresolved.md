---
type: tool_used
tool: Bash
input_match: 'gh\s+pr\s+create|git\s+push'
min: 0
max: 0
arm: both
---

Only a `resolved` run pushes and opens a draft PR; an unresolved stop pushes nothing and leaves `pr` out.
