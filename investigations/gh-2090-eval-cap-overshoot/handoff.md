# Handoff: gh-2090-eval-cap-overshoot

status: diagnosed
report: n/a (handoff only; lead commits)
boundary: scripts/skill-evals.sh (one script; the CLI's own cap logic is external)
repro: read-only. Saved run ~/.agentic-workflow/skill-evals/2026-11-05/20261105T101917-3831800-behaviour/ (one case review-forced, one `claude plugin eval` call, 6 runs 0.66+0.43+0.42+0.26+0.14+0.27 = 2.166; its results.json: costUsd 2.165891, partial false, 1 case)

## Root Cause
The PARTIAL line is a bookkeeping bug in scripts/skill-evals.sh. After each case the loop runs `awk 'exit !(c >= m)'` (scripts/skill-evals.sh:1143) and sets `ceil=1` whenever spend >= the kind's ceiling, even when that was the LAST case and nothing was skipped. `[ "$ceil" = 0 ] || partial=1` (about line 1160) turns that into partial=1, which prints "PARTIAL: the run hit its cost ceiling" (line 1221) and makes the script exit 2 (last line of the file). So a run that finished and was graded in full is reported as an incomplete measurement. The 2.17 vs 2 overshoot itself happens inside the CLI's single call (the script already passes the remaining budget as `--max-cost-usd "$cbl"`, line 1116, and the CLI only checks its cap between its own runs); the script only sees the total afterwards.

## Hypotheses

| # | Hypothesis | Cause-site files | Likelihood | Result |
|---|-----------|------------------|------------|--------|
| 1 | PARTIAL is set on spend >= ceiling, not on a case actually skipped | `scripts/skill-evals.sh:1143`, `:1160`, `:1221` | High | confirmed |
| 2 | No pre-launch check that what is left can pay for one more case; the loop only breaks after a case pushed spend past the ceiling | `scripts/skill-evals.sh:1137`, `:1143` | Medium | confirmed as a real gap, but not what produced this report (only one case existed, nothing was left to launch) |
| 3 | The CLI overshoots its own `--max-cost-usd` inside one call (checks between runs; 6 runs, last ones landed after the cap) | external `claude plugin eval` | Medium | consistent with the run's numbers: cap 2, settled 2.166, CLI itself says partial:false; cannot be fixed in the script, only bounded |
| 4 | Cost double-counted | `scripts/skill-evals.sh:1133-1135` | Low | ruled out: script total equals the CLI's costUsd 2.165891 and the per-run sum 2.166 |

## Ruled Out
- Double counting (4): settled spend = CLI costUsd = sum of the six runs.
- A skipped case: results.json has the one case and the CLI reports partial:false, so the PARTIAL text is wrong for this run.

## Suggested Fix
Within scripts/skill-evals.sh only:
1. Set `ceil=1` (and so partial) only when a case was really left unrun: break-after-spend only counts when more case names remain (track the remaining list, or check at the TOP of the loop: before launching a case, if remaining budget `bl - cost` <= 0 mark ceil=1 and break). Keep `crc == 2` -> partial as is.
2. When spend ends above the ceiling with every case run, print "over the ceiling by $X" (cost - bl) instead of PARTIAL, and decide the exit code deliberately (likely 0/unchanged, not 2).
3. Optional: pre-launch affordability check per case (skip when remaining < the $0.01 floor, or a per-case estimate) so later cases never start; for the in-call overshoot, document it as a CLI limit (cap is checked between runs), do not try to fix it here.
4. Tests: bash suite for skill-evals (stub claude printing a results.json whose costUsd > cap on the last/only case): expect no PARTIAL line, an "over the ceiling by" note; a two-case stub where case 1 exhausts the cap: expect PARTIAL and exit 2.
