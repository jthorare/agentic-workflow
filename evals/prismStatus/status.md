fire_rate: 9/9
delta: 1
last_run: 2026-11-04
pr: #TBD

Baseline (spec §8 step 5): agent claude-sonnet-5-5, judge sonnet, 3 runs per case, preamble off (the runner's default).
- fire_rate: prismStatus-natural-1, prismStatus-natural-2 and the held-out case fired 3/3 each; prismStatus-near-miss fired 0/3,
  which is right (20261104T010353-2616494-routing). The held-out wording came from ideas' review of #2007; it was read only to
  check the new description's words aren't in it.
- delta, re-graded: prismStatus-forced 1 with the skill, 0 without (20261104T012150-3911054-behaviour, the same traces graded
  again with this PR's port-used grader; no new run).
- delta as run: 0.8 with, 0 without. port-used failed in all 3 runs with the skill, and that was the grader: every run did
  `PORT=7391; curl ... "http://localhost:$PORT/"`, which is what SKILL.md tells it to do, and the grader wanted the literal
  localhost:7391. It now also takes PORT=7391 followed by a curl to localhost:$PORT; its _selftest has the real run's calls as a
  pass, and PORT=7391 with no curl, or a curl to another port, as fails.
- Caveat: 3 runs per arm. One run flips a case by about 0.1-0.3.
