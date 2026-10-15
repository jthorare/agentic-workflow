# Plan Design Review — sindri-factory-addendum

**Plan:** `docs/superpowers/specs/2026-10-09-sindri-factory-addendum.md` (revision 2, §17)
**Amends:** `docs/superpowers/specs/2026-10-07-sindri-design.md` (rev 7), `docs/superpowers/specs/2026-10-09-sindri-dashboard-design.md` (Plan 6)
**Reviewed:** 2026-10-09

## Inputs available
| Input | Present? |
|---|---|
| plan.md | ✓ (single-file spec addendum used as the plan) |
| design-brief.md | ✗ (no design brief; human-facing surfaces live in §17.4, §17.9, §17.12 and the Plan 6 dashboard spec, reviewed in its place) |
| product.md | ✗ (product intent taken from §17.0–§17.2) |
| .impeccable.md | ✗ |
| design-tokens.json | ✗ (none in repo; the Plan 6 spec defines only the single attention colour, dashboard §5.1) |

Absent `.impeccable.md` ⇒ Brand/voice is N/A, not scored.

## Summary
The addendum's best design move is matching each decision to a surface by how much context it needs (§17.12 table, L431–436): binary decisions on the phone, read-and-comment on walkthrough pages, heavy decisions at the desk, and live watching in the host. That framing is right and should be kept. But the phone surfaces are described only as a list of decision kinds. There are no card contents, no swipe semantics, no undo, and no loading, error, empty, offline or conflict states. Two of the listed phone decisions contradict desk-only rules elsewhere (tier raises in §17.9, evolve adoption in dashboard §5). The amended dashboard spec is not actually amended: its 127.0.0.1 Host/Origin checks, its token-in-URL login, "no native app / no chat" scope and "workplace text never leaves the machine" privacy basis all change when it is served to a phone over `tailscale serve`, and the addendum doesn't say how. Swipe-only interaction also has no stated non-gesture alternative, which is an accessibility blocker.

## Ratings
Anchors per `_shared/severity.md`.
| Dimension | Rating | Top gap |
|---|---|---|
| Information architecture | 3/5 | Three queues (dashboard Needs you, swipe inbox, desk queue) with no mapping of the addendum's new item kinds to them |
| Interaction states | 2/5 | Swipe inbox, walkthrough pages, desk queue, live session and studio have almost no states defined; phone-specific failures (offline, passkey, stale/conflicting decision) are missing |
| User flow completeness | 2/5 | Phone kinds contradict desk-only rules; the phone can't reach the dashboard as Plan 6 specifies it; passkeys break across host handover |
| Accessibility | 2/5 | Swipe gestures have no single-pointer/button alternative; mobile screen reader, touch targets, walkthrough/recording a11y unaddressed |
| Brand/voice consistency | N/A — missing .impeccable.md | — |

## Surface × state coverage
| Surface | Loading | Error | Empty | Success |
|---|---|---|---|---|
| Swipe inbox (phone, §17.12 L433) | Inherits dashboard §5 "skeleton rows on first load" (dash L215); no phone-specific spec | GAP (passkey failure, tailnet down, signature/ledger write failure, item already decided at the CLI). Only dashboard's generic banner (dash L217) | Inherits "same sentence the CLI prints" (dash L216); no "inbox clear" design for a swipe stack | GAP (no confirmation, no undo window, no "next card" behaviour; the signed record exists, L441–442, but not its UI) |
| Walkthrough pages (§17.4 L157, §17.12 L434) | GAP | GAP (generation failure, missing evidence artifact) | GAP (PR with no evidence yet) | GAP (where comments go and how they become `answer` records is unstated) |
| Desk queue (§17.12 L435, §17.4 L184) | GAP | GAP | GAP | GAP (no definition of whether this is a dashboard view, a CLI list, or both) |
| Live session (§17.12 L436) | GAP | GAP (attach fails over Tailscale SSH; session ended mid-attach) | n/a | Takeover semantics defined (L446–447, main spec §9.3); handback cue on phone GAP |
| Design studio + decision log (§17.4 L152–158) | n/a (desk sessions) | GAP (what the human sees when `/rule` capture or decision-log write fails) | GAP | Partial: decision log record shape (L154–155) |
| `sindri packet validate` / `freeze` (§17.4 L173–185) | n/a | Partial: the eight checks are listed (L174–181) but not their messages, codes or fix hints | n/a | Partial: freeze records hash in ledger (L183) |
| Standing-workflow tier display / change (§17.9 L349–368) | GAP | GAP | GAP | GAP (tier raise is listed as a phone swipe, L433, but no surface shows current tier, precision against the bar, or a demote) |
| Daily digest / `report` outputs (§17.9 L363) | n/a | GAP (where a failed digest shows) | GAP (a quiet day) | GAP (delivery channel unstated: dashboard, ntfy, file?) |
| Phone notifications (§17.13 L460) | n/a | GAP (push not delivered; iOS PWA not installed, L536) | n/a | GAP (payload content and lock-screen privacy) |

