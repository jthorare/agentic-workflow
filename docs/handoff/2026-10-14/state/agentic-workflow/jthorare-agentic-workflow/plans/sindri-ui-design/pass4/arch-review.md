# Architecture Review (pass 4, slim): Sindri UI app design, revision 4

_Reviewed by `/archReview --slim` on 2026-10-11. Focus: §6.7 E-1…E-18, §18, §9, consistency with the API surface and workflow engine specs._

## Verdict: SOUND

No CRITICAL or HIGH findings. Pass-3 fixes hold: one outcome key `launch.outcome:<launchId>` with a race test (A3-1, E-1); the instance input on `LaunchOptions.For` (A3-2, E-2); Resume takes over, and the takeover-with-no-session row is described (A3-3, §6.2, §6.4); E-6 uses one id and key (A3-4); the fold text is fixed (A3-5). The new findings below are three MEDIUM gaps in the launch and takeover handshake and four LOW consistency items.

## Scores

| Dimension | Score | Notes |
|---|:---:|---|
| Complexity | 4 | The launcher, expiry timer and engine wait form one small state machine. Its claim step and no-op takeover are under-specified (A4-1, A4-2) |
| Scalability | 4 | Re-query with a 250 ms debounce, capped panels, 429 → 5 s polling. Single-user loopback |
| Maintainability | 4 | Logic stays on the server (E-2, E-7, E-13, E-14, E-15), goldens per scenario (E-17), and each edit has an owner (§18.1). One engine edit has no number (A4-3) |

## Prior HIGH/MEDIUM fixes

| Item | Holds | Where |
|---|---|---|
| P2-1 expiry, wait inside the deadline, re-check before launch | Yes | E-1, §6.6 |
| A3-1 two outcome writers | Yes, but the claim isn't defined (A4-1) | E-1 |
| N2 an instance started during a launch | The rule is stated, but it has no edit number and no freshness rule (A4-3) | §6.4 |

## Edge-case matrix

| Boundary | Dependency unavailable | Invalid input | Load 10x | State leakage |
|---|---|---|---|---|
| App → API (§9) | OK: §4.1 states, 30 s stale, 2 s look-again, 429 → 5 s | OK: plain text (U9), E-16 plus a control/bidi backstop | OK: per-view debounce, caps, `--large` fixture | OK: focus kept by id, key guard |
| `OpenItem` → launcher service (E-1) | OK: start-up sweep, 30 s expiry | OK: id patterns, YAML serializer, argv quoting | OK: `-008`, one request per item | **A4-1** |
| Launcher → engine (E-6) | OK: the wait is bounded by the deadline | OK: trigger key `wf:{i}:open:{launchId}` | OK: 5 s router cadence inside 30 s | **A4-2** |
| Engine `StartInstance` ↔ launches (§6.4) | **A4-3** | OK | OK | **A4-3** |
| UI → engine commands (§9.1 nonce) | **A4-5** | OK: `SND-WF-002/009` treated as a 409 | OK | OK: nonce fetched at click time |
| Catalog variants (E-12) | OK | **A4-6** | OK | OK |
| Launcher → Warp/CLI | OK: probe 002/003/004, `exit 127` guard | OK: ids only in the seed, path only in `cwd` | OK: probe appends only on change | **A4-7** |

## Findings

| id | Severity | Section | Finding | Fix |
|---|---|---|---|---|
| A4-1 | MEDIUM | §6.7 E-1, §6.6 | "The launcher claims a request before calling `ILauncher.Launch`" names no mechanism, and "all three events" leaves no claim event. If the claim is in memory (the launcher and the timer share one hosted service), the timer text still expires "every request with no outcome past its deadline", claimed ones included. An expiry that wins while `Launch` is still running then records `SND-LAUNCH-007` even though Warp opens. The test asserts "exactly one outcome" but not which one. `ILauncher.Launch` also has no time bound, so §6.6's "Warp never opens late" depends on `open` returning quickly | Define the claim as in-process (one service owns both paths) and have the timer skip claimed requests. Bound `ILauncher.Launch` (for example 5 s, ending at the deadline) and record its timeout as `-001`. State the expected outcome in the race test (`launch.started` when the claim came before the deadline) |
| A4-2 | MEDIUM | §6.2 Attach, §6.4, E-6 | Once an instance exists, the launcher opens Warp only "after the engine has recorded that takeover (matched by causation)". But E-6 makes `open-request` a no-op when the instance is already taken over, which is every Attach and every second Open. A no-op on a fresh key appends no event, so the launcher waits until the 30 s expiry and every Attach fails with `SND-LAUNCH-007` | E-6: a no-op `open-request` still records its result under its trigger key (W12), as `workflow.open-acknowledged {launchId, alreadyTakenOver}` or the recorded command result. The launcher matches on that result (applied or no-op), not only on a takeover event. Add a U6b test: Attach on a with-you item gives `launch.started` within the router cadence |
| A4-3 | MEDIUM | §6.4, §6.7, §18.1 | "An instance started while a launch is open starts taken over (the engine reads the item's open launch in `StartInstance`'s `Commit`)" is a WF edit with no E-number and no row in §18.1. It reads the API's `launches` projection inside the engine's transaction, and that projection can lag: a request appended just before `StartInstance` may not be projected yet. The launcher sees no instance and opens at once, so automation runs beside the session. The spec also doesn't say what happens to an `item.open-requested` event routed after the instance exists | Number it (E-19, a WF §6.1/§5.4 edit landing in WF P5, needed by U6b). Read the request from the ledger, or wait for the projection to reach the head, instead of reading a possibly stale view. Let the router apply `open-request` to an instance created after the request while that launch has no outcome |
| A4-4 | LOW | §6.7 | The table runs E-1…E-16 with E-18 placed between E-15 and E-16. E-17 lives only in §18.1, and the intro ("E-12…E-16 elsewhere; E-17 in §18.1") doesn't mention E-18 | Order the rows; give E-17 a stub row pointing to §18.1; mention E-18 in the intro (an API P3 edit) |
| A4-5 | LOW | §9.1, §18 U5 | U5's engine commands need the nonce POST, whose class is still open as X23. U5's "Depends on" doesn't list X23 | Add X23 to U5's dependencies (and to §18.1 under WF P4) |
| A4-6 | LOW | E-12 vs API §2.1 | API §2.1 says `grant` and `accept` are invalid on any variant but `answer`, and API §3.4 uses `ApplyCommand:answer` as its example id. E-12 splits `answer` into five variants and doesn't restate the field rule | E-12: `grant` is valid only on `answer-grant` and `accept` only on `answer-accept`. Either field on another variant is refused, and the request is still classified `protected`. Update §3.4's example id |
| A4-7 | LOW | §6.6 `-007`, E-1 | A crash after `ILauncher.Launch` but before the outcome is appended is swept as `-007` "interrupted", though a Warp tab may be open. The fix "Open again" can then start a second session | The `-007` text says "a Warp tab may have opened; look for `sindri ITM-…` before opening again" |

verdict: PASS
