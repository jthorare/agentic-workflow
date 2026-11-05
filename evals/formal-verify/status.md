fire_rate: 9/9
delta: 0.70
last_run: 2026-11-05
pr: #2087

Baseline (spec §8 step 5): agent claude-sonnet-5-5, judge sonnet, 3 runs per case, preamble off (the runner's default).
- fire_rate: formal-verify-natural-1, formal-verify-natural-2 and the held-out case fired 3/3 each; formal-verify-near-miss fired
  0/3, which is right (20261105T092556-2594102-routing, $1.97). Two runs hit their turn limit after firing (4 and 8 turns); the
  graders still passed them.
- delta: formal-verify-forced 1.0 with the skill, 0.3 without (20261105T093544-2956081-behaviour, $0.91). With the skill every
  grader passed 3/3. Without it, all 3 runs named the race and quoted the invariant but wrote no model, config or properties,
  never ran tlc-check and gave no blocked/fail verdict or aw-result block.
- Caveat: 3 runs per arm. One run flips a case by about 0.1-0.3.
