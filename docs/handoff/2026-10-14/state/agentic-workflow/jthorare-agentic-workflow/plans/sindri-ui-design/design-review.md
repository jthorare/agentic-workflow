# Plan Design Review — sindri-ui-design

**Plan:** `docs/superpowers/specs/2026-10-11-sindri-ui-design.md` (worktree `scratchpad/ui-design`)
**Reviewed:** 2026-10-11
**Checked against:** WF `2026-10-10-sindri-workflow-engine-design.md` §3.7–§3.8, §5.6–§5.8, §6.2–§6.4; API `2026-10-10-sindri-api-surface-design.md` §3.6 (`NeedsYouRow`); `docs/sindri/DESIGN.md` (tokens, components, Known Gaps); `docs/sindri/README.md` (X-table); the mockup folder `2026-10-11-sindri-ui-design/`. Every contrast ratio in §11.1 was recomputed (WCAG relative luminance).

## Inputs available
| Input | Present? |
|---|---|
| plan.md (the single-file design spec stands in for it) | ✓ |
| design-brief.md | ✗ (n/a: the spec is itself the design document) |
| product.md | ✗ (n/a) |
| .impeccable.md | ✗ |
| design-tokens.json | ✗ (DESIGN.md is the token source; checked instead) |

`.impeccable.md` is absent, so Brand/voice is **N/A**. Visual-language alignment with DESIGN.md is still checked, under the token plan findings.

## Summary
The spec is unusually thorough. It has a shared state model with a priority order (§4.1), a per-screen state matrix (§4.9), a park/escalation answer table that matches WF §3.8 reason for reason, a catalog-driven control model (`ready` / `pending` / `cli-only`), and contrast numbers that are correct to two decimals. The biggest gap is the mockups: 11 of the 12 references §16 lists don't exist (`mockups/` is empty). The one visual that does exist, the picked shotgun variant 2, contradicts the spec on the attention hue, the focus ring and reduced motion. The other gaps are medium-sized and specific:
- command in-flight and success states are never designed;
- the sheet's behaviour when its subject clears is undefined;
- one hover state fails text contrast;
- `ErrorText` reuses the attention colour, which breaks U5's "needs-you only" rule;
- the keyboard model has two collisions (`X`, and `O` inside the palette);
- many § cross-references point at the wrong sections.

## Ratings
Anchors per `_shared/severity.md` (5 = nothing above LOW, 4 = MEDIUM only, 3 = at least one non-structural HIGH).

| Dimension | Rating | Top gap |
|---|---|---|
| Information architecture | 4/5 | Needs-you rows show an "escalated" kind that the API's `kind` enum doesn't have (D3); the Running panel has no cap, so the five-second test can fail (D16) |
| Interaction states | 4/5 | Command in-flight, success and "copied" states are undesigned (D5); an open sheet whose subject clears is undefined (D6); a launch stuck at `requested` has no state (D7) |
| User flow completeness | 3/5 | 11 of 12 listed mockups are missing (D1); the picked variant contradicts the spec (D2); a parent park that mirrors a child is not covered (D4) |
| Accessibility | 4/5 | `button-primary-hover` is 2.87:1 (D8); `ErrorText` = Attention (D9); disabled `pending` controls carry information (D10); focus behind a full-width sheet (D11); `X` / `O` collisions (D14, D15) |
| Brand/voice consistency | N/A — missing .impeccable.md | (DESIGN.md alignment covered in D9, D23) |

## Surface × state coverage
Cells cite spec line numbers. `GAP` means the spec doesn't address that state for that surface.

