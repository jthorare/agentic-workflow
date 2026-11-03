fire_rate: 9/9
delta: 0.89
last_run: 2026-11-03
pr: TBD

Baseline (spec §8 step 5): agent claude-sonnet-5-5, judge sonnet, 3 runs per case, preamble on.
- fire_rate: verify-web-natural-1, verify-web-natural-2 and the held-out case fired 3/3 each; verify-web-near-miss fired 0/3, which
  is right (20261103T122847-3959012-routing, at 3b1310e; the description hasn't changed since).
- delta: verify-web-forced 1.0 with the skill, 0.11 without (20261103T124822-47995-behaviour, at e8381a1). Every grader passed 3/3
  with the skill. Before:
  - 0.63 / 0.11 (20261103T122445-3949933-behaviour, at 3b1310e): the final reply left out the unnamed button, the console error, and
    the verdict and evidence_path lines (the skill said "end the report", read as report.md only). Fixed in b5c95fe09.
  - 0.85 / 0.11 (20261103T124316-3998892-behaviour, at b5c95fe09, traces kept): two eval faults, not skill faults.
    interacts-before-console named mcp__plugin_playwright_playwright__browser_click, but the eval serves the mock as
    mcp__plugin_aw-evals_playwright__browser_click, so it failed runs that clicked before reading the console. And the click and
    fill mocks echoed fixed text ("Save recipe", "Chickpea stew") whatever was asked; one run saw the mismatch and stopped before
    reading the console. Fixed in e8381a16c.
- Caveat: 3 runs per arm. One run flips a case by about 0.1-0.3.
