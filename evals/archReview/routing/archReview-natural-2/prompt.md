---
max_turns: 6
timeout_seconds: 300
allowed_tools: [Read, Glob, Grep, Skill, Bash, Write]
tags: [archReview, routing, natural]
---

We're replacing the recipe app's LIKE-query search with a separate indexer service fed from a queue, and the web tier calls it over HTTP. Can you go through the design for weak spots before we start: what happens when the indexer is down, when the queue backs up, at ten times today's traffic?
