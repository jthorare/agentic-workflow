# Consolidated Plan Review, pass 2: sindri-ui-design (revision 2)

**Plan:** docs/superpowers/specs/2026-10-11-sindri-ui-design.md
**Reviewed:** 2026-10-11 · lenses: product · arch · design · devex · security · checked against the API and WF specs, the S1b deps.yaml and the mockup files

## Pass-1 HIGH/MEDIUM check

| Pass-1 | Status in revision 2 | Evidence |
|---|---|---|
| N1 launcher delivery, dedupe, freshness, crash | **Partly fixed** (P2-1) | Cursor, fact keys, 007 on start-up and 008 are in E-1. Nothing expires a request that stays `requested` while the service runs |
| N2 open → takeover | Fixed (wording LOW P2-6) | E-6 `open-request` in `Commit`, `service:engine`, the launcher waits |
| N3 attach vs resume liveness | Fixed | §6.2: Attach only focuses; Resume only after a handback |
| N4 stopping a running worker | Fixed in §6.2; **X20 row still old** (P2-4) | §6.2 defers it; §13 X20 still says "stops its worker (E-6)" |
| N5 nonce, WF codes | Fixed, except `StartInstance` (P2-3) | §9.1 |
| N6 variants, `allowedCommands` shape | Fixed | E-12, E-13, §7 |
| N7 `AW_SINDRI_CHILD` | Fixed | §6.3 step 2; API §6 lists the variable |
| N8 split U6 | **Partly fixed** (P2-2) | U6a/U6b exist, but U6a holds Attach and Resume, which can't happen before the engine |
| N9 mockups | Files exist (11 + PNGs); **out of date** (P2-5) | — |
| N10 provability | Fixed | §18.1 owner table, §18.2 recipe, E-17 goldens (same direction as the existing `SindriUi -> Sindri` file edge, so no new cycle) |
| N11 logic leaks | Fixed | E-7 `label`, E-8, E-14, E-15 |
| N12 quoting, guard, CLI chars, key guard | Fixed | §6.3, E-16, §7 backstop, §8 key guard |
| N13 contrast and tokens | Fixed | Recomputed: white on `#535fc6` 5.5, on `#4c57be` 6.15, `#eb5757` on canvas 6.0 |
| N14 lifecycle and cleared states | Fixed | §4.1, §4.4 mirrored child, §6.6 15 s / 30 s, §3.2 caps |

## Verdict per lens

| Lens | Verdict | Why |
|---|---|---|
| Product | PASS (1 MEDIUM) | The headline ships early. Attach and Resume are mis-phased (P2-2) |
| Architecture | NEEDS_WORK | P2-1 HIGH; P2-3 and P2-4 MEDIUM |
| Design | PASS (1 MEDIUM) | Mockups lag the text (P2-5); the rest are LOWs |
| Devex | PASS | The recipe is runnable; only LOWs (P2-8) |
| Security | NEEDS_WORK | P2-1: a launch can fire long after the click, which breaks "never without a fresh click" |

## Unresolved or new findings

