---
type: regex
target: trace
flags: s
pattern: '^(?:(?!"skill"\s*:)[\s\S])*"skill"\s*:\s*"(?:[\w-]+:)?rootCause"'
---

The first Skill call names rootCause; a later call (bugFixOrchestrator calls rootCause on purpose) doesn't count.
