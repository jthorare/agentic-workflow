fire_rate: 9/9
delta: 0.62
last_run: 2026-11-04
pr: #TBD

Baseline (spec §8 step 5): agent claude-sonnet-5-5, judge sonnet, 3 runs per case, preamble off (the runner's default).
- fire_rate: planDevexReview-natural-1, planDevexReview-natural-2 and the held-out case fired 3/3 each; planDevexReview-near-miss
  fired 0/3, which is right (20261103T082028-2853512-routing).
- delta: planDevexReview-forced 1 with the skill, 0.38 without (20261104T012150-3911054-behaviour). Every grader passed 3/3 with
  the skill.
- Caveat: 3 runs per arm. One run flips a case by about 0.1-0.3.
