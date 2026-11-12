# Notes round 1 frames (Part of #2981)

Nine 1440x900 PNGs (`notes-<name>.png`) on `../grammar-2978/grammar.css`, layout in `notes.css`. Regenerate with `python3 gen.py && ./shoot.sh`. Top bar has Notes after Projects. Editor frames (today, link-complete, backlinks, live-chips) have an editor footer with chords, no bare letters (guess 22). Every frame has a "Screen reader" line in its footer saying what is read for the row or control. Text is ink-subtle (never ink-3), wires are #686870, one primary button per frame. No spec exists yet. Each PNG was looked at after the last shoot.

| Frame | Issue | What it shows | Guesses |
|---|---|---|---|
| notes-list | #2981 | Notes page: rows with title, first line, link count, folder, age; groups Today / Yesterday / Earlier this week / Older. Peek open on "Retry policy for exports" (a claude-code proposal waits, not in the file yet): fields, rendered body, Open (primary), Copy path, Copy link | 1, 2, 3, 4, 21 |
| notes-capture-bar | #3034 | Ctrl+Alt+N over a browser, no Sindri window. One-line bar, leading "[ ]" shows a empty task box, preview of the line that will be appended (`- [ ] 14:32 ...`), keys Enter / Shift+Enter / Esc | 5, 6, 7, 23 |
| notes-capture-saved | #3034 | After Enter: bar gone, notification "Saved to 2026-11-12 under ## Captured", the saved line, Open note (primary), Undo | 8, 9 |
| notes-today | #3035 | Daily note in the editor (live preview, current line shows raw Markdown). Link to previous day, "next day: none yet", carried open tasks at the top. Right pane: Linked objects with live state, Backlinks | 10, 11, 12 |
| notes-link-complete | #3036 | `[[ret` typed; popup grouped Notes, Issues, Pull requests, Threads, Runs, Scopes, Files; match letters bold; keys Enter insert, `/` headings | 13, 14 |
| notes-backlinks | #3036 | Right pane with Linked mentions (3, with the paragraph) and Unlinked mentions (2, title found, "Link it" button), filter box | 15, 16 |
| notes-palette | #3037 | Ctrl+K with `note:retry`: Notes scope chip, 4 notes (best match then newest), "Create note" row for Ctrl+Enter | 17 |
| notes-live-chips | #3038 | Chips for #3104 merged, #3109 CI red, #3034 closed, #3112 In Progress; state is a word plus a glyph. A selected line with "Make it an issue [m]" (primary), target Linear EXP | 18, 19 |
| notes-history | #3039 | Versions list (me, capture bar, claude-code sessions). The selected row is a proposed edit, not in the file yet, shown as a diff with its author; Accept (primary) writes it, Decline drops it; `r` restores older versions | 20, 21 |
