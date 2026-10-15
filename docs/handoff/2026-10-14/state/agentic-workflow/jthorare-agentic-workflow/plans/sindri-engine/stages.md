# Sindri engine lane — stages

Spec: `docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md` (rev 7), amended by the UI spec's E-6, E-9,
E-13, E-19 (`2026-10-11-sindri-ui-design.md` §6.7, §18.1). Cross-spec order: `docs/sindri/README.md` (waves, X1–X52).
Specs win over #166/#167/#169 (X17). Built-in recipes never ship approved.

## Contracts

- **Autonomy:** continuous. The lane runs phase to phase as dependencies merge; every PR stops at ready for the
  owner's merge (never self-merge). A phase that needs a spec change stops and asks the owner.
- **Git topology:** base `main`; remote `origin` (jthorare/agentic-workflow). P1 and P3 stack on
  `feat/sindri-s1-app-shell` (#163) and add files only; P2+ target `main` after S1 merges. Never push `main` or S1's
  branch. Worktrees only under `.claude/worktrees/sindri-wf-p<N>[-<unit>]`.
- **Merge gate:** CI can't run (billing); the AGENTS.md merge gate runs locally and its output goes in each PR.
- **Heavy builds:** every `dotnet build`/`test` through `~/.agentic-workflow/bin-r1b/heavy` (2 slots box-wide).
- **Proofs:** `scripts/sindri-prove.sh` (API P1 builds it; not on any branch yet). P1/P3 are `local` verbs only; until
  the script exists the Prover runs the built Host binary with a temp `AW_SINDRI_HOME` and re-runs through
  `sindri-prove.sh` before the PR goes ready (owner told).

## Stages

| Stage | Branch | Goal | Signal | Needs | Size | Status |
|---|---|---|---|---|---|---|
| P4a | feat/sindri-wf-p0-temporal-spike | Temporal foundation (rev 8.1; was the P0 spike, passed 1e586964) | the five Temporal acceptance checks | P3 (merged) | L | MERGED #188 (e4452c63) |
| P1 | feat/sindri-wf-p1-recipe-model | Recipes parse, normalize, hash, validate in-process | `recipe validate` RCP-REACH-001 line + exit 1; item/step exit 0 with hashes | — | L | MERGED #179 (6e3f25f5) |
| P3 | feat/sindri-wf-p3-pure-engine (on P1) | Pure engine decisions + recipe evals | `recipe eval workflow.step.yaml` happy/no-progress/rounds pass; edited expect exits 1 naming line; `--scaffold` | P1 contract unit pushed | L | MERGED #180 (739ca33e) |
| P2 | feat/sindri-wf-p2-registry | Registry + approval, challenge, pins, cascade, probe | owner approve preview+challenge; agent SND-AUTH-002 | S1, API P1 merged; P1 | M | MERGED #191 (cfd8a172) |
| P2b | feat/sindri-wf-p2b-recipe-rev8 | UI rev 8 E-21 urgency, E-22 outputs/inputs[].kind, 7 RCP rules, hash inputs; dev recipes triage-three-state + desk-round-trip | validate dev recipes; 7 invalid fixtures; hash statement | main (P2 merged) | S | lead running |
| P4 | feat/sindri-wf-p4-interpreter | Store, start, desk Steps, inbox; §6.3 route table; E-9, E-13 | start/status/complete/events, replay, SND-WF-009, agent SND-AUTH-002 | P2, P3, API P3 | L | PROVEN #192 ea603e9a; review loop |
| P4b | feat/sindri-wf-p4b-status-layout (on P4) | UI rev 8 E-20/E-23 engine side: structured status record, list-filter search attrs, fact producer+time | status shows all lines; list filters; temporal list --query | P4, P2b (urgency) | M | lead running |
| P5 | feat/sindri-wf-p5-timers | Timers, takeover, handback; `open-request` (E-6), E-19 | dev-timeout parks; takeover pauses; resume | P4 | M | DRAFT #209; review loop |
| P6 | feat/sindri-wf-p6-effects | Effects via outbox, dispatcher, notify.desk | dev-notify dispatched; revoke → refused, spec-conflict park | P4 | M | lead integrating + proving |
| P7 | feat/sindri-wf-p7-children | Children + workflow.item end to end | item → claim ref → child done; no-progress grant forwarded | P5, P6 | L | waiting |
| P7b | (tbd) | E-31 engine side: scoping recipe writes new surfaces as draft map proposals (R8-11) | — | P4, P5 | S | waiting |
| P8 | feat/sindri-wf-p8-agent-runtime | Agent runtime via `process` launch target; deps edge to Judge + fan-out exception | Claude Code process target reports `via: agent` | P7, API P10a, Judge #156 | M | waiting |

## Parallel execution (spec "Parallel execution with an agent team")

Orchestrator = this session (no product code). Per phase: contract unit first (one implementer, committed red, pushed),
then parallel units (≤3 implementers, ≤2 building at once), an integrator merges units + runs the gate + opens the
draft PR, a Prover runs the signals, reviewers loop `/review` to 0 blocking/issue (cap 5).
Each agent gets its own worktree and its own scratchpad subdir `scratchpad/<phase>-<unit>/`.

P1 units: contract · (a) reader/normalize/hash/schema+overlay, RCP-SCHEMA/STATE/INIT/DUR/HASH · (b) graph rules ·
(c) wiring, block, event rules · (d) RCP-CAP-001/002/004–007 · (e) descriptors, recipes/** + evals + dev profile,
fixtures · (f) CLI verb, golden files, guide.
