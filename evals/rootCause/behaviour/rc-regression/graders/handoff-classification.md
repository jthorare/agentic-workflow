---
type: llm
focus: last_message
---

PASS if the reply shows that the handoff file has a `## Classification` section holding the origin `regression (introduced by ...)` with PR #17 or its sha, with the revert listed first in the fix options. The reply may quote the section or say where it wrote it.
FAIL if there is no Classification section, or its origin is not a regression, or the revert is not first.
