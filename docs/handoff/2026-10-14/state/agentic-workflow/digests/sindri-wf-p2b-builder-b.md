# P2b builder B handoff (stopped at ~250k context; NOTHING committed yet)

Worktree /Users/thor/repos/agentic-workflow/.claude/worktrees/sindri-wf-p2b, branch feat/sindri-wf-p2b-recipe-rev8, head e7595e7d.
Lock rule (updated): `AW_HEAVY_SLOTS=2 ~/.agentic-workflow/bin-r1b/heavy dotnet build tests/Aw.Sindri.Tests -warnaserror` once, then
`heavy dotnet test --project tests/Aw.Sindri.Tests --no-build --filter-class '*DeclarationRulesTests'`. No `timeout` binary on this mac; use run_in_background.
A background build+test (task b91le3g8r) was started in the old session: kill any leftover `dotnet` from this worktree first.

## Done in working tree (uncommitted, RED state intended)
- src/Sindri/Aw.Sindri.Core/Workflow/Validation/DeclarationRules.cs: STUB (Check returns []), registered in RecipeValidator.Groups after WiringRules.
- tests/Aw.Sindri.Tests/Workflow/Wiring/WiringRecipe.cs: CheckDeclarations helper.
- tests/Aw.Sindri.Tests/Workflow/Wiring/DeclarationRulesTests.cs: full red unit tests (exact messages/paths/fixes below are the design).
- 7 fixtures tests/Aw.Sindri.Testing/Workflow/Recipes/invalid/RCP-{URG-001,OUT-001,OUT-002,OUT-003,TYPE-001,TYPE-002,TYPE-003}.yaml (declarations on lines 4-6, after `version: 1`).
- InvalidFixtureCases.cs: 7 entries (FixtureSetup.Shipped, expected positions are GUESSES; fix to real line:col after green by running), InvalidFixturesTests.cs _section91 extended.
Messages (see DeclarationRulesTests.cs for exact strings): URG-001 path `urgency`; OUT-001 `outputs[i].from` (state not declared; reserved engine states go to OUT-002 using SchemaRules.ReservedStates); OUT-002 terminal / engine-owned / "nothing there records it (step 'x' records ...)" Error, Information when wait child; OUT-003 at `outputs[i].key`; TYPE-001 `inputs[i].type`/`outputs[i].type` via FactTypes.IsKnown; TYPE-002 `inputs[i].kind` via SubjectKinds; TYPE-003 `inputs[i].kind` using SubjectKinds.PairedType, absent type = text (default), skipped when 001/002 fired for entry.
Facts a state records: Step descriptor Outputs (context.Catalog.Find(step.Use)), leaving transitions' Produces, wait event facts (Catalog.FindEvent(kind).Facts), FactKey.Answer for answer waits; child wait => information only.

## Left
1. Commit red (needs build to confirm compiles and only new tests fail), push. 2. Implement DeclarationRules (src/.../Validation/DeclarationRules.cs), fix Cases positions, commit green, push.
3. AW_GOLDEN_UPDATE=1 goldens review; docs/csharp/sindri-workflow.md rules table (~303-340) + any rule list/count (grep RCP-CAP-003).
4. `recipe show --json` test-first (grep SND-CLI-003 in src/Sindri/Aw.Sindri.Host; same shape as `recipe list --json`); usage text/docs/goldens.
5. AGENTS.md line: `recipe eval` exits 2 until P3 -> fix (also docs/usage).
6. Gate: build -warnaserror, format verify, unit+integration lanes, check-rules, check-deps, sync-rules --check, denylist-check on changed docs.
7. Coordinator extras AFTER part B: (A) fetch + merge origin/main (fe228345) no rebase, rebuild, retest; (B) deflake RecipeApproveCliTests.File_edited_without_raising_an_approved_version_is_SND_RECIPE_007_not_already_approved test-first, run 10x; (C) `git cherry-pick -x 7450463c fecdf01f` from origin/feat/sindri-wf-p4-interpreter, abort and report if not clean.
Commit trailers: Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com> / Claude-Session: https://claude.ai/code/session_019oQXkfxoLc3prCFtJoNGkM
