# Plan Design Review — sindri-factory-addendum (round 2)

**Plan:** `docs/superpowers/specs/2026-10-09-sindri-factory-addendum.md` (revision 3, §17 through §17.21)
**Also reviewed:** `docs/sindri.html` (interactive walkthrough, a human-facing surface derived from the spec); `docs/superpowers/specs/2026-10-09-sindri-dashboard-design.md` (Plan 6, the spec §17.19 says it amends)
**Round 1:** `round1/design-review.md` (BLOCKED: interaction states 2, flows 2, accessibility 2)
**Reviewed:** 2026-10-09
**Out of scope (decided):** C# port, stacked epics, swappable host, ring order, core tenet.

## Inputs available
| Input | Present? |
|---|---|
| plan.md | ✓ (single-file spec addendum used as the plan) |
| design-brief.md | ✗ (human-facing surfaces live in §17.4, §17.12, §17.19 "Phone decisions", §17.21 and `docs/sindri.html`; reviewed in its place) |
| product.md | ✗ (intent taken from §17.0–§17.2) |
| .impeccable.md | ✗ |
| design-tokens.json | ✗ (the walkthrough defines its own `:root` tokens, light and dark) |

Absent `.impeccable.md` ⇒ Brand/voice is N/A, not scored.

## Summary
Revision 3 closes most of round 1's blockers. The two phone-vs-desk contradictions are gone: tier raises, capability changes, evolve adoptions and freezes are now explicitly excluded from the phone (L485). The Plan 6 reachability problem has a stated fix (loopback + `tailscale serve`, Host/Origin accept the stable tailnet name, passkey replaces the token, L677–678). Passkeys survive handover through a stable name that follows the lease (L660–661). Enrollment and revocation are defined (L671–672), and there are eight named phone states, buttons alongside swipe, captions, and a queue mapping (L679–685). Two HIGH gaps remain. Walkthrough pages are meant for "read and comment" (L486), but they are now served from an origin with no cookies or endpoints (L675), so commenting has no path. And the 10-second undo (L683) isn't reconciled with a decision that is a signed ledger record going through the CLI code path (L492–494). Both are fixable without restructuring. `docs/sindri.html` is a strong accessible surface: keyboard-operable diagrams, a skip link, `aria-live` details, reduced motion, and status shown by glyph plus text. Its main issues are one contrast token and drift risk.

## Round-1 gap closure
| Round-1 gap | Status | Where |
|---|---|---|
| Phone list contradicts desk-only tier raise / evolve adoption | **Closed** | L485 "Never tier raises, capability changes, evolve adoptions or publishes, or packet freezes" |
| Plan 6 Host/Origin + token-in-URL refuse the phone | **Closed in the addendum**, not yet in Plan 6 | L677–678; the dashboard spec has no tailnet/passkey text yet |
| Passkeys break across host handover | **Closed** | L660–661 |
| Device enrollment / revocation unstated | **Closed** | L671–672 |
| No swipe card states (stale, decided elsewhere, offline, passkey fail) | **Mostly closed** | L673–674, L679–681 (states named; per-state UI deferred to Plan 6 amendment) |
| Swipe direction mapping, undo | **Closed, new gap** | L682–683; undo vs signed record is undefined (H2) |
| Swipe-only input, WCAG 2.5.1 | **Closed** | L682 "every swipe has visible buttons" |
| Video evidence lacks captions/transcript | **Closed** | L683 |
| Three queues with no item-kind mapping | **Mostly closed** | L684–685; L487 lists desk kinds |
| Walkthrough comment storage → `answer` | **Open, now contradictory** | L486 vs L675 (H1) |
| Standing-workflow tier display, pause/demote | **Open** | §17.9 L393–414 unchanged |
| Packet-miss "blocked on desk" flow | **Partially open** | L228–229; L497 free-text `answer` from phone, but no notification or blocked state |
| `packet validate` messages/fix hints | **Closed** | L700–701 `--json` with rule id and fix |
| Dashboard views for packets/epics/workflows/quota | **Open** | no "Dashboard amendments" list |
| Mobile SR custom actions, touch targets, focus after decision | **Open** | not stated |
| Walkthrough generated-HTML semantics | **Open** | not stated |
| Notification payload / lock-screen privacy | **Open** (security handoff) | L512, L717 give channel and budget, not payload |

