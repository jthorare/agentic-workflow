---
max_turns: 8
timeout_seconds: 300
allowed_tools: [Read, Glob, Grep, Skill]
tags: [system-design, routing, near-miss, archReview]
---

Here is our design for the seed catalogue sync: a cron job pulls the supplier CSV every night, loads it into a staging table and then swaps it with the live table in one transaction. If the CSV is half-written the job just retries at the next run. Where are the weak spots, and what breaks when the supplier file doubles in size?
