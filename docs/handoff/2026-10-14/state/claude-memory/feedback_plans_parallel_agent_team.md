---
name: plans-parallel-agent-team
description: "Design docs' Phases must include a parallelization plan so /specToProvenPR runs them with an always-busy agent team"
metadata:
  node_type: memory
  type: feedback
  originSessionId: 25bf1337-c236-4aad-9d71-4982c7055be2
  modified: 2026-10-11T05:57:12.074Z
---

Every design doc headed for /specToProvenPR gets a "Parallel execution with an agent team" section under Phases:
the phase dependency graph and waves, a contract unit first per phase then disjoint-write-scope units, hotspot files
owned by an integrator (reserved error-code and migration ranges), and team roles (orchestrator that never codes and
dispatches the next ready unit immediately, implementers in worktrees, integrator, prover, reviewers).

**Why:** the user wants agents always working and the session focused on orchestrating an agent team, not
running phases one at a time (asked 2026-10-10 on the Sindri workflow engine design).

**How to apply:** cut phases so independent ones run concurrently; respect the two-slot heavy lock
([[heavy-lock-two-slots]]) by limiting concurrent builds, not idle agents. See [[review-loop-severity]] for the loop stop rule.