### Accessibility sub-table
| Surface | Keyboard | Screen reader | Contrast | Focus mgmt |
|---|---|---|---|---|
| Swipe inbox | GAP (swipe is the only stated input; no buttons or keyboard equivalent) | GAP (no VoiceOver/TalkBack actions for approve/reject; dashboard `aria-live` rule, dash L237, applies only by inheritance) | Inherits attention colour check (dash L224–225) | GAP (where focus goes after a card is decided or removed by polling; dash L238 covers polling only) |
| Walkthrough pages | GAP | GAP (generated HTML has no semantics/heading requirement) | GAP | GAP |
| Evidence `recording` / `narrative` video (§17.11 L418–419) | GAP | GAP (no captions or transcript) | n/a | n/a |
| Desk queue | Inherits dashboard keyboard rule (dash L239) if it is a dashboard view; undefined otherwise | Same | Same | Same |
| Passkey (WebAuthn) prompt | Platform-provided | Platform-provided | Platform-provided | GAP (return focus to the decided card after the OS sheet closes) |
| Live session (Warp remote / `sindri attach`) | Host-provided | Host-provided | Host-provided | n/a |

## Detailed findings

### Information architecture (3/5)
- Evidence: §17.12 L431–436 splits decisions across four surfaces, but the dashboard's single "Needs you" list (dash §4.4, L163–180) is still the only defined aggregator. The addendum adds new item kinds (packet misses L70, scope-expansion requests L215, `scope-mismatch` parks L216, amendments L184, proposed rules L256, standing-workflow `propose` items L351, skill-change items L465) without saying which list each appears in.
- Evidence: Naming drifts across three docs: main spec "Waiting on you" (main L1104), dashboard "Needs you" (dash L187), addendum "desk queue" and "swipe inbox" (L433–435). It's unclear whether the swipe inbox is a filtered Needs you, and whether the desk queue is a dashboard view.
- Evidence: The addendum says it amends the Plan 6 spec (L12) but never names the dashboard views it adds or changes (packets, epics, lenses, standing workflows and their tiers, quota per account §17.15). The dashboard's five views (dash L187–212) have nowhere to show them.
- Recommended changes:
  - Add a table: item kind → surface (phone / walkthrough / desk / both) → dashboard view → CLI verb. Make "desk queue" and "swipe inbox" explicit filters of one Needs you list, so there is one source of truth.
  - Add a "Dashboard amendments" subsection to §17.12 listing new or changed views (Packets, Epics, Standing workflows with tier and precision, Accounts/quota).
  - Pick one name for the human's queue across all three docs.

### Interaction states (2/5)
- Evidence: The only interaction detail for the phone is the decision list (L433) and the auth rules (L438–447). There is nothing on what a card shows, which swipe direction means what, how a mistake is undone, or what happens when the item was already decided at the CLI or by another device.
- Evidence: Phone-specific failure states are unaddressed: tailnet disconnected, passkey cancelled or failed, signed-record write failure, PWA push not installed (acknowledged as a con, L536, but no fallback state).
- Evidence: "Walkthrough pages … Read and comment" (L434) defines no comment storage, no comment-to-`answer` conversion state, and no state for a walkthrough whose evidence is still running.
- Evidence: §17.9 gives tiers (L350–351) but no surface shows a workflow's current tier, its measured precision against the promotion bar (L367–368), or a demote/pause control. A rogue `act`-tier workflow has no stated "stop" interaction.
- Recommended changes:
  - Specify the swipe card: required fields per decision kind (item, repo, the one fact that decides it, link to walkthrough), direction mapping, a short undo window before the signed record is final (or state that it is final and require tap-confirm for irreversible kinds such as undraft), and a "decided elsewhere" state.
  - Add phone rows to the dashboard's per-view state table: offline/tailnet down, auth expired, passkey failed, decision conflict, push unavailable.
  - Give standing workflows a status row (tier, last run, precision vs bar) with pause and demote reachable from the phone (both reduce autonomy, so they are safe to allow).

