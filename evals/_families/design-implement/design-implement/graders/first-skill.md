---
type: regex
target: trace
pattern: '^(?:(?!"skill"\s*:)[\s\S])*"skill"\s*:\s*"(?:[\w-]+:)?design-implement"'
---

Family set (spec 3.1a): the FIRST Skill call in the trace must name `design-implement`. The pattern is anchored at the start of the trace and refuses to cross any earlier `"skill":`, so a later call cannot satisfy it; the closing quote keeps `design-implement` from matching its members.
