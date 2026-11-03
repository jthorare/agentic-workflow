fire_rate: 7/9
delta: 0.77
last_run: 2026-11-03
pr: TBD

Baseline (spec §8 step 5): agent claude-sonnet-5-5, judge sonnet, 3 runs per case, preamble on.
- fire_rate: verify-desktop-natural-1 and verify-desktop-natural-2 fired 3/3 each; verify-desktop-near-miss fired 0/3, which is
  right; the held-out case fired 1/3 (20261103T010649-382012-routing; 0/3 in the run before, 20261103T005929-208887-routing). In the
  kept traces the other two held-out runs called design-verify-desktop: its description names Avalonia and mockups, and its "not for"
  line calls verify-desktop the journey check (#1986).
- delta: verify-desktop-forced 1.0 with the skill, 0.23 without (20261103T005525-4015613-behaviour). Every grader passed 3/3 with the
  skill. First run: 0.69 / 0.23 (20261103T005010-3866273-behaviour): two of three runs with the skill skipped the result file and the
  aw-result block (fixed by the Done checklist), and two graders failed runs that did the right thing (pack-says-fail read a glob focus
  path the eval tool takes literally; screens-from-the-diff stopped at the quotes of --repo "$(...)").
- Caveat: 3 runs per arm. One run flips a case by about 0.1-0.3.
