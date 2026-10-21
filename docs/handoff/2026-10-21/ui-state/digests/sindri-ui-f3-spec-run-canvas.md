# F3 spec 3 of 5: run canvas + step inspector, from sindri-ideas 2026-10-20
Spec: /home/thor/.agentic-workflow/mockups/f3/specs/run-canvas.md. Prototype: ~/.agentic-workflow/mockups/f3/screens/run-canvas.html (?run=…, #fail).
- Slices: (1) ZoomBorder + edges extensions (MinZoom/MaxZoom/FitBounds; edges control renamed GraphEdgesControl with PortSide); (2) pure deterministic RunCanvasLayout + graph model with fallbacks; (3) page, stage cards, RUNS list, zoom pill, MiniMapControl, keys; (4) inspector with the stepRuns timeline; (5) failed / didn't-work-out / cancelled states.
- Reuses InstanceFrameViewModel, StateChipView, CommandControl, ProtectedActionButton, ChildGroupViewModel, WorkflowNames. InstanceFrameView stays for non-line recipes.
- Tokens: none of its own; "You took over" = Violet/VioletSoft (workflows spec), plus Wire/WireHi (context).
- Served: stepRuns, chip words, displayName (nullable), instance-changed, states, children, facts. InstanceEvents has NO golden yet: the ui adds a contract fixture for it (in slice 2 or 3). Pending with fallbacks: labelled transitions and state why (engine F5), per-step needs/produced, a finished-runs list (api), takeover on terminal instances.
- New action id: only "Run again" (protected terminal copy, or ApplyCommand:command with a verb); check with api BEFORE adding.
- Settled: DIDN'T WORK OUT neutral (api changing the served tone); a phase with several children shows the first not-done child + "Show the other N phases".
- Lead answers: "Approve the merge" is NOT hard-coded; the button renders the served StatusAnswer.ActionId/Class/Cli for the current step, and with none served it shows the terminal copy (command + Copy). Whether the line's last step serves one is engine/api's (the PR-watching stage is a stub); no ui catalog row added. Radius 12 (same as context). Fit floors at 30%.
- SEQUENCING: slice 1 extends context slice 1's controls; start after the context slice 1 PR is open (merge its branch) or landed.
- "Run again" SETTLED BY API (2026-10-20): NO new row and NOT ApplyCommand:command.
  Case 1, a failed step with the instance PARKED (needs-answer, kind step): reuse ApplyCommand:answer-text (desk; `sindri workflow answer <address> --text`) and ApplyCommand:answer-resume (`--resume`) where the park allows it; budget/rounds parks use the protected answer-grant-tokens / answer-grant rows. Label it "Run again" ONLY when the park frame's resume kind is rerun.
  Case 2, the instance/child ENDED failed or didn't work out: no restart verb exists; a protected ApplyCommand:restart would be engine's call (api asked engine). Until engine answers, draw case 2 WITHOUT the action.
- ENGINE ANSWER (2026-10-20), supersedes api's case 2: terminal instances are FINAL by design (no restart; spec `restart` only leaves one PARKED kind, SND-WF-010).
  Top-level ended failed: "Run again" = start a NEW instance of the same recipe with the same inputs via the existing workflow start (WorkflowStart row); the old run stays, new address. Engine adds an optional `again: <address>` on start (lead said yes, low priority, not on main): until then, no "again of" link.
  A child that ended failed: NO button on the child; the parent decides (its recipe routes it, or the parent parks -> the parent's park row).
  A parked failed Step: unchanged (api's ids).
  A true "re-open the same run" would be an owner product change via meta: don't build it.
- Run again, top-level ended failed: ideas' spec now says not drawn until engine adds `again:`. Engine said start works today and `again:` only adds the 'again of' link; the real gate is whether the UI can rebuild the old run's inputs from served facts (FactRow may carry refs, not values). Check at run-canvas slice 5 and ask engine; until then, not drawn (OK).
- OWNER 2026-10-20 (via meta), OVERRIDES ideas' 'neutral': DIDN'T WORK OUT is AMBER (the 2026-10-19 table); FAILED red; CANCELLED neutral. Use the served attn tone.
- SETTLED: per-step needs/produces will be served on the instance detail as steps[].needs (fact keys) and steps[].produces (FactRef[]) by api (builder-wf-rows); until then the 'For the whole stage' fallback.
