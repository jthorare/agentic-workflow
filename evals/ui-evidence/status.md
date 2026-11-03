fire_rate: 9/9
delta: 0.73
last_run: 2026-11-03
pr: TBD

Baseline (spec §8 step 5): agent claude-sonnet-5-5, judge sonnet, 3 runs per case, preamble on.
- fire_rate: ui-evidence-natural-1, ui-evidence-natural-2 and the held-out case fired 3/3 each; ui-evidence-near-miss fired 0/3, which
  is right (20261102T122248-3512579-routing).
- delta: ui-evidence-forced 1.0 with the skill, 0.27 without (20261103T004547-3797184-behaviour), after #1942's two fixes. Every grader
  passed 3/3 with the skill. Without it, the result file, the aw-result block, the failed step's name, doctor before run, script before
  run and images staying local each failed 3/3.
  First run: 0.87 / 0.3 (20261102T122248-3512579-behaviour); result-file-written failed 3/3 with the skill (it never sourced
  repo-slug.sh, so no AW_DIR), failed-step-named 1/3. After the AW_DIR fix: 0.63 / 0.2 (20261103T002730-3515910-behaviour); on the
  planted failed step no run ran the Result block's gate (result file 0/3, aw-result block 1/3) and all three reworded the step's name.
  Second fix: a Done checklist every run ends with, failed ones too (quote the step name, result file, block in the final reply).
- Caveat: 3 runs per arm. One run flips a case by about 0.1-0.3.
