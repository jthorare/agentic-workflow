# sindri-ui-u2: #197 brought onto #215 (7146baa) and #242 (a9e48f0). Final head 2e71fcd3

Commits: d6ba56be merge #215 (builds, 1072/1074 unit); bb57f403 item sheet in SheetContent + nav counts step; 3ef073b5 merge #242 (one conflict: GoldenContractTests _notRead, kept both); 2e71fcd3 test updates.

## Conflict resolutions
- PageId: #197's Queue/Decisions/Services/Health pages are gone. Queue page = PageId.Work (QueuePage in ShellWindow.axaml, IsWork; x:Name stays QueuePage), Health = System, G Q opens Work. Decisions/Services pages dropped (G D = Home filter per #215). Palette entry Go to System.
- NavEntry: class (ObservableObject) with settable Count + HasCount (identity kept so selection survives count changes); #215's 7 tabs; the queue count goes on the Work tab.
- Chord: ONE implementation, #215's ShellViewModel.Chords.cs (PressKey, 1 s Task.Delay on TimeProvider + generation, IsChordPending, ChordHint computed, hint in #215's top-bar grid). #197's Press(key,inSheet) now: key guard, then PressKey, then Single/RowKey. #197's timer/_chordAt code and its TopBar hint removed; its chord tests dropped in favour of #215's ShellChordTests (took theirs) + one Press-route test in ShellItemsTests. Window: OnKeyDown = Esc cancels chord first, then TabOutOfOpenRow || OpenByKey || (!OverlayOpen && PageKey). Removed #215's Chord(e) (PageKey/Press covers it). F6/ChordKey/RegionStep moved to ShellWindow.Regions.cs (300-line cap).
- Sheet: Navigate/CloseSheet/ReopenSheet/SheetSubject duplicates removed from Items.cs; #215's OpenSheet calls new OnSheetOpened (Items.cs: OpenerRow=null, Item.Show/Open for Item links); IsItemSheet computed from SheetLink. DeepLinkKind.Item => false added above both markers; marker texts updated. SheetTitle is now a Border (Control) – Keys.cs/ItemSheet code and tests use Control.
- ShellWindow.axaml: item sheet in SheetContent panel beside SheetBody (placeholder hidden for items); sheet keeps Group + SheetName.
- Page narrowing beside the sheet/PageLayout.Tier, rows' focus border, F2 Enter/Tab, UiThread late-post: kept as-is.
- deps.yaml note, ISindriApi/JsonContext/SindriApiContract/FakeSindriHandler: both sides' routes, markers last. Docs merged, U2 keys text points to Rev 8 chords.

## Additional fixes found by the lanes
- Top bar: Work count made the 900 px/1.3 scale bar overflow by 8 px; new last fit step NoCounts (TopBar.Counts.cs, class nav-count/has/no-counts).
- QueuePage x:Name GroupHeader removed (NamedControlsTests); QueueFramesTests selects by class group-head.
- Judgement calls in tests: modal cards (palette, shortcuts, create) stay peer type Window (#197), so main's named_groups test asserts Window for ShortcutsCard; chord-hint peer test updated to #215's Control/Text hint; NavTabClickTests Work case is the QueuePage, no arrives note.
- Red-first for step 2: ShellSheetKindTests.An_item_link_claims_the_sheet_from_the_placeholder and SheetEnterFocusTests.An_item_link_shows_the_item_sheet_in_the_shells_sheet_content failed with the arm commented out.

## #242 / follower
Follower tests pass against the P5 goldens (process-started PublicEvent etc.); no event-record change was needed.

## Gate (final head)
build -warnaserror 0 warnings; Aw.Sindri.Ui.Tests unit 1075/1075; integration 396 total, 394 passed, 2 skipped (macOS), 0 failed, no "error" in run summaries; dotnet format --verify-no-changes 0; check-deps 0; sync-rules --check 0.
