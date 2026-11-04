fire_rate: 9/9
delta: 0.82
last_run: 2026-11-04
pr: #TBD

Baseline (spec §8 step 5): agent claude-sonnet-5-5, judge sonnet, 3 runs per case, preamble on.
- fire_rate: verify-ios-natural-1, verify-ios-natural-2 and the held-out case fired 3/3 each; verify-ios-near-miss fired 0/3, which
  is right (20261104T010124-2484063-routing, at 9a8b55fb1; the description hasn't changed since).
- delta: verify-ios-forced 1.0 with the skill, 0.18 without (20261104T010605-2660617-behaviour, at 41e2f4cf5). Every check 3/3.
  Before:
  - 0.82 / 0.18 (20261104T005813-2304918-behaviour, at 9a8b55fb1, the first run with the checks naming the served
    mcp__plugin_aw-evals_xcodebuildmcp__* tools): every run wrote the gesture probe message only into report.md and pack.json, and
    one put the verdict and evidence_path lines only inside the aw-result JSON ("End the report with", the reading verify-web had).
    Fixed in 4da85f8e8.
  - missing-label-named was widened after that run to take "null label" (one run named waterToggle-2 that way). In the final run
    it is the reason run 1 passes ("null label", "no accessibility label"; the old check took only "accessible"). With the old
    check the final run is 0.92, not 1.0.
- Caveat: 3 runs per arm. One run flips a case by about 0.1-0.3.
