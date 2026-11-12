# Messages Release 1 frames: guesses (Part of #3188)

1. Class glyph colours: the square (class 3) is orange, the diamond (class 2) is the accent colour, the circle (class 1) is a grey ring. The spec fixes the shapes only.
2. Needs reply shows the New band as its own group above the ranked rows ("New 1": Lee's "got a sec?"), as Home does.
3. Reason chips: "DM", "mention", "review ask". The spec names "mention", "DM", "keyword", "your thread"; "review ask" is added for class 2.
4. The cursor row's buttons read "Open in Slack r", "Snooze h", "Done e" (the spec says Reply, Snooze, Done; in Release 1 Reply is Open in Slack).
5. Sender plus place only ("Sam, DM") is used for the toast, the OS notification and the screen-reader lines of rows. The open conversation's live region reads the message text (spec 6.7), so the conversation frames' screen-reader lines name the message.
6. Rail in Release 1 hides Later and Drafts (Later has no flag yet, Drafts is Release 2). The empty-state frames for them come with Release 2.
7. Snooze picker: Release 1 uses Home's picker as it is. The "Follow up if no reply" checkbox frame is drawn too because item 4 lists it. Its helper line is invented.
8. Connect card: a vertical stepper with one open step. The manifest text, the admin request text and the scope count (11) are invented placeholders. "Check again" and "Paste another token" are the fix buttons.
9. Error text for SND-MSG-029 under step 1's failure is a guess (the spec has no code for "the prefilled link did not open").
10. Header "Not connected" on the connect card replaces the Live status.
11. At 1,000 px the linked-object chips drop first; the reason chip stays because the title keeps over 120 px. At 1,000 the place column narrows to 56 px.
12. Hover and pressed: hover is surface s2/s4, pressed is one step darker with a 1 px nudge. State is never colour alone (a row also has the cursor ring).
13. Reaction glyphs are plain characters (check, heart) so the frames render without an emoji font.
