# Guesses, Notes round 1 (Part of #2981)

Fixture text (note titles, people, numbers #3104 / #3109 / #3112, run 8841, counts like 214 notes) is invented and not listed one by one.

1. Tabs on the Notes page: All, Daily, Linked to work. No issue names tabs; these map to the three kinds of note the ledger talks about.
2. A drawn page glyph for a note (blue outline for daily notes). The grammar has no note glyph; it is the only new glyph.
3. Row columns: links count, folder (mono), age, and a waits-on cell showing "agent edit" when an agent edit is unreviewed. The issue names title, first line, link count, age only; folder and the agent flag are added.
4. Peek fields: Edited, Links "4 out . 3 in", Work (states of linked PR/issue), File. Peek actions: Open, Copy path, Copy link (key y).
5. Hotkey Ctrl+Alt+N (given by lead; Projects uses Ctrl+Alt+I).
6. A leading "[ ]" draws a ticked box in the bar and a line "starts with [ ]: saved as a task".
7. Saved task format `- [ ] 14:32 text` (checkbox before the time). The issue says `- 14:32 <text>` for a plain line and does not give the task shape.
8. The saved notification has "Open note" (primary) and "Undo". The issue names no undo for capture.
9. The bar shows a preview line of the exact text that will be appended, and the "Adds to 2026-11-12 . ## Captured" chip.
10. Daily note title is the long date "Thursday 12 Nov 2026"; the file is `daily/2026-11-12.md`. The prior-art says `YYYY-MM-DD.md` in a folder the owner sets; `daily/` is invented.
11. Section names "Carried from earlier days", "Today", "Captured". "Captured" is from the issue; the other two are invented. Carried tasks show where they came from ("from 11-10").
12. Previous-day link points at the previous existing day (Foam rule), shown as "Wed 11 Nov". "next day: none yet" is drawn as plain text, not a link. In the editor, a bare letter types, so the editor footer has its own chords: Alt+Left previous day, Alt+Right next day, Ctrl+Shift+H History (all invented). The bare `g p` / `g d` of the prior-art note work only from the list (the list footer shows `g d`).
13. Popup groups in this order: Notes, Issues, Pull requests, Threads, Runs, Scopes, Files, one row each. The issue lists no "Notes" group; notes are added because `[[` is a note link first. Order of the six work groups follows the lead's brief.
14. Row ids in the popup: `note`, `#eng` for a thread, `run`, `scope`, `file`. Real ids are unknown.
15. Unlinked mention row shows the paragraph with the title in bold, and a "Link it" button with the chord Ctrl+Shift+L (a bare `l` would type in the editor).
16. Backlinks also list a PR body that links to the note ("#3104 (pull request)"); the prior-art note says object backlinks share the list. Filter chord Ctrl+Shift+F.
17. Palette: scope chip "Notes" next to the typed `note:`, a "Best match, then newest" header, and a "Create note" row with "in eng/". Ctrl+Enter creates; "Down for previous note when empty" is from the prior-art note.
18. Chip anatomy: glyph, id, short title, state word. States merged (violet glyph), CI red (red x glyph), closed (green check glyph), In Progress (half glyph). Chip text is shortened from the PR/issue title.
19. "Make it an issue [Ctrl+Shift+I]" (selection; the issue says `m`, which can't be a bare key in an editor) bar under the selected line, with a target picker "in Linear . EXP" and the note "replaces the line with its chip". Which tracker is the default is a guess.
20. History is a full view with a left version list (340 px). Each version shows who and the plus/minus line counts. Ctrl+Shift+H opens it from the editor, Esc returns. History keeps the bare-letter footer (j/k, a accept, d decline, r restore).
21. An agent edit is a proposal and is not in the file yet (meta on #2981: writes arrive as a diff the owner accepts). The version row reads "proposed edit, not in the file yet"; the band reads "Proposed by claude-code, session 7f3a ... the file stays at the 14:05 version until you accept". Accept (primary, `a`) writes it, Decline (`d`) drops it. No Undo and no owner rule text. Accepted versions are normal rows. The list shows "proposed edit" in the waits-on cell and the peek a "Proposal" field. Agent shown as "claude-code, session 7f3a".
22. Editor footer on today, link-complete, backlinks and live-chips: Esc leave editor, Ctrl+K palette, then the page's chords: Ctrl+Shift+I make it an issue (selection), Ctrl+Enter open chip, `[[` link, Ctrl+Shift+L link mention, Ctrl+Shift+F filter backlinks. All invented; Esc then moves focus to the list keys.
23. The capture bar draws an empty task box (not ticked) when the line starts with "[ ]".
