# sindri-ui-u2 mr fixes: head 04404a9f (pushed to feat/sindri-ui-u2)
- mr-0: ShellViewModel.GoTo(page, filter) resets Queue to All when page is Work; GoToQueue(filter) sets Queue.Filter then ShowPage (no reset). Red test ShellItemsTests.G_q_and_the_work_tab_show_the_whole_queue_after_a_running_only_visit (G Q + tab via SelectedNav) failed before the fix.
- mr-3: docs/csharp/sindri-ui.md: three regions are Groups, modal cards (shortcuts, palette, create) Window-typed.
- mr-4: comments updated; QueueFilter.WithYou dropped (no production caller), with its two tests (the Running equivalents remain).
- mr-5: Labels.Palette FixedKeys chord row and docs palette Go-to list follow SecondKey (H N D W Q R C A . S). No test pinned the old text.
- origin/main had not moved; no merge.
## Gate
build -warnaserror 0 warnings; unit 1074/1074; integration 396 total, 394 passed, 2 skipped, 0 failed; no error, no exit 134; format exit 0; check-deps 0; sync-rules --check 0.