| Surface | Loading | Error | Empty | Success |
|---|---|---|---|---|
| Home (4 panels) | L159, L162, L285 | L164, L285 ("—": a per-panel fetch error is GAP) | L180, L285 | GAP: no defined confirmation after an inline action (D5) |
| Needs you (panel + expanded) | L159, L284 | L164, L284 | L284 | L311 (live-region "cleared"); a focus move on clear is in L145–147 |
| Park / escalation sheet + answer forms (§4.4) | GAP: no submitting state for Answer/Resume/Complete (D5) | L164 (generic inline) | n/a | GAP: what the open sheet shows once the instance resumes (D6) |
| Decision sheet / Decisions page | L288 | L288 (`SND-ITEM-409`) | L288 | GAP: no confirmation after `Decide` (D5) |
| Queue / Running / With you | L286 | L286 | L238, L286 | L824 (signal only) |
| Item detail + timeline | L287 | L287 (404) | n/a | GAP: no timeline paging/loading-more state |
| Services / job panel | L289 | L289 | L289 | L272 (job lines); GAP: job finished/cancelled presentation |
| Health | L290 | L290 ("—") | n/a | n/a |
| Launch flow | L291 "Sending to Warp…" with no timeout (D7) | L423–436 | n/a | L438 "Sent to Warp" |
| Command palette | GAP | GAP (the catalog failed to load) | GAP (no matches) | n/a |
| Create item / File request forms | GAP | L164 (generic) | n/a | GAP |
| Copy-the-CLI block | n/a | n/a | n/a | GAP: no "Copied" feedback or announcement |
| Tray menu | n/a | L323–324 | L320 | n/a |
| Context tab (later) | L292 ("—") | L292 | L292 | L247–255 |
| Shared states (Not running, Connecting, Version mismatch, Unreadable, Stalled, Rebuilding, Stale) | L155–166 | L155–166 | n/a | n/a (`mockups/states.html` missing, D1) |

### Accessibility sub-table
| Surface | Keyboard | Screen reader | Contrast | Focus mgmt |
|---|---|---|---|---|
| Top bar + health pill | L502, L617 (`F6`) | L622–623 | L599–606 ✓ | L615 |
| Needs-you rows (inline actions) | L503–509; `X` ambiguous (D14) | L622, L627–629 (but vocabulary differs from the visible words, D21) | Attention 9.4/8.6 ✓; disabled `pending` text GAP (D10) | L145–147, L621 ✓ |
| Detail sheet | L504, L619 | L622 | ✓ | Full-width below 1100 px: GAP (D11) |
| Primary buttons | n/a | L622 | White on primary 4.70 ✓; **hover white on #828fff 2.87 ✗** (D8) | Ring 6.6:1 ✓ (L610) |
| Command palette | L501, L516–519; `O` then 1/2/3 collides with typing (D15) | GAP: result count / active-option announcement | ✓ | L620 trap and return ✓; Esc order (D19) |
| Pulse / attention band | n/a | L311 live region ✓ | ✓ | n/a; unbounded motion (D12) |
| Forms (Answer, Complete, Create, Request) | L515 ✓ | GAP: no validation-error association (≤ 1 MiB, required fields) | ✓ | GAP: where focus lands after submit or failure |
| Text scaling | n/a | n/a | n/a | 0.9–1.3 scale vs fixed row and control sizes (D13) |

## Findings table

