# sindri-ui-r8 R2 (focus ring lost on an arrowed nav tab), final head f72f99ce

Cause: not Y1. Avalonia 12.1.3 SelectingItemsControl.MoveSelection (ListBox.OnKeyDown) focuses the next ListBoxItem with
NavigationMethod.Unspecified, so the tab gets :focus but never :focus-visible (confirmed by a GotFocus stack trace). The
stock ListBox arrow behaviour; Y1's KeepFocus path never touched focus (HasUsableFocus is true).

Fix (ShellWindow.Focus.cs): the nav ListBox's GotFocus handler sees an Unspecified focus on a tab while a nav arrow key
(_navKey) is pending, and posts RefocusAsKeyboard: focus the health pill, then the tab, both Directional (focusing the
element that already has focus is a no-op; the ListBox itself is not focusable). Pointer press clears _navKey.

Commits: b382fa97 merge origin/main (clean); ea6ff992 test: red (ChordFocusTests.An_arrowed_nav_tab_keeps_the_keyboard_focus_ring, Right and End, failed on :focus-visible missing); f72f99ce fix.
N3 (hint over the magnifier at 900 px): not done, not trivial without a visual check.
Gate: build -warnaserror 0 warnings; unit 920/920; integration 312 total, 310 passed, 2 skipped (+2 new); dotnet format clean; check-deps 0; sync-rules --check 0.
