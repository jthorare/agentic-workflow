---
name: review-loop-severity
description: "In /specToProvenPR review loops, fix issues only; resolve suggestions/nits in writing without forcing another fix+review cycle"
metadata:
  node_type: memory
  type: feedback
  originSessionId: 73e60e77-78ca-4e58-9205-c43e24302bf2
  modified: 2026-10-11T03:01:18.886Z
---

Owner (2026-10-10, PR #148): "we don't need to fix nits / suggestions everytime."

**Why:** the zero-at-every-severity loop kept spending review runs on edge-case suggestions and wording nits after all real issues were gone (#148 runs 3–4 were 0 issues, 6 suggestions, 2 nits).

**How to apply:** fix `issue`s, plus any finding whose substance is data loss, broken behaviour or a false claim in a doc people rely on, whatever its label (reviewers mislabel: #148 run 3's "suggestion" would have deleted uncommitted work). Other suggestions/nits: reply on the thread with the reason, mark resolved in reviews/<pr>.json, and don't re-run review just for them; fold in only trivial one-line doc fixes. Overrides the skill's "zero means zero" rule for this owner. **Stop rule (owner, 2026-10-11, PR #158):** "we don't need to go to 0 findings, just 0 issues or blocking" — the loop ends on the first `/review` run with 0 `blocking` and 0 `issue`; leftover suggestions/nits get written replies, no further run. **Cap (owner, 2026-10-11, Sindri S1):** first "a max of 3 total times through the loop", then the same day "run it max 2 more times for 5 total": at most 5 `/review` runs per PR, the first one included. The loop still ends at the first run with 0 blocking + 0 issue. If one is still open after run 5, stop and ask the owner with the findings verbatim. Related: [[review_shell_hooks_overflagging]], [[parallel-agent-team]].
