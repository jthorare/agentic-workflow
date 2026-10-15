# Sindri UI lane — stages

Spec: docs/superpowers/specs/2026-10-11-sindri-ui-design.md (rev 6, owner-approved), §18 phases, §19 team.
Lead: this session (orchestrates only; never writes phase code, §19).

## Contracts
- **Autonomy:** run continuously; each phase stops at "PR ready" for the owner's merge (never self-merge). A phase
  starts the moment its API/engine/context dependencies have merged (README wave table). Spec changes → stop and ask.
- **Git topology:** remote `origin` (jthorare/agentic-workflow). Each phase is its own branch and PR, stacked:
  U0 on `feat/sindri-s1b-ui-shell` (#168, itself on S1 #163). Later phases stack on the previous U phase until it
  merges. Never push main or another lane's branch. #168 was brought up to S1's tip by merge (3e5eb8a2), not a
  rebase: a linear rebase replays 50 commits across four earlier S1 merges and conflicts at each; the merge is a
  fast-forward of #168 with no force-push.
- **Proof:** `scripts/sindri-prove.sh` arrives with API P1. U0 has no API phase; its prover uses the same guard by hand
  (a mktemp AW_SINDRI_HOME, port 0, refuse the default home) and re-runs under sindri-prove.sh once P1 merges.
- CI can't run: the AGENTS.md merge gate is run locally and pasted in each PR. Builds go through the `heavy` wrapper.

## Stages
| # | Goal | Depends on | Branch | Status |
|---|---|---|---|---|
| U0 | Theme (§14), shell (§3.2), pre-API states (§4.1), single instance, close-to-tray, IMotionPreference + switch, LiveSetting spike, X47 DESIGN.md fix | S1 (#163), S1b (#168) | feat/sindri-ui-u0 (#184) | review loop (run 2 next) |
| U1 | Catalog, headers, freshness, palette shell, fixture script | U0 + API P1, P2 | feat/sindri-ui-u1 (stacked on #184 + #181; P2 spec-shaped fixtures) | building (orchestrator go 2026-10-12) |
| U2 | Queue, item detail, timeline, Create/Close | U1 + API P3 | — | waiting |
| U4 | Services, jobs, Health | U1 + API P6 | — | waiting |
| U3 | Needs you, decisions, Home, pulse, badges, tray menu | U2 + API P4 | — | waiting |
| U6a | Open in Warp | U2 + API P10a | — | waiting |
| U7 | Context tab, map review | U2 + context S-4 + X9 | — | waiting |
| U5 | Engine surfaces | U3 + engine P4, P5 + API P11 | — | waiting |
| U6b | Opening is a takeover; Attach, Resume | U5 + U6a + API P10b | — | waiting |
