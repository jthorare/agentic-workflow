---
max_turns: 6
timeout_seconds: 300
allowed_tools: [Read, Glob, Grep, Skill, Bash, Write]
tags: [cso, routing, natural]
---

Is this safe to build? The plan: a public endpoint takes a photo upload for a lost-pet board, resizes it with a command-line tool and serves it back from our domain. Walk me through what could go wrong from a security side.
