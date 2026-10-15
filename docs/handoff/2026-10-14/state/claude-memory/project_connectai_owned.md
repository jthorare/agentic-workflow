---
name: connectai-owned
description: "Sindri's business-intent engine is Onyx (CE), not ConnectAI — the user decided 2026-10-10; ConnectAI (which the user owns) is not to be used"
metadata:
  node_type: memory
  type: project
  originSessionId: 01f9399c-c62c-4d21-a53c-b3818b4f85b6
  modified: 2026-10-11T05:08:48.831Z
---

Decided 2026-10-10 in the Sindri context-retrieval design: **Onyx (community edition, MIT outside `ee/`) is the first choice for business intent (Linear, Slack, docs). Don't use ConnectAI**, even though the user owns it (`joi-fairshare/connectai`, transferred to their account, so its BSL license was never the reason).

**Why:** the user's call. Sindri consumes Onyx unmodified through an adapter (its read-only MCP/HTTP search); the product graph and the cross-layer links stay Sindri-owned.
**How to apply:** don't propose ConnectAI as an adapter, arm or fallback in Sindri designs. This supersedes the "ConnectAI = work-laptop source of record" line in [[new-job-day1-kit]] for Sindri purposes.
