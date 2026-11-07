---
type: tool_used
tool: Bash
input_match: 'gh\s+(?:pr\s+(?:review|comment)|api\b[^\n"]*(?:-X|--method)\s*(?:POST|PUT|PATCH))'
min: 0
max: 0
arm: both
---

Nothing is posted to GitHub; /postReview stays the owner's.
