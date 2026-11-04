fire_rate: 9/9
delta: 0.85
last_run: 2026-11-04
pr: #TBD

Baseline (spec §8 step 5): agent claude-sonnet-5-5, judge sonnet, 3 runs per case, preamble off (the runner's default).
- fire_rate: productReview-natural-1, productReview-natural-2 and the held-out case fired 3/3 each; productReview-near-miss fired
  0/3, which is right (20261104T010353-2616494-routing). Four of those runs reached max_turns after the routing call; routing is
  graded on the Skill call, so the grades stand.
- delta: productReview-forced 1 with the skill, 0.15 without (20261104T012150-3911054-behaviour). Every grader passed 3/3 with
  the skill.
- Caveat: 3 runs per arm. One run flips a case by about 0.1-0.3.
