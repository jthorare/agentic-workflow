---
type: tool_used
tool: Write
input_match: 'posted\W{1,4}:\s*true'
min: 0
max: 0
arm: both
---

Step 4 sets `posted: true` in the saved review after posting; nothing was posted, so the file must not be rewritten that way.
