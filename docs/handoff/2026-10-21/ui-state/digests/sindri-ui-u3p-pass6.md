# #224 U3' pass 6 handoff

Head: see last line of `git log` on feat/sindri-ui-u3 (docs commit is the head). origin/main had not moved (no merge).

## Commits
- 24e5c833 test: column tops above the fold with 5 Needs-you + 3 strip rows (red, 977 px)
- b53301f1 feat: two-line Needs-you rows (NeedsYouRow.axaml, NeedsYouStyles.axaml)
- 3d710a31 test: Settings' text scale reaches both lists (red; ShellTextScaleTests, ApplyAppearance stub)
- 593ff86a-ish (two commits) feat: ApplyAppearance sets NeedsYou.TextScale + Strip.TextScale, wired in SindriApp.Settings; test: FocusRingTests health-strip test back to default 1280x800
- docs commit: screens spec 4.1 and docs/csharp/sindri-ui.md (provisional owner default, wiring)

## Measured at 1280x800, scale 1.0, needs-you `large` (5 rows) + 3 strip rows
Needs-you row height 61 px each (was 88 before compaction; 129 pre-pass); strip rows <= 56; RunningPanel top = QueuedPanel top = y 712 (was 977).
Row: line 1 chip, id, title, new, age; line 2 (DockPanel, controls docked right) reason, repo, notice (trimmed, tooltip) and the controls. A protected control's CLI sits beside its button (no wrap).

## Text scale
ShellViewModel.ApplyAppearance(AppearanceSettings) -> NeedsYou.TextScale and Strip.TextScale; SindriApp.Settings passes a callback that runs the theme apply then the shell's. Settings.Load applies at start, every change applies live. Tests: Shell/ShellTextScaleTests (real SettingsViewModel + preference store).

## FocusRingTests
Health-strip test reverted to the default window height; passes.

## Gate
build tests/Aw.Sindri.Ui.Tests -warnaserror exit 0; Ui unit 1300/1300; Ui integration 469 total, 459 ok, 10 skipped (macOS-only), 0 failed, no error, no 134; Aw.Architecture.Tests 117/117; dotnet format exit 0; check-deps ok; sync-rules up to date; sindri-ui-fixture.test.sh 0 failures.

## Left
- Owner answer on the fold default (two-line rows) is still pending; docs mark it provisional.
- Text-scale 1.3 check on narrow pages / the long-notice trimming at <= 900 px: the controls dock right and can squeeze the reason text (ResponsivePageTests passes).
- Screens_spec_states_the_strip_cap_at_scale_1_3 still unwritten (needs owner OK of the 1.3 cap).
- Items from pass 5b: CliText backstop, u3-file-request frame, live P4 run of fixture verbs, park-row rule re-check at P11, WindowsDockBadge on Windows.
