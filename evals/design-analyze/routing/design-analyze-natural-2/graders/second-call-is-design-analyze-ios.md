---
type: tool_order
before:
  tool: Skill
  input_match: '"skill"\s*:\s*"(?:[\w-]+:)?design-analyze"'
after:
  tool: Skill
  input_match: '"skill"\s*:\s*"(?:[\w-]+:)?design-analyze-ios"'
---

The dispatcher's Skill call comes first, then the member's.
