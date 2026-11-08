# Todo mockups (Ph0, rev 5)

Spec: `../../2026-11-07-sindri-todo-design.md` (§3.1 the panel, §3.2 quick-add, §3.3 keys, §3.4 states, D6, D11, D13, §4.5 the Settings card). Static mockups,
no production code. Colors, sizes and radii are the app's tokens (`src/Sindri/Aw.Sindri.Ui/Theme/Tokens.axaml`); no new tokens (the attention border is
`Color.AttentionFill`, the focus ring and the accent heading `Color.PrimaryHover`). The top bar is the app's nine tabs (no Todo tab). Sample date: Sat 2026-11-07;
data is made up.

`todo.html?s=<screen>` draws every screen. `bash shots.sh [screen...]` renders `screens/<screen>-1000.png` (1000 × 600, the window minimum) and
`screens/<screen>-1440.png` (1440 × 900) with headless Chrome (Google Fonts are fetched). The rev 1 ten-tab "Todo page" files are in `old/` (superseded).

The owner picks from five screens:

| # | Screen | `s=` | Files (add `-1000.png` or `-1440.png`) |
|---|---|---|---|
| 1 | Home, panel collapsed: five rows (Overdue first, an "agent" tag, 3 chips + "+7"), "Show all 11", From elsewhere, Needs you's first rows below | `home-collapsed` | `screens/home-collapsed-*` |
| 1 | Home, panel expanded, scrolled to the end: Show done open, a Done group and a "Removed" label with one todo | `home-expanded` | `screens/home-expanded-*` |
| 1 | Same panel, top of its scroll: Overdue / Today / Upcoming (the 1440 shot shows the rest) | `home-expanded-top` | `screens/home-expanded-top-*` |
| 2a | One conflict, the default one-line row: "Changed in Sindri and in Obsidian: reply to Sam. Enter to sort it out", four todo rows below | `attn-a` | `screens/attn-a-*` |
| 2b | Three conflicts: the oldest on one line, "Sort out 2 more: reply to Sam…, rotate…" in the next slot, three todo rows | `attn-b` | `screens/attn-b-*` |
| 2c | Three conflicts and a hold: same two lines, "Sort out 3 more" names the hold, sync line "Paused: the note changed 14 todos" | `attn-c` | `screens/attn-c-*` |
| 2c | A hold alone: "The note changed 14 todos. Enter to look" | `attn-c-hold` | `screens/attn-c-hold-*` |
| 2d | Removal, default line: "Changed in Sindri, removed in Obsidian: rotate the staging key" | `attn-d` | `screens/attn-d-*` |
| 2a, after Enter | The conflict open: edit vs edit, 278-character text clamped to 3 lines, "changed" marks, `1 Keep Sindri's` / `2 Keep Obsidian's` | `attn-open-a` | `screens/attn-open-a-*` |
| 2d, after Enter | The removal open: Obsidian's column says "Removed", its button "Keep it removed" | `attn-open-d` | `screens/attn-open-d-*` |
| 2c, after Enter | The hold open: `1 Put them back in the note` / `2 Apply in Sindri too` | `attn-open-hold` | `screens/attn-open-hold-*` |
| 2e, after a pick | Toast "Kept Obsidian's version. Undo (6 s)" counting down, sync line "Saving your pick to Obsidian in 6 s" | `attn-e` | `screens/attn-e-*` |
| 3 | Panel focused: ring around the region, heading in the accent colour, a row selected, key hints with `X` = Done | `focus-panel` | `screens/focus-panel-*` |
| 3 | Needs you focused: same treatment, key hints with `X` = Reject | `focus-needs` | `screens/focus-needs-*` |
| 4 | Quick-add over Reviews: "check flaky test fri", "Due Fri 11-13" | `quickadd` | `screens/quickadd-*` |
| 4 | Workflows: `N` starts a workflow, `Ctrl+Shift+N` opens the todo box (the dashed note is a mockup annotation, not app text) | `workflows` | `screens/workflows-*` |
| 5a | Settings → Connections, Obsidian card not set up: vault, note "Todo.md", Check note; Linear card beside it | `settings-a` | `screens/settings-a-*` |
| 5b | After Check note: "Import adds ids to 42 lines…", "3 lines can't be imported (lines 4, 17, 30)…", Import and start syncing / Cancel, and the line that the button saves and approves in one step | `settings-b` | `screens/settings-b-*` |
| 5c | Syncing: "Syncing with Notes/Todo.md · last synced 10:05", Change, Stop syncing | `settings-c` | `screens/settings-c-*` |
| 5d | One way: "Sindri can't write to Todo.md on this disk…", Import off, Start syncing (one way) | `settings-d` | `screens/settings-d-*` |

## Found while drawing

- **Opened rows push Needs you down.** After Enter the panel is 346 px (removal), 318 px (hold) or 385 px (conflict) against 286 px, so at 1000 x 600 Needs you shows one row or none; the default one-line screens keep Needs you above the fold.
- **Expanded at 1000 × 600.** The half-page cap leaves 200 px of scroll. "Show done" with its Done group (3 rows) and the Removed label and row needs 234 px, so
  34 px short: `home-expanded` is scrolled to the end and the toggle row is just above the top edge. The 1440 shot shows all of it.
