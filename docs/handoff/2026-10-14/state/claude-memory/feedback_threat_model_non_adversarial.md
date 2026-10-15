---
name: threat-model-non-adversarial
description: "Agent-harness gates (bugfix-state etc.) guard against honest mistakes, not a deliberate bad-actor agent — don't chase exploit-style review findings"
metadata:
  node_type: memory
  type: feedback
  originSessionId: 3819d9ee-9dde-4a9d-a18f-3b1c89d90f8b
  modified: 2026-10-11T02:48:25.460Z
---

The software factory's gates (e.g. bugfix-state's frozen check, clean tree, command classification) protect against an agent's honest mistakes and lazy shortcuts (editing the test, running the wrong command, a dirty tree), not against an adversarial agent crafting exploits. The harness is private and agents work under guidance.

**Why:** On PR #155 (R2 BugFix) review runs 5–8 each found new "bypass" exploits (shadowing modules, attached `-c'…'` flags, planted leftovers); the owner said this was splitting hairs — bad-actor protection on a non-public harness — after ~4 rounds of hardening churn.

**How to apply:** When reviewers report crafted-exploit bypasses of a harness gate, treat them as out of the threat model: resolve in writing (document the threat model / residual risk), don't loop on fixes. Still fix anything that breaks legitimate flows or that an honest agent could plausibly hit by accident. Tell security reviewers the threat model up front. Related: [[review-loop-severity]].
