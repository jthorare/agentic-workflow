# Notes round 2: guesses (Part of #2981)

Round 1 guess numbers no longer apply. Items below are invented in these frames and need a real answer in the spec or the build.

## Placeholders
1. Links written into the file (round 4): `[[Note]]`, `[[owner/repo#N]]` (always with the repo) and plain URLs. Thread, run, scope and code links are v1.1 and no longer drawn.
2. Error codes are now the spec's (PR #3205): SND-NOTE-003 (capture write), SND-NOTE-005 (today save), SND-NOTE-012 (history read), SND-NOTE-015 (make an issue failed).

## Behaviour
3. Capture: the Issue / Note switch shows only when a notes folder is set (lead call). The "No notes folder yet" error frame is reachable only if the folder was removed while the window was open.
4. Capture: Enter on an empty line saves nothing, the window stays open and says "Nothing to save." Undo on the saved toast lasts 8 s and is off once the note changed.
5. Today's page: the fold shows up to 5 rows per group, stays closed once you scroll past it, opens again the next day. Needs you rows: CI red PR, review asked, a decision.
6. Open tasks from earlier days are drawn from the other days' files. A tick edits the original line. The block is not in today's file.
8. Chips: a chip is raw text only on the cursor's line. "Not live" dims the chip and shows "as of 14:02". An unknown chip (repo not in the profile) says "unknown" and offers "Add the repo in Settings".
9. Chip glyphs: closed-not-merged PR has its own slashed circle (new glyph). Issue "closed" keeps the green check.
10. Make it an issue: default target is GitHub in the repo of the note's only linked PR, else the profile tracker (Linear EXP). Undo lasts 8 s (Ctrl+Z).
11. Palette: "Create note" path is the folder of the note you are in, else the notes folder root. The row is hidden when a note with exactly that title exists.
12. Link popup with no match: Enter creates the note and inserts the link.
13. Settings: agent write rule is a per-folder choice, two values drawn ("Agents propose, you accept", "Agents can't edit"). Folder rows listed are eng/, daily/, people/, design/ and "Other folders".
14. History: restoring a version writes it as a new version and compares against the file now. The current version has no Restore button. Stale proposal: Accept off, Decline primary.
15. Hotkey refused: the Settings row says "The OS refused this hotkey" and offers "Pick another key" (same row as #3170).

## Keys (all invented)
16. Alt+PageUp / Alt+PageDown previous / next day (round 3: Alt+Left and Alt+Right jump a word on macOS and Ctrl+Alt+Arrow switches workspace on GNOME; Alt+PageUp / PageDown clash with neither), Ctrl+Shift+H history, Ctrl+Shift+I make an issue, Ctrl+Shift+L link a mention, Ctrl+Shift+F filter backlinks, Ctrl+Enter open chip or create a note from the palette, Ctrl+Z undo a made issue, Ctrl+Tab Issue / Note.

## Fixture
17. One clock (14:32, Thu 12 Nov 2026) and one daily file in every frame. Counts: list 214 = 3 + 3 + 3 + 205. The history list says "13 + 1 proposed".

## Round 3
18. The capture window's title names the kind ("New issue" / "New note"). The Issue kind is the Projects window: Project, Assignee, "in Linear · EXP", footer Enter / Esc / Tab / Ctrl+Tab. Its errors reuse the make-issue sentences (Linear isn't connected, couldn't create the issue).
19. Checkbox border #70757c everywhere (3.4:1 on #222326).
20. Not live: glyph and chip edge dimmed (dashed edge), text at full ink, tooltip "as of 14:02". Right-pane rows dim their glyphs.
21. The list peek shows #3112 as a chip. The one-line preview in each list row stays plain text.
22. Chip legend has 11 states, including Todo and run failed / passed.

## Round 4
23. The capture window always opens on Note. The Issue kind creates on Ctrl+Enter; bare Enter never makes an issue. The capture window is one line, so no Shift+Enter.
24. Conflict: a clean buffer reloads silently (no frame); a dirty buffer merges on its own; only a line changed on both sides shows two versions, with no default button.
25. Deferred to v1.1 and left as drawn: make-all, Note about this, image paste, agent proposals, day chords, Moved since yesterday.

## Round 5 (spec #3205 round 2, reshoot list of section 5)
26. Conflict buttons are "Keep my line" and "Keep the file's line", both secondary, no default; each differing line carries a word ("you edited it", "ticked in the file"); focus is on the first differing line (ring drawn).
27. Make issue is v1.1 (phase P6b). The v1 frames (live-chips and the others) show no Make issue bar, button or key. The make-issue-* frames stay as v1.1 references: picker with a title field, target rows and a primary "Create issue in Linear"; done shows the Linear URL in the file and the chip "EXP-203"; noconn footer "Enter connect Linear".
28. Sindri down: the editor is read-only text. Banner reads the SND-NOTE-001 sentence ("Sindri isn't running. Chips and the brief can't update.") with the code under Details; the path line says "Read-only".
29. "Saved without Sindri" toast replaces capture-error-down. Its buttons (Open note, Start Sindri) and sub-line are guesses.
30. v1 frames leave out the "Moved since yesterday" group, the day chords and the list's `g d`. The list drops the "proposed edit" cell and the Proposal peek row. History rows name what made each version; the current row reads "Current"; the restore frame has no agent row and reads "13 versions".
31. Settings: "Start Sindri at login" row (Off, one button), "Agents can read this folder" switch rows (eng/ on as fixture, others off), the write-rule dropdown marked v1.1, and a no-folder frame with "Choose notes folder" (adds the vault and sets it in one step).
32. Links in raw text carry the repo: `[[acme/api#3109]]`. The `[[` popup shows `repo#id` in the id column.
33. Not live: hollow glyph and dashed edge, both #70757c (3:1 or more), text at full ink.
34. The capture window's primary button "Create issue in Linear" sits in the second row, after the "in Linear · EXP" chip. The no-folder frame's caption is a reviewer caption, drawn italic under the window.
