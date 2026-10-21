# sindri-ui g1 approve-all (builder digest)

Worktree /home/thor/repos/aw-wt/sindri-ui-approve-all, branch feat/sindri-ui-approve-all.

## State: gate green, PR open (see below)
Commits: 5241f5450 wip (unbuilt) -> b22470b18 reduced-motion hold -> c4f345dfb build/analyzer/cycle fixes -> doc note.
Gate (heavy, one script): build -warnaserror clean; Ui.Tests unit 2137/2137; Ui.Tests integration 663 ok + 10 skipped (mac/windows) of 673; Rendering.Tests 21/21; Architecture.Tests 128/128; scoped dotnet format clean (Ui, Ui.Tests, Ui.Testing).
Fixes made: namespace cycle (ApproveAllPlan.AsOf dropped, ApproveAllAccepted own type), CA1502/MA0051 splits (EscapeForm, OpenerOf, FakeSindriHandler.Approvals.cs), ShellViewModel under 300 lines.
Decisions: ApproveAllJob stays approve-all's own narrow read (jobs unit merges later); own-approval = text list + one sentence; hold fill honours reduce motion (ShowsFill, NeedsYou.Seen.ReducedMotion).
## Left
- 409 SND-JOB-409 shows the sentence only; link to the running job needs ProblemView to carry runningJob + the jobs page (jobs unit).
- Goldens provisional until engine's recipes-approve-all/* land; no red-first commit.

## Review round 2 fixes (R2-1..R2-8): pushed head f17c76dc4, gate green
Commits: 015dcdb56 R2-3 stale send guard; 9ba0cba76 R2-4 Try again/one re-ask/notice; 9c483791a R2-6 own-only plan; 2fd14b1a6 R2-5 Close resets hold; d7c3e203f R2-8 HoldHelp; bad8ae4b8 R2-7 cancelled; c4ba7872b R2-1 capture + R2-2 focus (+16eedef01 analyzers, f17c76dc4 test fix). Red tests first at 3fae8b75b.
Gate: Ui.Tests unit 2149/2149; integration 668 ok + 10 skipped of 678; Rendering 21/21; Architecture 128/128; scoped format clean; build -warnaserror clean.
Notes: Avalonia implicitly captures the pressed element (a ContentPresenter), so the release-off-button case already reached the handler via tunnel; explicit Capture(hold) is now pinned by a headless test. Try again is not offered for SND-AUTH-002 (an agent session is refused every time). 471.json resolutions set.

## Merge of #474 (feat/sindri-f5-routes)
Merged origin/feat/sindri-f5-routes (main already contained). Only conflict: Labels.Actions.cs ApproveAllRecipes (kept one, ApproveAllTitle = "Approve all workflows"). Plan/empty tests and fake default now read Api/workflow-recipes-approve-all goldens; SpecShaped plan/plan-first-approval/empty/applied/refused deleted. Still SpecShaped: recipes-approve-all/refused-no-profile, recipes-approve-all-start/*, approve-all-job/*, challenges-app/held (digest now 1c1fd4). PlanRow hash uses Labels.Hash12. Gate green: unit 2244, integration 679+10 skipped of 689, Rendering 21, Architecture 128. Head c1bc8fe62.
Merged latest origin branch tip (#474 landed); gate green same counts; pushed
