---
type: tool_order
before:
  tool: Bash
  input_match: 'gh pr view'
after:
  tool: Bash
  input_match: 'scripts/deploy\.sh'
---

The merge state is read before the deploy command runs.
