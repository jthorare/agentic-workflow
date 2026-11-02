fire_rate: 6/6
delta: 0.4
last_run: 2026-11-02
pr: #1944

Baseline (spec §8 step 5): agent claude-sonnet-5-5, judge sonnet, 3 runs per case, preamble on.
- fire_rate: rc-natural-1 and rc-natural-2 fired 3/3 each; rc-near-miss fired 0/3, which is right (20261102T101259-845797-routing).
- delta: rc-forced 0.97 with the skill, 0.57 without (20261102T130154-369794-behaviour), after #1919's fix: Phase 5 (report, handoff,
  result file, aw-result block) now passes 3/3 with the skill and fails 3/3 without. Before that fix: 0.57 / 0.6. The one miss with the
  skill is repro-before-edit in 1 of 3 runs.
- Caveat: 3 runs per arm. One run flips a case by about 0.1-0.3.