## Ratings
Anchors per `_shared/severity.md`.
| Dimension | Rating | Top gap |
|---|---|---|
| Information architecture | 4/5 | Dashboard views for packets, epics, standing workflows and quota are still unnamed; walkthrough HTML is a second hand-maintained source |
| Interaction states | 3/5 | H2: 10-second undo isn't reconciled with a signed ledger record and the outward actions it triggers; per-state UI lives in a Plan 6 amendment that doesn't exist yet |
| User flow completeness | 3/5 | H1: walkthroughs are "read and comment" but served from an origin with no endpoints, so comments have nowhere to go |
| Accessibility | 4/5 | Mobile screen-reader actions, target size, focus after a card leaves, and undo announcement unstated; walkthrough `--ink-3` text is 4.04:1 |
| Brand/voice consistency | N/A — missing .impeccable.md | — |

## Surface × state coverage
| Surface | Loading | Error | Empty | Success |
|---|---|---|---|---|
| Swipe inbox (phone) | L679 "deciding"; first-load inherits Plan 6 skeleton rows | L680–681 stale, offline, passkey failed, revoked device; L673–674 refusal "with a code" | Inherits Plan 6 empty sentence; no "inbox clear" for a card stack (MEDIUM) | L679 "decided here"; L683 10 s undo; **GAP** on what undo does after signing (H2) |
| Swipe up "queue for desk" | n/a | GAP (what if the item is already in the desk queue or decided) | n/a | L683, L684–685 (moves to desk queue in Needs you) |
| Walkthrough pages | GAP (evidence still running) | GAP (generation failed, artifact missing) | GAP (PR with no evidence yet) | **GAP**: comment path contradicts L675 (H1) |
| Desk queue / Needs you | Inherits Plan 6 | Inherits Plan 6 | Inherits Plan 6 | L684–685 lists both queues with their queue label |
| Live session (Warp / `sindri attach`) | GAP | GAP (attach fails; session ended mid-attach) | n/a | Takeover semantics L498–499 |
| Design studio + decision log | n/a | Partial: L700–701 `validate --json` with rule id and fix | GAP | Freeze recorded L227; `packet amend` L700 |
| Standing-workflow status | GAP | GAP | GAP | GAP (no tier/precision view; no pause) |
| Daily digest / notifications | n/a | GAP (push undelivered, iOS PWA not installed, L821) | GAP (quiet day) | Channel named L512 (ntfy or dashboard push); budget L717; payload GAP |
| Collaboration surfaces (§17.21, P4) | GAP | GAP (claim conflict besides first-signature-wins) | GAP | L767 "decided elsewhere" reused; L768 comments, @-mentions, handoff, presence listed only |
| `docs/sindri.html` walkthrough | n/a (static) | n/a | Filter-all-off: `aria-live` note L244 | Diagram details update in `aria-live` regions (L305, L321, L354) |

### Accessibility sub-table
| Surface | Keyboard | Screen reader | Contrast | Focus mgmt |
|---|---|---|---|---|
| Swipe inbox | L682 visible buttons (closed) | GAP (VoiceOver/TalkBack custom actions; announcement of "decided" and "undo available") | Inherits Plan 6 attention colour | GAP (focus after a card leaves; after passkey sheet closes) |
| Undo window (10 s) | GAP (keyboard path to undo) | GAP (announce, and whether 10 s is enough for AT users; WCAG 2.2.1 spirit) | n/a | GAP |
| Walkthrough pages (generated) | GAP | GAP (no heading/landmark/alt requirement on generators) | GAP | GAP |
| Recording / narrative evidence | n/a | L683 captions or transcript (closed) | n/a | n/a |
| Desk queue | Inherits Plan 6 | Inherits Plan 6 | Inherits Plan 6 | Inherits Plan 6 |
| `docs/sindri.html` | Good: skip link L225, diagram nodes Enter/Space L1087, `:focus-visible` L41 | Good: `role="img"`/`aria-labelledby`, `aria-pressed`, `aria-live` details | `--ink-3` #7d7468 on `--bg` #f4f0e8 = 4.04:1 at 12–13 px (fails AA); others pass (accent 5.41, built 5.39, dream 4.72) | Headings `tabindex=-1` for chapter jumps L42; reduced motion L219 |

