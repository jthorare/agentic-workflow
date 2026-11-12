# Brief: `.` actions menu + snooze picker + undo toast + Done list (#3030, #3031)

You draw static HTML frames, 1440x900, dark, in the #2978 grammar. No app code. No git. No dotnet.

Read first (all in ~/.agentic-workflow/joi-fairshare-agentic-workflow/design/grammar-2978/):
- grammar.md (whole file; Keys rules: a bare letter NEVER commits a write; writes go through `.` or Ctrl+Enter)
- grammar.css (link it, `<link rel=stylesheet href="../grammar-2978/grammar.css">`; add new classes to a local menu.css, never edit grammar.css)
- home.html (the Home page you start every frame from: copy it, keep its rows, footer and top bar)

What the issues decided (api comments on #3030 and #3031):
- Snooze: presets later today, tomorrow 9:00, Monday, next week, or a typed date ("fri 14:00", the server parses it). The row leaves Home,
  shows `back Mon 9:00` while away, returns early on new activity addressed to me. Undo 5 s.
- Done: any row kind. Leaves Home; returns only on activity addressed to me (mention, review request, reply in my thread). Done list from
  the footer, last 7 days. Undo 5 s. Never written back to the source (a Linear issue stays open).
- Keys: `.` opens the actions menu on the cursor row. Inside the menu a letter picks an entry (h Snooze..., e Done) - that is fine because
  the menu is cancellable. `h` and `e` on the bare row do nothing.

Frames to write in this folder (one .html each):
1. `menu.html` - Home, cursor on the 3rd Needs-you row, `.` menu open anchored under the row's right end: 240 px, s3 fill, r3, hair-strong
   border, 32 px entries: icon 16 · label · key kbd right-aligned. Entries: Open (Enter), Peek (Space), divider, Snooze... (h), Done (e),
   divider, Copy link (y). Typed filter field at the top ("Type an action").
2. `snooze.html` - the menu after `h`: same box becomes the picker: 5 rows (Later today 18:00, Tomorrow 9:00, Monday 9:00, Next week Mon 9:00,
   Pick a date...) each with its resolved time right-aligned in ink-3; text field on top showing `fri 14:00` typed and its parse below
   (`Fri 14 Nov, 14:00`). Enter commits.
3. `toast.html` - after commit: the row is gone, the rows below moved up, toast bottom-left above the footer: `Snoozed until Mon 9:00 · Undo [Ctrl+Z]` (Undo is a write, so never a bare letter)
   and a 5 s progress hairline. One toast at a time; a second write replaces it.
4. `snoozed-row.html` - the Moving / Snoozed view showing a snoozed row with `back Mon 9:00` in the age column (wide 136) and the waits-on blank.
5. `done-list.html` - the Done list (reached from footer key `g d` after the keys divider): rows grouped Today / Yesterday / Earlier this week,
   each with `done 14:02` and a `Restore` row action on hover.

Rules: sentence case, no all-caps, ids by kind (#4182 PR, BIL-301 issue), progress <= 5 chars, footer prints the grammar keys exactly then
`.keys-div` then page keys. Every frame has exactly one primary button at most.

When done: screenshot each with
  google-chrome --headless=new --virtual-time-budget=3000 --window-size=1440,900 --screenshot=<name>.png file://<abs path>
(check Inter loaded: text not in a serif/system fallback) and report the file list + anything you guessed. Do not post anything.
