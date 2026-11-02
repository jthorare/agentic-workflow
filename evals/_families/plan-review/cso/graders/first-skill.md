---
type: regex
target: trace
pattern: '^(?:(?!"skill"\s*:).)*"skill"\s*:\s*"(?:[\w-]+:)?cso"'
flags: s
---

The first Skill call in the transcript names cso. Later Skill calls do not count (autoplan calls the other five on purpose).
