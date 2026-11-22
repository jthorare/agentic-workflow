---
type: tool_used
tool: Write
input_match: '"file_path"\s*:\s*"[^"]*stages\.md"[\s\S]*\\n#{2,4}\s*Ph\d+\b[^\\]*(?:Performance|Bug) sweep'
min: 0
max: 0
arm: both
---

The performance sweep and the bug sweep are end checks, not PR stages: stages.md gives neither a `## Ph<n>` stage heading (specToProvenPR step 0.5, "The closing sweeps are not stages").
