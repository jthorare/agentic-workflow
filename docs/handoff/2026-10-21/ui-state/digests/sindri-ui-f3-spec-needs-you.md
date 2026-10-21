# F3 spec 5 of 5: Needs you, from sindri-ideas 2026-10-20
Spec: /home/thor/.agentic-workflow/mockups/f3/specs/needs-you.md (prototype screens/needs-you.html, #pending .. #handback).
- Slices: 1 inbox; 2 action bar + keys; 3 typed and held in the bar (needs setup slice 1's controls); 4 answer form + takeover + nudge + hand back; 5 pending data.
- Served: inbox rows (incl. idle-takeover), decisions, approve-all. "Allow more tries" is protected -> terminal copy. E = dashed "Not available yet" chip.
- Settled by ideas: merge = served StatusAnswer rule (no new id); Drop = "Drop this item" in the card's More menu with confirm; D disabled on settings/workflow approvals with tooltip "These can only be approved. Change the settings or workflow file to undo."; takeover canvas/inspector from run-canvas.md.
- Lead answers: 4 scroll-to-end flag only, short card counts as read at once; 5 radius 12; 6 slice 5 waits on the scope viewer (facts panel fallback); 8 the hand-back box lives in run-canvas's inspector (one copy, reused).
- The map-build failure row: offer RefreshContextMap (context, see setup spec digest), not `services run`.

# F3 BUILD ORDER (lead)
Running: context slice 1, setup slice 2. Token-touching slices (workflows 1, setup 1, run-canvas 1) go one after another once context slice 1's PR is open (merge its branch if it hasn't landed). Then: setup 1 (shared controls; needed by setup 3 and needs-you 3) -> workflows 1..4 -> run-canvas 1..4 -> needs-you 1..4 -> setup 3/4; context slices 2..4 in parallel. Max 4 builders, memory rule.
- OWNER 2026-10-20 (via meta), OVERRIDES ideas' 'neutral': DIDN'T WORK OUT is AMBER (the 2026-10-19 table); FAILED red; CANCELLED neutral. Use the served attn tone.
- OWNER 2026-10-20 (via meta), for needs-you slice 4: NO in-app free-form input to an agent (tabled). On an agent step, input goes through Attach ('Watch the agent': Warp + tmux; typing takes over). Do NOT build a 'Reply here' text box. An agent step's needs-you row must offer Attach and Take over directly. Slice order unchanged.
