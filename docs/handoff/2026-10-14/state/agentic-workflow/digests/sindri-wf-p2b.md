# P2b handoff (lead session stopped at ~225k tokens context; no code written yet)

Worktree: /Users/thor/repos/agentic-workflow/.claude/worktrees/sindri-wf-p2b, branch feat/sindri-wf-p2b-recipe-rev8 off origin/main 4a2389bd. Clean, nothing committed.
Done: verification-plan.md (proof/sindri-wf-p2b/, created 16:51 PDT, predates code); baseline Debug host built (artifacts/bin/Aw.Sindri.Host/debug/sindri, DOTNET_ROOT=$HOME/.dotnet);
baseline hashes (origin/main) in scratch/p2b-lead/hashes-before.txt + before-<id>.json (validate --json output):
  workflow.item 331dd0a5e034..., workflow.step 12e030dc92a0..., dev-notify de3c04e3a865..., dev-timeout 550a3b7aed30...
Spec text copy: scratch/p2b-lead/wf.md (WF 3.1, 3.2, 3.3, 9.1 rows "Added (UI E-21, E-22)"). Specs are already written (no spec questions).
Lock rule now: AW_HEAVY_SLOTS=2 ~/.agentic-workflow/bin-r1b/heavy <cmd>; project-scoped dotnet test --project tests/Aw.Sindri.Tests; full gate once.

## Design decided (follow it)
Hash (WF 3.1/3.2, quote in evidence): normalized doc writes absent optionals null / lists empty; urgency default is WRITTEN ("work"), so absent == `urgency: work`.
=> EVERY existing recipe hash changes (adds urgency:"work", outputs:[], inputs[].kind:null). No approval data in repo (ShippedRecipes holds none; dev profile hashes
a profile, not recipes). Registry rows are content-addressed (hash+stored text) and are never rehashed, so old rows keep hash/standing; re-registering the same file at
same id+version gives new hash => SND-RECIPE-007 naming RCP-VERSION-001 (re-approval = bump version or new hash row). Prove with tests + CLI.
Model (src/Sindri/Aw.Sindri.Core/Workflow/Recipes): Recipe += `string Urgency` (normalizer writes "work" when absent: Infrastructure/Workflow/RecipeNormalizer.cs) and
`IReadOnlyList<RecipeOutput> Outputs`; new `RecipeOutput(FactKey Key, string? Label, string? Type, string From)`; `RecipeInput(Key, Label, string? Type, string? Kind)`.
Use STRINGS (not enums) so bad values reach RCP-URG-001/TYPE-001/002 instead of reader RCP-SCHEMA-002; delete enum InputType (+ RecipeTypes entry, PublicAPI.Unshipped lines);
add static helpers FactTypes (text,file,item,link,scope-map,packet,evidence,decision,payload; default text), SubjectKinds (item,linear-project,webhook,file,text; pairs item-item,
file-file,text-text, linear-project/webhook-payload; kind with absent type = type text), Urgencies (incident,triage,work).
Call sites to update: tests Engine/LoopFixture.cs, Engine/FrameFixture.cs (new Recipe(...)), Wiring/WiringRecipe.cs, RecipeReaderTests.cs:227, UpstreamRulesTests.cs:57.
Legacy stored text: RegisteredRecipe text from before P2b lacks urgency/outputs/kind and AwJson is strict (missing ctor param = error). Add one reader
`RegisteredRecipe.Read(json)` that adds defaults to the JSON then deserializes; replace the 3 AwJson.Deserialize<Recipe>(recipe.Text) sites: RecipeRegistryReader.Parse,
RecipeApprovalPlanner lines ~89 and ~187. Test with a seeded old-format row (InMemoryRecipeStore + RegistryHarness in tests/Aw.Sindri.Tests/ApproveRecipe).
RecipeSummary (Core/Workflow/Registry) += Urgency, Outputs (list/show/JSON); CLI show text: add urgency/inputs(key,type,kind)/outputs(key,type,from) lines in
Host/Commands/Recipe/RecipeLineText.cs + RecipeRegistryCommands.Show. Nested records need no SindriJsonContext change; RecipeSchemaTests: regenerate
schemas/recipes/recipe.schema.json with AW_GOLDEN_UPDATE=1 and hand-edit recipe.editor.schema.json (RecipeInput kind/type enums, RecipeOutput def, urgency enum).
Rules: new internal group (e.g. DeclarationRules : IRecipeRules) in Validation/, registered in RecipeValidator.Groups after WiringRules. Messages (RecipeSource.Finding(rule,path,msg,fix)):
 URG-001 path `urgency`; OUT-001 `outputs[i].from` names no state; OUT-002 (Error) from is terminal / reserved engine state / state whose Step descriptor outputs, leaving
 transitions' `produces` and trigger-source facts (event/answer) never include key; (Information) when the state waits on a child; OUT-003 duplicate key or key == an input key;
 TYPE-001 bad `type` on input or output; TYPE-002 bad `kind`; TYPE-003 kind/type unpaired (skip when TYPE-001/002 already fired).
 Open minor interpretation to report: `from` naming a reserved engine state is reported as OUT-002 (more specific) rather than OUT-001.
Fixtures: tests/Aw.Sindri.Testing/Workflow/Recipes/invalid/RCP-<rule>.yaml x7; add to InvalidFixtureCases.cs (exact expected lines), _section91 array in InvalidFixturesTests.cs;
 plus per-rule unit tests and an info-case for OUT-002; tests: absent urgency hashes == `urgency: work`; old long-form JSON (literal) hash != new, new minus (urgency, outputs, kind) == old
 JSON; seeded old registry row still lists (hash unchanged) and re-register => SND-RECIPE-007.
