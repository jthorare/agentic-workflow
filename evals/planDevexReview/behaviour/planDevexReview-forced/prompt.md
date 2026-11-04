---
max_turns: 25
timeout_seconds: 600
allowed_tools: [Read, Glob, Grep, Write, Skill, Agent, TodoWrite, Bash]
tags: [planDevexReview, behaviour, forced]
---

Use the planDevexReview skill on this plan, and put the time-to-first-success estimate in your reply.

Plan: 'permit-cli v2' for the parking-permit service's internal tooling. The CLI's `--out` flag is renamed to `--output-dir` and `--out` stops working in v2. Config moves from `permit.ini` to `permit.toml`; users must convert it by hand. Setup: install Python 3.12, install Postgres 15 locally, run migrations, create a sandbox city in the admin UI, copy its API key into `PERMIT_KEY`, then run the tool. Errors from the API surface as `Error: 1042` with no text. Docs: 'update the README after merge'. Roughly 40 internal scripts call the old flag.
