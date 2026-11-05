fire_rate: 9/9
delta: 0.56
last_run: 2026-11-05
pr: #2084

Baseline (spec §8 step 5): agent claude-sonnet-5-5, judge sonnet, 3 runs per case, preamble on.
- fire_rate: system-design-video-natural-1, system-design-video-natural-2 and the held-out case fired 3/3 each;
  system-design-video-near-miss (a written TDD, system-design's job) fired 0/3, which is right (20261105T083415-1733857-routing, at
  49e8d7508, $1.77). natural-2 ("whiteboard walkthrough so the team gets the same picture") still lands here after system-design's
  description gained "shareable TDD and eng-review deck" (#2049).
- delta: system-design-video-forced 1.0 with the skill, 0.44 without, fired 3/3 (20261105T091415-2309812-behaviour, at 830b8d766,
  $0.93). Every grader passed 3/3 with the skill: each run reached playback-check.sh, reported the planted B-frames failure, delivered
  nothing and wrote its result file and block. Without the skill, two runs never ran a check and the third skipped the result file and
  block and never named the B-frames.
- Second run: 0.92 / 0.50 (20261105T085214-1958773-behaviour, at a30c68f22, $0.99). The points it lost were grader bugs: no-write-checks
  matched the Write tool's whole input, so a result file whose summary named playback-check counted as a write to the check (with-1,
  with-2); result-file-written counted only the Write tool, and with-3 wrote its result file from Bash. It also hid a fixture bug (found
  in review): the planted B-frames lived in the workspace's ffprobe.json, found from the cwd, so with-3's review.sh, run in
  $TMPDIR/take, printed a clean playback pass and caught the failure only because deliver.sh ran from the workspace. Both fixed before
  the run above; the planted values now travel inside the mp4 (scripts/tests/eval-av-fakes.test.sh).
- First run: 0.78 / 0.44 (20261105T084243-1840480-behaviour, at 49e8d7508, $0.84). The fakes for bun and ffmpeg wrote no files and
  printed "stub frames"; in two of three runs with the skill the agent then read the bun fake's "canned stand-in" header and stopped
  before playback-check.sh, and one of them wrote no result file or block. Fixed in the fixture, not SKILL.md: the fakes now write
  what the real tools write and never call themselves fakes (evals/stubs/README.md).
- Caveat: 3 runs per arm. One run flips a case by about 0.1-0.3.