| id | severity | lens | location | finding | recommended fix |
|---|---|---|---|---|---|
| D1 | HIGH | design | §16, §4.4 L223–224, §6 L332, Phases "Done" L884–885 | `mockups/` is **empty**. None of the 11 listed files exists: `home`, `needs-you`, `queue`, `item-detail`, `decisions`, `services-health`, `launch-flow`, `park-grant`, `park-needs-answer`, `escalated-gate`, `states`. Only `shotgun/*` exists. §4.4 says its table exists "so the mockups and tests can check it", and every phase's Done requires "design review passed against the mockups". | Generate the 11 mockups (from variant 2, with the spec's tokens) before approval, or mark §16 rows "to come in phase Uₙ" and make each phase's contract unit produce its own mockup. Re-run this check once they land. |
| D2 | MEDIUM | design | §3.1, §11.1, §5 vs `shotgun/variant-2.html` | The picked variant 2, the only visual reference, contradicts the spec in four ways: <ul><li>it uses `--attn:#f0b429` (amber), not `#f2994a`;</li><li>its focus ring is `rgba(94,106,210,.5)`, the 1.8:1 ring §11.1 rejects;</li><li>its pulse is 1.8 s and its band a 2.4 s "breathe", not 1.6 s "with them";</li><li>reduced motion has no `new` word.</li></ul> The contact sheet explains the hue, but an implementer diffing against variant 2 copies all four. | Patch variant 2 (or `home.html` when it exists) to `#f2994a`, a 2 px `#828fff` ring, 1.6 s, and `new` under reduced motion. Add a line in §16 saying variant 2 is a layout reference only. |
| D3 | MEDIUM | IA | §3.2 L93, §4.3 L185 vs API §3.6 `kind` | Rows show the kind as a word, including `escalated`. The API's `kind` is only `decision\|park\|failure\|profile`, so "escalated", or the park reason, would have to be derived by the app from instance state. That is logic U1 forbids. | Add a served display field to `NeedsYouRow` (for example `label: "escalated · gate"`, or `state` + `reason`) as part of E-7, and render it. Or show `park` plus the reason the API sends. |
| D4 | MEDIUM | flow | §4.4 table vs WF §5.7 (child parked → mirrored park), §6.4 first layout | A parent's **park that mirrors a child** ("Parked (no-progress) … forwarded to the child") has no row. The table covers only `escalated: child`. Its answers are the child's, forwarded, and protected ones are copy-the-CLI against the parent address. | Add a row: "`parked` mirroring a child: the child's answer, rendered from the parent's `allowedCommands` (forwarded), labelled 'goes to <child>'". Make sure §4.3 lists the parent and child once (WF `--needs-me` rule). |
| D5 | MEDIUM | states | §4.1 L168–169, §7, §9.1 L528 | <ul><li>**In-flight:** between the click and the response there's no busy or disabled state, so a double-click can send twice. The "commands are sent once" rule doesn't cover this.</li><li>**Success:** the "done" confirmation §4.1 mentions is never designed (inline? where does focus go? for how long?).</li><li>**Copy:** no "Copied" feedback or announcement.</li></ul> | Add a "Command lifecycle" row set to §4.1: <ul><li>`sending` (control disabled, spinner, announced);</li><li>`accepted` (inline check plus a polite "Resumed ITM-…" announcement, then the row clears per L145);</li><li>`refused` (L164).</li></ul> Copy shows "Copied" for 1.5 s and announces it. |
| D6 | MEDIUM | states | §3.2 L118–121, §4.4 | The spec doesn't say what an **open sheet** shows when its subject clears elsewhere (a decision decided at the CLI, an instance resumed, the row gone), or what happens to a half-typed Answer draft. | The sheet keeps the subject, shows a banner "Cleared: <what happened> (from the timeline event)" with **Close**, and disables the form while keeping its text, which can be copied. Add this to §4.9. |
| D7 | MEDIUM | states | §4.9 L291, §6.6 L438 | "Sending to Warp…" has no end if the launcher service never records `launch.started` or `launch.failed` (the service is down or hung). | After N s (for example 15) at `requested`, show "Warp hasn't confirmed: run `sindri doctor`" with a retry. Better, have the API's `LaunchView` move to `failed: SND-LAUNCH-005 timeout` so the app stays logic-free. |
| D8 | MEDIUM | a11y | §14.1 L715, §14.4; DESIGN.md `button-primary-hover` | The primary button's hover is white on `#828fff` = **2.87:1**, which fails WCAG 1.4.3. §11.1 audits the base state (4.70) but not hover. | Keep the `#5e6ad2` background on hover and lighten it to at most `#6d78db` (≥ 4.5 with white), or switch the hover label to `canvas` ink. Add the pair to §11.1 and to U0's contrast test. Record it in X22 beside the focus-ring edit. |
| D9 | MEDIUM | a11y / tokens | §14.4 L769 (`ErrorText → Attention`), U5, §11.3 L630 | Mapping Fluent `ErrorText` to Attention breaks "the attention colour is used only for needs-you". There is also **no error or danger token** at all (DESIGN.md Known Gaps: "Form-field error and validation styling is not visible"), so the stalled banner, inline `ProblemDetails`, destructive confirmations and form validation have no specified colour. | Add an `Error` token from Linear's product palette (for example `#eb5757`, 6.0:1 on the canvas, already measured in §11.3) and map `ErrorText` to it. Style the stalled banner and destructive confirms with it (word + glyph ✕). Record it as a second DESIGN.md exception beside Attention. |
| D10 | MEDIUM | a11y | U3 L36, §8 L517, §14.1 L713 | A `pending:<phase>` control is "disabled, with the phase and the CLI to copy", but disabled text is `InkTertiary` (3.3:1, "never for information"). The phase and the CLI are information. | Render the control itself disabled, and put the phase and CLI on an adjacent line in `InkSubtle` (5.9:1) that stays focusable, so it can be read and copied. |
| D11 | MEDIUM | a11y | §3.2 L121 vs §11.2 L619 | Below 1100 px the sheet covers the full width, but opening it "keeps focus on the row", which is now hidden behind the sheet. | When the sheet occludes the list, move focus to the sheet's heading. The back control or `Esc` returns focus to the row. |
| D12 | MEDIUM | a11y | §5 L300, L304–305 | The pulse runs until each row has been focused for 1 s. With several rows the motion can run indefinitely, and the OS reduced-motion setting is the only way to stop it. WCAG 2.2.2 (Pause, Stop, Hide) wants a mechanism. | Add a **Mark all seen** action (palette + key, for example `Shift+A`, or on the Needs-you header) and an in-app "Reduce motion" setting (§12.3 preferences) that forces the §11.4 path. |
| D13 | MEDIUM | a11y | §14.2 L743, §14.3 L751, §3.2 L115–117 | Text scales 0.9–1.3 through one resource, but `Size.Row` 36/40, `Size.TopBar` 48 and `Size.Control` 30 are fixed, so text clips at 1.3. The "above the fold at 1280 × 800 with 6 rows" claim is made only at 1.0. | Make row, top-bar and control heights `MinHeight` scaled by the same factor (or content-sized), and state the fold rule at 1.0 with "at 1.3 the cap drops to 4 rows" (or similar). |
| D14 | MEDIUM | a11y / keyboard | §8 L507 | `X` means "Reject / Decline / Cancel". An escalated-gate row has both **Reject** and **Cancel** (§4.4, WF §3.8), and a park has Cancel beside other answers, so one key maps to two destructive actions. | Bind `X` from the catalog's `shortcut?` (E-8) so each action has a single key (for example `X` Reject or Decline; `Shift+X` Cancel instance), and make the `?` sheet and `kbd` chips show the per-row binding. |
| D15 | MEDIUM | a11y / keyboard | §6.1 L346–347 vs §8 L505, L516 | "`O` then `1`/`2`/`3` in the palette" conflicts with typing `O` as a palette query, and duplicates `Shift+O` (target menu). | Choose one: `Shift+O` opens the target menu, where `1`/`2`/`3` pick a target. Remove the palette variant from §6.1. |
| D16 | MEDIUM | IA | §3.2 L115–117, §4.2 L175–177, §2 L58–59 | Needs you is capped at 6 and Queued at 5, but **Running and With you has no cap**. Many running items push Health below the fold and fail the "no scrolling" five-second test. Behaviour at the 900 × 600 minimum is unstated. | Cap Running at N rows with "Show all N in Queue (`G R`)", pin With you rows first, and state that the five-second test runs at 1280 × 800 (the minimum may scroll). |
| D17 | MEDIUM | IA (doc) | L58 "(§14)", L59 "(§16)", L67 "(§12)", L397 "(§14 U7)", L679 "(§14, §15)", Status L4 vs §17 L812 | The cross-references are stale: <ul><li>phases are cited as §14 (that's the token plan; Phases is unnumbered);</li><li>the fixture ledger is cited as §16 (that's Mockups);</li><li>app copy is cited as §12 (that's heads);</li><li>the P10 spike is cited as "U7" (U7 is the Context tab; the spike is E-10);</li><li>the Status line says §17 lists the review resolutions, but §17 is "(Filled after `/autoplan`)".</li></ul> | Number the Phases section (§18) and fix each reference. Add the fixture definition (or point to the API spec's `sindri-prove.sh` fixture). Add the missing app-copy section (see D18). Make the Status line match §17. |
| D18 | MEDIUM | IA / voice | U1 L34, §2 L66–67 vs L157, L163, L493, L528, L438, §5 tray table | "The app's own copy is limited to labels and headings", yet the app writes many sentences of its own: "Sindri and the app are different versions", "Sindri not responding: run `sindri doctor`", "decided elsewhere" / "changed since you looked", "Sent to Warp", "Needs a typed confirmation at a terminal", "retry after the rebuild", the tray tooltips and the urgency words. Some have to be the app's (no API), but the rule as written is false. | Add an explicit **App-owned copy** table (pre-API states, launch acknowledgement, the copy-block line, tray tooltips, urgency words), and state that everything else, including 409 text, comes from `ProblemDetails.detail` or `empty`. |
| D19 | LOW | keyboard | §8 L504 | Esc order is "close the detail, then leave the palette, then clear the filter". The palette is modal and sits on top. | Esc closes the topmost layer: palette or dialog → sheet → filter. |
| D20 | LOW | keyboard | §8 L502 | The `G` chord's timeout and visible pending state aren't specified. | A 1 s chord window with a small "G …" hint in the top bar. A non-matching key cancels the chord. |
| D21 | LOW | a11y / voice | §4.3 L186 vs §11.3 L628–629 | The visible urgency words ("pulsing · urgent", "steady · no motion", "calm") describe animation, which is wrong under reduced motion, and differ from the accessible names ("needs you now", "failed", "to look at"). | Use one vocabulary for both: "needs you now", "failed", "to look at". |
| D22 | LOW | a11y | §11.1 L609 | DESIGN.md's 50 % focus ring is described as "about 2:1". It computes to **1.83:1** on the canvas (1.85 on surface-1). The conclusion stands. | Write "1.8:1". |
| D23 | LOW | tokens | §14.1–§14.3 vs DESIGN.md | Tokens are left out without saying so: `brand-secure #7a7fad`, `inverse-*`, `rounded.xl/xxl/full`, and the display, headline, subhead, body-lg and body ramps. `primary-focus` is renamed `PrimaryPressed`, which drops DESIGN.md's "focus" meaning (fine, but say why). | Add a "Not used in the app" row like `section`'s, and a note on the `primary-focus` → `PrimaryPressed` rename. |
| D24 | LOW | flow | §8 L506 | `A` on a decision accepts the recommended option with one key and no confirmation. That's an honest-mistake risk on a row that merely has focus. | Require that `A` on a decision applies only when the sheet is open, or show "Accepted A · Undo" for 5 s before sending. |
| D25 | LOW | doc | §11.3 L632–634 | The attention-colour section still reads as a candidate list ("recommended") although U5 records the owner's pick. | Keep the decision and its contrast numbers; move the alternatives to §17 or a footnote. |
| D26 | LOW | flow | §6.4 L408–409 vs WF §5.6 | The UI and API §3.6 treat `takeoverIdle` as a needs-you `park` row cleared by handback. WF §5.6 still says it "emits `notify.desk` … then clears". The API spec flags the WF edit (API L427), but U5 depends on it silently. | Add "WF §5.6/§6.3 edit (idle takeovers and desk tasks as `park` rows)" to U5's Depends on. |

