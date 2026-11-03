---
type: regex
target: trace
pattern: '^(?:(?!"skill"\s*:).)*"skill"\s*:\s*"(?:[\w-]+:)?archReview"'
flags: s
---

The first Skill call in the transcript names archReview. Later Skill calls do not count (autoplan calls the other five on purpose).
