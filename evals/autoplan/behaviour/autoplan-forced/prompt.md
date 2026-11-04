---
max_turns: 25
timeout_seconds: 600
allowed_tools: [Read, Glob, Grep, Write, Skill, Agent, TodoWrite, Bash, Edit]
tags: [autoplan, behaviour, forced]
---

Use the autoplan skill on this plan:

Plan: 'Share a class waitlist' for a climbing-gym app. Members open a share link to a class and anyone with the link can see the member list and phone numbers. The link is `https://gym.example/classes/812/share?token=<member api token>`, so it works without signing in. Staff get a new tab in the admin app with a table of waitlists; the plan lists no loading, empty or error states for it. Delivery is one PR touching the API, the admin app and the SMS provider config, estimated at four days; no README or API docs are planned. Scope mode: mvp.

When it finishes, paste the lens status table and the overall verdict into your reply.
