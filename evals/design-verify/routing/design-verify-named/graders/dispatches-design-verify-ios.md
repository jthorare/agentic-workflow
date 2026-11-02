---
type: tool_order
before:
  tool: Skill
  input_match: '"skill"\s*:\s*"(?:[\w-]+:)?design-verify"'
after:
  tool: Skill
  input_match: '"skill"\s*:\s*"(?:[\w-]+:)?design-verify-ios"'
---

design-verify fires first, then dispatches to the iOS member the prompt names.