**Count:** 26 findings: 0 CRITICAL, 1 HIGH, 17 MEDIUM, 8 LOW.

## Detailed findings

### Information architecture (4/5)
- Evidence: the four-question goal (L49–53) maps one-to-one onto Home's fixed panel order (L113–114, L173–178), and the cap and "Show all N" (L115–117) handle the long-list case. The navigation and deep-link table (§3.3) is complete, and stable focus by row id (L145–147) is specified.
- Evidence: L93 renders `escalated` as a row kind, but API §3.6 has only four kinds (D3). Running has no cap (D16). Several § references point at the wrong sections (D17), and the "labels and headings only" copy rule is contradicted throughout (D18).
- Recommended changes: D3 (a served label on `NeedsYouRow`), D16 (cap Running), D17 and D18 (fix references; add an App-owned copy table).

### Interaction states (4/5)
- Evidence: §4.1's ten shared states in priority order and §4.9's matrix cover loading, rebuilding, stalled, stale, version mismatch and not-running for every screen, with all sentences sourced from `empty` / `ProblemDetails`. This is better than most plans.
- Evidence: the gaps are around actions. No sending or accepted state (D5); an open sheet whose subject clears (D6); launch stuck at `requested` (D7); palette and form states (sub-table GAPs).
- Recommended changes: add a command-lifecycle row group to §4.1 (D5), a "cleared while open" row to §4.9 (D6), and a served launch timeout (D7).

