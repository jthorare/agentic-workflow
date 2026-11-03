---
type: regex
pattern: '```aw-result(?=[\s\S]*"skill"\s*:\s*"archReview")(?=[\s\S]*"artifacts"\s*:\s*\[)(?=[\s\S]*"summary"\s*:)'
flags: s
---

The reply carries the fenced aw-result block with skill, artifacts and summary.
