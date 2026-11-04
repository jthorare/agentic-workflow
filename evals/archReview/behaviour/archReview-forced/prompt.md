---
max_turns: 25
timeout_seconds: 600
allowed_tools: [Read, Glob, Grep, Write, Skill, Agent, TodoWrite, Bash, Edit]
tags: [archReview, behaviour, forced]
---

Use the archReview skill on this plan, which is short on purpose:

Plan: parking-permit service. A request to POST /permits writes the permit to Postgres and then publishes a `permit.issued` message to a queue. One billing worker (single thread, no concurrency) consumes the queue and calls the card processor's HTTP API to charge the applicant, then marks the permit paid. If the charge call fails the worker moves on to the next message. The processor client uses the library defaults. There is one Postgres primary and no replica. Rollout: deploy everything at once on Friday evening.

Write the review where the skill says to. In your reply, list the top risks it found.
