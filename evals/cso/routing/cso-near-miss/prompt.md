---
max_turns: 6
timeout_seconds: 300
allowed_tools: [Read, Glob, Grep, Skill]
tags: [cso, routing, near-miss, archReview]
---

Look at the design for our nightly report exporter: a scheduler, a worker pool, a results table and an S3 upload. I want to know where the component boundaries are, what happens when the worker pool is overloaded, and where the first bottleneck will be.
