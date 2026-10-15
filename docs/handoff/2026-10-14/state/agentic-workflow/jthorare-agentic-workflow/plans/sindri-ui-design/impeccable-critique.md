# Impeccable critique: Sindri UI mockups (2026-10-11)

Target: `docs/superpowers/specs/2026-10-11-sindri-ui-design/mockups/*.html` (11 files) in the ui-design worktree.
Measured against `docs/sindri/DESIGN.md` (Linear) and the spec (§3.2, §4, §5, §6, §8, §11, §14). Register: product.
The owner's decisions (single-glance home, attention colour `#f2994a`, the 3 px urgency bar from §5) are not reopened here.
Note: the deterministic `impeccable detect` CLI returned no output in this environment. Every finding below comes from reading the PNGs and the HTML.

## Verdict

- **5-second test (home.png): passes.** The eye goes to "5 need you" (an orange dot plus the word), then to the Needs-you panel with its orange bars, then to the inline actions. The primary action reads at once (the focused lavender **Accept**), and protected answers carry a lock icon plus "at a terminal". Hierarchy comes from surface lift and weight, not from colour, as DESIGN.md asks.
- **AI-slop check: mostly passes.** There are no gradients, glass, hero metrics or identical card grids. The tells are product-level, not aesthetic:
  - the same "Open in Claude Code" split button repeats 4 times in Queued and 4 times in the Queue page;
  - the bottom-right "design reference" caption overlaps content in two files;
  - the shells differ from file to file (see C1), which is the clearest sign the screens were generated separately.
- **Density and rhythm:** dense and Linear-like. Row heights are 36 to 66 px, and the 8 px panel gaps work. Rhythm breaks where the files disagree on page padding (16, 24 and 32 px) and on where the title summary sits (inline or below).
- **Heuristic score (Nielsen, 0-4):** status 4, match 3, control 3, consistency 2, error prevention 4, recognition 3, efficiency 4, minimalism 3, error recovery 3, help 3. **Total 32/40.** Consistency across files is the weak spot.

## Findings

