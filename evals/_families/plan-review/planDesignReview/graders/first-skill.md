---
type: regex
target: trace
pattern: '^(?:(?!"skill"\s*:).)*"skill"\s*:\s*"(?:[\w-]+:)?planDesignReview"'
flags: s
---

The first Skill call in the transcript names planDesignReview. Later Skill calls do not count (autoplan calls the other five on purpose).
