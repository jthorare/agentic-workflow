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
| agents3340-2-oneoff-focus-list.png | Starting (a just-started one-off, trust dialog) · focus in the list | §15.3 Focus, Header; 15.4 Starting |
| agents3340-2-oneoff-focus-list-1000.png | Starting (a just-started one-off, trust dialog) · focus in the list · 1000 wide | §15.3 Focus, Header; 15.4 Starting |
| agents3340-2-oneoff-focus-pane.png | Starting (a just-started one-off, trust dialog) · focus in the pane | §15.3 Focus, Header; 15.4 Starting |
| agents3340-2-oneoff-focus-pane-1000.png | Starting (a just-started one-off, trust dialog) · focus in the pane · 1000 wide | §15.3 Focus, Header; 15.4 Starting |
| agents3340-2b-oneoff-4s-focus-list.png | One-off 4 s after a worktree start: chip "Answer in the pane" · focus in the list | §15.4 Starting bullet (line 799) |
| agents3340-2b-oneoff-4s-focus-list-1000.png | One-off 4 s after a worktree start: chip "Answer in the pane" · focus in the list · 1000 wide | §15.4 Starting bullet (line 799) |
| agents3340-3a-connecting-focus-list.png | Connecting (socket opening, empty pane) · focus in the list | §15.3 Header |
| agents3340-3a-connecting-focus-list-1000.png | Connecting (socket opening, empty pane) · focus in the list · 1000 wide | §15.3 Header |
| agents3340-3a-connecting-focus-pane.png | Connecting (socket opening, empty pane) · focus in the pane | §15.3 Header |
| agents3340-3a-connecting-focus-pane-1000.png | Connecting (socket opening, empty pane) · focus in the pane · 1000 wide | §15.3 Header |
| agents3340-3b-reconnecting-focus-list.png | Reconnecting (dimmed screen, no button; footer "Reconnecting · Esc close · n new") · focus in the list | §15.3 Connection, Header |
| agents3340-3b-reconnecting-focus-list-1000.png | Reconnecting (dimmed screen, no button; footer "Reconnecting · Esc close · n new") · focus in the list · 1000 wide | §15.3 Connection, Header |
| agents3340-4-cant-reach-focus-list.png | Can't reach Sindri with Retry now and 'Esc closes the view' · focus in the list | §15.3 Connection |
| agents3340-4-cant-reach-focus-list-1000.png | Can't reach Sindri with Retry now and 'Esc closes the view' · focus in the list · 1000 wide | §15.3 Connection |
| agents3340-4-cant-reach-focus-card.png | Can't reach Sindri with Retry now and 'Esc closes the view' · focus on the card | §15.3 Connection |
| agents3340-4-cant-reach-focus-card-1000.png | Can't reach Sindri with Retry now and 'Esc closes the view' · focus on the card · 1000 wide | §15.3 Connection |
| agents3340-5-take-it-back-focus-list.png | Open in another Sindri window with Take it back (close 4001) · focus in the list | §15.3 Connection; 15.8 close table |
| agents3340-5-take-it-back-focus-list-1000.png | Open in another Sindri window with Take it back (close 4001) · focus in the list · 1000 wide | §15.3 Connection; 15.8 close table |
| agents3340-5-take-it-back-focus-card.png | Open in another Sindri window with Take it back (close 4001) · focus on the card | §15.3 Connection; 15.8 close table |
| agents3340-5-take-it-back-focus-card-1000.png | Open in another Sindri window with Take it back (close 4001) · focus on the card · 1000 wide | §15.3 Connection; 15.8 close table |
| agents3340-6-cant-attach-focus-list.png | Can't attach: Warp tab card, tail, Jump to it, Copy resume command; chip "Can't see this terminal" · focus in the list | §15.5 |
| agents3340-6-cant-attach-focus-list-1000.png | Can't attach: Warp tab card, tail, Jump to it, Copy resume command; chip "Can't see this terminal" · focus in the list · 1000 wide | §15.5 |
| agents3340-6-cant-attach-focus-card.png | Can't attach: Warp tab card, tail, Jump to it, Copy resume command; chip "Can't see this terminal" · focus on the card | §15.5 |
| agents3340-6-cant-attach-focus-card-1000.png | Can't attach: Warp tab card, tail, Jump to it, Copy resume command; chip "Can't see this terminal" · focus on the card · 1000 wide | §15.5 |
| agents3340-7-gone-focus-list.png | Gone: tmux session gone, Copy resume command, no Stop · focus in the list | §15.3 Header (Gone); 15.8 SND-AGENT-016 |
| agents3340-7-gone-focus-list-1000.png | Gone: tmux session gone, Copy resume command, no Stop · focus in the list · 1000 wide | §15.3 Header (Gone); 15.8 SND-AGENT-016 |
| agents3340-7-gone-focus-card.png | Gone: tmux session gone, Copy resume command, no Stop · focus on the card | §15.3 Header (Gone); 15.8 SND-AGENT-016 |
| agents3340-7-gone-focus-card-1000.png | Gone: tmux session gone, Copy resume command, no Stop · focus on the card · 1000 wide | §15.3 Header (Gone); 15.8 SND-AGENT-016 |
| agents3340-8-ended-focus-list.png | Ended one-off: frozen screen, no cursor, Copy resume command, Remove worktree, Discard session · focus in the list | §15.3 Leaving, last bullet; 15.4 worktree bullet |
| agents3340-8-ended-focus-list-1000.png | Ended one-off: frozen screen, no cursor, Copy resume command, Remove worktree, Discard session · focus in the list · 1000 wide | §15.3 Leaving, last bullet; 15.4 worktree bullet |
| agents3340-8-ended-focus-card.png | Ended one-off: frozen screen, no cursor, Copy resume command, Remove worktree, Discard session · focus on the card | §15.3 Leaving, last bullet; 15.4 worktree bullet |
| agents3340-8-ended-focus-card-1000.png | Ended one-off: frozen screen, no cursor, Copy resume command, Remove worktree, Discard session · focus on the card · 1000 wide | §15.3 Leaving, last bullet; 15.4 worktree bullet |
| agents3340-8b-ended-remove-refused-focus-list.png | Ended one-off, Remove worktree refused: git status lines, SND-AGENT-018 · focus in the list | §15.4 worktree bullet; 15.8 error 018 |
| agents3340-8b-ended-remove-refused-focus-list-1000.png | Ended one-off, Remove worktree refused: git status lines, SND-AGENT-018 · focus in the list · 1000 wide | §15.4 worktree bullet; 15.8 error 018 |
| agents3340-8b-ended-remove-refused-focus-card.png | Ended one-off, Remove worktree refused: git status lines, SND-AGENT-018 · focus on the card | §15.4 worktree bullet; 15.8 error 018 |
| agents3340-8b-ended-remove-refused-focus-card-1000.png | Ended one-off, Remove worktree refused: git status lines, SND-AGENT-018 · focus on the card · 1000 wide | §15.4 worktree bullet; 15.8 error 018 |
| agents3340-8c-ended-no-worktree-focus-list.png | Ended one-off with no worktree: no Remove worktree button · focus in the list | §15.3 last bullet |
| agents3340-8c-ended-no-worktree-focus-list-1000.png | Ended one-off with no worktree: no Remove worktree button · focus in the list · 1000 wide | §15.3 last bullet |
| agents3340-8c-ended-no-worktree-focus-card.png | Ended one-off with no worktree: no Remove worktree button · focus on the card | §15.3 last bullet |
| agents3340-8c-ended-no-worktree-focus-card-1000.png | Ended one-off with no worktree: no Remove worktree button · focus on the card · 1000 wide | §15.3 last bullet |
| agents3340-8d-ended-lane-focus-list.png | Ended lane: no Discard session · focus in the list | §15.3 Leaving |
| agents3340-8d-ended-lane-focus-list-1000.png | Ended lane: no Discard session · focus in the list · 1000 wide | §15.3 Leaving |
| agents3340-8d-ended-lane-focus-card.png | Ended lane: no Discard session · focus on the card | §15.3 Leaving |
| agents3340-8d-ended-lane-focus-card-1000.png | Ended lane: no Discard session · focus on the card · 1000 wide | §15.3 Leaving |
| agents3340-f1-form-default.png | New agent form, default, focus on Prompt | §15.4 table, Keyboard |
| agents3340-f2-form-filled.png | Form filled, New worktree on | §15.4 table |
| agents3340-f3-form-starting.png | Form starting: Starting..., fields locked, Making the worktree | §15.4 Ctrl+Enter bullet |
| agents3340-f4-form-error-tmux-missing.png | Error 014: tmux is not installed, install command, Retry | §15.4 folder and tmux checks |
| agents3340-f5-form-error-014-worktree.png | Error 014: "Couldn't make the worktree. git said: ...", Retry | §15.4 Ctrl+Enter bullet; 15.8 errors |
| agents3340-f6-form-repo-not-onboarded.png | Repo not onboarded: dot, "not onboarded", link under the select | §15.4 not onboarded bullet |
| agents3340-f7-form-error-012-folder-gone.png | Error 012 under Repo: folder gone | §15.4 folder and tmux checks |
| agents3340-f8-form-error-013-not-signed-in.png | Error 013 under Agent: sign-in command, Copy command (copy TBD) | §15.4 Ctrl+Enter bullet |
| agents3340-f9-form-error-017-prompt-too-long.png | Error 017 under Prompt, counter in error red (copy TBD) | §15.4 Ctrl+Enter bullet |
| agents3340-f10-form-background-start.png | Ctrl+Shift+Enter foot line "Started claude in sindri. Show it"; list shows the new Starting row | §15.4 Ctrl+Shift+Enter |
| agents3340-f1-form-default-1000.png | New agent form, default, focus on Prompt · 1000 wide, sheet over the glyph column | §15.4 table, Keyboard |
| agents3340-f2-form-filled-1000.png | Form filled, New worktree on · 1000 wide, sheet over the glyph column | §15.4 table |
| agents3340-f8-form-error-013-not-signed-in-1000.png | Error 013 under Agent: sign-in command, Copy command (copy TBD) · 1000 wide, sheet over the glyph column | §15.4 Ctrl+Enter bullet |

