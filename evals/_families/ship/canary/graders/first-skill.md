---
type: regex
pattern: '^(?:(?!"skill"\s*:)[\s\S])*"skill"\s*:\s*"(?:[\w-]+:)?canary"'
flags: s
target: trace
---

The first Skill call in the transcript names canary. Later Skill calls do not count.
