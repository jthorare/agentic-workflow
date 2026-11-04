fire_rate: 9/9
delta: 0.54
last_run: 2026-11-04
pr: #TBD

Baseline (spec §8 step 5): agent claude-sonnet-5-5, judge sonnet, 3 runs per case, preamble on.
- fire_rate: verify-app-natural-1, verify-app-natural-2 and the held-out case fired 3/3 each; verify-app-near-miss fired 0/3, which
  is right (20261104T002835-1467893-routing, at c16ffbfec; the description hasn't changed since).
- delta: mean over the 4 behaviour cases, 1.0 with the skill and 0.46 without (20261104T004210-1532017-behaviour, at 1d8c52ace).
  Every case 1.0 and every check 3/3 with the skill, args-passed-through included. Before:
  - 0.875 / 0.46 (20261104T002213-1436883-behaviour, at c16ffbfec): every desktop run wrote the dispatch line through a Bash echo,
    one named-iOS run skipped it, and one stopped to ask although the prompt said "the iOS app". Fixed in adc5e514a.
  - 0.96 / 0.46 (20261104T003348-1492427-behaviour, at adc5e514a): desktop and named went to 1.0, but 2 of 3 web runs dispatched
    with no line (the rule sat mid-sentence). Dispatch became three numbered steps in 1d8c52ace. args-passed-through used (?s), which
    JavaScript rejects, so it threw in every run; as a with-only check it was never scored or printed (#2024, fixed in #2025).
  - The dispatch-echo checks changed after the first run (last_message -> whole trace, reply text only). Re-checked against the same
    9 with-skill traces of that run: same pass/fail on every one.
- Caveat: 3 runs per arm. One run flips a case by about 0.1-0.3.
