# Messages frames: guesses (Part of #3188)

1. Class glyph colours: the square (class 3) is orange, the diamond (class 2) is the accent colour, the circle (class 1) is a grey ring. The spec fixes the shapes only.
2. Needs reply shows the New band as its own group above the ranked rows ("New 1": Lee's "got a sec?"), as Home does.
3. Reason chips: "DM", "mention", "review ask". The spec names "mention", "DM", "keyword", "your thread"; "review ask" is added for class 2.
4. The cursor row's buttons read "Open in Slack r", "Snooze h", "Done e" (the spec says Reply, Snooze, Done; in Release 1 Reply is Open in Slack).
5. Sender plus place only ("Sam, DM") is for the toast and the OS notification. A row's screen-reader name keeps its message text (spec 6.7): "Direct question from Sam in a direct message, 8 minutes ago: can you ship the fare table today". The conversation frames' lines name the message too.
6. Rail in Release 1 hides Later and Drafts (Later has no flag yet, Drafts is Release 2). The empty-state frames for them come with Release 2.
7. Snooze picker: Release 1 uses Home's picker as it is (frame `snooze-picker`). The "Follow up if no reply" checkbox frame (`snooze-picker-followup`) is Release 2 and belongs to that push (spec 9.4 last line, 6.5 `h` row). It still shows the old fixture; it is redrawn when R2 is regenerated.
8. Connect card: a vertical stepper with one open step. The manifest text, the admin request text and the scope count (11) are invented placeholders. "Check again" and "Paste another token" are the fix buttons.
10. Header "Not connected" on the connect card replaces the Live status.
11. At 1,000 px the linked-object chips drop first; the reason chip stays because the title keeps over 120 px. At 1,000 the place column narrows to 56 px.
12. Hover and pressed: hover is surface s2/s4, pressed is one step darker with a 1 px nudge. State is never colour alone (a row also has the cursor ring).
13. Reaction glyphs are plain characters (check, heart) so the frames render without an emoji font.

## Release 2 push
0. `messages-snooze-picker-followup` moved here from Release 1 (ideas call a).
14. The composer sits at the bottom of the peek, under the thread. Its toolbar (bold, italic, strike, code, quote, list) and the "Ask an agent to draft" button are drawn small; the spec lists the formats, not the toolbar.
15. The agent draft label reads "Agent draft by claude-code · run 8860 · not sent". Risk chips: "broadcast: @channel", "link", "no attachment".
16. The submit menu is a Home-style menu anchored above the Send button; its helper text is invented.
17. Make-issue sheet fields follow 9.5: title, team, project, assignee, label, body. Team "Platform (PLT)" and the body text are placeholders. "Reply in the thread with the issue link" is a checkbox, off.
18. The brief shows channel names, facts lines and written lines with message-time source chips. "12 messages missing" is the honesty line for a channel with a gap.
19. Search: "Search Slack too" results sit under the store results with a "from Slack" tag. The Everything scope has four groups (Messages, PRs and issues, Notes, Code), counts in each header.
20. User peek fields (title, local time, status, in common) and the app message blocks (section, context, divider, actions) are placeholders. The unknown block text is a guess.
21. The huddle row says the user token cannot join; that sentence is invented.
22. Later and Drafts empty frames show the full rail (Later and Drafts present), which Release 1 hides.

## UX-pass round (R1 fixes against spec 28d94d4dbb)
23. Fixture clock is about 09:10. Needs reply holds 22 ranked rows plus the New band row (Lee), so the header, the rail, the tab and Home's tab all read 23. Pat and Eli left Needs reply (their newest message is class 1, spec call 21). The 14 Unread rows are all unread; the circle is only on Pat and Kim's thread (a thread of mine); the others are not mine and have no class glyph.
24. Rank order (spec 7.2): all class 3 squares first, oldest first, then diamonds by `blocks` (Ada's #3120 thread 2, Ada's stacked #3125 and Tomas 1, the rest 0), oldest first. The blocks numbers are made up.
25. One #harbor thread: root Ada 07:30 "@joi review #3120 when you can", 3 replies (Kim 08:20, Ada 08:41, Kim 08:55). Collapsed line and peek both say 3 replies; the DM peek shows Sam's DM.
26. Tab counts: the Messages tab equals the header's need-reply count in every frame (23; 22 in the snoozed frame; 0 on first sync, listening and empty Needs reply; 3 on empty Unread). A frame that is not connected, and the loading frame, show no count. "Messages 0" is drawn literally.
27. Snoozed frame: Mira is snoozed and leaves Needs reply (shown only under Snoozed, 1). Tomas came back early on a new reply and is in Needs reply with "back early: new reply". 23 - 1 = 22.
28. At 1,200 and 1,000 px the cursor row drops its chips and reason and its three buttons shrink to key chips (r h e) with titles Open in Slack, Snooze, Done. At 1,440 the buttons keep their words. The age is never covered at any width. Chip size and positions are mine.
29. The hover time sits in its own 34 px gutter left of the text, so it never touches the message text.
30. Banner buttons and the toast button carry no Enter hint, because Enter opens the cursor row in list mode. The reconnect banner holds the one primary; the header status line has no button.
31. First-run step 1 shows the manifest text beside the link button; its failure frame has no code (no code covers a link that does not open).
32. The token field line before Connect says "User token (xoxp-). Sindri checks the 11 read scopes when you press Connect"; the scope check needs a call, so it comes after.
33. Not redrawn this round (nits): the orange glyph colour, the "3 new" chip with one line, the follow-up box on Mira (R2 frame), the Home 9 tab rule. The R2 frames were left alone and still show the older fixture until they are regenerated.

## Round 3 (R2 check at f78255aed2, spec 742763f484)
34. Release labels: composer-*, composer-hand-off, snooze-picker-followup, peek-user, peek-huddle, empty-later, empty-drafts and emoji-no-match are Release 2. make-issue-sheet, attach-picker, brief, brief-model-off, search-messages, search-everything, peek-file and peek-app-message carry a "Release 3" tag in the header.
35. In Release 2 the cursor-row buttons are Reply, Snooze, Done and `r` reads "reply". The R2 rail shows Drafts with one count (0 before a draft exists, 1 after the agent draft, the failed send and the offline draft). Later shows only once a flag exists, and in the Later-empty frame (open after the last flag is gone).
36. One #harbor thread: root Ada 07:30 and three replies (Kim 08:20, Ada 08:41, Kim 08:55); You 09:14 where a reply was sent. Needs reply uses the same sorted fixture; the list scrolls so the cursor row (Ada) is visible.
37. composer-sent: Ada's row stays in place, dimmed, "replied 09:14"; counts 22 / 21. The confirmation line is "Sent." and a "Follow up if no reply" button; Esc leaves the composer, then `h`.
38. Footer mode chips are List keys, Message keys and Composer only. Pickers, menus, sheets, the brief and search show their keys with no chip.
39. Search: the scope chip is a button ("Messages", "Everything") with the palette entry messages.search-scope. Tab does nothing to it. Group counts equal their rows.
40. File too large (SND-MSG-010) has no Retry, only "Open in Slack". The app message's Retry is off with its reason, and Open run is a link button.
41. composer-hand-off is a new frame (spec 9.7 item 8): provider, what goes along (4 messages, 1 linked row, about 1k tokens), a note field, the "normal session that can run shell and gh" line, and a primary "Send thread to claude-code". Counts of tokens are placeholders.
42. List cursor is a mark, not a ring, while the composer has focus (one focus ring per frame).

## R1 recheck at 09f8e96689 (spec 84d61ef298)
43. Ada's #3120 thread is the 07:30 root alone (no replies), so its row reads Ada, the root text, 2h. Kim's 08:20 "ok, 90 s holds then" is a reply in a thread I started (08:05, "what hold time do we want in staging?"): its own class 1 row in Unread, and a collapsed "Kim, 1 reply" line in the channel. In the R2 frames the first reply to Ada's thread is my 09:14 line.
44. One #harbor unread number: 3 messages (09:08, 09:09, 09:10 under the New divider, chip "3 new", rail 3). Unread shows one #harbor channel row plus Kim's thread row. empty-unread shows no #harbor count.
45. snoozed-rows draws the band as 21 rows: the list is scrolled to the end, with a "6 more above" cue and 15 rows, then Snoozed.
46. The cursor row's title ends with an ellipsis before the buttons at 1440, 1200 and 1000. At 1200 and 1000 the linked-object chips are dropped on every row, not only the cursor row.
47. The snooze picker has no mode chip. The loading frame shows 23 in the header, the tab and the rail.
