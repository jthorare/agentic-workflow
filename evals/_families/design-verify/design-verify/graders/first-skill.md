---
type: regex
target: trace
pattern: '^(?:(?!"skill"\s*:)[\s\S])*"skill"\s*:\s*"(?:[\w-]+:)?design-verify"'
---

Family set (spec 3.1a): the FIRST Skill call in the trace must name `design-verify`. The pattern is anchored at the start of the trace and refuses to cross any earlier `"skill":`, so a later call cannot satisfy it; the closing quote keeps `design-verify` from matching its members.
