fire_rate: 9/9
delta: 0.43
last_run: 2026-11-04
pr: #2035

Baseline (spec §8 step 5): agent claude-sonnet-5-5, judge sonnet, 3 runs per case, preamble off (the runner's default).
- fire_rate: judge-natural-1, judge-natural-2 and the held-out case fired 3/3 each; judge-near-miss fired 0/3, which is right
  (20261104T010353-2616494-routing). The held-out wording came from ideas' review of #2007; it was read only to check the new
  description's words aren't in it.
- delta, re-graded: judge-forced 1 with the skill, 0.57 without (20261104T012150-3911054-behaviour, the same traces graded again
  with this PR's graders; no new run).
- delta as run: 0.86 with, 0.33 without, delta 0.52. The misses were in two graders, not the skill: in the eval box the judge
  wrapper prints "no C# build is linked (run scripts/install-cs.sh)", and every run with the skill reported it and named
  install-cs.sh. missing-reported wanted "not found / missing / not installed" and fix-named wanted setup.sh, wording from before
  the cutover. Both now also take the wrapper's words; their _selftest has the real run's reply as a pass and a reply that names
  no cause or neither script as a fail. Runs without the skill pass those two graders now too (they relay the same error), which is
  why the delta drops.
- Caveat: 3 runs per arm. One run flips a case by about 0.1-0.3.
