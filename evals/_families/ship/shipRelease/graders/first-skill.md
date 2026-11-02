---
type: regex
pattern: '^(?:(?!"skill"\s*:)[\s\S])*"skill"\s*:\s*"(?:[\w-]+:)?shipRelease"'
flags: s
target: trace
---

The first Skill call in the transcript names shipRelease. Later Skill calls do not count.
