---
type: regex
pattern: 'resolveReviewThread|addPullRequestReviewThreadReply|addPullRequestReviewComment|pulls/\d+/comments/\d+/replies|gh pr comment'
target: trace
---

The bot's thread gets a reply or a resolve: either an attempted call (refused under readonly, still in the trace) or a line under `Would run:`. Only a write names these (the canned thread reads do not), so a run that reads the thread and moves on fails.
