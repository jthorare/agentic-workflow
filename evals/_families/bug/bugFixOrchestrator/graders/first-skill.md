---
type: regex
target: trace
flags: s
pattern: '^(?:(?!"skill"\s*:)[\s\S])*"skill"\s*:\s*"(?:[\w-]+:)?bugFixOrchestrator"'
---

The first Skill call names bugFixOrchestrator; a later call (bugFixOrchestrator calls rootCause on purpose) doesn't count.
