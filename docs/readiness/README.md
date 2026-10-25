# Readiness

Agent-readiness measurements for this repo (spec: `docs/superpowers/specs/2026-10-09-agent-readiness-design.md`,
§2, §4c, §8). `aw-readiness check` is the score of record (decision R4); no paid Factory report or Droid is used.

| File | What it is |
|---|---|
| `readiness.yaml` (repo root) | The repo's name, ring, scope and each ring's target level |
| `src/Readiness/` | `Aw.Readiness`: the `aw-readiness` NativeAOT CLI (`Aw.Readiness.Core` + `Aw.Readiness.App`) |
| `baseline-2026-10.md` | The R0 baseline of the TS apps (historical: they are gone), from the throwaway `scripts/readiness-baseline.mjs` |
| `aot-spike.md` | The R1a AOT spike (R7, R8, R9) |
| `r2-estimate.md` | The R2 estimate fixed at the R1b exit: per-packet hours from the template packages' measured throughput, and the §10 stop-rule thresholds |

## aw-readiness check

```bash
dotnet run --project src/Readiness/Aw.Readiness.App -- check            # JIT, from the repo root
bash src/Readiness/aot-smoke/run.sh                                     # publish NativeAOT → artifacts/aot-readiness/bin/aw-readiness
artifacts/aot-readiness/bin/aw-readiness check [--json] [--root DIR] [--no-remote]
```

- **Exit codes:** 0 when the level reaches the target for the repo's ring, 1 when it doesn't, 2 on a usage or tool
  error (an `AW-READINESS-NNN` error on stderr: 001 bad arguments, 002 root not found, 003 `readiness.yaml` missing or
  malformed, 004 history unreadable or unwritable).
- **States:** each criterion is `pass`, `fail`, `n/a` (with a reason) or `unknown`. Pass and fail are scored, and so is an
  unread `unknown` (as not passing); `n/a` and a not-yet-reproducible `unknown` (TD-4, PE-3) aren't. A failure prints
  the criterion's fix. `unknown` has two causes, told apart by the finding's `unread` flag: the check
  can't be reproduced yet (TD-4, PE-3), or its data couldn't be read this run (`--no-remote`, gh missing, failing or
  not an admin). An **unread** criterion counts as not passing, so the level is a lower bound: a run that couldn't
  read the host never scores higher than one that could, and a target the level reaches holds whatever the unread
  criteria turn out to be. When the target isn't reached, the unread criteria at or above the level reached and below
  the target (the only ones that could still raise it) are listed under `unconfirmed` ("does not hold; … weren't read
  and could raise the level").
- **Levels:** every repo starts at Level 1; Level N+1 unlocks when at least 80% of the scored Level N criteria pass.
  A level with nothing scored neither unlocks nor blocks: the climb skips it, so the level is one above the highest
  scored level that passed with every scored level below it passing (the R0 baseline's rule; with L1 passing, L2
  empty and L3 failing the level is 2). The first scored level under 80% stops the climb. Reported per pillar and overall.
- **Delta:** each run is diffed against the repo's previous run in the same scope; a criterion unread on either run
  isn't compared (so an offline run doesn't show host criteria flipping), and a level move with no compared change is
  reported as such. The first run records a baseline and says so.
- **History:** one JSON file per run, `{schemaVersion: 1, repo, scope, recordedAt, level, states[] {id, state, unread}}`,
  under `${AW_STATE_DIR:-~/.agentic-workflow}/readiness/runs/<repo>/<scope>/<UTC time>-NNN.json` (NNN keeps runs at
  the same instant apart, in save order). It writes nothing else (spec §4c), and nothing installs or schedules it (no
  `setup.sh`, provider or launchd entry). Use a scratch `AW_STATE_DIR` when checking another checkout or a scratch
  merge, so its runs don't become the real checkout's baseline.
- **Host criteria** (BS-4 releases, SEC-1 branch protection, SEC-2 secret scanning, and TD-2's "applied to the repo"
  half) come from read-only `gh repo view` and `gh api` GETs run in the checkout. `gh` is found only on absolute PATH
  entries, never in the current directory (a checkout can't plant its own); `AW_READINESS_GH` overrides it with an
  absolute path. A call that outlives its 30 s timeout is killed. Offline (`--no-remote`) or when gh can't answer,
  they are unread `unknown`s, never guessed. A plan that can't protect branches (HTTP 403 "Upgrade to GitHub Pro") is
  a known fail for SEC-1, and so is a 404 "Branch not protected" with no protective ruleset rule (pull_request,
required_status_checks, non_fast_forward, required_linear_history, deletion, update); a plain 404 (what a non-admin
gets) is unread. A repo admin seeing no `security_and_analysis` is a known fail for SEC-2.
- **Files** are read and listed only inside the root: a path with a symbolic link (file or directory) or `..` in any
  segment reads as missing, and a file over 1 MiB reads as missing.
- **Output:** text by default — every state is a word, so colour never carries meaning and `NO_COLOR` has nothing to
  strip. `--json` writes the report object, `schemaVersion: 1` (recorded in
  `tests/Aw.Readiness.Tests/Golden/report.json`): `repo`, `scope`, `recordedAt`, `target {ring, level}`, `level`,
  `holds`, `unconfirmed[]`, `recordedTo`, `delta {since, levelBefore, levelAfter, changes[], isFirstRun}`, `pillars[]`,
  `tallies[]`, `findings[] {criterion {id, pillar, level, title, fix}, state, evidence, unread}`. A field change bumps
  `schemaVersion`.

## Scope: the C# template

`scope: csharp-template` checks the C# build surface — `src/`, `tests/`, the root build files, the CI workflows,
`AGENTS.md`, the rules and the repo-level files (templates, devcontainer, CODEOWNERS, dependency updates) — against the
R0 baseline's 34 criteria, with the same ids and levels. Two read narrower than R0: TS-4 checks the CI configuration
only (no run or its time), and TD-2 needs both the scheme file (`.github/labels.yml`, the type:, area:, size: and
ring: families) and labels of each family on the repo (R0 read only the repo's labels). The TS apps are not scored: they were removed at the switch. The criteria live in `src/Readiness/Aw.Readiness.Core/Criteria/` (one file per pair of pillars); a change
to a criterion is a reviewed change to its check and its test in `tests/Aw.Readiness.Tests/Criteria/`.

## readiness.yaml

```yaml
version: 1                  # the only version
repo: agentic-workflow      # 1-64 of a-z 0-9 . _ - : the history's directory
ring: 0                     # which target below applies
scope: csharp-template      # the only scope
targets:                    # one item per ring; each ring at most once; level 1-5
  - ring: 0
    level: 4
```

A flat YAML subset (read without a library): `key: value` lines, the `targets:` list, `#` comments. Anything else is
refused with `AW-READINESS-003` naming the line, the key and the fix.

## The R0 baseline

`baseline-2026-10.md` scored the TS apps with `scripts/readiness-baseline.mjs`, a **throwaway** script (Node ≥ 20, no
dependencies). Its scope differs from `aw-readiness`'s (the TS apps, not the C# template), so `aw-readiness` does not
diff against it: its first run records its own baseline. The baseline is kept as a record of where the TS apps stood.

```bash
node scripts/readiness-baseline.mjs --out docs/readiness/baseline-2026-10.md   # regenerate (reads gh api, GET only)
node scripts/readiness-baseline.mjs --no-remote                                # offline: remote criteria → unknown
bash scripts/tests/readiness-baseline.test.sh                                  # tests on fixture trees
```
