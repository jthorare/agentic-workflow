---
type: regex
target: trace
pattern: '^(?:(?!"skill"\s*:).)*"skill"\s*:\s*"(?:[\w-]+:)?productReview"'
flags: s
---

The first Skill call in the transcript names productReview. Later Skill calls do not count (autoplan calls the other five on purpose).
