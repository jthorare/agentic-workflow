# sindri-ui-r8 run 4, pass B handoff

Branch feat/sindri-ui-r8-contract. origin/main (00f53f6, #237) merged first (6fa48ab6).

## Commits
- W3: fa11bfa6 red, f1b21f9b green (focus-region style in FluentOverrides.axaml: AccessibilityView=Control, ControlTypeOverride=Group; peer tests for Page, Sheet, TopBar region + focused Page peer named by PageName). ShortcutsCard gets it from the same class.
- W4: b5b94f49 red, then green (SheetTitle is now a Border, Text-typed, Name=SheetName, wrapping Raw TextBlock SheetSubjectText; Focus.cs and axaml.cs use the Border; tests renamed to SheetSubjectText). Red test first used an item link (item names stay bare, so SheetName == subject); fixed to workflow/WFI-1 in the green commit.
- W6: 3caab84d red, 8071c779 green (ServedCli Focusable; OnOpened posts focus to it when CanCopy is false; Esc already returns to the lock button).
- W5: 2feed074 red, b402052d green.
- Last: fix commit (ShellSheetKindTests XAML pin, IDE0002 format).

## W5 outcome
Avalonia 12.1.3 has no ClearFocus on IFocusManager and Focus() on the focused element raises nothing. Working approach: on CurrentPage change with focus on Page, toggle Page.IsEnabled false/true via SetCurrentValue (focus drops), then Focus again. The test counts GotFocus with Source=Page: exactly 1 for System->Home and Home->Work from the region; the control-start row still lands once. Whether the OS screen reader (macOS) speaks it is not provable headless: keep it on the owner backlog. Method lives in ShellWindow.Focus.cs (KeepFocusOrReannounce).

## Gate (final tree, before the last two trivial test-only edits' re-run of integration)
build -warnaserror 0 warnings; UI unit 918/918; UI integration 302 (300 passed, 2 skipped); Architecture 117/117; Aw.Sindri.Tests unit 2381 (2380 passed, 1 skipped); dotnet format exit 0; check-deps, check-rules, sync-rules --check all 0.
The one unit failure found (ShellSheetKindTests XAML pin) was fixed and UI unit re-run 918/918.

## Left
PR body "Review run 4" (coordinator), MERGEABLE check, W7 (recipe goldens; #233 is on main now, not done here, not in scope).
