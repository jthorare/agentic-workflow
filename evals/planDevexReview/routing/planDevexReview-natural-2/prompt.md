---
max_turns: 6
allowed_tools: [Read, Glob, Grep, Skill]
tags: [planDevexReview, routing, natural]
---

We're changing our public SDK so `client.fetch(id)` becomes `client.get(id, options)` and old config files need converting. Go through the plan from the point of view of the developers who'll have to adopt it: setup steps, error messages, what happens to existing users.
