---
type: tool_used
tool: Write
input_match: '"file_path"\s*:\s*"[^"]*stages\.md"[\s\S]*\\n##[ \t]+Ph\d+\b[^\\]*(?:Performance|Bug) sweep'
min: 0
max: 0
arm: both
---

The performance sweep and the bug sweep are end checks, not PR stages: stages.md gives neither a `## Ph<n>` stage heading. A `### Ph<n>` line under `## Closing checks` is fine (specToProvenPR step 0.5, "The closing sweeps are not stages").
