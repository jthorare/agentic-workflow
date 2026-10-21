# #224 U3' pass 1 handoff

Branch feat/sindri-ui-u3, worktree .claude/worktrees/sindri-ui-u3.

## Merges
- origin/main (e1a4a75) merged: one real conflict, ShellViewModel.cs. Main won (Items/Queue/Palette/Create ctor lines; the sheet state moved to ShellViewModel.Sheet.cs, so U3's inline sheet block was dropped); kept U3' `Attention = AttentionSummary.For(...)` init. IsAttentionBandVisible came back from main (unused, harmless; U3' withdrew the band, remove it when the band tests allow).
- Other fixes: Labels.NewRows/Cleared duplicated in Labels.NeedsYou.cs and main's Labels.Items.cs (identical; U3' copies removed); two tests needed the new ShellViewModel `clipboard` ctor arg.
- Second merge of origin/main (406d89f, #255) was clean.
- Lane anchors untouched (client/ISindriApi/JSON context inserts sit above the U3' markers); anchor tests pass.

## T1 (done)
- Records in Api/: NeedsYouList, NeedsYouRowView (+ optional served `chip` as Workflow.StateChip), SubjectRefView, DecisionList/View/OptionView, RequestList/View, DecideRequest, FileRequestRequest, RequestFiledView. JSON context, ISindriApi (NeedsYou, Decisions, Requests, Decide, FileRequest), SindriApiClient.NeedsYou.cs + NeedsYouRoutes.cs. Decide/FileRequest sent once; RequestFiled's sequence feeds X-Sindri-After; 409/404/400 refusals come through as served code+detail.
- Fakes: FakeSindriApi.NeedsYou.cs, FakeSindriHandler routes, SeenRequest.Query, contract facts (SindriApiContract.NeedsYou.cs).
- No P4 goldens exist on main yet (no needs-you*/decisions*/requests* under tests/Aw.Sindri.Tests/Golden/Api): spec-shaped files under tests/Aw.Sindri.Ui.Tests/Golden/SpecShaped/{needs-you,decisions,decisions-id,requests}. Present: needs-you fixture, one-request, empty, stalled, rebuilding, after-decide, extra-profile-null-subject; decisions pending, decided, voided, by-item, empty; decisions-id accepted, declined, decided-elsewhere, voided, not-found, bad-choice; requests list-fixture, list-empty, list-by-item, list-state-accepted, filed, invalid, item-not-found, item-closed. GoldenContractTests places the slugs.
- NeedsYouMapper (ViewModels/NeedsYou): served rows + catalog + pending decisions -> NeedsYouFeed. Readiness from catalog ui word (ready / cli-only* / else pending; unknown action = pending); Decide expands to one action per option (OptionId, RejectLike, option cli); chip = served chip else derived by kind (DECISION/FAILED/PROFILE/PARKED); Reason = served label; ShownId = decision id for decisions else item; unknown urgency = Pulse.

## T5 re-read tests (done)
- ShellViewModel.NeedsYou.cs: Health.Follow(ReadNeedsYou) reads needs-you + pending decisions on connect and every event, any page, maps and calls ShowNeedsYou; a failed read keeps the last rows. CatalogViewModel.Listed keeps the last CommandList. Counts come from the queue groups (running/withYou/queued), read just before.
- RequeryTests: 9 tests. Requests re-read and profile re-read are NOT wired (no view/route in the app yet): the plan's requests/profile rows of the table are left for the Requests tab and Profile tasks.

## Left
- Not done from T1: CliText control/bidi backstop test (no CliText exists in the UI package; do it where copy strings first render). Remaining goldens from the plan (needs-you large, session-repo, decisions stalled/empty-decided, requests too-long/too-many/etc., by-item variants) and re-pointing to P4's real goldens when they land.
- T2, T3, T4, T14 were already done; T5 summary/shell done. Left: T6 (decide from row and decision sheet) through T20 (Home, Decisions/Requests pages, sheets, strip, tray menu wiring, views, proof).
- Home does not render the feed yet; NeedsYouViewModel is not hosted by the shell.

## Commits (newest last, after merge 36ea35bf)
3c376fe1 T1 red, 321b3c8e T1 client green, a3243f04 mapper red, eb1abd52 mapper green, 22815ef9 T5 red, 053997f0 T5 green, attention-test fix, e6309d6d merge of #255.

## Gate (final)
build -warnaserror 0 warnings; UI unit 1210/1210; integration 416 total, 410 passed, 0 failed, 6 skipped; Aw.Architecture.Tests 117/117; dotnet format exit 0; check-deps ok; sync-rules ok.
