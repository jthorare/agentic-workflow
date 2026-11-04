fire_rate: 9/9
delta: 0.71
last_run: 2026-11-03
pr: #2007

Baseline (spec §8 step 5): agent claude-sonnet-5-5, judge sonnet, 3 runs per case, preamble off (the runner's default).
- fire_rate: cso-natural-1, cso-natural-2 and the held-out case fired 3/3 each; cso-near-miss fired 0/3, which is right
  (20261103T073813-1372118-routing).
- delta: cso-forced 1.0 with the skill, 0.29 without (20261103T084223-653257-behaviour, skills from before #2003's opening line).
  Every grader passed 3/3 with the skill.
- Caveat: 3 runs per arm. One run flips a case by about 0.1-0.3.
