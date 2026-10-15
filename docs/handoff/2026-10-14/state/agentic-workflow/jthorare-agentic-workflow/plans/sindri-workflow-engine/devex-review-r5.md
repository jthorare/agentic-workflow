# Plan Devex Review (pass 5, final) — sindri-workflow-engine

**Plan:** `docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md` (revision 5; scratchpad copy `wf-engine-pr/`)
**Reviewed:** 2026-10-11
**Checked against:** the S1 branch (read only) at `.claude/worktrees/sindri-s1-app-shell` @ `2a31206a`
(`src/Sindri/Aw.Sindri.Host/Aw.Sindri.Host.csproj`, `Directory.Build.props`, `docs/csharp/sindri.md` "The binary",
"Files", "Profile"), and `scripts/install-sindri.sh` on the main checkout.
**Scope:** only (1) earlier devex findings that are still open and (2) new problems in revision 5. Resolved items
and §13 answers that hold up are left out.

## Summary

Revision 5 resolves both pass-4 HIGHs in design:
- the fixed `AW_SINDRI_HOME`, the built binary and an owner-started `serve` give the owner and the Prover one
  server;
- `route: causation` stops the engine's own notification from completing `dev-notify`.

It also fixes most pass-4 mediums: the `script` code (`SND-RECIPE-004`), the P5 instance names, the P7 profile and
`fact:item.spec`, the dev evals, the `"fact:spec"` child key, the notifications path in §6.3, the waves note and the
probe owner steps.

Two things would still make a phase signal fail as written:
- **The dev loop's binary path doesn't exist.** S1 builds `sindri` under `artifacts/`. The `sindri()` function also
  doesn't reach the shell that `script` starts, and an installed TS `sindri` wrapper is on the PATH, so the call can
  silently run the TS sindri.
- **P4 step 3's `--run 2.1` is a run token that was already applied.** It is checks' own run from step 2, so the
  key check returns `already applied`, exit 0, before the nonce check can return `SND-WF-009`.

The rest are mediums and lows in the runbook mechanics.

Time-to-first-success: about 12 min to validate a recipe (P1, agent). About 25 min to a first instance moving (P4),
once the binary path is fixed.

## Findings by severity

### CRITICAL

None.

### HIGH

- **Setup / dev loop (new): the binary the dev loop calls isn't built at that path, and the function doesn't
  survive into the `script` runbook or the Prover's shell.**
  - **The path:** S1 sets `<AssemblyName>sindri</AssemblyName>`. The repo's `Directory.Build.props` sets
    `UseArtifactsOutput=true` with `ArtifactsPath=$(MSBuildThisFileDirectory)artifacts`. The build therefore writes
    `artifacts/bin/Aw.Sindri.Host/debug/sindri`. Nothing is written at `src/Sindri/Aw.Sindri.Host/bin/Debug/net10.0/Aw.Sindri.Host`.
  - **The result:** every owner and agent step in P2, P4, P5, P6 and P7 fails with "no such file", exit 127.
  - **The function:** `script -q <transcript>` starts a new `$SHELL` (zsh for this owner). Shell functions aren't
    inherited, so the comment "a function, so `script` runbooks work" is wrong unless the function is defined
    inside the `script` session. The same goes for the Prover: Claude Code's Bash tool keeps no shell functions
    between calls.
  - **The shadow:** `scripts/install-sindri.sh` installs the TS `~/.local/bin/sindri` wrapper. Without the function,
    `sindri workflow …` runs the TS sindri. The owner gets an unknown-command error or wrong output, not a clear
    "binary missing".
  - **The order:** the dev-loop text runs `sindri profile approve <hash>` before `sindri serve`. S1's
    `profile approve` is an API client and exits 1 with `SND-API-001` while nothing is running ("The binary" table).
  - Evidence: plan lines 529–537; S1 `Aw.Sindri.Host.csproj:10`; `Directory.Build.props:43–44`; S1
    `docs/csharp/sindri.md` "Which verbs are API clients"; `scripts/install-sindri.sh:2,187`.
  - Fix:
    - Use `SINDRI="$(git rev-parse --show-toplevel)/artifacts/bin/Aw.Sindri.Host/debug/sindri"` and call `"$SINDRI"`
      in runbooks and in the Prover's steps. Or tell the runbook to put a `sindri` symlink first on `PATH` inside
      the `script` session.
    - Make the runbook's first step, inside `script`, check `command -v sindri` or `"$SINDRI" --headless`.
    - Order the prelude: `serve` (in its own terminal), then `profile validate`, then `profile approve <hash>`.