### User flow completeness (2/5)
- Evidence: Contradiction 1. The swipe inbox lists "raise a standing workflow's tier" (L433), but §17.9 says a tier rises "only through an approval-tier profile change" (L367), and §17.12 says approval-tier surfaces "stay at the desk" (L443–444).
- Evidence: Contradiction 2. The swipe inbox lists "approve or reject … evolve proposal" (L433), but the dashboard keeps `evolve adopt` and `evolve publish` as typed-confirmation commands that are "only shown, never run" from the browser (dash L190–191, L262–263). The addendum says Plan 6's gate rule still holds (L449–450), so it's unclear whether phone "approve" means adopt, publish, or only a pre-approval that still needs the desk.
- Evidence: Reachability. Plan 6 refuses any `Host` other than `127.0.0.1:<port>`/`localhost:<port>` and requires a matching `Origin` (dash L254–255), and logs in by one-time token in a printed URL (dash L250–252). `tailscale serve` (L426–427) reaches the server with the tailnet hostname, and a phone can't easily receive a terminal-printed token. The phone flow as written would be refused at the first request. The addendum doesn't amend these rules.
- Evidence: Host handover. WebAuthn passkeys are bound to the relying-party domain. With the active host swappable (§17.13 L454–461), the dashboard's tailnet hostname changes between laptop and Linux box, so passkeys enrolled on one host fail on the other unless a stable RP ID is chosen. Device enrollment and revocation flows are also unstated (L440).
- Evidence: The mid-build product-question flow is split. Packet misses go "back to the desk queue" (L184), but "free-text feedback from the phone is an `answer` record" (L445). Can the human answer a packet-miss question from the phone? Who tells them the build is blocked waiting on the desk?
- Evidence: Design studio has no failure path. When `sindri packet validate` fails (L173–181), the human's fix loop (which check, which task, how to split or defer) isn't described.
- Recommended changes:
  - Remove tier raises and evolve adoption from the phone list, or restate them as "pre-approve; the desk confirms," and update §17.9 and dashboard §5 to match.
  - Add a "Remote access" amendment to the dashboard spec: allowed Host/Origin for the stable tailnet name, phone enrollment (passkey registration from an authenticated desk session), session cookie scope, and a stable RP ID (a MagicDNS alias or service name that follows `host handover`).
  - Define the blocked-on-desk flow: a notification with the packet-miss question, a phone "answer" path for single questions, and desk-only for amendments that re-freeze. Add `packet validate` output (code, task id, fix hint) like the existing `SND-*` codes.

### Accessibility (2/5)
- Evidence: "Swipe inbox" (L433) and "Swipe gestures need care to feel native" (L536) treat the swipe as the interaction. No single-pointer alternative (visible approve/reject buttons) is stated. WCAG 2.5.1 (Pointer Gestures) and 2.5.7 (Dragging Movements) require one, and swipe is unusable with VoiceOver's default gestures.
- Evidence: The dashboard's a11y rules (dash L234–239: reduced motion, text states, polite `aria-live`, polling never moves focus, keyboard order) are desktop-shaped. Nothing covers touch target size, VoiceOver custom actions, focus after a card leaves the stack, or the passkey sheet returning focus.
- Evidence: Walkthrough pages are generated HTML (L157, L434) shown on phone and desk with no requirements for headings, landmarks, alt text for screenshots, or contrast. The `recording` and `narrative` evidence kinds (L418–419) have no captions or transcript, and they are attached to the human's epic PR (L376–377).
- Recommended changes:
  - State that every swipe action has an equal visible button and a screen-reader custom action, and that swipe is an accelerator only.
  - Add a mobile a11y block to the dashboard spec: 44×44 pt targets, focus moves to the next card (or the empty state) after a decision, the `aria-live` message names the decision recorded, and Playwright runs at a phone viewport with reduced motion.
  - Require walkthrough generators to emit semantic HTML (one `h1`, section headings, alt text from evidence captions) and require a text transcript for video evidence.

### Brand/voice consistency (N/A — missing .impeccable.md)
- Not scored. Note: the addendum's own copy is plain and imperative, consistent with the main spec. The only visual rule in scope is the single attention colour (dash L224–225); the phone view should reuse it unchanged.

### Additional note (privacy, raised for the security lens)
- The dashboard justified showing workplace text because "it never leaves the machine and the dashboard serves only 127.0.0.1" (dash L118–119). Phone views and push notifications (L460) put ring-1 text on a personal device and possibly its lock screen. The addendum should restate the privacy basis (for example, notification payloads carry no item text, only counts and kinds) and confirm the phone is allowed under the workplace agent policy (L511). Hand off to `/cso`.

## Verdict
Interaction states, user flow completeness and accessibility each rate 2/5. The phone surfaces have no defined states, two phone decision kinds contradict desk-only rules, the Plan 6 Host/token rules would refuse the phone as written, and swipe-only input has no accessible alternative.
verdict: BLOCKED
