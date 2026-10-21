# needs-you: open decisions (recommended answers)

1. Question: should the inbox use one list grouped by kind, or tabs per kind?
   Options: grouped list; tabs.
   Recommended: grouped list. Linear groups issues under sticky headers and keeps one scroll; the four kinds stay distinct through glyph, header word and kind word. Follows Linear.

2. Question: where do Approve and Send back live, in the card or in a fixed bar?
   Options: inside the card; fixed bar under the request.
   Recommended: fixed bar, so the keys A and D always have a visible target. Follows HumanLayer (actions under the request, kbd chips).

3. Question: what does A do when the request was not read to the end?
   Options: approve at once; show "Approve?" and need a second A.
   Recommended: show "Approve?". Follows HumanLayer's confirming state.

4. Question: is the reason required when sending back?
   Options: required; optional.
   Recommended: required, with the send button disabled until text exists. Follows HumanLayer (deny needs a reason, Ctrl+Enter sends, Esc cancels).

5. Question: is "Edit before approve" built?
   Options: drawn only; wired; left out.
   Recommended: drawn and marked "our addition" until the API can approve with changed output. HumanLayer has no such action.

6. Question: should stop 1 show answered questions, since none are open?
   Options: show them with the chosen option marked; hide the section.
   Recommended: show them, so the owner sees what they decided. No reference app has this; closest is SuperPlane's run inspector showing inputs next to outputs.

7. Question: how long does the 2-hour hand-back nudge stay?
   Options: until dismissed; 5 seconds.
   Recommended: until dismissed, because it asks for an action. HumanLayer's toasts expire after 5 s but only inform.

8. Question: takeover clock figure, 1d 4h (brief) or 2h 50m (sample data)?
   Options: either.
   Recommended: 2h 50m, from `paused.timeoutLeftSeconds` and consistent with that step's 3 h limit.

9. Question: show ids such as AC-1 to AC-3 next to the done-criteria?
   Options: show them as small grey references; hide them.
   Recommended: show them, small and grey, because people say "AC-2" when they send a scope back. Follows Linear (identifier in mono tertiary at the start of a row).

10. Question: what do people type to confirm a settings or workflow approval?
    Options: the full action text (`approve profile 3f0c9a`); a short phrase (`approve 3f0c9a`).
    Recommended: the short phrase, bound to the change's fingerprint. It keeps internal words off the screen and still stops a slip. Follows the setup-once spec section 4 (typed confirmation).

11. Question: does "Approve all new workflows" also open a typed field?
    Options: hold button; typed field.
    Recommended: hold button, 1.5 s, as the setup-once spec says for approve-all. The list above the button is the preview.

12. Question: should the "Handed back" toast stay?
    Options: stays; fades after 5 s.
    Recommended: fades after 5 s, because it only informs. Mockup keeps it visible for review. Follows HumanLayer toasts.