Existing tests with hard-coded hashes will change (grep `sha256:` in tests/Aw.Sindri.Tests: RecipeRoutesTests, RecipeWorkflowTests, RecipeHashingTests, InvalidFixtureCases CAP-003 base hash
 5aa84cb58a5a / CapabilityRecipe.BaseHash, Cli/Golden/validate-*.txt, Engine fixtures, RecipeValidateCliTests). Regenerate goldens with AW_GOLDEN_UPDATE=1 and review the diff.
Dev recipes (recipes/dev/, ids follow the existing dev- convention; ShippedRecipes test list in ShippedRecipesTests.cs must gain them; each needs evals/<id>/happy.jsonl and `evals:`):
 triage-three-state.yaml id workflow.dev-triage-three-state: urgency triage, inputs [{key: fact:item, type: item, kind: item}], 3 states (desk step.produce -> answer wait -> terminal),
   outputs: fact:output type file from the desk state (coordinator asked for a file output on the desk state), fact:answer type decision from the wait.
 desk-round-trip.yaml id workflow.dev-desk-round-trip: urgency work, input fact:request text/kind text; export (step.produce, output fact:output type file) -> import (step.checks,
   fail -> back to export with fact:findings payload, loop cap 2) -> done.
 incident-hold.yaml (new, from coordinator, exact name) id workflow.dev-incident-hold: urgency incident, a desk state held until answered (step.produce desk state, long timeout, file output fact:output).
 None ships approved. Tell coordinator exact path + commit after push (they relay to UI lane). Ids are unspecified by the specs: report as a non-blocking question.
Docs: docs/csharp/sindri-workflow.md (Recipe reference ~line 195-225, rules table ~303-340, register/show/list ~118-132), usage text.
Then: proof (scripts/sindri-prove.sh with AW_SINDRI_HOME=$TMPDIR/sindri-dev-p2b AW_SINDRI_PORT=0; host binary at artifacts/bin/Aw.Sindri.Host/debug/sindri; SINDRI="$PWD/artifacts/bin/Aw.Sindri.Host/debug/sindri"),
manual-test.md, evidence.md, draft PR (title/body per task), /review loop (<=5), reviews/<pr>.json, report. Push the schema shape EARLY and message coordinator.

## Update (lead 2, after builder A) - part A DONE and pushed
Branch head e7595e7d (53df19d7 red, 69cb05c7 green model+dev recipes, e7595e7d docs). Unit+integration lanes of tests/Aw.Sindri.Tests green (1711), project-scoped build/format clean.
Done: Recipe.Urgency/Outputs, RecipeOutput, RecipeInput.Kind (strings), FactTypes/SubjectKinds/Urgencies helpers, normalizer default "work", RegisteredRecipe.Read legacy reader (3 sites), RecipeSummary urgency/outputs, CLI show lines, schemas, goldens/hashes, docs Recipe reference, 3 dev recipes (validate 0 + happy eval pass).
LEFT (part B onward): DeclarationRules (RCP-URG-001, OUT-001/002/003, TYPE-001/002/003) + 7 invalid fixtures + InvalidFixtureCases + _section91 + per-rule unit tests + OUT-002 info case; docs rules table (~303-340); check-deps/check-rules/sync-rules; whole-solution gate once; sindri-prove.sh journeys 1-6 (fresh AW_SINDRI_HOME=$TMPDIR/sindri-dev-p2b, AW_SINDRI_PORT=0); manual-test.md + evidence.md (UI evidence skipped: CLI/schema stage, no web UI); draft PR (title/body per task, merge-gate output, trailer); /review loop <=5 with reviews/<pr>.json + "Review resolutions"; do not mark ready.
Notes: recipe show --json is rejected (SND-CLI-003), pre-existing; spec 6.3 says it returns the new fields -> open question. AGENTS.md says recipe eval exits 2 until P3 but it works (pre-existing doc drift). Dev recipe ids are builder/coordinator choice (specs silent). Builder commits used trailer "Claude Sonnet 5.5".

## Update (lead 3, started part B)
Builder (sonnet, agent a181696ca60c942c6) dispatched for Part B items 1-4 (DeclarationRules, 7 fixtures, recipe show --json, AGENTS.md eval line). Digest of builder: ~/.agentic-workflow/digests/sindri-wf-p2b-builder-b.md if it stopped early. Lead remaining after builder: verify commits/push, whole-solution gate once, sindri-prove journeys 1-6, manual-test.md, evidence.md, draft PR, message coordinator when PR is open, /review loop (<=5), reviews/<pr>.json. Never merge.
Lock rule tightened (coordinator, swap full): build once `heavy dotnet build tests/Aw.Sindri.Tests`, then `heavy dotnet test --project tests/Aw.Sindri.Tests --no-build ...`; builds/format/npm take a box slot. Re-read unit-brief.md. Builder was told.
Extra from merge orchestrator (main now fe228345): A merge origin/main at a clean point; B make RecipeApproveCliTests.File_edited_without_raising_an_approved_version_is_SND_RECIPE_007_not_already_approved deterministic (find race, commit showing it, then fix); C cherry-pick -x 7450463c fecdf01f from origin/feat/sindri-wf-p4-interpreter (no different fix; tell coordinator if it doesn't apply). Queued to the builder after part B (A, B, C in order).
Builder 1 (a181696ca60c942c6) stopped early on context, nothing committed (groundwork uncommitted in worktree). Builder 2 (ae8541bcca05b9b77) dispatched from ~/.agentic-workflow/digests/sindri-wf-p2b-builder-b.md with the full task list incl. extras A,B,C and gate.
