# sindri-ui-u2 review run 1, part B: handoff

Worktree /home/thor/repos/agentic-workflow/.claude/worktrees/sindri-ui-u2, branch feat/sindri-ui-u2, head 02c5a017 (pushed, green).

## Done (red commit then fix commit, pushed)
- L6: 9bf4deec (red) ac360065 (fix). ItemSheetViewModel.Reads.cs holds the reads (split: file limit 300 lines AW1005, method limit 40 MA0051).
  `_generation` bumps per Read and in Clear; ReadItem/ReadTimeline drop answers when generation differs; `_reads` CTS is cancelled by the
  next Read (OCE caught when the caller's token is not cancelled); LoadMore has `_loadingMore` and drops a page when id/Next moved.
  Fake: FakeSindriApi.Items.cs got ItemGates (per id) and TimelineGates (per 'after') TaskCompletionSource dictionaries.
- L7: red (commit before 02c5a017) fix 02c5a017. Queue.HasProblem/ProblemCode/ProblemDetail (first read failed, IsLoading ends),
  sheet shows the same via Problem() with `_readProblem`, cleared on the next good read. Stale marks: QueueStale (QueuePage),
  RunningStale/QueuedStale (HomePage pheads), ItemStale (ItemSheet facts). ItemText.Stale added.

## Left (in the fix-list order), with the designs I settled on
- L8: ItemSheetViewModel.Actions.cs ShowActions: keep a control whose Phase is Sending/Accepted (and Failed while the sheet HasProblem)
  even when the item no longer allows its action; track `_allowed`; subscribe to each control's PropertyChanged(Phase) and re-run
  ShowActions(_allowed) when it reaches Idle (or Failed with HasProblem) so it drops. In Close(), the 409 test must use
  `_allowed.Contains(action)` not `_controls.ContainsKey`; when the control is gone, mute it (`control.IsMuted = true`, internal set) so
  only the sheet speaks. Sheet live region: copy QueueViewModel.Say/Rest (QueueViewModel.Selection.cs ~L95) into the sheet
  (IsAnnouncing, Announcement rest Labels.Sindri), speak the 409/404 line once; add `ItemLive` TextBlock (live-region class, Width 0) to
  ItemSheet.axaml like QueueLive in ShellWindow.axaml. Focus: sheet event raised BEFORE Actions is swapped with the dropped controls;
  ItemSheet.axaml.cs focuses ItemTitle (make it Focusable) synchronously if the focused element is inside a CommandControl whose
  DataContext was dropped. Tests: fake OnClosed moves the item to items-id/after-close; Press, Refresh -> control stays (Accepted,
  announced once: log Announcement while IsAnnouncing), clock.Advance(AcceptedFor) -> Actions empty (continuations run inline on
  FakeTimeProvider); view test in QueueFocusTests/QueueFramesTests: focus RunButton, 409 path, focus ends on ItemTitle, never null.
- L9: 409 where the control survives: no sheet Problem, control ErrorCode SND-ITEM-409 + detail, retry sends the new version
  (CloseItemRequest version 2). corr-9: Press takes `_version`-style snapshot for the destructive confirm (ControlViewModel.Confirming.cs);
  Keep cancels the question when the version changes.
- L10: CreateItemDialog modal: count Create.IsOpen in the OverlayOpen computation (ShellWindow.Overlays.cs / ShellViewModel Keys),
  Tab cycle in the card, focus back to opener (fallback CreateButton), AutomationProperties.Name on the card as dialog, title label
  "Title (required)", blank submit shows "Title is required" under the field (spoken once, focus to title, nothing sent),
  CanSubmit false once CreatedId set.
- L11: ConfirmButton name "Confirm: <question>", KeepButton "Keep <id>"; question spoken once via a gated region; drop ConfirmText's
  duplicate name; Esc on the row refocuses RunButton (same path as Keep click, CommandControl.axaml.cs).
- L12: after LoadMore lands, focus first new timeline row; button name "Load more timeline for <id>".
- L13: fake/contract parity for P3 (FakeSindriApi.Items.cs: CreateItem/CloseItem check Down/Refusal before recording and set _pending;
  Queue/Item/Timeline honour Carried/Behind + Lag; FakeSindriHandler passes X-Sindri-After; add P3 routes to the four every-route
  contract theories in SindriApiContract.cs; one RYW contract fact), then the behind-case tests.
- L15: Refresh after LoadMore keeps all pages (TimelineCalls, 101 rows); page error mid re-read keeps old timeline; failed LoadMore
  leaves rows/Next. (The L6 code already preserves these; write the tests.)
- Cheap: testing-4 catalog parity; testing-5 Keep fallbacks + G W filter; testing-6 CommandGate "sends once", drop unused fake surface;
  arch-5 palette calls Press and ControlViewModel.Run becomes private; arch-6/corr-12 remove ControlViewModel.ClearError;
  arch-7 doc catalog-p3.json -> commands/catalog in docs/csharp/sindri-ui.md; corr-9 (above). Not in my brief: corr-11, a11y-12.

## Process notes
- Build+test helper: /tmp/claude-1000/ut.sh '<method glob>' ['<glob>' ...] (multiple globs OR together; none = whole project).
  It runs under ~/.agentic-workflow/bin-r1b/heavy with `timeout 1700`. A single UI test run takes 3-5 min on the shared box.
  ALWAYS keep the timeout: a hanging test (second LoadMore awaited on a gated fake) once held the slot for 21 min.
- Analyzer limits that bit: AW1005 300 lines/file, MA0051 40 lines/method, MA0042 CancelAsync, IDE0058 unused result.
- Final gates still to run: UI unit lane x3, integration x1, Aw.Architecture.Tests, heavy dotnet format --verify-no-changes,
  check-deps, check-rules, check-env-example, LC_ALL=C scripts/sync-rules.sh --check, bash scripts/tests/sindri-ui-fixture.test.sh,
  gh pr view 197 --repo jthorare/agentic-workflow --json mergeable.
- Written replies: arch-3 (refresh registry -> ViewRefresh object: do with U3' when the next follower arrives), arch-4 (sheet controls
  on catalog re-read: follow-up), arch-8 (IAccepted interface: follow-up).

## B2 (second builder): L8, L9, L11, L12, corr-9 done; head see git log (pushed, green, format clean)
- L8/L9/corr-9: red aa9dc41d, fix 1ddeaddc. L11/L12: red e7e21845, fix f526ba2b. Then a format chore commit.
- Design: Actions is now an in-place ObservableCollection; ItemSheetViewModel.Reconcile keeps Sending/Accepted controls (and Failed while no sheet line owns the error); ControlsLeaving event -> ItemSheet focuses SheetTitle.
  Sheet live region ItemLive in ShellWindow.axaml. Questions cancelled on version change (Show -> CancelQuestions). Fake got QueueGate.
- Left: L10, L13, L15, L14?, cheap items (see list above). Note arch-6: ControlViewModel.ClearError still unused-but-present.

## B3 (third builder): L10, L13, L15 done (pushed, head e270d572, format clean, UI unit 741/741, integration 257 passed + 2 skipped of 259)
- L13: red ed62a52b, fix (next commit). Fake: Queue/Item/Timeline internal overloads take X-Sindri-After, share View<T>; CreateItem/CloseItem check Down/Refusal before recording and set _pending. Contract: SindriApiContract.Items.cs (P3Errors in the four every-route theories, never-recorded theory, 2 RYW facts).
- L15: tests only (the L6 code already holds), one commit.
- L10: red + fix e270d572. ControlViewModel.Precondition (Func<bool>) used by CreateItemViewModel.Ready (blank title -> TitleError, gated region, TitleFocusRequests); ShellWindow overlays handle Create like palette/shortcuts. View tests: tests/Aw.Sindri.Ui.Tests/Views/CreateItemDialogTests.cs.
- Left: L14, cheap items (testing-4/5/6, arch-5/6/7, corr-11, a11y-12).
