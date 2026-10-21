# B1 frame digest (worktree /home/thor/repos/aw-wt/sindri-ui-f3-b1-frame, branch feat/sindri-ui-f3-b1-frame)

State: committed 987134e01, gate run (scratch/sindri-ui/b1/gate.log): build ok; Ui unit 2053/2054 (ContextGoldenTests.Item_membership.. fails, untouched file); Rendering 21/21; Architecture 127/128 (Context.Infrastructure Graph<->Store cycle, untouched); format clean on touched files. PR opened.

## Inventory (kept / renamed / moved)
- Header: Title kept; identity line now "<Workflow display name> · <stage>[: detail] · since" (was recipe@hash · version · state). Address, workflow id@hash, version -> closed "Details" Expander with Copy address. Recipe link renamed "Workflow type ▸" (Labels.FrameTypeLink, my own; Labels.RecipeLink is B2's). displayName optional (InstanceRecipe.DisplayName / InstanceDetail.DisplayName), fallback in a private method (replace with WorkflowNames once B2 merges).
- States band: "States" -> "Stages", closed Expander. Order note reworded.
- Expanded stage: next sentence, paused (taken over), drift, child pointer, run line ("An agent is working on this step"), session link ("Agent session ▸"), Attach ("Watch the agent"), commands stay up front. Step line, gates, budget, timeout, launch id -> closed "Stage details" Expander.
- Facts: "FACTS" -> "What this run collected" (closed Expander, hint line; opens when an output waits on the person or a viewer is open); status words done/in progress/waiting; View -> Show; raw key shown only when the row is open.
- Children: "CHILDREN" -> "Child runs", closed Expander, opens when a key wants focus on a child; "r1/3" -> "round 1 of 3".
- Run/Attach/Protected: launch/attach/P10a/U8b jargon replaced by plain sentences; "instance" -> "run".
- Left alone: budget/timeout lines (compared with the engine's status text in tests), tabs strip + its labels (Labels.Workflows.cs = B2), ScopeMap graph words (mapped/new/unmapped), stream ids, OpenInstanceName(subject,address) used by Workflows rows (B2).
