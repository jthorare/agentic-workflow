# Consolidated Plan Review — sindri-ui-design

**Plan:** docs/superpowers/specs/2026-10-11-sindri-ui-design.md (branch docs/sindri-ui-design)
**Reviewed:** 2026-10-11
**Lenses applied:** product · arch · design · devex · security

## Normalized findings

HIGH and the MEDIUMs that change the design. Every other MEDIUM and LOW is listed by id in the lens files and resolved in
the spec's §17.

| id | severity | lens | location | finding | recommended fix |
|---|---|---|---|---|---|
| N1 | HIGH | arch, security | §6.7 E-1 | The launcher has no delivery, dedupe, freshness or crash rules: launches can get stuck, double, or fire after a restart without a click | Launcher ledger cursor; fact keys per `launchId`; an interrupted request becomes `launch.failed SND-LAUNCH-007` and is never retried; act only on requests < 30 s old; `OpenItem` refused while a launch is `requested`; a `launches` projection |
| N2 | HIGH | arch | E-6, X18 | The engine router can't turn an event into a takeover (inbox rows apply only to waits; takeover needs a nonce), and E-6 has the engine stamp a human actor | A WF trigger kind `open-request` applied directly in `Commit`, actor `service:engine`; the launcher waits for the takeover before opening Warp |
| N3 | HIGH | arch | §6.2, E-2 | No source tells the server whether a session is live, so attach versus resume can't be decided in v0 | v0: Attach only focuses Warp; Resume only after a handback; session lifecycle via hooks later |
| N4 | HIGH | arch | §6.2, E-6, X20 | Nobody owns stopping a running worker | Defer the "take over a running worker" case to the engine phase that adds the launcher Step runtime; v0 Take over = pause only |
| N5 | HIGH | arch | §9.1 | The UI never mints the engine's nonce, and doesn't map the WF error codes | Mint at click time; map `SND-WF-002/009` as 409; show the others inline; a cross-spec row for the nonce POST's class |
| N6 | HIGH | arch | §4.4, §7 | `answer-reject` and `answer-resume` are undefined variants (so fail-closed = protected); `allowedCommands` are verbs, not action ids | E-12 variants; E-13 `allowedCommands` as `{actionId, cli}` |
| N7 | HIGH | security | §6.3 | Codex and Cursor sessions launched by Sindri may pass the CLI's agent check and act as `human` | The exec line sets `AW_SINDRI_CHILD=1` |
| N8 | HIGH | product | Phases | The headline launch comes last and is gated on the engine | Split U6: U6a Open (P10, before the engine), U6b attach, resume and take over (engine) |
| N9 | HIGH | design, devex, product | §16 | The listed mockups didn't exist at review time | Generated (11 files) |
| N10 | HIGH | devex | Phases | No fixture ledger; no way to stall the projector; E-edits have no owning phases; X7's phase unnamed; fake `ISindriApi` contract-tested against nothing; no OS-screenshot, five-second or keyboard recipe; launch proof unrunnable | A seeding script; a raw ledger insert for the stall; an E-edit owner table; golden JSON from the API phases; a proving recipe (§18.2) |
| N11 | MEDIUM | arch, design | §3.2, §4.3, E-8 | Logic leaks: the health word, the needs-you "escalated" label, `CloseItem` destructive only when cancelled, the event→views table, `readsViews` | Served `health`, `NeedsYouRow.label`, `CloseItem:done/:cancelled`, `readsViews`, `EventCatalog.invalidates` |
| N12 | MEDIUM | security | §6.3, §7 | Shell quoting in the exec line; a CLI-not-found guard that falls through; copied CLI strings with control characters; keys hitting the next row after an auto focus move | YAML library, shell-quoted argv, `exit 127`; API-built CLI strings from checked values plus a client refusal; a 500 ms key guard and pointer hover-freeze |
| N13 | MEDIUM | design | §11, §14 | Primary hover fails AA (2.87:1); no error token (ErrorText mapped to Attention); pending info in disabled ink; no in-app way to stop the pulse; text scaling clips fixed heights | `#6d78db` hover... (see §11.1); an `Error` token `#eb5757`; pending info in `InkSubtle`; Mark all seen + an in-app reduce-motion setting; scaled min heights |
| N14 | MEDIUM | design | §4 | No command lifecycle states, no cleared-subject state, no launch timeout, no mirrored-child park row, Running uncapped | Added to §4.1, §4.4, §4.9, §3.2 |

## Top tensions (cross-lens)

1. **Speed to the headline vs. correctness of takeover** (product N8 vs arch N2/N4). Resolution: U6a ships Open without
   the engine (nothing to pause before S2); takeover semantics land with U6b once the `open-request` trigger exists.
2. **Logic-free UI vs. API surface growth** (arch N11 vs devex N10, effort). Resolution: accept the extra served
   fields; each E-edit is small and has an owning phase, which keeps the client testable from goldens.
3. **One-key speed vs. honest-mistake safety** (design D24/D14 vs security F2). Resolution: `A` acts on a decision only
   with the sheet open; keys come from the catalog's `shortcut`; a key guard after automatic focus moves.
4. **Product C4 (run protected commands in Warp from the app) vs. security's CLI-only rule.** Resolution: deferred and
   flagged for the owner; the confirmation would still be typed in the terminal, but it widens the launcher to run
   arbitrary catalog CLI strings.
5. No tension between design and devex on mockups: both want them before approval.

## Per-lens highlights
### Product
- Top wins: the five-second goal; one list; the headline interaction defined end to end.
- Top gaps: launch sequencing (C1), uncapped Running (C3), success metrics (C5), the dropped Agents goal (C6).
- File: `plans/sindri-ui-design/product-review.md`
### Architecture
- Top wins: the rendering model is sound; CQRS respected for reads.
- Top gaps: N1–N6, logic leaks (F7–F9), no GUI session (F10), probe churn (F11), no Start control (F12).
- File: `plans/sindri-ui-design/arch-review.md`
### Design
- Top wins: the §4.4 answers match WF §3.8 (13/13); the contrast audit is correct.
- Top gaps: mockups (D1), variant 2 contradictions (D2), lifecycle and cleared states (D5–D7), a11y gaps (D8–D13), keys (D14–D15).
- File: `plans/sindri-ui-design/design-review.md`
### Devex
- Top wins: the controls map; the reuse of S1b.
- Top gaps: provability (N10), Avalonia platform gaps (dock badge, tray, LiveSetting), logging, TimeProvider.
- File: `plans/sindri-ui-design/devex-review.md`
### Security
- Top wins: protected stays CLI-only; plain text; ids-only seed; nothing runs from the tray or a stream event.
- Top gaps: N7, N1 (F5), N12, plus 9 LOWs.
- File: `plans/sindri-ui-design/security-review.md`

## Recommended next actions
1. Rewrite §6 launch contract (N1–N4, N7) — the headline is unsafe as written — arch, security.
2. Add the provability recipe and E-edit owner table (N10) — agents can't prove phases otherwise — devex.
3. Split U6 and add the served fields that remove client logic (N8, N11) — product, arch.

## Lens status
| Lens | File | Status |
|---|---|---|
| Product | product-review.md | ✓ (NEEDS_WORK) |
| Architecture | arch-review.md | ✓ (NEEDS_WORK) |
| Design | design-review.md | ✓ (NEEDS_WORK) |
| Devex | devex-review.md | ✓ (NEEDS_WORK) |
| Security | security-review.md | ✓ (NEEDS_WORK) |

## Overall verdict
NEEDS_WORK, driven by the HIGH findings N1–N10; no CRITICAL.
verdict: NEEDS_WORK
