---
max_turns: 25
timeout_seconds: 420
allowed_tools: [Read, Write, Glob, Grep, Skill, Bash, Edit]
tags: [officeHours, behaviour, forced]
---

Use the officeHours skill on this feature for a plant-watering reminder app: "household sharing, so everyone in a home sees the same plant list and reminders go to whoever watered last".

I'm away, so don't stop to ask me anything. These are my answers to your discovery questions:
- Problem: couples and roommates each assume the other one watered, so plants die or get drowned.
- Who and when: anyone sharing a home; it surfaces when a plant becomes due (an event) and while a plant is overdue (a state).
- Today: a group chat and sticky notes. It fails when someone forgets to post, or two people water the same plant on the same day.
- Feel: effortless and fair, one tap to log a watering.
- EARS types: 1, 2 and 5 (ubiquitous, event-driven, unwanted).
- Unwanted case: if a reminder push notification fails to deliver, the reminder must still be waiting in the app the next time it opens, never silently dropped.
- Approach: the simplest one, a shared household record on our existing backend, with a scheduled job for due dates.
- Success: 70% of two-person households log a watering from both accounts within 30 days.

Write the whole plan directory.
