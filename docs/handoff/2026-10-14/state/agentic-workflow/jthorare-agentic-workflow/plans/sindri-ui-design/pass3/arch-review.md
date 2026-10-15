# Architecture Review (pass 3, slim): Sindri UI app design, revision 3

_Reviewed by `/archReview --slim` on 2026-10-11. Checked against the API surface and WF engine specs. They are byte-identical to `docs/sindri-api-surface` @ e056149f, so the E-edits are still proposals to those specs, which is the intent._

## Verdict: SOUND

No CRITICAL or HIGH findings. Every pass-2 HIGH and MEDIUM holds in the text. One new MEDIUM (A3-1, a race between the expiry timer and the launch) and five LOWs remain. Scores are all ≥ 3.

## Pass-2 fixes verified

| Pass-2 | Holds? | Evidence |
|---|---|---|
| P2-1 HIGH (expiry, late launch) | Yes, with a new race (A3-1) | E-1: a 5 s timer appends `SND-LAUNCH-007` past the 30 s deadline and also runs at start-up. The E-6 wait sits inside the deadline. Freshness is re-checked right before `ILauncher.Launch`. `-008` counts only requests still inside their deadline. §6.6 row "The launcher never answered" |
| P2-2 Attach/Resume phase | Yes | §6.2 Phase column says U6b. U6a is Open only. U6b's signal covers Attach (no process) and Resume (same session id) |
| P2-3 `StartInstance` nonce | Yes | §9.1 matches WF §6.1: an existing `(recipe, item, attempt)` returns that instance |
| P2-4 X18/X20 | Yes | §13: X18 is the `open-request` trigger in `Commit`. X20 is pause-only until a later worker runtime |
| P2-6 E-6 wording | Yes | "a trigger, not a command, so it takes no nonce; exempt from approval like `takeover`" matches WF §5.3 steps 3 and 5 |
| P2-8 E-17 pointer | Yes | The §6.7 intro points to §18.1 |
| P2-9 reserved keys | Yes | §8 bullet 2 |

## Scores

| Dimension | Score | Notes |
|---|:---:|---|
| Complexity | 4 | The launcher service, timer and engine wait form one small state machine. A3-1 is its only gap |
| Scalability | 4 | Re-query with a 250 ms debounce, capped panels, a stream fallback to 5 s polling (API §5.4). Single-user loopback |
| Maintainability | 4 | Server-owned logic (E-2, E-7, E-14, E-15), goldens (E-17), and an owner per edit (§18.1) |

## Edge-case matrix

| Boundary | Dependency unavailable | Invalid input | Load 10x | State leakage |
|---|---|---|---|---|
| App → API (§9) | OK: §4.1 states, 30 s stale, 2 s look-again, 429 → 5 s | OK: plain text (U9), E-16 plus a control/bidi backstop | OK: per-view debounce, caps, `--large` fixture | OK: focus kept by id, key guard, hover freeze |
| `OpenItem` → launcher service (E-1) | OK: start-up sweep, 30 s expiry | OK: ids pattern-checked, YAML serializer, argv quoting (§6.3) | OK: one request per item (`-008`) | **A3-1** |
| Launcher → engine (E-6) | OK: wait bounded by the deadline; a refusal is recorded as `launch.failed` | **A3-4** | OK: router cadence 5 s, inside 30 s | **A3-3** |
| Launcher → Warp/CLI (`ILauncher`) | OK: probe codes 002/003/004, `exit 127` guard | OK: path only in `cwd`, seed is ids only | OK: probe appends only on change | OK: file deleted after 60 s, start-up sweep |
| `ItemView.launch` (E-2) | OK: `blockers` | OK | OK | **A3-2** |
| Home layout (§3.2) | OK | OK | **A3-5** | OK |

## Findings

| id | Severity | Section | Finding | Fix |
|---|---|---|---|---|
| A3-1 | MEDIUM | §6.7 E-1 | Two outcome events, two fact keys (`launch.started:<id>` and `launch.failed:<id>`), written by two writers: the launch path and the 5 s expiry timer. The launcher can pass its freshness re-check at about 29.9 s and call `ILauncher.Launch`, which spends time on `open`. Meanwhile the timer appends `launch.failed SND-LAUNCH-007` at 30 s, and then `launch.started` lands too. The ledger then holds both outcomes. `LaunchView` may show "failed" while Warp opened, and the engineer clicks Open again and gets a second session. The same happens if the timer fires during the E-6 wait just as the takeover lands | Give both outcomes one key, `launch.outcome:<launchId>`, so the first append wins and the loser is a no-op. Alternatively, claim the request (`launch.claimed`, same key family) before calling `Launch`, and have the timer expire only unclaimed requests. Add an integration test with `FakeTimeProvider` where the timer fires during `Launch` and the ledger ends with exactly one outcome |
| A3-2 | LOW | §6.7 E-2 | `LaunchOptions.For(item, launches, probe, profile)` has no instance or takeover input, yet Attach needs "with you" and Resume needs "handed back since", both engine facts (X7 projection) | Add the engine's item projection (or the instance's frame state) to the inputs, and note the U6b dependency |
| A3-3 | LOW | §6.4, §6.6 | When the engine records the takeover but the launch then fails or expires (001, 007), the item stays taken over with no session. That is safe, but no section says it. Nor does §6.4 say that `OpenItem:resume` also goes through `open-request`, and it must: Resume follows a handback, so automation is running again by then | State both cases. Resume appends `item.open-requested` and takes over like Open. A failed launch after the takeover leaves the item With you, with Hand back and Open offered |
| A3-4 | LOW | §6.7 E-6 | E-6 says "id the source sequence" but "key `wf:{instance}:open:{launchId}`", which mixes two identities (W12 keys by trigger). E-6 also adds an actor row `service:engine`, but WF §6.2's `system` class already covers engine services. And E-6 says nothing about a live instance whose state forbids takeover | Pick one id: the `launchId` (from the fact key) is the natural choice. Say `system` (stamped `service:engine`). A non-terminal state that forbids takeover fails the launch with the engine's code, as for a terminal one |
| A3-5 | LOW | §3.2 | The "Needs-you cap" bullet still says Running, Queued and Health stay above the fold "with up to 6 rows". The corrected fold rule says at most 5 rows and lets Home's health strip sit below the fold | Reword it to match the fold rule (5 rows; health is shown in the pill) |

## Top risks

| # | Risk | Impact | Likelihood | Mitigation |
|---|---|---|---|---|
| 1 | Contradictory launch outcomes lead to a second session (A3-1) | med | low | A single outcome key or a claim |
| 2 | Paused instance with no session after a late launch failure (A3-3) | low | low | Document it; Hand back is always visible |

verdict: PASS
