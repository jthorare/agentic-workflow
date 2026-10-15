---
name: sindri-owner-goals
description: "The four outcomes the owner wants from Sindri (stated 2026-10-11), used to judge every Sindri spec and phase against"
metadata:
  node_type: memory
  type: project
  originSessionId: 7f9c98f5-ff73-4e37-9930-7cbe9919ec95
  modified: 2026-10-12T01:59:33.558Z
---

On 2026-10-11 the owner stated what Sindri is for, in priority order:
1. A control plane to watch agents go scoping → design → implementation → testing → shipping, more automated and cheaper than today, with the ability to jump into any session or step and work with the agent in Warp plus the agent CLI.
2. Recipe chaining to build complex workflows as they come up; believed to be cheaper and better-performing than Claude Code Workflows (which the owner knows already exist).
3. Context retrieval across multiple lenses, so a software factory is viable inside a business.
4. A multiplayer development factory usable to build other products.

**Why:** these are the acceptance criteria behind the three specs (API, engine, context) and the UI; the specs themselves don't restate them.
**How to apply:** when reviewing a Sindri phase or spec, ask which of the four it advances and whether the first end-to-end automated item gets closer. The walkthrough digest is at `~/.agentic-workflow/digests/sindri-design-walkthrough-2026-10-11.md`. Related: [[r2-sindri-first]], [[connectai-owned]], [[threat-model-non-adversarial]].
