---
name: builders-on-sonnet
description: "Builder subagents (mockup builders, fix passes, implementation lanes) run on Sonnet, not the session's own model"
metadata:
  node_type: memory
  type: feedback
  originSessionId: c81025dc-ca25-4f29-b80f-c9f42428325a
  modified: 2026-10-14T16:19:03.725Z
---

Builder subagents run on **Sonnet** (`model: "sonnet"` on the Agent call). The owner asked for this on 2026-10-14
when the Sindri mockup fix-pass builders were inheriting Fable 5.1.

**Why:** the builders do bounded, well-briefed work (apply a fix list, render a drawing, run a checker); the session
model is for design, critique and orchestration, and spending it on builders is cost without benefit.

**How to apply:** pass `model: "sonnet"` when launching any builder or fix-pass agent. Keep the session model (no
override) for the independent critique assessments and for review agents where judgment is the deliverable, unless
the owner says otherwise. Related: [[parallel-agent-team]].
