# #224 (U3') review run 1 fixes: handoff (2026-10-16)

Worktree ~/repos/aw-wt/sindri-ui-u3-r1 (branch fix/u3-r1), pushed to feat/sindri-ui-u3. Final head fb3c3d9b.
Main merged twice (91f7ca77, ff5ede85 = origin/main 2a989f5); P4c made SubjectRef.Kind non-null (one test follows, c824b07a).
Does main serve the strip's list shape (counts/asOf, API P11)? No. ListInstancesResponse is still {Instances, NeedsYou, Empty}; the
B6 sentence still shows for it and now only for a body this build can't read (a plain 500 keeps the rows). The strip's live proof is not reachable yet.

Gate (touched projects): build tests/Aw.Sindri.Ui.Tests -warnaserror 0 warnings; Desktop head builds -warnaserror; Ui unit 1357/1357;
Ui integration 499 total, 489 passed, 0 failed (10 skipped); Aw.Architecture.Tests 117/117; dotnet format --verify-no-changes rc 0;
check-deps ok; sync-rules --check ok; sindri-ui-fixture.test.sh 115 ok, 0 not ok; check-env-example ok. NativeAOT smoke (Ui.AotSmoke) NOT run.

## Blocking and the re-proof items
| Item | Commits (red / fix) |
|---|---|
| K1 | 2b1cdb1e / 6fbefac7 |
| K2 | b2d9442c / 03cf55af (the outlined row restates the PrimaryHover ring after itself, Primary when selected, same padding as the ring) |
| B7 | 91b75f23 / 14d0e014 (SeenTracker timers hop to the UI context; OffThreadTimers test clock) |
| B8 | e9392387 / 7882763b (cause found headless: TabOutOfOpenRow used FocusManager.FindNextElement, the nearest element on screen, so Tab from a row's Accept jumped into the sheet and skipped the row's Decline; it now leaves only from the row's last stop). The Linux "no ring, no AT-SPI focus" dead stop did NOT reproduce headless: every stop in the walk is visible, enabled and ringed (the new test pins that); re-prove on the real app. |
| N6 | not taken: the design spec (sindri-ui-design.md §5, §11.3, §11.4) writes the word as `new` in lower case and Labels.New is "new"; only the plan prose says "New". Left at the spec's word; one constant to change if the owner wants "New". |

## Issues
| K | Commits | Note |
|---|---|---|
| K3 | 3e5bcb27 / 43f5b5f2 | HomePage reports focus in a row to Seen.Watch; the panel sets NeedsYou.PointerOver; the off-thread timer test is SeenTrackerThreadTests (+ NeedsYouUndoTests on OffThreadTimers) |
| K4 | fee9e384 / 108f6a75 | Update(feed, show): the filter is applied in the list, retain/known ids use the whole feed |
| K5 | 3992b0d3 / fb7c950c | Entry.Label (secondary line) and Entry.Reason (served sentence); sheets show Reason |
| K6 | 76cc3bf1 / b22aff8e | DecisionSender shared by the decision sheet and the item sheet |
| K7 | 99a59161, 46a6d8d8 / 9d9ec557 | stable option list; focus to the heading when all options go disabled; the ring follows how the press was made; FocusNeedsYouRow falls back to KeepFocus when the frame is unusable |
| K8 | 16fa0555 / d82c83cd | NeedsYouAction.Recommended from the served `recommended` |
| K9 | 6c424ced / 04ba6668 | LiveLine (flag before text); one Needs-you region says rises, "<id> cleared", "<address> done"; AttentionAnnouncer keeps its count while not counting; the first list at startup announces (documented) |
| K10 | f62af96b / 8dafb508 | names; also fixed an Architecture cycle I introduced (a08d88d9) |
| K11 | 99fb0534 / 419dfce0 | pulse animates Background between two tokens; stale = muted Background (Surface2), no opacity |
| K12 | e509eb85 / 4ace88e6 | ShowOnDisabled + HelpText |
| K13 | 542e7955 / 7312b8d7 | focus follows an automatic move only if it was in the cleared row or already lost |
| K14 | 72556be7 / 8f39699c | Navigate canonicalizes via Canonical(); LinkOf uses it |
| K15 | 67838a64 / c33ec2e7 | steady/subtle classes; slide-in 180 ms via TranslateTransform.Y (no RenderTransform animator exists) |
| K16 | 76eb3bdc / 3b941a02 | CatalogReadiness.Parse, one fallback (CliOnly) |
| K17 | 75aea46e / 0b79f31d | NeedsYouChip/NeedsYouChipTone deleted; Entry.Chip is StateChip? |
| K18 | test in 0b79f31d / 0b79f31d, docs be512bef | park lookup keys on the served kind `park` (Entry.Kind); NeedsYouKinds holds the kind strings; client presentation rules listed in docs. The K18 test passes only with the fix (it could not be committed red separately: same commit as K17). |
| K19 | e0f17b33 | deps.yaml |
| K20 | be512bef | docs |
| K21 | 23982fe6, c9f2abc2 (tests), 89dc5786 / 9a84906f (fixture) | six Shell test classes under UiThread.Run, ShellWait (change-notification wait) and Health.ViewReads (a seam: counts completed view re-reads) replace polling and call counts; B1 J and refused-X focus tests; fixture request uses --fail-with-body |

## Cheap
arch-9, arch-12: 96f2a3e2. arch-10, arch-14: b7b21e7b (InitNeedsYou/InitStrip, HomeFold, marker texts). arch-13 rest: adcb0d1b (FailureDetails/ProfileDetails, Details cached).
arch-11/test-5: 4026c227 / f3dd784b (contract cases for Decisions pending/decided/by item, Requests by item, Decide, FileRequest; method guards on the P4 read routes). FileRequest kept with its contract case.
test-6: b54a174f / 32fce344 (a newer `since` alone now replaces the strip row; Signature includes it). test-7 (Mark all seen stops the pulse): b54a174f. test-9 (two routes log once each): b54a174f (passed at once; the key was already per route).
corr-11: 02d89db0 / fcb1aa4d. corr-13: 8c2b4a15 / 324333ed (SND-UI-003 for an unexpected status now has Kind Unavailable via SindriUiErrors.BadStatus, same code; the strip's sentence needs the unreadable-body kind). test-10: 89dc5786.
a11y-14, a11y-11, a11y-12 (name + hold while focused or pointer): 6ecf3dbb / 0bafb377 / 504c190e. a11y-13: ca050a7c / d7bcb0e0 (only the Answer path; the other strip opens go to the Workflows page, so Home's row is not on screen).
Not taken: arch-8 (MainWindowHandle seam, WindowsDockBadge text), a11y-12's palette Undo entry, as the brief says.

## B7 audit (anything that raises PropertyChanged or a VM event runs on the UI thread)
- SeenTracker.cs Watch dwell timer (~L142) and Undo timer (~L165, L195): pool thread; fixed 14d0e014 (OnUi hop, captured context).
- SeenTracker.cs Restore (~L65): continued on a pool thread after ConfigureAwait(false) and raised Seen; fixed daa6017b (ConfigureAwait(true)); red 53b70481.
- Desktop PolledMotionPreference.cs:25/Refresh: Changed raised on the timer thread; its only VM subscriber NeedsYouSeenViewModel changed rows and bound state there; fixed daa6017b (OnUi in NeedsYouSeenViewModel; context taken in Update/Restore); red 53b70481. SettingsViewModel.OnMotionChanged already posts to its context: ok.
- HealthViewModel.cs:108 ticker and HealthViewModel.Refresh.cs:61 sending timer: pool thread, both through OnUi: ok.
- HealthViewModel.Run / ControlPlaneFollower.Run, Cycle and the event-stream loop (ControlPlaneFollower.cs:167 `await foreach ... ConfigureAwait(true)`): each event resumes on the UI context; no ConfigureAwait(false) in the follower: ok. SindriApiClient's many ConfigureAwait(false) are inside the client (no VM/event raised there); callers await with ConfigureAwait(true): ok.
- ShellViewModel.Run = Health.Run (captures the UI context; all awaits ConfigureAwait(true)): ok.
- ControlViewModel.Run (ControlViewModel.cs:159) and CommandPaletteViewModel.Run (:150): awaits ConfigureAwait(true), Task.Delay on the clock with ConfigureAwait(true): ok. DecisionSheetViewModel.DropOutcome, ShellViewModel.Chords delay, ProtectedActionViewModel copy delay: ConfigureAwait(true): ok.
- Preferences.cs Update/Load (ConfigureAwait(false)): run only IO and the caller's `change` lambda; every lambda is a pure record `with` (SeenTracker.Persist, SettingsViewModel, ShellViewModel.Remember): ok. Load's callers resume with ConfigureAwait(true) (Settings, Shell, NeedsYouSeenViewModel); SeenTracker.Restore was the exception (fixed above).
- InstanceListener.Listen (Views/InstanceListener.cs:28-41): message loop on a pool thread; the action is claimed and run in Dispatcher.UIThread.InvokeAsync; the pool part only picks a delegate under a lock: ok.
- AppLifetime tray and dock-badge paths: ShowAttention runs from shell.PropertyChanged(Attention), which is raised by ReadNeedsYou on the UI context; tray row clicks come from the tray's menu on the UI thread (RowRequested -> OnRow -> navigate): ok. Quit awaits with ConfigureAwait(true): ok.
- SindriApp.axaml.cs:87 Open(...).ContinueWith(..., FromCurrentSynchronizationContext): UI: ok.
- Desktop file/socket adapters (FileHumanToken, FileEndpointRecords, FilePreferencesStore, SocketSingleInstance): IO on pool threads, no VM events: ok.
Not covered: native tray/dock adapters (MacDockBadge, WindowsDockBadge, AvaloniaTray) were read for threading only, not run; they are called from the UI thread by AppLifetime.
