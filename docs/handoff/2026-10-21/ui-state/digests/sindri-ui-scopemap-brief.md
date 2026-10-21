# Brief: styled scope-map fact viewer (demo, owner demos 2026-10-19)

Worktree: create ~/repos/aw-wt/sindri-ui-scopemap on a new branch feat/sindri-ui-scopemap-viewer from origin/main (`git -C ~/repos/agentic-workflow worktree add -b feat/sindri-ui-scopemap-viewer ~/repos/aw-wt/sindri-ui-scopemap origin/main`).
Rules: ~/.agentic-workflow/digests/sindri-ui-builder-rules.md (binding). The lane shares ONE heavy slot: batch each heavy run as one script under `timeout 5400 ~/.agentic-workflow/bin-r1b/heavy bash <script>`.
Digest: ~/.agentic-workflow/digests/sindri-ui-scopemap.md.

Goal: a `scope-map` fact in the instance frame's facts panel draws as a readable table instead of the generic payload tree. Read-only, small, cheap.

Where: `src/Sindri/Aw.Sindri.Ui/ViewModels/Facts/` (FactViewerRegistry's doc says U13 registers ScopeMap there, in its own files, with a view in FactViewTemplates). Follow PayloadFactViewer / TextFactViewer for how a viewer reads `GET …/facts/{key}`.

Shape: two shapes exist on main and the viewer must read both, without making the fact unreadable:
- the API golden `tests/Aw.Sindri.Tests/Golden/Api/workflow-instance-fact/scope-map.json`: body.surfaces[] {name, graph, files[], items[]}, plus body.brief;
- the scoping schema `schemas/scoping/scope-map.json` ($defs.fact), which is what a live workflow.scoping run writes: surfaces[] {id, key, kind, title, detail, citations[], lens, graph (mapped|new|unmapped…)}, plus implications, workstreams, flagged, excluded, unmapped, unresolved.
Read it as a JSON tree (tolerant), never with a strict record. Title = title ?? name; show id, kind, lens, the graph status as a chip (▣ mapped / ◇ new / ░ unmapped, per screens spec U13; any other word is drawn raw), and a files/citations count. Counts line on top ("4 surfaces · 2 mapped · 1 new · 1 unmapped"). Then short sections for workstreams, flagged, excluded and unresolved when present (title plus one line each). Keep a "Show raw" toggle that falls back to the existing payload tree. If the body has no surfaces array, use the payload viewer.

A11y: Border wrapper pattern for text names; peer-tree walk test. Labels for every word in Labels.*.

Tests (TDD): unit tests over both shapes (the golden, plus a schema example `schemas/scoping/examples/scope-map.fact.valid.json`), an unknown graph word, no surfaces → payload. Add at most ONE integration frame (the Ui integration lane budget is 90 s).
Gate: the lane gate. Then a PR (not a draft) whose body ends with the 🤖 line and the session link. Report the sha; the lead sends ready.
Docs: a short "Scope-map viewer (demo slice of U13)" paragraph in docs/csharp/sindri-ui.md under U8's facts.
