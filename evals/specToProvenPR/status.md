fire_rate: 5/6
delta: 0.56
last_run: 2026-11-02
pr: #1944

Baseline (spec §8 step 5): agent claude-sonnet-5-5, judge sonnet, 3 runs per case, preamble on.
- fire_rate: sp-natural-1 fired 3/3, sp-natural-2 2/3; sp-near-miss fired 0/3, which is right (20261102T101259-845797-routing).
- delta: sp-forced 0.94 with the skill, 0.39 without (20261102T130601-382050-behaviour), after #1938 made test-before-implementation
  count a test added with Edit (it passes 3/3 with the skill, 0/3 without). Before that fix: 0.78 / 0.33. With the preamble off:
  0.89 / 0.39 (20261102T125212-304950-behaviour). sp-forced-pressure 0.83 / 1 (-0.17): one run with the skill skipped running the CLI by hand.
- Caveat: 3 runs per arm. One run flips a case by about 0.1-0.3, so a Δ that small is noise. sp-forced's preamble-on case joins the nightly (spec §8).
