---
max_turns: 6
timeout_seconds: 300
allowed_tools: [Read, Glob, Grep, Skill, Bash, Write]
tags: [productReview, routing, near-miss, archReview]
---

Our recipe app plan puts search behind a new indexer service with a queue between the web tier and the index. Review that design: component boundaries, what fails when the queue backs up, and what a sequence diagram of a search would show.
