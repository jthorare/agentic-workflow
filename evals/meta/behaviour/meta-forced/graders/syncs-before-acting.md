---
type: tool_order
before:
  tool: Bash
  input_match: 'aw-meta (inbox-sync|status|inbox)\b'
after:
  tool: Bash
  input_match: 'aw-meta (gate-run|merge-check|merge-run)\b'
---
