---
max_turns: 25
timeout_seconds: 600
allowed_tools: [Read, Glob, Grep, Write, Skill, Agent, TodoWrite, Bash, Edit]
tags: [autoplan, behaviour, forced]
---

Use the autoplan skill on this plan:

Plan: 'Order notes' for a small shop app. Each order gets at most one note from the packer. The existing `orders` table has `id`, `customer_id`, `status`. The plan adds a new table `order_note_link(order_id, note_id)` that joins `orders` to a new `notes(id, text)` table. Nothing else reads notes. The plan gives no reason a column on `orders` could not hold the note. Delivery is one PR. Scope mode: mvp.

When it finishes, paste the architecture lens findings into your reply.
