---
type: regex
pattern: '^(?:(?!"skill"\s*:)[\s\S])*"skill"\s*:\s*"(?:[\w-]+:)?review"'
flags: s
target: trace
---

The first Skill call in the transcript names review. Later Skill calls do not count.
