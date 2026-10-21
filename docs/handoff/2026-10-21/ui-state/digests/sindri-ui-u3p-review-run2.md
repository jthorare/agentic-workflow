# #224 review run 2 fixes: PARTIAL handoff (builder stopped at context guard)

Worktree ~/repos/aw-wt/sindri-ui-u3-r1 (branch fix/u3-r1). Head ebefebe6 (merge of origin/main a5da181), pushed to feat/sindri-ui-u3.
Brief: ~/.agentic-workflow/jthorare-agentic-workflow/plans/sindri-ui/u3p-review-run2-fixes.md. Helpers (scratchpad, may be gone):
/tmp/claude-1000/s/t.sh (heavy dotnet test --project tests/Aw.Sindri.Ui.Tests ARGS) and c.sh (commit+push with trailers).
Commit trailers: Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com> / Claude-Session: https://claude.ai/code/session_015vbT7hF9WZaACP2HjKKBtZ

## Done (red / fix)
- L10: b62c75d0 / ca9d98af. New ViewModels/UiContext.cs (captured at construction, used by SeenTracker, NeedsYouSeenViewModel, HealthViewModel, SettingsViewModel); SeenTracker timers carry generations (SeenTracker.Timers.cs partial).
- L1: (red "test: a filtered-out selected row...") / ad262ca9. Filtered-out selection is deselected quietly; only a row gone from the whole feed Clears.
- L2: red (RecommendedOptionTests) / c7fa4e29. BuildControls: option actions primary iff Recommended; served order kept.
- L3: red (ItemDecideRetryTests) / d344d370. ItemNeedsViewModel.Reset() from ItemSheetViewModel.Clear(); Choose guards on the item pressed on (race itself has no test).
- Merge of origin/main done and pushed; sindri-ui-fixture.test.sh ran after it (ok lines seen, tail only; rerun for totals in the final gate).
Unit lane after L3: 1366/1366 pass.

## Not started
L4 (was reading HomePage.axaml.cs Look(); needs window IsActive + Activated/Deactivated), L5, L6, L7, L8, L9, L11, all cheap items, final gate, PR-body "not taken" list.
Note: AW1005 300-line limit hit once (SeenTracker, split). MA0002 needs StringComparer.Ordinal in Assert.Equal/NotEqual on strings.


## Update (second builder, stopped at context guard) -- head 10b11c83, pushed
- L3 follow-up: 33bd7238 test only (Choose_sets_a_refusal_only_if... passes at once: the pressedOn guard already existed, so there was no red).
- L4: 6c356b64 (test and fix in one commit, no separate red). HomePage.Look needs Window.IsActive; Activated/Deactivated handlers re-evaluate the focused row. IsActive is not yet true inside Activated, so the handler passes the state itself. The test raises the headless platform's Activated/Deactivated callbacks by reflection (NeedsYouLookTests).
- NB1 (BLOCKING, done): red "test: a row's own option button ..." then afe6cbee. Cause: NeedsYouRow.OnAction raised ActionEvent with RaiseEvent, which reset e.Source to the row, so ActionOf (which read Source's DataContext) returned null. Fix: new Views/NeedsYou/RowActionEventArgs carries the action; HomePage reads e.Source as NeedsYouRow. Tests: Views/NeedsYouActionButtonTests (click, Space). Not covered yet: failure/profile/park row buttons through the same path (they share the one template and handler; add a Park-row case if wanted).
- N7: 10b11c83. scripts/sindri-ui-fixture.sh fact_args supplies fact:scope for the triage recipe; sindri-ui-fixture.test.sh covers it (full file exit 0).
- NOT STARTED: L5, L6, L7, L8, L9, L11, the cheap items, the final gate (build -warnaserror, Ui.Tests unit+integration, Aw.Architecture.Tests, format, check-deps, sync-rules --check), PR-body "not taken" list.
- Helpers: /tmp/claude-1000/s/t.sh (test) and c.sh (commit+push).

## Update (third builder) -- head c035e0fc, pushed (origin/main 6b958ed merged at b16f1e26)
- L5: red 1e00ec96 / fix ed8c4d34 (CloseSheet focuses SelectedId when the open row is gone).
- L6: red 1caf2edb / fix d77a986a (outlined+selected: 4,2,2,2 border with matching padding; focus-visible restates 2 all round). The red's precondition was weak (focus stayed), fixed in the fix commit.
- L7: red / fix 1a9e24ed (ShowNeedsYou collects the cleared line and the rise into "X cleared, 3 now need you").
- L8: red / fix b7ec3a7b (DecisionOptions.From x2; item sheet takes the catalog's Decide readiness; ItemDecisionRow doc fixed).
- L9: red / fix 2b8a9969 (NeedsYouSubject.DecisionId -> NeedsYouEntry.DecisionId; NeedsYouViewModel.Row / RowOfDecision; NeedsYouKinds docs).
- L11: 11ae56b2, test only (already green: pins (Code,Kind) for non-2xx and unparseable 200).
- Cheap done: test-r2-5 and a11y-r2-4 (c035e0fc; HasNewWord, NewWord shown with motion on).
- Cheap NOT taken (context): test-r2-4, test-r2-3, corr-3, arch-6, arch-7, test-r2-6..9, and the rest.
- Gate (after the main merge, before the last commit): Ui.Tests unit+integration 1877 total, 0 failed, 10 skipped (rerun after c035e0fc: same totals); Aw.Architecture.Tests 117/117; check-deps ok; sync-rules --check ok; dotnet format exit 0 (on c035e0fc); sindri-ui-fixture.test.sh exit 0, no "not ok". Build -warnaserror is the test build (zero errors).
