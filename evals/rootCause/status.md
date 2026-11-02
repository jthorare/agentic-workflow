fire_rate: 6/6
delta: -0.03
last_run: 2026-11-02
pr: PR_PENDING

Baseline (spec §8 step 5): agent claude-sonnet-5-5, judge sonnet, 3 runs per case, preamble on.
- fire_rate: rc-natural-1 and rc-natural-2 fired 3/3 each; rc-near-miss fired 0/3, which is right (20261102T101259-845797-routing).
- delta: rc-forced 0.57 with the skill, 0.6 without (20261102T113844-2713013-behaviour). With the preamble off: 0.6 / 0.53.
- Known gap #1919: once the fix passes, rootCause skips Phase 5 (report, handoff, result file, aw-result block) in 3/3 runs, even with Bash writes working. Its four Phase 5 graders fail until #1919 lands, which caps rc-forced near 0.6.