| id | severity | lens | section | finding | fix |
|---|---|---|---|---|---|
| P2-1 | HIGH | arch, security | §6.7 E-1, E-6, §6.6 | Nothing writes the "expires at 30 s" outcome that §6.6 promises. E-1 writes `SND-LAUNCH-007` only on start-up. A request the running service skips (> 30 s, a lagging cursor, a stalled engine) stays `requested` forever, and `SND-LAUNCH-008` then blocks every re-open of that item until Sindri restarts. Freshness is checked only when the service picks the request up, so a launch that is waiting on E-6's takeover can open Warp minutes after the click | The launcher service appends `launch.failed` (`SND-LAUNCH-007`, or a new `-009` "expired") for any request with no outcome at 30 s, on an injected-`TimeProvider` timer. It checks freshness again just before `ILauncher.Launch`, and bounds the E-6 wait by the same deadline. `SND-LAUNCH-008` counts only requests that are fresh and in `requested`. Add an integration test where a stalled engine expires the request and a re-open is then accepted |
| P2-2 | MEDIUM | product, arch, devex | §6.2, §18 U6a, §17 | Attach needs the item "with you" (`withYou` = `taken-over`, API §3.6), and Resume needs a handback (`ApplyCommand:handback`, WF-P5). Both states exist only once the engine runs, yet both are put in U6a, and U6a's observable signal proves neither. The pass-1 fix (N8) put them in U6b | Move Attach and Resume to U6b and add both to its signal. The other option is to define a pre-engine precondition (for example, Attach when the item's last `launch.started` has no later handback or launch) and prove it in U6a. Also state how Attach brings Warp forward (activate the app by bundle id), since §6.3 says tab focus is undocumented |
| P2-3 | MEDIUM | arch | §9.1 | `StartInstance` is listed as taking a nonce from `POST /workflow/instances/{a}/nonce`, but it has no instance address yet. WF §5.4's command nonces are per instance and version | Remove `StartInstance` from the nonce list and name its idempotency key (item plus attempt, per WF `workflow start`), or raise it as a cross-spec row |
| P2-4 | MEDIUM | arch | §13 X18, X20 | Stale rows that will be copied into the README. X20 still says "Takeover of a running Step stops its worker at the next safe point … (E-6)", which contradicts §6.2 and §17. X18 says "the router turns `item.open-requested` into a `takeover` trigger", but E-6 is now an `open-request` trigger applied in `Commit` | X18: use E-6's wording. X20: "deferred to the engine phase that adds a headless worker runtime; v0 Take over = `pause` only; no worker resume until then" |
| P2-5 | MEDIUM | design | §16, mockups/ | The mockups predate revision 2. None shows Mark all seen, the "Show all N" cap, the `Error` token or the `#535fc6` hover, and `launch-flow.html` says "Sindri stops the worker first", which contradicts §6.2. §19's design reviewer checks each phase against these files | Regenerate `home`, `needs-you` and `launch-flow` (at least), or add a "known deltas (text wins)" list to §16 that the design reviewer reads |
| P2-6 | LOW | arch | E-6 | "Exempt from the nonce and approval checks like `takeover`": WF §5.3 exempts `takeover` from approval only (a takeover command needs a nonce). `Commit`'s actor step also needs WF §6.2 to let `service:engine` fire `open-request` | Say "not a command, so no nonce; exempt from approval like `takeover`", and add the actor row to E-6 |
| P2-7 | LOW | design | §3.2, E-14 | The pill's word list leaves out "Rebuilding", which E-14 serves. "Stale" and "Not running" are client connection states and should be labelled as such | Add "Rebuilding"; note which words come from `health.word` and which from §4.1 |
| P2-8 | LOW | devex | §6.7 intro | The intro says "E-12…E-16 are UI-driven edits elsewhere", but E-17 (goldens) is defined only in §18.1 | List E-17 in §6.7, or point to §18.1 |
| P2-9 | LOW | design | §8, E-8 | `O`, `A`, `T`, `B` and `Y` are fixed keys, while `X` comes from the catalog's `shortcut`, so a catalog shortcut could take a fixed key | Reserve the fixed keys. A catalog test rejects a `shortcut` that uses one |
| P2-10 | LOW | design | §7, §14.1 | "The only engine-shaped controls the app shows are none" is garbled. The `primary-focus` row (`#5e69d1` → `#4c57be`, renamed `PrimaryPressed`) reads as two tokens | Reword: "Before S2 the app shows no engine controls". Split the row into DESIGN.md's token (unused) and the derived `PrimaryPressed` |

## Overall
One HIGH, a residual of pass-1 N1: launch requests that never expire or that fire late. Four MEDIUMs: Attach and Resume
in the wrong phase, the `StartInstance` nonce, stale X18 and X20 rows, and stale mockups. No CRITICAL.

verdict: NEEDS_WORK
