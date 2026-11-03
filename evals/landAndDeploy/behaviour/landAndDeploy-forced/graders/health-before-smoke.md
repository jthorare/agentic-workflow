---
type: tool_order
before:
  tool: Bash
  input_match: 'curl[^"]*toollib\.example\.test/health'
after:
  tool: Bash
  input_match: 'curl[^"]*toollib\.example\.test/api/'
---

Smoke tests run after the health check.
