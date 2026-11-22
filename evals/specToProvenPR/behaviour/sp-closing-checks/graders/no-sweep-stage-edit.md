---
type: tool_used
tool: Edit
input_match: '"file_path"\s*:\s*"[^"]*stages\.md"[\s\S]*"new_string"\s*:\s*"(?:(?:[^"\\]|\\.)*\\n)?##[ \t]+Ph\d+\b[^\\]*(?:Performance|Bug) sweep'
min: 0
max: 0
arm: both
---

The same rule for an Edit of stages.md: no `## Ph<n>` stage heading for either sweep (a `### Ph<n>` line passes).
