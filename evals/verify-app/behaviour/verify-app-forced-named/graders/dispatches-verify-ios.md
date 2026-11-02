---
type: tool_order
before:
  tool: Skill
  input_match: '"skill"\s*:\s*"(?:[\w-]+:)?verify-app"'
after:
  tool: Skill
  input_match: '"skill"\s*:\s*"(?:[\w-]+:)?verify-ios"'
---
