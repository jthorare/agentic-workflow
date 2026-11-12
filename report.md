# Notes round 2 frames (Part of #2981)

51 1440x900 PNGs (`notes-<name>.png`), same CSS tokens as round 1. Regenerate with `python3 gen.py && ./shoot.sh`.
Settled calls applied: one hotkey Ctrl+Alt+I with an Issue / Note switch (Ctrl+Tab), a capture never loses its line, today's page opens with a
"Needs you / Moved since yesterday" fold, the daily file has no heading (the view draws the long date), backlinks in the right pane, every issue / PR / run
reference is a live chip with a "not live" state, no PR-body backlink row, make-issue is Ctrl+Shift+I, one Notes Settings frame, palette ranks title matches first.
Agent edits stay proposals. Editor keys stay chords.

| Issue | Frames |
|---|---|
| #3034 | capture-bar, capture-bar-plain, capture-bar-issue, capture-empty, capture-error-down, capture-error-folder, capture-error-write, capture-no-folder-no-switch, capture-saved, settings, settings-hotkey-refused |
| #3035 | today, today-folded, today-new, today-down, today-conflict, today-savefail, list, list-empty, list-loading |
| #3036 | link-complete, link-complete-empty, link-complete-reading, link-written, backlinks, backlinks-empty, backlinks-linked |
| #3037 | palette, palette-empty, palette-none, palette-reading, palette-nofolder |
| #3038 | live-chips, live-chips-legend, live-chips-notlive, make-issue-picker, make-issue-noconn, make-issue-fail, make-issue-done |
| #3039 | history, history-restore, history-stale, history-empty, history-error, history-loading, settings |

Round 3: capture window titled by kind and the Issue kind drawn as the Projects window; checkbox border #70757c; not-live chips dim only the glyph and edge, with an "as of 14:02" tooltip; peek chip; legend has 11 states; day chords are Alt+PageUp / Alt+PageDown. The one-line preview in each list row stays plain text.

Round 4: capture window opens on Note, Issue kind creates on Ctrl+Enter, no Shift+Enter; conflict frame has no default button; links carry the repo and only Note / owner/repo#N / URL links remain (link-rename frame removed, run chips and legend rows dropped).

Round 5: reshoot per spec #3205 round 2 (section 5): see guesses 26 to 34. New frames: capture-saved-without-sindri, settings-no-folder, list-folder-gone. Removed: capture-error-down.

Round 6: ideas round 5 MUSTs (Notes 11, Projects 2), the round 3 ask and spec c7b11da917 calls; see guesses 35 to 42. New: today-ghost, today-ghost-down, settings-folder-error, settings-hotkey.
