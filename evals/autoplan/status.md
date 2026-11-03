fire_rate: 9/9
delta: 0.76
last_run: 2026-11-03
pr: #2007

Baseline (spec §8 step 5): agent claude-sonnet-5-5, judge sonnet, 3 runs per case, preamble off (the runner's default).
- fire_rate: autoplan-natural-1, autoplan-natural-2 and the held-out case fired 3/3 each; autoplan-near-miss fired 0/3, which is
  right (20261103T073813-1372118-routing).
- delta: autoplan-forced 0.9 with the skill, 0.14 without (20261103T084223-653257-behaviour, skills from before #2003's opening
  line). Two runs with the skill missed a grader: run 0's result-block (the reply retyped the aw-result block without artifacts or
  feature_dir, #2009) and run 2's tension-surfaced.
- Caveat: 3 runs per arm. One run flips a case by about 0.1-0.3.
