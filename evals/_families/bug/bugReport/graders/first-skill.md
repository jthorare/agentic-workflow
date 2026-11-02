---
type: regex
target: trace
flags: s
pattern: '^(?:(?!"skill"\s*:)[\s\S])*"skill"\s*:\s*"(?:[\w-]+:)?bugReport"'
---

The first Skill call names bugReport; a later call (bugFixOrchestrator calls rootCause on purpose) doesn't count.