## Detailed findings

### Information architecture (4/5)
- Evidence: Queue structure is now explicit. "The swipe inbox holds non-protected binary items; the desk queue holds open questions, amendments, freezes and every approval-tier item; the dashboard's 'Needs you' lists both with their queue" (L684–685). That settles round 1's naming drift.
- Evidence: The addendum still doesn't list the dashboard views it adds. Packets, epics, standing workflows with tier and precision, accounts/quota (§17.15), human-minutes (L715) and devices (L671) have no named view. Plan 6's five views can't show them.
- Evidence: `docs/sindri.html` is a second human-facing rendering of the spec, maintained by hand, with status tags (built / specced / designed / dream) and `§` refs. The spec doesn't name it or say how it stays in sync. It already reflects §17.19 states (html L673–676) and §17.21 (html L598–612), so it is current today, but nothing keeps it so.
- Recommended changes:
  - Add a "Dashboard amendments" list under §17.19 (or in Plan 6): new views and their CLI equivalents (packets, epics, standing workflows, accounts/quota, devices, human-minutes).
  - Name `docs/sindri.html` in the spec as a derived artifact, and add a docs check (each `§` ref resolves; status tags match §17.16 rollout) or a note in `/syncDocs`.

### Interaction states (3/5)
- Evidence: The phone now has eight named states: pending, deciding, decided here, decided elsewhere, stale, offline (queued, refused if stale), passkey failed, revoked device (L679–681). This is a real fix. But they are "spec'd in Plan 6's amendment" (L679), and the Plan 6 file has no tailnet, passkey or swipe text yet.
- **H2 (HIGH):** "each choice can be undone for 10 seconds" (L683), yet "each decision is a signed ledger record … through the same code path as the CLI verb" (L492–494), and outward actions are outbox commands (L763). It's unstated whether the signature and the outward command (undraft, accept evidence) wait for the window to close, or whether undo writes a compensating record after the fact. The answer also decides what "decided elsewhere" means during the window, and what undo does for an offline-queued decision.
- Evidence: Standing workflows still have no status surface: no current tier, last run, precision against the bar (L413–414), or pause. With tier raises correctly kept off the phone (L485), there's still no stated stop for a misbehaving `act`-tier PR babysitter (L401).
- Evidence: Walkthrough pages have no loading, error or empty states (evidence still running, generation failed, no evidence yet).
- Recommended changes:
  - State the undo model. Recommended: the client holds the choice for 10 s and signs and submits only when the window closes, or when the user taps "done". Undo before then sends nothing. Offline decisions are signed at reconnect and re-checked for staleness. Then "decided here" means committed.
  - Add a standing-workflow row to the dashboard (tier, last run, precision vs bar). Allow **pause** and **demote** from the phone as autonomy-reducing kinds, and keep raises at the desk.
  - Give walkthrough pages a "building evidence" placeholder and a "generation failed: <code>" page.

