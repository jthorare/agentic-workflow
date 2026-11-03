---
max_turns: 25
timeout_seconds: 600
allowed_tools: [Read, Glob, Grep, Write, Skill, Agent, TodoWrite]
tags: [cso, behaviour, forced]
---

Use the cso skill in plan mode on this plan:

Plan: 'Permit lookup API' for a city parking-permit service. GET /api/permits/{permitId} returns the permit with the holder's full name, home address and licence plate; any signed-in user may call it, and permit ids are sequential integers. Search is GET /api/permits?plate=<text>, implemented by building the SQL string from the `plate` parameter. Holders upload a proof-of-residence PDF to a public cloud-storage bucket and the file name is the permit id. Passwords are stored as unsalted SHA-1. No audit logging is planned.

When it finishes, paste the report's Summary and Coverage table into your reply.