| id | severity | file | issue | fix | status |
|---|---|---|---|---|---|
| U1 | P1 | home, needs-you, decisions, park-grant, park-needs-answer, escalated-gate, launch-flow | Urgency words weren't the spec's. Rows read "pulsing · urgent", "Pulsing · escalated", "Pulsing · idle", "steady · no motion" or "calm"/"Calm · when you can". §4.3 and §11.3 say "needs you now", "failed", "to look at" (the same words as the accessible name) | Replaced them in every row and sheet header | fixed |
| U2 | P2 | all needs-you rows | The kind chip shows `park` / `decision` / `escalated` with the reason inline. §4.3 says the chip should carry the API's `label` (`parked · no-progress`, `escalated · gate`, `decision · re-scope`). §11.3's word plus glyph is met, but the label is not | Spec owner: decide whether the chip shows `label` (it would need a wider kind column, about 130 px) or keep word plus glyph and leave the reason on line 2 | left for spec owner |
| E1 | P1 | all 11 files | The Error token `#eb5757` (§14.1, X22) appeared nowhere. Errors used ink, ink-muted or the attention orange | Added `--error:#eb5757` to every `:root`. The ✕ glyph of the health pill's "index failed" is now Error | fixed |
| E2 | P1 | services-health | The stalled banner had an ink-muted 3 px side stripe and an hourglass glyph. The failed `index` service row used the **attention** bar, which is orange on a non-needs-you surface. Its "✕ failed" was plain ink | Banner: a 2 px Error left rule plus "✕ Stalled" with the ✕ in Error. Failed row: a 2 px Error rule, and the ✕ glyph in Error with the word "failed" kept | fixed |
| E3 | P1 | park-needs-answer | The refused frame (SND-ITEM-409) had no Error treatment | ✕ glyph in Error plus a 2 px Error left rule. The word and code are kept | fixed |
| E4 | P1 | launch-flow §8 | Failure frames used a neutral ⚠ triangle. §11.3 makes `failure` the ✕ glyph, and §14.1 makes it Error | Swapped to a ✕-in-circle glyph in Error plus a 2 px Error left rule on each failure notice | fixed |
| B1 | P1 | home, needs-you, queue, states, park-grant, park-needs-answer, escalated-gate (plus the 4 files with no hover rule) | The primary hover filled `#828fff` behind white text (2.87:1, which fails AA, §11.1). There was no pressed state | Hover is now `#535fc6` (`--brand-hover-fill`) and pressed is `#4c57be` (`--brand-pressed`) in all 11 files. A hover and an active rule were added where they were missing | fixed |
| L1 | P0 | launch-flow §5 | Frame 5 said Sindri stops the worker and then offers "Resume in Claude Code (session 4f1c…)", with a button labelled "Attach — pauses automation". §6.2 says there's no worker process in WF P1-P7 and Resume-on-worker is later (X20) | Rewrote it as **Take over** `T` "pauses automation", then "Taken over · automation paused". The caption now reads "Take over — pauses automation. Pauses the instance. Stopping a running worker and resuming its session arrive with a later engine phase." The resume step is removed and the chip is `ApplyCommand:takeover` | fixed |
| L2 | P1 | launch-flow §4, §6 | Frame 6 offered Resume on any ended session. §6.2 offers Resume only after a handback. Frames 4 and 6 had no phase tag | Frame 6 now reads "Handed back 12:40 · Resume Claude Code session · Open new", with the annotation "offered only after a handback". Frames 4 and 6 are tagged "with the engine (U6b)" | fixed (see L3) |
| L3 | P2 | launch-flow §4, §6 vs spec §6.2 | The coordinator's U6b tag on Attach and Resume conflicts with §6.2's table, which lists both modes as **U6a** | Spec owner: either change §6.2's phase column or retag the frames U6a | left for spec owner |
| K1 | P2 | launch-flow §3, §7 | The Hand back shortcut was `H` in two frames (not a key in §8) and `A` in two others. Decline was `D` (§8 says `X`) | Hand back is `B` everywhere (as on Home and in the Queue footer), and Decline is `X` | fixed |
| K2 | P2 | home, needs-you, queue, park-grant, park-needs-answer, escalated-gate, launch-flow 7d | Decision rows outside the sheet showed **Accept `A`**. §8 says `A` applies the recommended option only while the decision's sheet is open. Home's hint bar said "A Accept" | Removed the `A` chip from list rows, and the hint now reads "A Primary action" | fixed |
| K3 | P2 | decisions | The Decisions page's inline Accept still shows `A`, and its hint bar says "A Accept". This page is a list, not the sheet | Spec owner: say whether `A` applies on the Decisions page (as an inline "sheet") or only in the sheet | left for spec owner |
| H1 | P1 | home | The Needs-you header had no **Mark all seen** (§5) | Added a ghost "Mark all seen `⇧A`" button in the panel header | fixed |
| H2 | P1 | home | The Running and With you panel never showed its cap (§3.2: at most 5 rows, then "Show all N in Queue `G R`") | Fixture is now Running 6 · With you 1, showing 5 rows plus "Show all 7 in Queue `G R`" | fixed |
| H3 | P1 | home vs spec §3.2 | **The fold rule fails.** With 5 needs-you rows and a capped Running panel (5 rows, one with a budget line), the Health strip ends at about 840 px, below the 800 px fold. home.png is now 1280 × 920 to show this. §3.2 claims the fold holds with up to 6 needs-you rows | Spec owner: either cap Running at 3 on Home, make Running rows one line (budget as an inline meter), or restate the fold rule | left for spec owner |
| C1 | P1 | home vs park-grant / park-needs-answer / escalated-gate; queue vs item-detail | The shells disagree. Page padding is 16 / 24 / 32 px. The title summary is inline on home and queue but below the title in the others. The repo tag sits on line 1 in home but line 2 in the park files. item-detail's Queue is a different layout from queue.html: it has no column header and no Action column, and it folds JC8 into "Waiting on you" with no Parked group, while §4.5 asks for five groups | Spec owner or mockup author: pick home.html and queue.html as canonical and regenerate the background of the four sheet mockups to match. This is a restructure, so it's out of scope here | left for spec owner |
| C2 | P2 | all | The fixture drifts. Decisions shows `1` in home and queue but `2` elsewhere. The health pill shows "profile changed" only in home, queue and needs-you. "5 need you" against "6 need you" is intended per scenario, but the Decisions count is not | Align the fixture: one Decisions count per scenario, and the same pill words | left for spec owner |
| C3 | P2 | home, queue, needs-you, decisions, item-detail, services-health | The top bar's **Decisions count uses the attention chip** (orange), while the park and escalated files show it neutral. §5 puts attention only on the Needs-you count chip and the band | Spec owner: say whether the Decisions nav count is a needs-you surface. If not, make it `.count` (neutral) everywhere | left for spec owner |
| R1 | P3 | launch-flow, services-health, states vs home | Reduced motion is drawn three ways: `outline` with offset −2, `box-shadow` inset with 5 px width, and a full row outline in states | Use one rule: states.html's full-row 2 px outline plus `new` (matches §5) | left for spec owner |
| R2 | P3 | launch-flow | The pulse animation was 1.8 s. §5 says 1.6 s | Changed to 1.6 s | fixed |
| V1 | P3 | launch-flow §9 | In the "Never" list, inline `<code>` broke onto its own column ("( cli-only:typed- confirmation )") because the `li` was a flex container | The `li` is now a block with an absolutely positioned ×, so the text flows | fixed |
| V2 | P3 | home, queue | Four identical "Open in Claude Code" buttons in Queued (and 4 more in the Queue page) are noisy. §4.2 asks for **Open** | Use "Open" with the target in the tooltip, or one split button on the focused row only | left for spec owner |
| V3 | P3 | services-health, home | The fixed "design reference" caption overlaps the "12 actions arrive…" list and the Health strip's Profile cell | Cosmetic in the mockup only: move the caption into the page flow | left for spec owner |
| V4 | P3 | states | The **Unreadable** (SND-UI-003) and inline **Error** shared states (§4.1) aren't mocked. §16 lists only five | Add an Unreadable banner frame, or note the omission in §16 | left for spec owner |
| X1 | ok | all | Checked: there is no "Set as default" or "Make default" menu item (the menu says "profile default" / "last used in…" as §6.1 wants). Lavender `#5e6ad2` is never used as text, and links and selected labels use `#828fff`. The focus ring is 2 px `#828fff` with a 2 px offset everywhere. Attention is confined to needs-you, apart from E2 (fixed) and C3 (open) | none | n/a |

## Files changed

All 11 HTML files have the tokens, hover and press fixes, and every PNG was re-screenshotted:
- home.png is 1280 × 920 (see H3);
- launch-flow.png is 1280 × 4800;
- states.png is 1280 × 1660;
- the rest are 1280 × 800.

A pre-edit copy of the folder is at `scratchpad/mockups-backup/`.
