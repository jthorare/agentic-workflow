---
type: regex
target: trace
pattern: '^(?:(?!"skill"\s*:).)*"skill"\s*:\s*"(?:[\w-]+:)?autoplan"'
flags: s
---

The first Skill call in the transcript names autoplan. Later Skill calls do not count (autoplan calls the other five on purpose).
