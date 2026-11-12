# Messages frames, Releases 1 and 2 (Part of #3188)

54 PNGs (31 for Release 1, 23 for Release 2), `messages-<name>.png`, 1440x900 unless the name ends in -1200 or -1000 (that wide x 900). Spec: `docs/superpowers/specs/2026-11-12-sindri-messages-page-design.md` at f8ee690dc0. Frame list is section 15, state rows are section 6.6. Built on `../grammar-2978/grammar.css` plus `messages.css`. Regenerate with `python3 gen.py && ./shoot.sh`. Every frame has a "Screen reader" line in the footer. Outlines #70757c, codes under Details, footers list only working keys. The app is dark only, so `messages-needs` and `messages-conversation` are the dark frames of 1 and 2. Release 1: the peek shows "Open in Slack" and has no composer. Toasts and notifications show sender and place only ("Sam, DM"). Each PNG was looked at after the last shoot.

| Frame | Spec item | What it shows |
|---|---|---|
| needs | 1 | Needs reply with the peek open: New band, 24 ranked rows (first 22 visible), class glyphs (square, diamond), reason chip, linked-object chips, three buttons on the cursor row, thread peek with a live link row and "Open in Slack" |
| unread | 1 | Unread list: class 1 circle glyph (Release 1 shows class 1 here), bold name plus dot, mention bar and "you" |
| conversation | 2 | #harbor: gap marker, day divider, grouped runs, hover time, reactions, code block, diff block, inline link row, collapsed thread line, New divider, "3 new" chip; thread in the peek |
| conversation-dm | 2, 13 | DM with three live link rows, "+3 links", and a Linear link that stays plain text (`none`) |
| conversation-focus | 11 | Focus ring on a message line, footer chip "Message keys" |
| snooze-picker | 4 | Home's snooze picker as it is in Release 1 (no checkbox) |
| snooze-picker-followup | 4 | The same with "Follow up if no reply" checked (Release 2 content, drawn because item 4 asks for it) |
| snoozed-rows | 4 | Snoozed group: "back Mon 9:00", "back early: new reply" |
| firstrun-1 / -1-fail | 8 | Step 1 and its failure line (link did not open: manifest and steps by hand) |
| firstrun-2 / -2-blocked | 8 | Step 2, and the admin-blocked state with copyable request text (SND-MSG-019) |
| firstrun-3 / -3-wrong-token / -3-scope / -3-offline | 8 | Step 3 with the token kind and scopes read, the wrong-kind line (SND-MSG-030), the missing scope (SND-MSG-003), offline (SND-MSG-028) |
| firstrun-sync / firstrun-listening | 8 | "40 conversations, 0 messages so far" and "Listening" |
| needs (header live) , header-behind / -offline / -expired / -scope | 9 | Live (polling every 30 s), Behind with the rate-limit words, Offline: last read 09:41, Token expired (SND-MSG-002), Scope missing (SND-MSG-003) |
| needs-1200 / needs-1000 | 12 | 1,200 (rail 220) and 1,000 (rail 48; chips and reason chip drop) |
| needs-rail-collapsed | 12 | Rail collapsed at 1,440 |
| states-hover-pressed | 12 | Row, chip and button: rest, hover, pressed (and the row cursor) |
| empty-needs / empty-unread / loading | 13 | "You're caught up." with the next snooze time, "Nothing unread.", 6 skeleton rows; the rail with no starred channels |
| toast | 13 | Home's toast and OS notification: "Sam, DM", no text |
| footer-modes | 13 | The "List keys" and "Message keys" chip |


## Release 2 push

| Frame | Spec item | What it shows |
|---|---|---|
| composer-empty, composer-typing-picker, composer-agent-draft, composer-submit-menu, composer-sent | 3 | Composer in the peek: empty with the target named; double-bracket picker (PR and issue insert the URL, note and scope say "local only: not a link"); agent draft with label, run link and risk chips; the "Notify 412 people" menu; the 5 s "Follow up if no reply?" line |
| composer-failed, composer-send-unknown, composer-offline, composer-429 | 3 | Failed send (Retry, Discard, SND-MSG-004), "Slack may have delivered this" (SND-MSG-032), offline with the draft kept (SND-MSG-028), 429 wait with the 12 s countdown (SND-MSG-005) |
| composer-focus-ring | 11 | Focus ring on the composer |
| make-issue-sheet, attach-picker | 5 | Make-issue sheet with the full body shown before Create; attach picker |
| brief, brief-model-off | 6 | Catch-up brief with source chips; model-off facts-only state |
| search-messages, search-everything | 7 | Messages scope with "from Slack" tags; Everything in groups |
| peek-user, peek-file, peek-app-message, peek-huddle | 10 | User peek; file thumbnail, chip and failed file; app message with blocks and an unknown block; huddle row with "Open in Slack" |
| empty-later, empty-drafts, emoji-no-match | 13 | Later and Drafts empty, emoji picker with no match |

## Release 1 fixes (ideas FIX, 16 MUSTs, spec 28d94d4dbb)
Frames redrawn: needs, needs-1200, needs-1000, needs-rail-collapsed, header-behind, header-offline, header-expired, header-scope, unread, conversation, conversation-focus, conversation-dm, snooze-picker, snoozed-rows, firstrun-1, firstrun-1-fail, firstrun-2, firstrun-2-blocked, firstrun-3, firstrun-3-wrong-token, firstrun-3-scope, firstrun-3-offline, firstrun-sync, firstrun-listening, empty-needs, empty-unread, loading, toast, footer-modes, states-hover-pressed. `snooze-picker-followup` moves to the Release 2 set and is not redrawn. Release 2 frames are untouched.
