# Agents live pane and New agent form mockups (DG2, #3379)

Spec: `../../2026-11-12-sindri-agents-page-design.md` (§15.3 the terminal view, §15.4 the New agent form, §15.5 a session the pane can't
attach, §15.8 the close codes and errors; the DG2 row of the §15.8 phases table). Static HTML, no production code. Colors, sizes and
radii come from `grammar-2978/grammar.css`, which copies the app's tokens (`src/Sindri/Aw.Sindri.Ui/Theme/Tokens.axaml`); no new
tokens. The pane text is 13 px JetBrains Mono. A one-off runs on Sindri's own tmux socket, so its header reads `tmux sindri` with no other client. Made-up sample data (lane `ideas`, repo `sindri`, `ledgerly-app`).

`python3 gen.py` writes every HTML file, then `bash shoot.sh` screenshots them with headless Chrome (1440 wide; `-1000` frames are 1000
wide). `agents3340.css` adds layout only.

## What DG2 covers

Issue #3379 and the spec's DG2 row (§15.8) ask for the terminal view in nine states and the New agent form in nine. This set adds a tenth
form frame, 017 (prompt too long). Each terminal state is drawn with focus in the list and focus in the pane (reconnecting, can't attach
and gone: list only, see the picks), at 1440 wide and at 1000 wide with the list as a 56 px glyph column.

- **Terminal view:** connecting, reconnecting, can't reach Sindri with Retry now, open in another window with Take it back, starting,
  attached, can't attach, gone, ended with Discard session (and Remove worktree).
- **Focus:** the focused side has the accent border; the unfocused pane has a neutral border and a hollow block cursor; the footer
  changes with focus. Reconnecting, can't attach and gone are list-focus only (below).
- **New agent form:** default (focus on Prompt), filled, starting, error 012 (folder gone), 013 (not signed in), 014 as tmux not
  installed, 014 as a failed worktree, 017 (prompt too long), a repo not onboarded, and the background-start line.

## Every frame

