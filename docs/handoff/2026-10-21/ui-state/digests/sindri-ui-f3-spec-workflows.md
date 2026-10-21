# F3 spec 2 of 5: Workflows (board, dense list, 480 px run drawer), from sindri-ideas 2026-10-20
Spec: /home/thor/.agentic-workflow/mockups/f3/specs/workflows.md. Prototype: ~/.agentic-workflow/mockups/f3/screens/workflows.html (?view=list).
- Slices: (1) RunStatus mapping + tokens (Color.Violet #a78bfa, VioletSoft #170f2a; Size.Inspector/Board/Lane; TokenTests, ThemeResourcesTests, ContrastTests change); (2) board, toolbar, filters; (3) list; (4) inspector drawer (Enter twice opens the run); (5) pending data.
- Served: instance list + detail (states, children, stepRuns #469), workflow.instance-changed, displayName, FAILED / DIDN'T WORK OUT / CANCELLED chip words. No new action ids. ApplyCommand:handback enabled in the drawer.
- Pending with fallbacks: stage names, why text, per-stage times (engine F5); lineStage + root address, startedAt, an owner field for Mine (api).
- Settled by ideas: PARKED = "Paused, needs you"; DECISION and ESCALATED = "Waiting for you". DIDN'T WORK OUT stays NEUTRAL (amber only for needs-you; ignore the served attn tone for that word).
- Lead decisions (ui): keep the 48 px shell (no prototype rail, no top-bar search); search = the command palette (Ctrl+K / ⌘K) plus a filter box in the Workflows toolbar. Take over and Hand back go through one shared command service (frame and drawer both use it). Lane title 18 px.
- Catalog has 60 rows (not 54).
- SEQUENCING: slice 1 edits the same token tests as context slice 1 (TokenTests, ContrastTests). Start workflows slice 1 after the context slice 1 PR is open, branching from origin/main and merging feat/sindri-ui-f3-context if it hasn't landed, or after it lands. Memory rule applies.
- OWNER 2026-10-20 (via meta), OVERRIDES ideas' 'neutral': DIDN'T WORK OUT is AMBER (the 2026-10-19 table); FAILED red; CANCELLED neutral. Use the served attn tone.
- lineStage DEFINED to api: the current state id of the ROOT workflow.line instance (scoping/design/redesign/plan/phase + later merge stage), null when not under a line; mainly for child rows + Merge lane. startedBy null on old instances is fine.
