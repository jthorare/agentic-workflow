# sindri-ui-g1 needs-you slice 1 (builder digest)
Worktree /home/thor/repos/aw-wt/sindri-ui-needs-you, branch feat/sindri-ui-needs-you (b6f564402, merged origin/main 94785e953).
## Built
- VM: NeedsYouGrouping (KindOf/Split), NeedsYouInboxGroup (IRowGroup), NeedsYouPageViewModel (shell.Inbox), ShellViewModel.Inbox.cs (OpenNeedsYouInbox/LeaveNeedsYouInbox; Home stays the page), Labels.NeedsYouPage.cs, NeedsYouRowViewModel.Inbox.cs (InboxLine).
- Views: StickyGroupList (Views/Controls), NeedsYouPage, NeedsYouInboxRow, styles in NeedsYouStyles.axaml, hosted in ShellWindow.axaml beside the Pages scroller.
- Entry: Home "all N", Enter / title click on a Home row, needs-you/<row> link (non-decision). Esc + breadcrumb back. Decision rows and strip Answer + answer-in-child fallback keep the sheet (NavigateToRowSheet).
## Commits
1d712c20a red tests; f996fc5ff feat; fix(headings x:Name + tests); merge origin/main; fix hold/sticky a11y.
## Gate (last full numbers before merge): unit 2281 (all pass after answer-in-child fix), integration 705 (695 pass, 10 skipped, 0 failed), frames PNGs viewed at 1440/900.
## Re-gate on merged head: running (scratchpad gate2.log). Needs: Rendering.Tests, Architecture.Tests, scoped format, IDE0001.
## Decisions for lead
- Card shows the row's existing controls inline (no ActionBar until slice 2) so a link to a park/desk row can still answer it.
- Key hints show only "J K move" (A/D/Enter belong to slice 2); J/K walk the inbox, other Home keys inert while it shows.
- StickyGroupList lives in Views/Controls (spec says Controls/); non-virtualized (inbox is tens of rows), no reflection (AOT).
- Head item link reads "Open the item" and the grey reference shows ShownId/Id with Copy (spec); the plain-words guide says no raw ids up front, so the reference is the one place an id shows.
- Kind tile glyph is one generic diamond for all kinds (per-kind glyphs not specified); no dot-grid (context page not merged), plain canvas.
- "pauses in ..." omitted (timeout not on the row); "N new" pill pluralised ("requests").
- Closing the inbox with Esc: select row clearing picks the next row in FEED order, not group order (NeedsYouViewModel.Clear unchanged).

## DONE (2026-10-20)
PR #521 https://github.com/jthorare/agentic-workflow/pull/521, head b5ed3dcd2 (pushed). Gate on merged head: unit 2281/2281; integration 705 (695 pass, 10 skip, 0 fail); Rendering 21/21; Architecture 128/128; scoped format + IDE0001 clean.
Left: slices 2-5 (action bar + keys, typed/held, answer form + takeover, pending data).
