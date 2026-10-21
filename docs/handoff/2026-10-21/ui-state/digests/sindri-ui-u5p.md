# sindri-ui-u5p digest (U5', the Start form)

Branch feat/sindri-ui-u5p, worktree ~/repos/aw-wt/sindri-ui-u5p. Head: ec9351404 (pushed). Uncommitted files: none. No PR opened.
Origin/main (1d6bac7) was merged at dfd574a57, cleanly.

## Plan tasks
- T0 (API goldens): done upstream (#337). GoldenContractTests now reads `workflow-recipes` (RecipeList) and `workflow-instances-start` (request and started); both removed from _notRead.
- T1 contract: DONE. ISindriApi.Recipes/StartInstance, SindriApiClient.Start.cs, records (StartInstanceRequest, StartFact, StartedView, StartedInstanceView, RecipeList, RecipeSummaryView), fake + handler routes, SindriApiContract.Start, StartGoldens.
- T2 view models: DONE. StartFormViewModel (+ .Reads/.Send partials), StartFieldViewModel, IFilePicker/PickedFile/NullFilePicker, FakeFilePicker, Labels.Start.
- T3 dialog + openers: DONE. StartDialog, `N`, Workflows and Home buttons, Ctrl/Cmd+N, palette entry, AvaloniaFilePicker, Esc, Ctrl+Enter. StartArrives label and its tests removed. 4 headless tests in StartDialogTests; 2 frames (u5p-start-filled, u5p-start-refused).
- T4 docs: DONE (docs/csharp/sindri-ui.md, U5' section; older "Start arrives" lines updated). Screens-spec phase-table row NOT edited.

## Commits (red first)
397256ef4 red tests+stubs; fc230d30c T1 client; 283ae4645 T2 view models; 8cc6110e9 T3 dialog/openers; dfd574a57 merge main; ec9351404 docs.

## Gate (not finished)
- Last test run before the merge: Aw.Sindri.Ui.Tests non-heavy, 2323 total, 3 failed (NamedControls StartPickFile, 2 StartDialog Ctrl+Enter tests). Fixed afterwards (file button unnamed; send chord moved to a tunnel handler); that build succeeded, but the tests were NOT re-run.
- Full-solution `dotnet build -warnaserror` was queued on the heavy lock when stopped (background task, result unknown; log: scratchpad g.log).
- STILL TO DO: re-run Ui.Tests, Ui.Rendering.Tests, Architecture.Tests (non-heavy), record the Ui integration duration; `dotnet format --verify-no-changes --no-restore`; `node scripts/check-deps.mjs`; `scripts/sync-rules.sh --check`; PublicAPI.Unshipped.txt check (internal types only, probably nothing). Check LaneAnchorTests and the Ui integration lane stay under 90 s.

## Plan vs main
- Recipe list is used only to pick; inputs come from GET recipes/{id} (agreed with API).
- No SND-ITEM-404: no "item not found" state; served refusals pass through verbatim.
- Start answer: only instance.id/address, existing, finished are read (extra engine members ignored).
- FakeSindriHandlerRoutingTests: POST `instances` is now the StartInstance route, so that case changed to `recipes`.

## Defaults taken
Q1 subject = item; Q2 read only id/address/existing/finished; Q5 new attempt = second explicit click; Q6 no U5' lane anchor (new files, inserted below U8 block); Q7 readiness from catalog row, own sender (no catalog.On); Q8 unapproved/revoked hidden, one row per recipe id at highest version.
Also: an `item` input has no field, the picked item is sent as `item` and as fact:item `{"id":...}`; text facts are sent as <key>.md, links .url, payload .json. ShellViewModel took `Files` as an init property (ctor was at the 7-dependency limit).

## Not done
Live AT-SPI proof (separate step); the gate items above; screens-spec row.

## Update (2026-10-18, later)
- Gate on ec9351404+: build 0 warn; Ui.Tests 2323/0 fail; Rendering 21/0; Architecture 126/0; Ui integration lane 1m40s (budget 90 s; Start tests ~5 s of it); check-deps, sync-rules OK; format fixed (IDE0001/2 in Start tests); screens-spec U5' row annotated (commit "chore: format Start tests...").
- Proof found: the form's refusal sentence stayed after the field was filled. Fixed test first (ff64d208a, StartFieldViewModel.Edited event).
- Merged main several times; head 0aaa136d4 (merged 9990ace/34c91d3; one conflict in GoldenContractTests _notRead, resolved: kept only "sessions", "sessions-id").
- Proof home: proof/sindri-ui-u5p (Xvfb :95 pids in raw/pids-live.txt, fixture home-live). Lead rule: do not run from copies of bin/ under proof/; raw/bin-host and raw/bin-ui are such copies (lead removes). Re-run from detached worktree ~/repos/aw-wt/proof-u5p artifacts/bin.
- Journeys done on 33015f483: J1 PASS, J2 PASS, J3 client-side PASS, J4 PASS, triage item fact sha matches {"id":..}; Esc, Ctrl+Enter PASS.

## DONE (2026-10-18): draft PR #351, head 0aaa136d4 (merged main 9990ace). Gate: unit 1727 + integration 597 pass, Rendering 21, Architecture 126, build 0 warn, format/deps/rules clean; Ui integration 1m34s. Proof PASS (evidence.md, manual-test.md in proof/sindri-ui-u5p). Bin copies for lead to remove: proof/sindri-ui-u5p/raw/bin-host, raw/bin-ui. Torn down.

## Review 351 run 1 addressed (2026-10-18)
Commits: 3350980ec (picker contract, StreamFilePicker, WindowPorts), d628603f3 (state, item fact repo), 67fb00573 (focus, a11y, view tests), 619037a53 (format). Review file marked (49 items: 1 blocking + 19 issues fixed; suggestions/nits folded|follow-up|declined). Gate at 619037a53: build 0 warn; Ui.Tests non-heavy 2367/2357 pass/10 skipped; Rendering 21; Arch 126; Ui integration 1m27s; format/deps/rules/env clean. Third live pass at 67fb00573 PASS (shots3/, evidence.md). PR body PATCHed. Torn down; proof worktree removed. raw/bin-host, raw/bin-ui still for the lead to remove.

## Review 351 run 2 addressed
Commits 595c1982f (code+tests), b8d8ef928 (docs). Gate: build 0 warn; Ui 2386/2376/10 skipped; Rend 21; Arch 126; integration 1m24s; format etc clean. Pass 4 live at 595c1982f PASS (evidence.md rewritten, raw/*p4*). PR body PATCHed. 26 run-2 items resolved. Torn down.
