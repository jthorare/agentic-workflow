# sindri-ui-r8 run 5 fixes (final head dc13ce19)

Commits (oldest first, recent ones listed): see git log. Items:
- Z1: 4bf07270 red, bf189460 green. New tests/Aw.Sindri.Ui.Tests/Views/ChordFocusTests.cs (chord from pill, nav item, non-covering sheet, region; palette Go to). KeepFocusOrReannounce now always focuses Page (re-announce toggle if already there); over a covering sheet (Page disabled) it falls back to KeepFocus. Palette path decision: 2 GotFocus on the region (palette close hands focus back, then re-announce), pinned at 2. Nav-tab clicks take the same path (any CurrentPage change except the Settings chord).
- Z2: fabf3848 (Settings chord test: SettingsSlot content set, exactly 1 focus event, focus inside SettingsSlot). Red verified by disabling the _openingSettings assignment: 2 events.
- Z3: 6e6083b7 red, 80bac39f fake -> SND-RECIPE-002 (both unknown id and unknown hash). Merged origin/main (3118181e, had #243), then 129f53c5 serves not-found golden through NotFound("workflow-recipe","not-found"); yes, #243's golden used. WorkflowGoldenTests not-found test now takes the code per route (later commit).
- Z4: 2516dbcc. Not red: every running recipe id already had a golden; the dead not-found branch is gone, hash of golden == recipe.hash of every instance, plus RecipeView Id/Hash/Version vs golden (testing-4). Doc line fixed.
- #227: SubjectRef.Kind is string?; client + fake test with kind null built from a golden in memory.
- Cheap: arch-3, arch-4, testing-5, testing-6 (test commit); arch-1/corr-4/a11y-3/testing-3 (red then fix commits: HasFocusableAction, OpenInAgent.IsEnabled binds !IsOpenInAgentPending, ServedCli.Focusable binds !HasFocusableAction); testing-2 ShortcutsCard peer row; corr-2 comment by CoverPage; arch-0/arch-2/arch-5 and null-kind note in docs/csharp/sindri-ui.md.
- Not done: moving ChordKey/IsChordStep into ShellWindow.Keys.cs (optional; ShellWindow.axaml.cs is 291 lines, doc tells lanes to use partials).

Gate (touched project only): build -warnaserror 0 warnings; UI unit 920/920 (after fixing the not-found golden test); UI integration 308 (306 passed, 2 skipped, run before the final test-only commit); dotnet format exit 0 on final tree; check-deps 0; sync-rules --check 0.
