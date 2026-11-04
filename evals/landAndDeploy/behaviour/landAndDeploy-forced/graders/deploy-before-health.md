---
type: tool_order
before:
  tool: Bash
  input_match: 'scripts/deploy\.sh'
after:
  tool: Bash
  input_match: 'curl[^"]*toollib\.example\.test/health'
---

Health is polled after the deploy, not before.
