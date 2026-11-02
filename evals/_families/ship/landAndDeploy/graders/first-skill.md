---
type: regex
pattern: '^(?:(?!"skill"\s*:)[\s\S])*"skill"\s*:\s*"(?:[\w-]+:)?landAndDeploy"'
flags: s
target: trace
---

The first Skill call in the transcript names landAndDeploy. Later Skill calls do not count.