## Copy TBD

The spec has no wording for these. Each is a first draft:
1. Error 013 under Agent (decided in review): "Codex isn't signed in. Run `codex login` in a terminal, then Start again." with **Copy command**.
2. Error 017 under Prompt: "The prompt is 8,214 characters. The limit is 8,000." Copy TBD.
3. The 014 worktree line: "Couldn't make the worktree. git said: ..." (git's line is sample text).
4. "Onboard it", the link text under a repo that is not onboarded.
5. The refused Remove worktree text in 8b: "Couldn't remove the worktree: it has changes." plus two sample `git status` lines. Copy TBD.
6. List chip "Answer in the pane" (2b): the review round asked for it; origin/main's line 799 still reads "Waiting for you in the pane".
7. Sample data only: the prompt, the worktree path, the Warp tail lines, "Ended 14:02".

## Picks

1. **Frame set (spec PR #3397, §15.3 Card buttons):** list and pane focus for connecting, starting and attached; list and card focus for can't reach, take it back, can't attach, gone and every Ended frame; list focus only for reconnecting (no button). An Ended session never shows a dead cursor (line 779).
2. **Card keys:** F6 (or Ctrl+Shift+L, Enter on the shown row, a click) focuses the card's first button; Tab moves between buttons. List-focus footer: "F6 to Retry now · Esc close · n new" (the first button's real label per state). Card-focus footer: "Enter to Retry now · F6 back to the list", with a focus ring on the button.
3. **The top-bar pill reads "Healthy"** in every frame. At 1000 wide the search field folds to its magnifier, as the app's bar does.
4. **List chip:** a just-started one-off reads "Starting"; 4 s after a worktree start it reads "Answer in the pane"; a Warp session reads
   "Can't see this terminal".
5. **No Stop** on a Gone or Ended header (nothing to stop); Close stays. Gone shows no "1 other client".
6. **No Cancel button** in the form: Esc closes it. The Start button reads "Start (Ctrl+Enter)". At 1000 wide the 440 px sheet covers the
   pane view (f1, f2, f8).
7. **The 360 px list's chip column** is right-aligned and sized to its content; rows without an id keep an empty id slot.
