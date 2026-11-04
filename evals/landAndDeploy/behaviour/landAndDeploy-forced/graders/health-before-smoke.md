---
type: tool_order
before:
  tool: Bash
  input_match: 'curl[^"]*toollib\.example\.test/health'
after:
  tool: Bash
  input_match: '(?=[\s\S]*\bcurl\b)(?=[\s\S]*toollib\.example\.test/)(?=[\s\S]*\bapi/)'
---

Smoke tests run after the health check. The `after` match takes the smoke URL anywhere in the Bash command, so a loop such as `for u in api/ping ...; do curl https://toollib.example.test/$u; done` counts.