### User flow completeness (3/5)
- **Park/escalation vs WF §3.8:** checked reason by reason, and all thirteen §4.4 rows match WF §3.8's ways out and tiers:
  - `--grant` and `--grant-tokens` are human-terminal, so they're copy-the-CLI;
  - `--text` on `needs-answer` and `no-progress` is desk;
  - `--resume` matches for `timeout`, drift (after re-approval), dispatch, `SND-WF-010` and escalated `timeout`;
  - `--accept` is protected and `--reject` is desk;
  - `taken-over` → handback, and revoked → cancel only;
  - `cancel` and `takeover` appear as "Others" on every park (L197).
  The 1 MiB answer limit matches WF §5.8. The one gap is the **mirrored child park** (D4).
- Evidence: the launch flow (§6) covers choosing a target, open/attach/take over/resume, the never-resume-running rule, and eight failure states with codes and fixes. Takeover and handback are offered in four places (L405–411).
- Evidence: the flows have no visual check. 11 of 12 mockups are missing (D1), and the picked variant disagrees with the spec on hue, focus ring, timing and reduced motion (D2).
- Recommended changes: D1 (generate the mockups or re-scope §16 per phase), D2 (patch variant 2), D4 (add the mirrored-park row).

### Accessibility (4/5)
- **Contrast:** every §11.1 number recomputes correctly (19.61, 14.28, 6.42/5.86/5.63, 3.62/3.30, 4.44, 7.27, 4.70, 6.58, 9.37/8.55; hairlines 1.36/1.69). The focus-ring deviation to `#828fff` (6.64 on surface-1) is correct and needed. The pair §11.1 misses is the hover label (D8). Don't put `primary #5e6ad2` text on `PrimarySoft` (it's 1.3:1 vs the canvas as a background). The selected nav label in `#828fff` on `#1d2036` is 5.58, fine.
- **Focus order** (§11.2): well specified: `F6` regions, `Tab` within a region, `J`/`K` across panels, dialog traps, no focus move on refresh, and announced clears. The full-width sheet breaks it (D11).
- **Reduced motion** (§11.4): platform-read via `IMotionPreference`, with all durations set to 0, a static outline plus the word `new`, and no information carried by motion alone. Good. It still lacks an in-app pause for the unbounded pulse (D12) and scaled sizes (D13).
- **Keyboard:** the model is Linear-faithful and scoped to focus, which satisfies WCAG 2.1.4. There are collisions on `X` (D14) and on `O` inside the palette (D15), plus Esc order (D19), the chord timeout (D20) and `A`'s one-key decision (D24).
- **Token plan vs DESIGN.md:** all 19 DESIGN.md colours the app needs are mapped, plus Attention, AttentionSoft and PrimarySoft. The type ramp is honestly scaled down with sources, and the spacing, radius and motion groups are complete. Missing: an error/danger token, with `ErrorText` wrongly reusing Attention (D9), and the unused tokens aren't declared as such (D23).
- Recommended changes: D8, D9, D10, D11 first (they change tokens and focus rules U0 bakes in), then D12–D15.

### Brand/voice consistency (N/A — missing .impeccable.md)
- Not scored. The spec follows DESIGN.md (near-black surface ladder, no shadows, lavender kept scarce, one documented chromatic exception). Voice is mostly delegated to API sentences. D18 and D21 are the voice-adjacent issues.

## Verdict
NEEDS_WORK, driven by one HIGH (D1: 11 of 12 listed mockups don't exist, though every phase's Done depends on them) and User flow completeness at 3/5. The park/escalation answers, state model and contrast audit are otherwise solid, and the 17 MEDIUM findings each have a concrete fix.
verdict: NEEDS_WORK
