fire_rate: 6/9
delta: 0.77
last_run: 2026-11-03
pr: #1999

Baseline (spec §8 step 5): agent claude-sonnet-5-5, judge sonnet, 3 runs per case, preamble on.
- fire_rate: after the "not for" fix (#1999, 20261103T123421-3980385-routing): verify-desktop-natural-1 and verify-desktop-natural-2
  fired 3/3 each; verify-desktop-near-miss fired 0/3, which is right; the held-out case fired 0/3. Before the fix it fired 1/3
  (20261103T010649-382012-routing) and 0/3 (20261103T005929-208887-routing); in those kept traces the other held-out runs called
  design-verify-desktop. The held-out prompt asks which screens drifted from their mockups and never asks for a ship verdict, which is
  design-verify-desktop's job, so the case doesn't separate the two skills. It stays as is and counts as a miss here; a new held-out,
  written before any run, replaces it (#1986).
- delta: verify-desktop-forced 1.0 with the skill, 0.23 without (20261103T005525-4015613-behaviour). Every grader passed 3/3 with the
  skill. First run: 0.69 / 0.23 (20261103T005010-3866273-behaviour): two of three runs with the skill skipped the result file and the
  aw-result block (fixed by the Done checklist), and two graders failed runs that did the right thing (pack-says-fail read a glob focus
  path the eval tool takes literally; screens-from-the-diff stopped at the quotes of --repo "$(...)").
- Caveat: 3 runs per arm. One run flips a case by about 0.1-0.3.
