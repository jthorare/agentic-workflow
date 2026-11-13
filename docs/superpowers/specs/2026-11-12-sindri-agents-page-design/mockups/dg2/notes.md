# DG2 picks where the spec (#3334 section 15) is open

## Copy TBD (invented, needs an owner word)
- 013 text decided in review: "Codex isn't signed in. Run `codex login` in a terminal, then Start again."
- 017 text: "The prompt is 8,214 characters. The limit is 8,000." (spec: "with the character count"). Copy TBD.
- 014 git line in the worktree-failed frame is a sample git error, not real output.
- "Onboard it" (link under a repo that is not onboarded): spec says "a link to onboard it", no text.
- Sample data only: the prompt, ledgerly-app, the worktree path, the Warp tail lines, "Ended 14:02".

## Picks
- Frame 2: state word Starting; list chip reads "Starting" (the 120 px column clips "Waiting for you in the pane"); target name "claude in sindri".
- Frame 3a Connecting shows an empty pane. Frame 3b Reconnecting is list-focus only (a drop moves focus to the list).
- Frame 4: Retry now and "Esc closes the view" only in the card. Frame 5: spec sentence and Take it back only in the card, no header state word.
- Can't attach (6) and Gone (7) are list-focus only: a card has nothing to type into, so the pane-focus footer would be false.
- Frame 4: the top-bar pill reads "Not running" with the grey dot (the grammar's `.status-pill.off`, the app's HealthPill word Labels.NotRunning).
- Ended (8) uses a dashed glyph and the chip "Ended" under a "Recently ended" group. The banner sits over the dimmed last screen.
- Header on can't attach shows socket kind "warp tab" (spec lists sindri, default, team for tmux only).
- Form: no Cancel button (Esc closes). Start button reads "Start (Ctrl+Enter)". Form opens over the Agents list with a scrim.
- Terminal text is sample Claude Code output in the existing mono (13 px) and ink tokens; no new colours or sizes.
