# B3 home-work digest (Sonnet builder, resumed 2026-10-19)
Worktree /home/thor/repos/aw-wt/sindri-ui-f3-b3-home-work, branch feat/sindri-ui-f3-b3-home-work.

## Commits
- 2c3d32861 checkpoint from the dead builder (tests + StatusLine, Sentence, DetailsOpen, HealthSentence)
- next: labels + ItemSheet.axaml + HealthPage.axaml (plain words, Details expanders)

## Inventory (keep / rename / move into Details)
| Where | Was | Now | Why |
|---|---|---|---|
| Queue columns | ITEM/STATE/STEP/SINCE | ID / STATUS / STAGE / AGE | guide: state->stage |
| Queue group + state word | Parked | Paused / Paused: needs a decision | guide: park |
| Queue badge | parked | paused | same |
| Item sheet header | repo, state, "version N", age in one row | one status sentence "Running for 2h 56m" + repo | primary thing |
| Item sheet "Waiting on" | Waiting on | Waiting for (kept up front) | decides what to do |
| Item sheet Created by / Version / Instance address | up front | behind closed "Details" expander | bookkeeping |
| Item sheet TIMELINE | TIMELINE | History | plain word |
| Item sheet Instance | Instance | Run (in Details) | guide |
| Needs-you profile sheet | Profile hash / Approve with | Fingerprint / To approve, run | plain |
| Action cancel | Cancel instance | Cancel run | guide |
| System page | six sections up front | one sentence ("Everything is running." / what is wrong) + closed Details holding control plane, services, profile, actions, last event | one primary thing |
| Home | Needs you leads already | kept | |
| Settings | not changed (Reduce motion, Text size are plain) | kept | |
Left alone (shared files, B4): Labels.cs "NEEDS YOU" panel eyebrow, "CONTROL PLANE/SERVICES/PROFILE/LAST EVENT" card names (now inside Details), Labels.Palette.
Not done: "facts" list is on the instance frame (B1), not the item sheet.

## Gate
script ~/.agentic-workflow/scratch/sindri-ui/b3/gate.sh, log gate.log.

## Review fixes (round 1, resumed 2026-10-20)
- ad8af7db6 456-2: Sentence = Checking… (no health), "The profile needs your approval." (profile fix), else as before. Actions not counted (pending catalog rows always exist).
- c3435b327 456-1: profile fix command + Actions section moved out of the Details expander (up front under the sentence); test with Details closed.
- 3f84d503f 456-3: queue ID column removed (PageLayout widths now 6 columns, title is column 0); item id + Copy in the item sheet's Details (ItemSheetViewModel.CopyId); ColumnItem label removed.
- 311264a64 456-4: SheetDetail.Secondary; profile hash + approve command in a closed "Details" expander on the needs-you sheet (PrimaryDetails/MoreDetails/ShowsCliUpFront).
- "Waiting on you" -> "Waiting for you" not changed (group label in Labels.Items.cs, pinned by other tests; not touched).
- Gate: ~/.agentic-workflow/scratch/sindri-ui/b3/fix-gate.sh, log fix-gate.log.
- Gate green at 9e36efd8c: unit 2062, QueueFrames 21, Rendering 21, Architecture 128, format clean. PR body updated.
