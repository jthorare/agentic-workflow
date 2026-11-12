# Home inbox frames: guesses (spec #3091, head 1e3015fdcb)

Places where the spec leaves something undefined, or conflicts with the grammar, and what the frames pick.

1. **Id vs object column (§7.1 vs grammar "Row").** Spec puts the mono ref (`PR #3120`) in the object fact column. Grammar says ids sit left of the title, never in a fact column. Frames follow the spec: PR rows have no `.id` before the title.
2. **"you" in waits-on (§7.1 vs grammar "A column with one value in a whole group says nothing").** Grammar wants the ask verb in that column. Frames draw the spec's bold "you" on every Needs you row.
3. **Header "blocks N" (§4.1, §7.1).** Spec defines N per row only. Frames use distinct dependents across the group: 7 on All, 4 on Reviews.
4. **Brief default shape (§6.1, D3 in §1).** Cap is 3 lines. Frames draw the summary line plus 2 fact lines, then "b 3 more lines". Header count 6 covers the summary and all facts.
5. **Todo line (§12).** A 28 px non-group line, empty-circle glyph, "Todo, 4 open", "Enter opens the panel" at the right, no chevron (Left/Right do nothing, §7.4).
6. **Snooze keys vs the field (§5.3 vs grammar "Keys inside a menu").** Digits 1 to 5 clash with typing "14:00". Frames put 1 to 4 on the presets and 5 on "Pick a date or time"; assumed: digits work while the field is empty, 5 focuses the field.
7. **Hand-to-agent picker chips (§5.1).** It has a field, so no letter or digit chips; Enter picks. qa is `.dis` ("asking you") because it waits on the owner.
8. **No key strip inside menus (lead's rule).** No `.mfoot`. In picker frames the page footer swaps to "up/down move, Enter pick, Esc cancel"; the spec doesn't say the footer does this.
9. **Footer at 1000 px wide (§7.1).** Grammar keys plus Home keys don't fit in 952 px. At 1000 the page keys shrink to `h snooze, t hand, e done, ? all keys` and "Done list" is dropped. At 1440 it is `h, t, e, R`, then "Done list" at the right.
10. **Done list entry point (§5.4, §7.4).** Spec says "reached from the footer", no key. Frames draw a plain "Done list" text at the footer's right, no kbd. `G D` is retired (§7.4).
11. **Split contents (§7.1).** The Reviews split shows only its Needs you rows. No Moving, brief or Todo line: the spec doesn't say whether they follow the split.
12. **Glyphs for lane rows (§7.1, §15).** Spec says lane and merge-queue glyphs are drawn in D1. Frames use the needs-you diamond for lane-recycle rows and the PR-open ring for ready-to-merge rows; no new glyph invented.
13. **Health chip (§7.1).** "Health ok" sits in the page header; the shell's "Connected" pill stays in the top bar, so there are two status marks. Decision for the reviewer: drop the top-bar pill?
14. **Stale and error lines (§7.3, FailedReadLine).** One 32 px `.state-line`: glyph, one sentence, outlined primary, optional `Details ▸`. Error = error tint, attention = attention tint. Rows from the stale source get a muted title and age. The header chip changes to "GitHub stale" / "GitHub not read". "Collector error row" is read as the §7.3 connection-stale case with Connect GitHub.
15. **Done state (§5.5).** The "every source fresh" fact shows only as the "Health ok" chip, since the spec's one line is just "3 snoozed, the next back Mon 9:00". Header shows Snoozed 3.
16. **Text scale 1.3 (§7.5).** Text only: title 17 px, facts 15.6 px, row 40, group header 36. Column widths, insets and the 440 peek stay in px, so facts like `dm sam.ort…` ellipsize.
17. **Tooltip (§7.5).** Not described in the spec. Frames draw a plain s3 box with the full title, placed under the truncated row.
18. **Back from snooze (§4.3, §5.3).** Age cell says "back" in both cases. "back early: new reply" goes in the title sub-line (spec says the row shows it, not where). "snoozed until 9:00" as a sub-line is an addition.
19. **Tab counts (§7.1).** Computed from row kinds. All 9 = New 1 + Needs you 8; Reviews 3, Messages 2, Work 1, Agents 3. In the back-from-snooze frame Messages 3, Agents 2.
20. **Peek actions (§5.1, grammar peek rule).** Merge… `Ctrl+Enter` is primary and opens the submit menu with Merge preselected; bare Enter stays Open; Approve lives in that menu. Label shortened to "Open" so three buttons fit in 440 px. The Merge note says Merge writes nothing when the head moved (no error code shown).
21. **Content (§4.2).** Rows come from the spec's worked example (meta 205k, ideas 210k, #3099, #3101, #3120, Sam's DM). Added: a failed run, a mention and the brief lines.
22. **Glyph aliases for error/stale (lead's FailedReadLine).** No Down or Stale glyph in the grammar. Frames use `.g.fail` (Down) and `.g.parked` (Stale). Candidates: "Down/Stale glyph aliases".
