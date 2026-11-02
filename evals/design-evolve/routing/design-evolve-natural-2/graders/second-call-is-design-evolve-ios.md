---
type: tool_order
before:
  tool: Skill
  input_match: '"skill"\s*:\s*"(?:[\w-]+:)?design-evolve"'
after:
  tool: Skill
  input_match: '"skill"\s*:\s*"(?:[\w-]+:)?design-evolve-ios"'
---

The dispatcher's Skill call comes first, then the member's.
