# Sindri API rev-8 contract unit (branch feat/sindri-api-rev8-contract, worktree .claude/worktrees/sindri-api-rev8)

Commits (off main 4a2389bd):
- e48c5ca9 feat: records, StateChip, ConfirmText, stub WorkflowViewComposer + GetInstanceFact, fixtures, public API, JSON context, API spec §3.8 `confirm?` text
- 249aec3a test: 30 golden files + WorkflowViewGoldenTests (integration lane, passes; AW_GOLDEN_UPDATE=1 re-records)

## What is in
- `src/Sindri/Aw.Sindri.Core/Workflow/Views/` (39 records, enums, FactTypeJsonConverter, StateChip, ListCounts, ParentRef, ConfirmText.Cancel). `RecipeUrgency` is in `Core/Workflow/Recipes` (engine P2b should adopt it).
- Rename: the API's `InstanceView` is `InstanceDetail` (engine #192 keeps `InstanceView`). JSON field names are exactly §3.8 / plan §8.
- AW1001 (raw string ids) forced CLR names `Instance`, `Item`, `State`, `Gate`, `Stream`, `Action`, `Name` carrying `[JsonPropertyName("id"|"itemId"|"actionId")]`; the wire is unchanged.
- §8 items in: chip{word,tone} on InstanceSummary/ChildRow, `counts` on InstanceList plus `open` golden (30), `answer` on list rows, `id` + `current.desk.run`, `parent`, facts-route goldens 24-29 (workflow-instance-fact/{scope-map,packet,evidence,text,payload,truncated}), `commands[].confirm` (Cancel only; "Cancel <address>? <N> running steps stop; <M> children are cancelled."; N=1, M=0, PRJ-sync M=5 from the fixture).
- Dev recipe fixtures/goldens: workflow.dev-triage-three-state, workflow.dev-desk-round-trip (goldens `dev-*`). No dev-incident-hold fixture/golden yet.
- Golden list: plan §3 (1-23) with 8/9 renamed `dev-desk-round-trip`/`dev-triage-three-state`, plus `workflow-instances/open` and the six `workflow-instance-fact/*` files = 30.
- `FactView(Key, FactType Type, JsonElement Body, bool Truncated)` lives in Features/GetInstanceFact (public); service stub internal, not registered (MA0182 pragma).
- `not-found` golden is a hand-written ProblemDetails (SND-ITEM-404 is not a code yet); U7 swaps it for the served body.

## Waits
- Engine P2b: StatePathOrder, Recipe.Urgency/Outputs/Input.Kind, real dev recipe hashes (goldens re-record; hashes here are placeholders); ShowRecipeResponse wiring of RecipeView (Q5).
- #192 / engine P4(b): composer U2-U4 (states, status from StatusLayout, timeout/budget/paused, counts), search attributes for ListInstances filters (`root`,`recipe`,`subject`,`open`), N/M for confirm, route bodies replaced (U7), `workflow status --json` = StatusFields.
- Engine P5 (#209): timeout deadline; P7/P8: children rows with agent/round/workstreams (only PRJ-sync has workstreams, hand-built); ParentRef.task/workstream.
- API P4 (U8): item projection, needs-you `park` rows, `CloseItem` refusal, `?instance=` filters, `items?parent=`; NeedsYouRow.chip.
- Not reached from the lead's notes: HealthProblem.clause/health.sentence (belongs to #190 unit E2); needs-you EmptyText wording (P4's).
- Red slice tests (K3) were deliberately left out; they go in later commits per unit.
