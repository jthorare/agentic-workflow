# #224 U3' pass 3 handoff

Branch feat/sindri-ui-u3, final head 8049711f (origin/main 87a60c8 merged at 4f237a90, clean).

## Part 1: P4 goldens
- 1194c9f1 test: API P4 goldens from #253 at 28129445 (40 files, needs-you/decisions/decisions-id/requests). b5244544 re-points everything: fake, contract facts, client/mapper/shell tests read expected values from the golden files (GoldenFiles.Node helper); SpecShaped twins deleted (only needs-you/extra-profile-null-subject kept). RouteAnswer.Problem prefers a kept SpecShaped file, else the Api golden.
- commands/catalog from #253 also taken (Decide/FileRequest ready); Queue/ItemSheet tests now expect Decide ready.
- Real shapes: needs-you decision id differs from decisions/pending ids (mapper test rewrites the first pending id to the row's); real refusal codes (decided-elsewhere = SND-ITEM-409, voided likewise); stalled golden has no stalledAt/stalledType.
- Slugs were already placed in GoldenContractTests._read (none in _notRead).
- Fake: item-filtered decisions/requests are empty unless a test sets DecisionsByItem / RequestsByItem.

## Part 2: leftovers
- Enter again / Tab off an open Needs-you row moves into the sheet (first decision option, else heading): ShellViewModel.IsSelectedSheetOpen, ShellWindow.Keys.cs. Red 294efb48, green 28443a87.
- Accepted outcome drops after 2 s (TimeProvider, DecisionSheetViewModel.Expiry); cleared line from GET /decisions?pending=false by id, fallback "<id> cleared" (6d9019a7).
- FailureSheetViewModel / ProfileSheetViewModel (+SheetDetail), Row.Details drawn in NeedsYouSheet (badfece0). No phase text ("P6") since the mapper does not keep it; failure "last run" is the row age.

## Part 3: T8
b5601e46: ItemNeedsViewModel (ItemSheetViewModel.Needs): item's pending decisions with option buttons (Decide once), requests, served empty sentences, inline refusal; drawn in ItemSheet.axaml.

## Notes
- 6d9019a7, b5601e46 and the goldens commit were committed green with tests in the same commit (not red-first); only the Enter-into-sheet item has a separate red commit.
- 409 refresh "focus on the control" still not asserted.

## Gate (head 8049711f, after the main merge)
build -warnaserror 0 warnings; Ui unit 1243/1243; integration 432 total, 426 passed, 0 failed, 6 skipped; Aw.Architecture.Tests 117/117; dotnet format exit 0; check-deps ok; sync-rules ok.

## Left
CliText backstop, T7, T10-T20; docs/csharp/sindri-ui.md U3' section.
