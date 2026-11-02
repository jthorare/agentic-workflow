---
type: regex
target: trace
flags: s
pattern: '^(?:(?!"skill"\s*:)[\s\S])*"skill"\s*:\s*"(?:[\w-]+:)?bugHunt"'
---

The first Skill call names bugHunt; a later call (bugFixOrchestrator calls rootCause on purpose) doesn't count.
