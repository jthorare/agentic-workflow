fire_rate: 9/9
delta: 0.42
last_run: 2026-11-05
pr: #2084

Baseline (spec §8 step 5): agent claude-sonnet-5-5, judge sonnet, 3 runs per case, preamble on.
- fire_rate: system-design-video-natural-1, system-design-video-natural-2 and the held-out case fired 3/3 each;
  system-design-video-near-miss (a written TDD, system-design's job) fired 0/3, which is right (20261105T083415-1733857-routing, at
  49e8d7508, $1.77). natural-2 ("whiteboard walkthrough so the team gets the same picture") still lands here after system-design's
  description gained "shareable TDD and eng-review deck" (#2049).
- delta: system-design-video-forced 0.92 with the skill, 0.50 without, fired 3/3 (20261105T085214-1958773-behaviour, at a30c68f22,
  $0.99). Every run with the skill reached playback-check.sh and reported the planted B-frames failure with no deliverables. The
  points it lost were grader bugs, fixed after the run and not rerun: no-write-checks matched the Write tool's whole input, so a
  result file whose summary named playback-check counted as a write to the check (with-1, with-2); result-file-written counted only
  the Write tool, and with-3 wrote its result file from Bash. Regrading those traces by hand under the fixed graders gives 1.0 with
  and about 0.53 without; that is a hand count, not a run.
- First run: 0.78 / 0.44 (20261105T084243-1840480-behaviour, at 49e8d7508, $0.84). The fakes for bun and ffmpeg wrote no files and
  printed "stub frames"; in two of three runs with the skill the agent then read the bun fake's "canned stand-in" header and stopped
  before playback-check.sh, and one of them wrote no result file or block. Fixed in the fixture, not SKILL.md: the fakes now write
  what the real tools write and never call themselves fakes (evals/stubs/README.md).
- Caveat: 3 runs per arm. One run flips a case by about 0.1-0.3.