### User flow completeness (3/5)
- **H1 (HIGH):** Walkthrough pages are for "Read and comment: designs, scope maps, PR walkthroughs, incident timelines" (L486). But §17.19, which wins over earlier sections (L624), serves them "from a separate origin with no cookies or decision endpoints" (L675). As written, a comment can't be posted, so the phone/desk review of designs and scope maps has no output. Comments on walkthroughs show up again only as a P4 collaboration surface (L768).
- Evidence: The blocked-build flow is still half-defined. A packet miss "goes back to the desk queue" (L228–229), and the phone can send free-text `answer` records (L497), but nothing notifies the human that a build is blocked, and nothing routes a single-question miss to the phone.
- Evidence: Ring-1 defaults turn phone decisions off (L692), and ring 1 runs first (L795). The first real usage is therefore desk-only. That is consistent and fine, but F4 "Approvals from the phone" (L556) will first be exercised on ring 2 or after a workplace policy change. Worth a sentence so F4's exit criterion is testable on ring 1.
- Evidence: §17.21 defines the two-humans rule (first signed decision wins, the other sees `decided elsewhere`, L767) and lists claims, presence, handoff and a shared desk queue (L768) without flows. That's acceptable at the P4 roadmap level (MEDIUM): claim/unclaim, hand-to-person and per-user notification preferences need states when P4 starts.
- Recommended changes:
  - Fix H1. Either (a) comments are posted from the dashboard origin's item view, with the walkthrough embedded or linked read-only (comment → `answer` record from the passkey session), or (b) walkthroughs are read-only and §17.12's row says "read; comment on the item in the dashboard."
  - Add a "blocked on you" notification kind for packet misses. A single closed question is answerable on the phone as an `answer`; anything that re-freezes stays at the desk.
  - State F4's ring-1 test path (desk dashboard over the tailnet), or note that phone decisions are proven on ring 2.

### Accessibility (4/5)
- Evidence: Round 1's blocker is closed: "every swipe has visible buttons (WCAG 2.5.1)" (L682), and recordings carry captions or a transcript (L683).
- Evidence: Mobile specifics are still missing. Nothing covers VoiceOver/TalkBack custom actions for approve/reject/queue, 44×44 pt targets, where focus goes after a card leaves or after the passkey sheet closes, or how the undo window is announced and reached by keyboard or switch. Swipe up for "queue for desk" (L682) competes with page scroll and the iOS home-indicator gesture; with buttons present this is MEDIUM, not blocking.
- Evidence: Generated walkthrough pages still carry no semantic requirements (headings, landmarks, alt text from evidence captions).
- Evidence (`docs/sindri.html`): Strong overall. It has `lang`, a skip link (L225), `:focus-visible` (L41), keyboard-operable SVG nodes (L1087), `aria-pressed` filters (L240–243), `aria-live` details (L305, L321, L354), reduced motion (L219), and status carried by glyph and text, not colour alone. Issues: `--ink-3` (#7d7468) on `--bg` (#f4f0e8) is 4.04:1 and is used for 12–13 px text (L52, L65, L73, L75), which fails AA (MEDIUM). The phone-state chips (L676) use the `.chipbtn` button style with `cursor: pointer` (L179) but are inert list items, a false affordance (LOW).
- Recommended changes:
  - Add a mobile a11y block to the Plan 6 amendment: SR custom actions per card, 44 pt targets, focus moves to the next card or the empty state, `aria-live` names the recorded decision and "Undo, 10 seconds", the undo is a focusable button, and a Playwright phone-viewport run with reduced motion.
  - Require walkthrough generators to emit one `h1`, section headings, landmarks and alt text, checked by the existing `a11y` evidence kind (L465).
  - In `docs/sindri.html`, darken light-mode `--ink-3` to about #6f675b (≥4.5:1 on `--bg`), and give the state chips a non-button style.

### Brand/voice consistency (N/A — missing .impeccable.md)
- Not scored. Note: spec copy and the walkthrough share a plain, imperative voice. The walkthrough's warm-paper palette and serif headings are its own; if Plan 6's phone view borrows them, record them in a `design-tokens.json` so the dashboard's single attention colour stays the only alert hue.

### Additional note (privacy, for the security lens)
- Round 1's lock-screen concern is still open. L512 names the notifier (ntfy on the tailnet or dashboard push), and L717 budgets it, but payload content isn't defined. Recommend: pushes carry counts and kinds only, never item text, and ring-1 pushes stay off while phone decisions are off (L692).

## Verdict
Round-1 blockers on states, flows and accessibility are largely closed (every dimension now ≥3). Two HIGH findings keep this from passing: the walkthrough comment path contradicts the cookie-less origin (H1, flows), and the 10-second undo is undefined against signed decisions and outward actions (H2, states).
verdict: NEEDS_WORK
