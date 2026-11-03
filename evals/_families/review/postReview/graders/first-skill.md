---
type: regex
pattern: '^(?:(?!"skill"\s*:)[\s\S])*"skill"\s*:\s*"(?:[\w-]+:)?postReview"'
flags: s
target: trace
---

The first Skill call in the transcript names postReview. Later Skill calls do not count.
