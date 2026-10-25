# UI evidence: cost baseline and status

Spec: "Make visual QA cheaper" (28 Sep 2026). Baseline commit `df40fb3`.

## What the runner already did right

`run-script.ts` drives Chromium with Playwright and **no model per click**, so
swapping in `agent-browser` cannot remove a per-click cost that isn't there.
The model calls are: one planning call (`qa-runner`), selector repair
(`judge ui-element-repair`), and one visual critique (`judge visual-critique`).

## Status by step

| Step | State | Notes |
|---|---|---|
| 00 baseline | **Instrumented; not yet measured on real PRs** | `summary.json` records every invocation; a reader can build per-phase and per-PR/route tables from them (the scorer's report is gone; its rebuild inside Sindri is later). Tokens are `unknown` because the `judge` CLI does not surface provider usage — only count and elapsed time are recorded (a proxy, labelled as such). |
| 01 false-pass | **Done** | Structured `expectedState`, asserted per step; repair executes the mapped action on a DOM-validated candidate and re-checks. Real-Chromium fixture tests cover both. |
| 02 spend models where needed | **Gate done; planner pilot pending** | Pixel gate skips the image model on unchanged runs; Jev never receives image bytes (tests). `qa-runner.md` still says `model: sonnet` (the rollback value) — no cheaper model is switched in until the pilot below is run. |
| 03 verdict cache | **Done** | Keyed on actual image hashes + prompt/model, app build, script, fixtures, viewport, browser version. Never skips the browser run. |
| 04 compact discovery | **Not started — conditional** | Do only if step 00 shows planner input tokens dominate. No `agent-browser` adapter added. |
| 05 packaging | **Partly done** | `RunSummary` carries `diffScore` and `evidence` (traces, videos, diff overlay); `publish.ts` renders URLs from an approved `uploadArtifact`, seeded-provenance only. **Not done:** the consuming app's CI (private, not inspected), so no CI artifact upload, retention or access settings were chosen. |

## Also fixed along the way

`visual-critique.ts` and the repair call passed `input` to async `execFile`,
which ignores it; `judge` reads stdin to EOF, so both calls hung until their
timeout and returned null (`unchecked` visual, no repair) in production. They
now go through `judge-exec.ts`, which writes and closes stdin.

## Measurement protocol (needed before any percentage claim)

1. Pick 5+ representative UI PRs. For each, plan with the current `sonnet`
   `qa-runner`, put `planning: { pr, model, elapsedMs }` (plus token counts if
   the harness exposes them) in the script, and run `runScript` against a
   main-branch baseline.
2. Total the `invocations` of the baseline runs' `summary.json` files (per phase, per PR/route) and keep that as the **before**.
3. Pilot the cheaper planner model on the *same* PRs; compare scripts on
   affected routes, edge states, and assertion quality (not just cost).
   Roll back (`model: sonnet`) if detection weakens.
4. Only then state a saving, from same-workload before/after numbers.
