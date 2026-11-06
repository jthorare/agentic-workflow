---
type: tool_used
tool: Bash
input_match: '\bgh\s+(?:pr|api|repo)\b'
min: 0
max: 0
arm: both
---

Under AW_RESULT_SKILL review reads the PR from the local ref the host fetched ($AW_SINDRI_PR_REF); the Step has no GitHub network, so it makes no gh call.
