# Workflows: open decisions

1. What does a single click on a card or row do?
   - Options: (a) open the run inspector; (b) go straight to the run canvas.
   - Recommended: (a). A click opens the 480 px inspector, a double-click or Enter on an open run goes to the canvas, and "Open run" is always there. Why: SuperPlane's board opens the task drawer on click and the canvas only from the automation link. Follows SuperPlane.
2. Does the Needs you chip approve here?
   - Options: (a) link to Needs you; (b) approve inline.
   - Recommended: (a). The chip links to `needs-you.html`, where the full request is shown. Why: HumanLayer shows the whole request before any approve. Follows HumanLayer.
3. Which address does a list row open for a child run (a scoping or design run)?
   - Options: (a) the child's own address; (b) the item's whole-run address.
   - Recommended: (a) for the canvas link of a list row, (b) for the inspector, which always shows the item's five stages. Why: the list shows each workflow run, the board shows one run per item. Follows Linear (a row is one issue).
4. What does "Mine" mean without an owner field?
   - Options: runs that need you or that you took over; or wait for an owner field.
   - Recommended: needs you plus you took over, marked as a data gap. Why: SuperPlane's "My" filter has the same meaning of "assigned to me". Follows SuperPlane.
5. Is there a Done lane card when nothing has finished?
   - Options: show an empty lane; hide the lane.
   - Recommended: show it empty. Why: SuperPlane keeps empty lanes with "Nothing here." Follows SuperPlane.
6. One status word per card, or a status word plus a "Needs you" chip? (The brief says both.)
   - Options: (a) status word only, and the word is the link; (b) word plus chip.
   - Recommended: (a). "Waiting for you" and "Paused, needs you" link to `needs-you.html#stop-<item>`; "Needs you" stays only as the filter. Why: rule 3 asks for one status word per thing. Follows SuperPlane's board card (one status icon, one chip).
7. How many runs does the board show, and what is invented?
   - Recommended: 12 runs. Eight are invented to show every status and every lane (see notes); the four real ones come from the sample.
