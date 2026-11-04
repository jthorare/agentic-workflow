---
type: tool_used
tool: Write
input_match: '"file_path"\s*:\s*"[^"]*planning/TESTING\.md"'
min: 0
max: 0
arm: both
---

Only a Write whose target is planning/TESTING.md counts. AGENTS.md and the result file name that path in their content, and that is
not a write to it.
