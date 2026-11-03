---
type: regex
pattern: '^(?:(?!"skill"\s*:)[\s\S])*"skill"\s*:\s*"(?:[\w-]+:)?addressReview"'
flags: s
target: trace
---

The first Skill call in the transcript names addressReview. Later Skill calls do not count.
