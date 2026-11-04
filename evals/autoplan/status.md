fire_rate: 9/9
delta: 0.83
last_run: 2026-11-04
pr: #2035

Baseline (spec §8 step 5): agent claude-sonnet-5-5, judge sonnet, 3 runs per case, preamble off (the runner's default).
- fire_rate: autoplan-natural-1, autoplan-natural-2 and the held-out case fired 3/3 each; autoplan-near-miss fired 0/3, which is
  right (20261103T073813-1372118-routing).
- delta: autoplan-forced 0.96 with the skill, 0.13 without (20261104T014231-601512-behaviour, after #2009's fix). result-block-copied
  passed 3/3, and each reply's aw-result block is identical to the result file it wrote. One run missed tension-surfaced.
- Before the fix: 0.95 with, 0.14 without (20261104T013238-215104-behaviour), and earlier 0.9 / 0.14 (20261103T084223-653257).
  Across those 6 runs with the skill, 3 replies retyped the aw-result block instead of copying the file (no artifacts or
  feature_dir, or bare file names); result-block only checks that skill, artifacts and summary are there, so it passed them.
  result-block-copied (absolute artifacts and feature_dir) fails exactly those 3 (#2009).
- tension-surfaced missed in 1 of 3 runs in each of the 3 behaviour runs; not #2009, still open.
- Caveat: 3 runs per arm. One run flips a case by about 0.1-0.3.
