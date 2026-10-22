# api lane handoff, 2026-10-21

Lane: api (session `sindri-api`). Running log: `~/.agentic-workflow/orchestrator/handoff/api.md` (not on the new box; this file
replaces it). Reviews: `~/.agentic-workflow/jthorare-agentic-workflow/reviews/<pr>.runN.md` (544.run1.md is summarized below).

**Owner focus (2026-10-20 night):** 1 context retrieval wired to the code index, 2 the index and the context map in sync, 3 the UI
redesign through every demo screen, 4 the line recipe blocks (no api work). API endpoints, contracts and goldens on the path for 1-3
go first. **Parked:** #543 (#478), A3c (onboard and profile init), A7a part 2, the pre-commit hook, the switch slice.

## Branches and PRs

"Pushed" means the head below is on origin. A branch that says "no" has commits only on the old box until the owner's go there
pushes it (the hook refuses pushes in a turn the owner didn't type).

| PR | Branch | Head | Pushed | State |
|---|---|---|---|---|
| #528 AddRepo (goal 3) | `feat/sindri-add-repo` | `9f5ca765a` | no (origin `945f9a17c`) | Approved (run2). Merged main ccddefc locally; full checks were running |
| #544 needs-you PR1 (goal 3) | `feat/sindri-needs-you-rows` | `375005304` | no (origin `70a43a9a1`) | Approved (run1, 0/1/2). Test fix + main ccddefc merged locally; full checks queued |
| none yet: needs-you PR2 (goal 3) | `feat/sindri-needs-you-stops` | `4f9322828` | no (no remote branch) | Code + tests committed; last commit is WIP, unbuilt |
| #546 wf-rows (goal 3) | `feat/sindri-wf-rows` | `baea0aec9` | no (origin `100489e75`) | Draft; fix round 1 half done (WIP commit, unbuilt) |
| #547 code-index reads (goal 1) | `feat/sindri-codeidx-reads` | `9453011a2` | no (origin `7092726cb`) | Draft, stacked on feat/ci-1 (#542); waits on context's coverage sha |
| #543 ledger by instance (#478) | `feat/sindri-ledger-instance` | `fd89a6d2b` | yes | Draft, parked (off focus) |

### #528 AddRepo
- Left: confirm the checks on `9f5ca765a` (build, arch, Sindri unit + integration, Ui unit + integration). The run before
  (`aaf3b9ea7`, main cba5030) was green except one integration flake: `JobEndpointTests.A_command_with_an_Origin_header_is_403...`
  failed in `TempHome.Dispose` ("Directory not empty"); the class passes alone (16/16).
- Merge of main ccddefc had one conflict in `tests/Aw.Sindri.Ui.Tests/Api/GoldenContractTests.cs` `_notRead`: kept `"setup-repos"`,
  dropped `"setup"` (main's #540 now reads GET /setup).
- Next: push, PR comment, `gh pr ready 528`, send meta `ready #528 <sha>`, `dotnet clean` in the worktree.
- Owed after: re-point Host `EmbeddedShippedEffects` onto engine's `IEmbeddedRecipes` (#532 E1), whichever lands second (at the
  latest in A7a part 2). A7a part 2 must assert embedded recipe count == `recipes/sindri/*.yaml` in the staged archive.

### #544 needs-you PR1 (setup:recipes + recipe:<id> rows, "workflows" wording)
- Review run1 APPROVE 0 issues / 1 suggestion / 2 nits, all resolved in writing (comment text ready; post it with the ready):
  - S1 `since` = read time: kept, same as the Onyx row (a current state, not an event).
  - N2 `ApproveAllPlan.EmptySentence` says "recipe": kept; it is the approve-all API contract (spec, `docs/csharp/sindri.md`,
    golden `workflow-recipes-approve-all/empty.json`).
  - N3 overlapping tests: kept (Core plan vs endpoint).
- After main 93ad5f6 (#540), Ui test `SetupViewModelTests.A_done_row_shows_what_it_did_and_a_waiting_row_why` expected
  "26 recipes."; fixed to "26 workflows." in `8651c015b` (Ui unit 2428/0). Then merged main ccddefc → `375005304`.
- Left: full checks on `375005304` (build, arch, Sindri unit + integration, Ui unit + integration), push, comment, ready, meta.
- ui is asked to update #540's SpecShaped fixtures (setup/extra-priority-mix, setup/extra-three-running-onyx-failed,
  needs-you/extra-setup-rows) from "recipes" to "workflows" text; ids stay `recipes` / `setup:recipes`. No test reads that text.

### needs-you PR2 (desk/park `stop{n,of}` + `timeout`)
- Worktree `~/repos/aw-wt/api-nyr-stops`. `4ca02c905` is the feature commit; `4f9322828` is a WIP commit of the last four files
  (NeedsYouJoins.cs + three test files) the builder had open when it hit the usage limit. main 9260072 merged; not cba5030 or later.
- Decisions: `stop{n,of}` = the parked desk state's position among ALL the recipe's desk states, in recipe order;
  `timeout{kind,inSeconds,on}` = a read-time join on the status computation, omitted when that read fails; `stageTitle` comes from
  engine F5 stage names once #532 is in, omitted before. Goldens `needs-you/{desk-with-stop,park-with-timeout}`.
- Left: merge main, build, scoped format, arch, Sindri unit + integration, Ui unit + integration + Rendering; push; draft PR;
  review run1; tell ui the PR number.

### #546 wf-rows (instance list fields)
- Pushed head `100489e75` has startedBy, startedAt, lineStage (WRONG, see below), rootAddress, finished runs via
  `GET instances?root=true&recipe=<id>&open=false`, takeoverAllowed dropped, StartedBy recorded on InstanceStart.
- Fix round 1 (WIP commit `baea0aec9`, unbuilt; the builder said PublicAPI entries and the detail goldens were still to do):
  1. lineStage = the current state id of the ROOT `workflow.line` instance (own state for a root line row); null outside a line.
     Not `ItemRow.Step`. Resolve each distinct root once per list. Tests: root line row, child of a line, outside a line, shared root.
  2. `steps[].needs` (descriptor Reads) and `steps[].produces` (the outgoing transitions' produces) on the instance DETAIL only
     (engine agreed). Re-pin the detail goldens.
  3. `open=false` changed from "no filter" to "finished only": audit every caller (CLI list, Home strip, Ui client, MCP, docs);
     absent `open` must return everything; add that test; list callers in the PR body.
  4. Merge main; run engine, Sindri unit + integration, Ui unit + integration, Rendering, arch one project at a time.
- Engine's conditions on StartedBy: optional, no workflow branch reads it, no Patched, replay fixtures unchanged, no hash change.

### #547 code-index reads (CI-10a)
- `sindri code-index status|symbols|callers|callees` (+ `--json`), `GET /sindri/code-index/{status,symbols,callers,callees}`,
  21 goldens, `FakeCodeIndex` in Aw.CodeIndex.Testing, `UnbuiltCodeIndex` stand-in in Host (every repo `missing`, every read
  SND-CODEIDX-013) that CI-9 replaces. No MCP tools, no catalog rows, no similar/neighbors (spec §10; context agreed).
- Local `9453011a2` merges feat/ci-1 `3a815a056`; the build of that merge had not reported when the builder stopped.
- **Blocker:** context's coverage sha on feat/ci-1. #542's review adds `Coverage` (Full|Partial|Unavailable, string names) on
  CallEdge and on CallsAnswer; every callers/callees row carries it, any partial row makes the answer partial, an empty partial
  answer reads "none found". Then: merge it, carry coverage through FakeCodeIndex (contract tests), CLI text + `--json`, route JSON;
  tests for partial and "none found"; re-pin goldens; lanes; push; review run1. Stays draft until then.
- When #542 merges: retarget #547's base to main and merge main.

## Owed and open
- **MCP log flake** (meta, from #548's full integration lane): `Aw.Sindri.Tests.Mcp.SindriRequestToolTests.
  Neither_request_text_nor_item_text_reaches_a_log_line_even_at_trace` failed in a full run, passes alone. api owns it; next free builder.
- Next api item for ui (goal 3, item 4): Onyx routes (SetupOnyx, ConnectOnyx, DeferOnyx), RegisterMcp, POST /setup/* writes.
- Context F1P-c map-status surfaces (`sindri context map status --json`, doctor `context:publish`) after context says F1P-b2 is open.
- Tell ui each PR number as it opens (wf-rows = #546, codeidx = #547 told; needs-you PR2 not yet opened).

## Working rules carried over
- Merges go through meta's batch: `ready #N <full sha>` to meta + `gh pr ready N`. Pushes, PR comments, `gh pr ready` and readies
  need the owner's own typed turn in the api session (hook).
- Builders and reviewers on Sonnet or Haiku, never Opus. Every dotnet build, restore, format and non --no-build test through
  `heavy … -nodeReuse:false`; `--no-build` tests with `MSBUILDDISABLENODEREUSE=1` inline; never `dotnet build-server shutdown`.
- Before ready: arch on any Core or Host change; Ui unit + integration + Rendering on any catalog, golden or error-code change;
  aot-smoke ×2 on auth, prover or refusal text. `dotnet clean` the worktree after each hand-off.
- gh: `--repo jthorare/agentic-workflow --head jthorare:<branch>`; edit bodies with `gh api -X PATCH .../pulls/N -F body=@f`.