| PNG | State | Spec |
|---|---|---|
| agents3340-1-lane-focus-list.png | Attached (a lane opened from the list) · focus in the list | §15.3 Focus, Header |
| agents3340-1-lane-focus-list-1000.png | Attached (a lane opened from the list) · focus in the list · 1000 wide | §15.3 Focus, Header |
| agents3340-1-lane-focus-pane.png | Attached (a lane opened from the list) · focus in the pane | §15.3 Focus, Header |
| agents3340-1-lane-focus-pane-1000.png | Attached (a lane opened from the list) · focus in the pane · 1000 wide | §15.3 Focus, Header |
| agents3340-2-oneoff-focus-list.png | Starting (a just-started one-off, trust dialog) · focus in the list | §15.3 Focus, Header; 15.4 Starting chip |
| agents3340-2-oneoff-focus-list-1000.png | Starting (a just-started one-off, trust dialog) · focus in the list · 1000 wide | §15.3 Focus, Header; 15.4 Starting chip |
| agents3340-2-oneoff-focus-pane.png | Starting (a just-started one-off, trust dialog) · focus in the pane | §15.3 Focus, Header; 15.4 Starting chip |
| agents3340-2-oneoff-focus-pane-1000.png | Starting (a just-started one-off, trust dialog) · focus in the pane · 1000 wide | §15.3 Focus, Header; 15.4 Starting chip |
| agents3340-3a-connecting-focus-list.png | Connecting (socket opening, empty pane) · focus in the list | §15.3 Header |
| agents3340-3a-connecting-focus-list-1000.png | Connecting (socket opening, empty pane) · focus in the list · 1000 wide | §15.3 Header |
| agents3340-3a-connecting-focus-pane.png | Connecting (socket opening, empty pane) · focus in the pane | §15.3 Header |
| agents3340-3a-connecting-focus-pane-1000.png | Connecting (socket opening, empty pane) · focus in the pane · 1000 wide | §15.3 Header |
| agents3340-3b-reconnecting-focus-list.png | Reconnecting (dimmed screen, focus in the list) · focus in the list | §15.3 Connection, Header |
| agents3340-3b-reconnecting-focus-list-1000.png | Reconnecting (dimmed screen, focus in the list) · focus in the list · 1000 wide | §15.3 Connection, Header |
| agents3340-4-cant-reach-focus-list.png | Can't reach Sindri with Retry now and 'Esc closes the view' (top-bar pill reads Not running) · focus in the list | §15.3 Connection |
| agents3340-4-cant-reach-focus-list-1000.png | Can't reach Sindri with Retry now and 'Esc closes the view' (top-bar pill reads Not running) · focus in the list · 1000 wide | §15.3 Connection |
| agents3340-4-cant-reach-focus-pane.png | Can't reach Sindri with Retry now and 'Esc closes the view' (top-bar pill reads Not running) · focus in the pane | §15.3 Connection |
| agents3340-4-cant-reach-focus-pane-1000.png | Can't reach Sindri with Retry now and 'Esc closes the view' (top-bar pill reads Not running) · focus in the pane · 1000 wide | §15.3 Connection |
| agents3340-5-take-it-back-focus-list.png | Open in another Sindri window with Take it back (close 4001) · focus in the list | §15.3 Connection; 15.8 close table |
| agents3340-5-take-it-back-focus-list-1000.png | Open in another Sindri window with Take it back (close 4001) · focus in the list · 1000 wide | §15.3 Connection; 15.8 close table |
| agents3340-5-take-it-back-focus-pane.png | Open in another Sindri window with Take it back (close 4001) · focus in the pane | §15.3 Connection; 15.8 close table |
| agents3340-5-take-it-back-focus-pane-1000.png | Open in another Sindri window with Take it back (close 4001) · focus in the pane · 1000 wide | §15.3 Connection; 15.8 close table |
| agents3340-6-cant-attach-focus-list.png | Can't attach: Warp tab card, tail, Jump to it, Copy resume command · focus in the list | §15.5 |
| agents3340-6-cant-attach-focus-list-1000.png | Can't attach: Warp tab card, tail, Jump to it, Copy resume command · focus in the list · 1000 wide | §15.5 |
| agents3340-7-gone-focus-list.png | Gone: tmux session gone, Copy resume command · focus in the list | §15.3 Header (Gone); 15.8 SND-AGENT-016 |
| agents3340-7-gone-focus-list-1000.png | Gone: tmux session gone, Copy resume command · focus in the list · 1000 wide | §15.3 Header (Gone); 15.8 SND-AGENT-016 |
| agents3340-8-ended-focus-list.png | Ended: frozen screen, Copy resume command, Remove worktree, Discard session · focus in the list | §15.3 Leaving, last bullet; 15.4 worktree bullet |
| agents3340-8-ended-focus-list-1000.png | Ended: frozen screen, Copy resume command, Remove worktree, Discard session · focus in the list · 1000 wide | §15.3 Leaving, last bullet; 15.4 worktree bullet |
| agents3340-8-ended-focus-pane.png | Ended: frozen screen, Copy resume command, Remove worktree, Discard session · focus in the pane | §15.3 Leaving, last bullet; 15.4 worktree bullet |
| agents3340-8-ended-focus-pane-1000.png | Ended: frozen screen, Copy resume command, Remove worktree, Discard session · focus in the pane · 1000 wide | §15.3 Leaving, last bullet; 15.4 worktree bullet |
| agents3340-f1-form-default.png | New agent form, default, focus on Prompt | §15.4 table, Keyboard |
| agents3340-f2-form-filled.png | Form filled, New worktree on | §15.4 table |
| agents3340-f3-form-starting.png | Form starting: Starting..., fields locked, Making the worktree | §15.4 Keyboard, Ctrl+Enter bullet |
| agents3340-f4-form-error-tmux-missing.png | Error 014: tmux is not installed, install command, Retry | §15.4 (folder and tmux checks bullet) |
| agents3340-f5-form-error-014-worktree.png | Error 014: worktree failed, git's line, Retry | §15.4 Ctrl+Enter bullet; 15.8 errors |
| agents3340-f6-form-repo-not-onboarded.png | Repo not onboarded: dot, "not onboarded", link | §15.4 (not onboarded bullet) |
| agents3340-f7-form-error-012-folder-gone.png | Error 012 under Repo: folder gone | §15.4 (folder and tmux checks bullet) |
| agents3340-f8-form-error-013-not-signed-in.png | Error 013 under Agent: sign-in command, Copy command (copy TBD) | §15.4 Ctrl+Enter bullet |
| agents3340-f9-form-error-017-prompt-too-long.png | Error 017 under Prompt with the character count (copy TBD) | §15.4 Ctrl+Enter bullet |
| agents3340-f10-form-background-start.png | Ctrl+Shift+Enter foot line: Started claude in sindri. Show it | §15.4 Ctrl+Shift+Enter |

## Copy TBD

The spec has no wording for these. Each is a first draft:
1. Error 013 under Agent: "Codex is not signed in. Sign in with `codex login`" with a **Copy command** button.
2. Error 017 under Prompt: "The prompt is 8,214 characters. The limit is 8,000."
3. The git line of the failed-worktree frame (`fatal: a branch named ... already exists`) is sample text, not real git output.
4. "Onboard it", the link text under a repo that is not onboarded (the spec says "a link to onboard it").
5. Sample data only: the prompt, the worktree path, the Warp tail lines, "Ended 14:02".

## Picks

1. **Can't attach and gone are list-focus only.** The right side is a card with nothing to type into, so the "Typing goes to ..."
   footer would be false. Reconnecting is list-focus only too, because a drop moves focus to the list (§15.3 Connection).
2. **The can't-reach frame's top-bar pill reads "Not running"** with the grey dot (`.status-pill.off`; the app's word is
   `Labels.NotRunning`). The other frames read "Connected".
3. **A just-started one-off's list chip reads "Starting"**, not "Waiting for you in the pane". That chip is for 4 s later, and the 120 px
   column clips it. The header state word is also Starting. The row uses a dashed no-state glyph.
4. **Take it back has no header state word**; the spec lists none for that case. The sentence and button sit in the card only.
5. **The 360 px list's chip column is right-aligned** and sized to its content (120 px at most); titles take the rest. Rows without an
   id keep an empty id slot so titles share one column.
6. **No Cancel button** in the form: Esc closes it. The Start button reads "Start (Ctrl+Enter)", and starting reads "Starting...".
7. **Ended** sits under a "Recently ended" group; the banner is drawn over the dimmed last screen, with the worktree path, **Remove
   worktree** and **Discard session**.
