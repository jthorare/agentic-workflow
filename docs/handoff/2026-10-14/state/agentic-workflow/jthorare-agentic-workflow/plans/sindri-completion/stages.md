# Epic: Sindri completion (evolve privacy B1, B2, Plan 6 dashboard)

Specs:
- docs/superpowers/specs/2026-10-09-sindri-evolve-privacy-design.md (B1, B2)
- docs/superpowers/specs/2026-10-09-sindri-dashboard-design.md (Plan 6)

## Contracts

- **Autonomy:** run continuously (user, 2026-10-09). Stack stages: when a stage is ready, start the next
  one while its PR waits for the human merge. B2 branches off B1's branch and rebases onto main after B1 merges.
- **Git topology:** base `main`. Push remote `origin` = jthorare/agentic-workflow (the user moved repo
  ownership there on 2026-10-09; remote URL updated; gh account `jthorare` has ADMIN). Never push `main`. One branch per stage.
- **State dir:** repo slug is now `jthorare-agentic-workflow`; epic state copied from `joi-fairshare-agentic-workflow`.
- **Order:** B1 → B2 → Plan 6 (user). Plan 5 Task 14 (switch-on) is a human step after B1 and B2 merge.
- **Baseline:** sindri at 75b518c has 1149 passing tests, 2 skipped, 87 files; typecheck clean.

## Stages

| # | Stage | Branch | Plan | Status |
|---|---|---|---|---|
| 1 | Evolve privacy B1: hardened publish gate | feat/sindri-evolve-privacy-b1 | plans/2026-10-09-sindri-evolve-privacy-b1-publish-gate.md (11 tasks) | **READY — awaiting human merge.** PR #85 ready at 63f6161 (2026-10-10). Proof PASS (verification/20261010-061358-…); tests 1348/100%; review loop cap 5 reached (25→20→8→6→6), run-5 findings fixed, zero gate waived by user decision |
| 2 | Evolve privacy B2: all sessions, all providers | feat/sindri-evolve-privacy-b2 | plans/2026-10-09-sindri-evolve-privacy-b2-all-sessions.md (6 tasks) | pending |
| 3 | Plan 6: dashboard (fleet, aw:fleet hook, dashboard, attach, badge) | feat/sindri-plan-6-dashboard | plans/2026-10-09-sindri-plan-6-dashboard.md (13 tasks) | pending |

### Stage 1: B1
- **Goal:** nothing workplace-specific reaches the public plan file through `sindri evolve publish`.
- **Observable signal:** with a synthetic private repo (`Globex`, `quarbleScheduler`, `GLX-1234`) indexed and
  `private: true`, real `sindri evolve stage` + `publish --dry-run` + `publish` hold leaking proposals with
  layer-only reasons (no matched text), and the plan file contains none of the synthetic terms; a clean
  proposal publishes; missing index → SND-EVOLVE-017; missing word list → SND-EVOLVE-018; `sindri doctor` warns per §8.
- **Files:** sindri/src/evolve/** (privacy, generalize, stage, publish, refs), sindri/src/ledger (v5),
  sindri/src/profile (denyPatterns, private), sindri/src/doctor, sindri/src/errors.ts, docs/sindri/*.
- **Size:** L (11 tasks, ~1.5k plan lines).

### Stage 2: B2
- **Goal:** evolve reads every Claude/Codex/Cursor session in any repo, attributed to a repo.
- **Observable signal:** real `sindri evolve telemetry/correct --since` count lines from all three providers'
  fixture stores; `sindri evolve reflect --repo <other> --pr N` keys `reflect:<repo>:pr-N`; published tasks
  from other repos render "reflect on another repo's PR" with `pr:` refs withheld.
- **Files:** sindri/src/evolve/sources/**, reflect, weekly, render; profile sources keys; docs.
- **Size:** M (6 tasks).

### Stage 3: Plan 6
- **Goal:** one place to see every agent on the machine and what needs the builder.
- **Observable signal:** `sindri fleet` lists live sessions across providers with correct states; the
  `aw:fleet` hook spool flips a session to approval/input; `sindri dashboard` serves five views on loopback
  with the attention signal; terminal-gated actions only shown; attach never resumes a live session; SwiftBar badge count.
- **Files:** sindri/src/fleet/**, sindri/src/dashboard/**, sindri/ui/**, config/hooks/fleet.sh,
  config/swiftbar/, scripts/install-sindri.sh, docs/sindri/dashboard.md.
- **Size:** XL (13 tasks, ~8.6k plan lines). Has a web UI → `/verify-web` + `/ui-evidence` apply.
