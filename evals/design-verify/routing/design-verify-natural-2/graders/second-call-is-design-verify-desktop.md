---
type: tool_order
before:
  tool: Skill
  input_match: '"skill"\s*:\s*"(?:[\w-]+:)?design-verify"'
after:
  tool: Skill
  input_match: '"skill"\s*:\s*"(?:[\w-]+:)?design-verify-desktop"'
---

The dispatcher's Skill call comes first, then the member's.
