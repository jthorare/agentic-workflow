# G1 builder briefs (sindri-ui lane, 2026-10-20, after main 0d0c974c4)

Common to all four:
- Read ~/.agentic-workflow/digests/sindri-ui-builder-rules.md and ~/.agentic-workflow/digests/sindri-ui-f3f4-guide.md FULLY first. Ignore the context-guard hook's token numbers.
- Worktree /home/thor/repos/aw-wt/sindri-ui-<unit>, branch feat/sindri-ui-<unit> from origin/main 0d0c974c4 (already pushed). Never touch ~/repos/agentic-workflow.
- Digest ~/.agentic-workflow/digests/sindri-ui-g1-<unit>.md: keep it current (state, commits, gate numbers, what's left).
- TDD: red test first, commit red, then green. Push after every commit.
- Gate (one heavy script): build -warnaserror of touched projects; Aw.Sindri.Ui.Tests unit lane; the FULL Aw.Sindri.Ui.Tests integration lane; Aw.Sindri.Ui.Rendering.Tests if views change; Aw.Architecture.Tests; scoped format (--include touched files only).
- Before opening the PR: check duplicate members in the partial class Labels against origin/main (`git diff origin/main... -- 'src/Sindri/Aw.Sindri.Ui/ViewModels/Labels*.cs' | grep -E '^\+\s*internal (const|static)'` and grep each name on origin/main).
- Then ONE review pass: spawn no agents yourself; instead do a careful self-review of the diff for correctness, fix ISSUES only, write nits in the PR body.
- Open a PR titled "feat(ui): <unit summary>". Body: what changed, tests that ran with counts, what's left. Ends with exactly the line in the rules.
- Final report to the lead: PR number, head sha, tests with counts, what's left, decisions needed. Short. Do not send `ready` to anyone; the lead does that.

## followups
One PR, five small fixes:
1. The instance frame uses ViewModels/Workflows/WorkflowNames.cs instead of its private display-name fallback (delete the private copy).
2. The frame's "How it works" link (Labels.OpenRecipeName) passes the workflow's display name, not id@hash.
3. InstanceRowViewModel's AccessibleName includes the "N inputs missing" count when there is one.
4. The WorkflowsLegend hand-back line agrees with HandBackArrives (same wording/meaning).
5. The Agents page filter combos' accessible names start with what they show (e.g. "Provider: all"), 457 nit R1-3.

## f5-displayname
The engine contract is final: `displayName` is REQUIRED on recipe, recipe list, instance and item views; sentence case, max 60 characters. The UI reads it as optional today (InstanceRecipe / RecipeView / RecipeSummaryView, and WorkflowNames.cs). The engine's goldens are not on main yet (no "displayName" under tests/Aw.Sindri.Tests/Golden/Api/workflow-*); the lead is asking sindri-engine for them. Until they land, build against fake JSON in tests (follow the existing fixture pattern) carrying displayName. Make it show everywhere a workflow is named (lists, frame header, item sheet, needs-you rows, palette, agents rows that name a workflow); never show the raw recipe id in user-visible text (the id may stay in Details with Copy). Keep a fallback for a missing displayName (old servers) through WorkflowNames. If the followups unit is also touching the frame's name, keep your change minimal there (the lead merges).

## approve-all
Contract (api, spec PR #445 §8; routes NOT on main yet, build against fakes): GET /sindri/workflow/recipes/approve-all returns a plan: an ordered list (children first), a reason per recipe, one combined preview. POST returns 202 + a job id. Goldens to come: recipes-approve-all/{plan,applied,refused,empty}.json; write your own fixtures in that shape and mark them as provisional in a comment. UI: a catalog row ApproveAllRecipes (protected) — the app asks for a typed or held confirmation, shows the plan (ordered list with reasons, the combined preview) before applying; agent sessions get SND-AUTH-002 (show it in plain words). A needs-you row "N recipes waiting for approval" that opens the plan. Catalog rows are pinned by Aw.Sindri.Ui.Tests; update those tests honestly. Read the spec section from the PR: `gh pr diff 445` (read only).

## jobs
GET /sindri/jobs exists on main (ListJobsEndpoint, ?state=&kind=; goldens tests/Aw.Sindri.Tests/Golden/Api/jobs/{running,by-kind,empty,empty-unfiltered,bad-kind,bad-state}.json; jobs-id/*, jobs-id-cancel/*). Build a Jobs page: each row shows the kind (served label), state, started by, started/ended, summary, and Cancel where allowed (through the existing cancel route). Live from job.* events on the event stream. A Job deep link (follow the existing deep link pattern) opens the job's detail. Context's "Refresh map" job kind (RefreshContextMap) is not on main: render unknown kinds by their served label, no hardcoded kind list beyond what's needed. Read the golden files to drive the client model; add a GoldenContractTests entry honestly.

## instance-live (starts after api #467 is on main)
Contract (api #467, merged): opt in with `GET /sindri/events?types=workflow.instance-changed[&instance=<address>]` (a workflow.* glob never includes it). Frame: `event: workflow.instance-changed`, NO `id:` line, `data: {"instance":"T-1/step/1","state":"checks","cursor":4211}`. `state` is the state after the change and is authoritative; `cursor` is the ledger sequence. The server coalesces to one frame per instance per page of <=100 rows; the UI keeps its own 250 ms window. There's no /events/catalog row: treat a frame as "invalidates instance(address)". Read the real code and goldens on main for this (grep instance-changed under src/Sindri and tests/Aw.Sindri.Tests/Golden).
Build:
1. The instance frame (an instance shown) and the Workflows list re-query `GET /workflow/instances/{address}` when a frame for that instance arrives, within the existing 250 ms coalescing. The view is read live and can lag the frame: if the re-read state doesn't equal the frame's `state`, retry a few times with a short BOUNDED backoff (via TimeProvider; tests use a fake clock), then stop.
2. Reconnect rule: on every connect and reconnect, open the stream FIRST, then re-query every instance shown (frames sent while away are lost; a derived-only stream opens at the ledger head). A frame has no id, so it never moves the resume cursor; if the stream also carries raw ledger types, those rows' ids keep the cursor as today.
3. Opt in only where it's needed (the frame: `&instance=<address>`; the list: the type without an instance), and never by widening a glob.
Tests first: a frame triggers exactly one re-read after coalescing; a lagging view is retried and then stops; a reconnect re-queries after the stream opens; a frame never moves the cursor; an unknown instance's frame is ignored on the frame page.

## step-runs (from main 067e568b0)
api #469 is on main: GET /workflow/instances/{address} serves stepRuns[] (the UI DTO is already there: src/Sindri/Aw.Sindri.Ui/Api/Workflow/StepRun.cs, InstanceDetail.StepRuns). Fields: token, step, attempt (from 1), maxAttempts (the loop cap or null), state running|passed|failed|refused|cancelled|abandoned, runtime, times, durationMs, outcome, tokens, actor. A park, takeover or escalation leaves a run running. Docs: docs/csharp/sindri.md (search stepRuns).
Build MINIMAL and plain-worded (the F3 redesign may restyle it later): in the instance frame, the current stage's run line says "Attempt 2 of 3" (or "Attempt 2" with no cap) and who is on it (the actor in words), and a closed "Runs" disclosure lists each run: step name in words, attempt, state in plain words (Running / Passed / Failed / Refused / Cancelled / Abandoned), duration ("4 min"), and who ran it. No tokens or runtime up front (Details only, if at all). Handle an empty or missing stepRuns (older server). Accessible names for each row. Tests first.
Also run the IDE0001 project check: `dotnet format <Ui csproj> --verify-no-changes --no-restore --diagnostics IDE0001` on the merged head. Gate scripts use `set -eo pipefail`.

## followups-2 (from main 067e568b0)
Small, one PR:
1. WorkflowNames.Of treats a displayName equal to the recipe id (or blank) as missing and falls back to the readable id. A test.
2. #476 review leftovers: InstanceLive/Catch tests' Settle() uses the fake clock, not real-time Task.Delay; remove the unused InstanceStreamItem.IsOpened; drop a superseded retry when a newer frame for the same instance is pending (L2), with a test.
3. Labels.FrameTypeLinkName's parameter is renamed `workflowName`.
Also run the IDE0001 project check as above. Gate scripts use `set -eo pipefail`.

## setup (OWNER APPROVED #445, 2026-10-20; the Setup page is no longer held)
Contract: spec PR #445 (docs/sindri-setup-once, head 680e96272 at approval), the file docs/superpowers/specs/2026-10-19-sindri-setup-once-design.md, read with `git show 680e96272:<path>`, read only. Use the owner-approved recommended answers in it. The read route GET /sindri/setup is api PR #458 (feat/sindri-setup-read, OPEN, not on main): read its DTOs and goldens with `git show origin/feat/sindri-setup-read:<path>`, and build the client to that shape on fakes until it merges. Approve all (already on main) is one step of setup: link to its dialog, don't rebuild it.
Build: a Setup page (and a needs-you row when setup is incomplete) that shows, in plain words, each setup step from the spec, its state (done / needs you / blocked, with why), and the one action that moves it forward (protected actions follow the catalog: an app-ready row opens the in-app confirmation; a cli-only row shows the command with Copy). One primary thing: the next step that needs you. Details (ids, codes) behind disclosure. Live from events (re-read on the relevant event types while shown). Tests first; the full gate with `set -eo pipefail`, IDE0001 and the integration lane.
Spec units for ui (#445 §13; api 2026-10-20): U1 = the Setup card on Home (route `setup`, chord G U); U2 = typed and held confirmations (AppConfirm: minHoldMs/typedAlternative, held redeem; the api side is A2, after A1); U3 = add repo, Onyx, MCP; U4 = the setup rows and displayName. U1, U2 and U4 can start from the spec's §8 goldens before A1 merges. Live read: api #458 (in a fix round). Incoming #458 changes: an Unknown Onyx with passing checks becomes `failed` "Sindri couldn't ask Docker about Onyx."; a map waiting without an approved profile says "Waits for an approved profile with a repo."; a throwing source fails only its own step with SND-API-005. Order: U1 first (one builder), then U4, then U2 (it reuses #471's hold or typed confirmation; move it into a shared component), then U3 once the write routes land. The builder raises any §7 SetupView field that's a problem for the card with api.

## f3-context slice 1 (from main b6f564402)
Spec: /home/thor/.agentic-workflow/mockups/f3/specs/context.md (read it fully; edited in place by sindri-ideas) and the summary ~/.agentic-workflow/digests/sindri-ui-f3-spec-context.md. Prototype: ~/.agentic-workflow/mockups/f3/screens/context.html (#map, #filemap, #loading).
THIS PR = slice (1) only: the 4 new tokens Wire, WireHi, LaneFill, RingAnchor (update TokenTests and ContrastTests), an own ZoomBorder, ContextEdgesControl and CoverageStrip, as reusable controls with tests (rendering tests for the visuals). Don't wire them into the page yet beyond what the slice needs; keep the seams' x:Names (LensStackHost, NodeHost, SourcesHost; AnchorBox inline, NamedControlsTests). No new action ids. Questions on the spec go to the lead (who asks sindri-ideas).
Worktree /home/thor/repos/aw-wt/sindri-ui-f3-context, branch feat/sindri-ui-f3-context. Digest sindri-ui-g1-f3-context.md.

## setup U1 (from main b6f564402)
See "## setup" above. THIS PR = U1: the Setup card on Home, the Setup page (route `setup`, chord G U), and the needs-you row when setup is incomplete, on the #445 §8 goldens / api #458's shape (fakes; git show origin/feat/sindri-setup-read:<path>). Include #458's incoming wording changes. One primary thing: the next step that needs you. Approve all is a link to the existing dialog. U2/U3/U4 are later PRs.
Worktree /home/thor/repos/aw-wt/sindri-ui-setup, branch feat/sindri-ui-setup. Digest sindri-ui-g1-setup.md.

## needs-you slice 1 (F3 spec 5)
Spec: /home/thor/.agentic-workflow/mockups/f3/specs/needs-you.md (read fully, incl. "Settled by ideas" and "Answered by ui"); prototype ~/.agentic-workflow/mockups/f3/screens/needs-you.html; summary ~/.agentic-workflow/digests/sindri-ui-f3-spec-needs-you.md.
THIS PR = §8 slice 1 ONLY: NeedsYouPage, StickyGroupList, the group and row templates on the existing rows, the card head and status footer, the entry points and Esc back. Red first: NeedsYouGroupingTests (goldens to groups), then NeedsYouFramesTests (pending, 1440 and 900). No new tokens (AttentionFill and Violet belong to other slices); no action bar, keys or confirmations (later slices). No change to Home or nav beyond the entry points the spec names.
Worktree /home/thor/repos/aw-wt/sindri-ui-needs-you, branch feat/sindri-ui-needs-you. Digest sindri-ui-g1-needs-you.md.
