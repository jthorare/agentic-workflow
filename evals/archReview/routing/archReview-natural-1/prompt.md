---
max_turns: 6
timeout_seconds: 300
allowed_tools: [Read, Glob, Grep, Skill]
tags: [archReview, routing, natural]
---

I wrote up how I want to build the "ready to renew" row for the permit desk. The nightly job can't read the renewal rules, so the row gets computed when the page loads instead: skip suspended permits, skip ones that already have a review open, and if the lookup fails just drop that one row, never the whole list. A row that was already stored under the same id wins. Before I hand this to anyone: does that design hold together technically, and where will it bite me?