- **Phases P4 (pass 4 MEDIUM, now made to fail): step 3's second half replays a token that was already applied, so
  it exits 0 instead of 2 with `SND-WF-009`.**
  - **The tokens:** `entry` rises on each real state entry, and the run token is `{entry}.{n}` (§5.1). Step 2's
    three completes are therefore `--run 1.1` (produce), `2.1` (checks, `passed`) and `3.1` (verify).
  - **The key:** a desk report's trigger key is the run token, and "replaying the same report hits the key and is
    `already applied`" (§5.4). The key check is `Commit` step 2, ahead of the nonce check at step 3 (§5.3).
  - **The result:** `complete T-1/step/1 --run 2.1 --outcome passed --nonce <used>` is exactly step 2's second
    report. It prints `already applied` and exits 0, and the signal expects exit 2 with `SND-WF-009`.
  - Evidence: plan lines 315, 328, 361–366, 390, 1258–1264.
  - Fix: use a token that was never applied, such as `--run 4.1`. Its key misses, and the used nonce fails at
    step 3 with `SND-WF-009`. Or state that a desk report's key includes the nonce, but then the first half of
    step 3 (the exact replay with a newly fetched nonce) would no longer print `already applied`.

### MEDIUM

- **Setup (new): parallel phases and the owner's own Sindri share port 7410.**
  - S1's control plane listens on `api.port` (default 7410, or `AW_SINDRI_PORT`), and a taken port is
    `SND-API-003`, exit 2.
  - Wave 4 runs P5 and P6 at the same time, each "left running for the whole phase" under its own home. The second
    `serve` fails, and so does an owner's real S1 `sindri` if one is running.
  - Evidence: plan lines 530, 536–537, 1143–1149; S1 `docs/csharp/sindri.md` (profile `api.port`, `SND-API-003`).
  - Fix: the dev loop sets `export AW_SINDRI_PORT=0` (a free port, which clients find through `control.json`), or
    one port per phase (`74<N>0`).

- **Setup (new): the fixed per-phase home is reused across runbook retries.**
  - `/tmp/sindri-dev-p<N>` survives a failed run. On a retry:
    - `workflow start … --item T-1` returns the existing instance (§6.1), so P4 step 1's `--run 1.1` and P5's
      timing don't hold;
    - recipes that are already approved change P2's first preview;
    - P6's revoked `dev-notify` can't be approved again (`SND-RECIPE-005`).
  - Evidence: plan lines 523, 530; §4.1 (revoked hash).
  - Fix: the runbook prelude runs `sindri stop; rm -rf "$AW_SINDRI_HOME"` first. "Proven" means one clean run from
    an empty home.

- **Setup (pass 4 HIGH, the sandbox half still open): the loopback fallback covers only read verbs, but the
  Prover's (agent) steps are writes.**
  - The P2 (agent) `recipe approve` and the P4 step 4 (agent) `complete` must reach the server to be refused with
    `SND-WF-007`.
  - If the sandbox blocks loopback, they exit 1 with `SND-API-001`. The fallback, "the owner approves running the
    Prover's read verbs outside the sandbox", doesn't cover them.
  - Evidence: plan lines 538–539, 1216–1218, 1265–1266.
  - Fix: "the Prover's steps", not "read verbs". Also say that running outside the sandbox keeps the harness
    markers, so the session class (and `SND-WF-007`) still holds.

