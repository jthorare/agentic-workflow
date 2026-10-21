# B2 workflows (sindri-ui lane F3/F4)
Worktree /home/thor/repos/aw-wt/sindri-ui-f3-b2-workflows, branch feat/sindri-ui-f3-b2-workflows.
Gate script: /tmp/b2-scripts/gate.sh (one heavy slot, `timeout 3600 ~/.agentic-workflow/bin-r1b/heavy bash ...`), log /tmp/b2-scripts/gate1.log.

## State
DONE. PR #459, head 9e8f57019 (pushed), origin/main 44eede181 merged. Gate scripts/logs in ~/.agentic-workflow/scratch/sindri-ui/b2/ (gate8.log is the last green run).
Gate: unit lane 2075/0; integration classes 5+2+6+22+2+11 all pass; Rendering 21; Architecture 128; format clean on the head before the last test-only commit.

## Inventory (kept / renamed / moved)
- Workflows list row: primary = item title (button), workflow display name, chip, stage (StageTag), who (Who); Now stays as quiet text.
  Moved to row Details expander (closed): inputs, every stage cell, workflow type id, run address + Copy. Address never up front.
- Group header: display name + "N runs" (was recipe id + count). Per-state column heads/scroll removed. Recipe link "Recipe >" -> "How it works >".
- Filters: "Recipe"/"Subject" -> "Workflow type"/"Item"; "All recipes" -> "All workflow types" (choices are names, RecipeIdOf maps back to id).
- Legend (marks + keys) behind an Expander "Key and shortcuts"; words: paused: needs a decision / you took over / repeats; "desk" -> "waiting for you".
- Phase jargon removed from HandBackArrives, ArrivesWorkflowStrip, StripUnreadable. Tab strings (Labels.Workflows TabsArriveU8b/TabArrivesU9) left: B1's frame uses them.
- Home strip row: recipe id -> display name; names no longer carry the address.
- Start form: picker shows display names; labels "Workflow type"; hint "Filled in for you if you leave it empty"; unlabeled input keys humanized (WorkflowNames.InputName); item picker title first.
- Recipe sheet: title = display name; id@hash, version, urgency, name template and per-input key/type lines moved to Details expander; headings "What it needs" / "What it produces".
- Shared helper: internal static ViewModels/Workflows/WorkflowNames.cs (Of, InputName) for B1.

## Left / decisions
- "Approve all" for recipes: left out (no endpoint yet; lead briefs it).
- Instance "name where there is one": no served field known; not read.
- Finished-run outcome words (FAILED / DIDN'T WORK OUT / CANCELLED): chip words are served (StateChip, api lane); nothing authored here.
- Left alone: "Answer in the child", "child" cell word, StartedBehind/StartAlreadyRunning addresses, Start Note "code: message", RecipeStatesBand loop words.
- B1: Labels.OpenRecipeName now reads "How it works: <subject>"; the frame still passes id@hash (should pass the display name).

## Review fixes (round 1, head 666b28d43)
B2-1 (terminal Who empty), B2-2 (chip word in name), B2-6 (N inputs missing) at 9c0724344; B2-3 (unique filter choices) at 60d00b6c4; B2-4/5 (title-first names) at 3a7a8830e; 666b28d43 updates the incident-name test. B2-7 left (legend wording). Gate log fix-gate.log: unit 2078/0, integration 5+2+11, Rendering 21, Architecture 128, format clean.