- **API surface / P7 (new): whether the profile allowlist applies to engine-origin notifications is undefined, and
  P7's profile lists only `effect.claim.ref`.**
  - §5.4 exempts `origin: engine` from the **approval** check only, and the dispatcher re-checks "approval and the
    allowlist" (P6 goal).
  - If the allowlist applies to engine-origin notifications, then with `effects: [effect.claim.ref]`:
    - the T-3 child's first-task notice and the T-7 park notice are refused with `SND-WF-011`;
    - `outbox.refused` then parks the instance `spec-conflict` "whatever frame it is in now";
    - the P7 signal fails.
  - Evidence: plan lines 408–418, 1288–1289, 1323–1324.
  - Fix: either "engine-origin notifications skip the allowlist as well", or the P7 signal names
    `effects: [effect.claim.ref, effect.notify.desk]`. Add a test for whichever is chosen.

### LOW

- **Docs (pass 4, half done):** §6.3 now says `$AW_SINDRI_HOME/notifications.jsonl`, but the §13 pass-3 written
  answer still says `$AW_STATE_DIR/sindri/notifications.jsonl`. Evidence: lines 589, 1088. Fix: correct the §13
  line.
- **Phases (pass 4, half done):** §13 gives the session cap (3 implementers, 2 building), but Phases still says "The
  session cap is respected." Evidence: lines 1105, 1179. Fix: put the number in Phases.
- **Phases P5:** the signal expects `paused timeout 0m 40s left`, but §6.4's duration format and the golden layout
  are `40s left`. Evidence: lines 641, 652–653, 1282. Fix: `paused timeout 40s left`.
- **Phases P2:** §6.2 and the P2 contract unit say the Codex and Cursor probes are owner steps in P2's runbook, but
  the P2 Signal doesn't list them, and P2(e) needs their output before it can finish. Evidence: lines 559–561, 1155,
  1214–1222. Fix: add a numbered owner step to the P2 signal that captures each harness to `$STAGE_DIR/probe/<harness>.txt`.
  Say that P2(e) waits on it.
- **Phases P7:** `--item T-3` and `item.json`'s `id` both name the item, and the doc doesn't say what happens when
  they differ. Evidence: lines 873, 1326. Fix: refuse a mismatch with `SND-WF-008`, or drop `id` from `fact:item`.

## Per-dimension summary

| Dimension | Worst severity | Findings |
|---|---|---|
| API surface ergonomics | MEDIUM | 2 |
| Setup / install (time-to-first-success: ~12 min P1, ~25 min P4) | HIGH | 4 |
| Error messages & observability | None | 0 |
| Docs completeness | LOW | 1 |
| Backwards compatibility | None | 0 |
| Ramp-up cost (tribal-knowledge items: 6) | LOW | 0 |
| Phases (signals, runbooks) | HIGH | 4 |

**Time-to-first-success assumptions:**
- **P1, about 12 min:** a worktree off S1, a locked restore, a 3–5 min build, then `recipe validate` in-process.
- **P4, about 25 min:**
  1. Build once.
  2. Start `serve`.
  3. Approve the profile and the recipe, with the evals run.
  4. `start`, then three `complete` calls at about 1 s each on the built binary.
  5. The Prover replays the checks on the same home.

**Tribal-knowledge list:**
1. S1 builds into `artifacts/bin/Aw.Sindri.Host/debug/sindri`, not `bin/Debug/net10.0`.
2. A TS `sindri` wrapper is on the PATH, and shell functions don't reach `script` or the Prover's Bash calls.
3. `profile approve` needs `serve` running.
4. Every dev `serve` binds 7410 unless `AW_SINDRI_PORT` is set.
5. The fixed dev home survives retries and has to be wiped.
6. A desk report's key is its run token, so any past token replays as `already applied`.

## Verdict

Two HIGH findings would fail phase signals as written: the dev loop's binary path and function don't reach the built
`sindri` (P2–P7), and P4 step 3 replays an applied token (`--run 2.1`), so it gets `already applied` instead of
`SND-WF-009`. Each is a one- or two-line fix. The mediums cover the port, home reuse, the sandbox write steps and the
engine-notification allowlist.

verdict: NEEDS_WORK
