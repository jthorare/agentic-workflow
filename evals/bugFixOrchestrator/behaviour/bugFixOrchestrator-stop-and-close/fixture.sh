#!/usr/bin/env bash
# A finished /rootCause handoff whose Classification says the behaviour matches the baseline variant (an "as designed" report).
set -eu
. "$(dirname "${BASH_SOURCE[0]}")/../../../_lib/fixture-lib.sh"
mkdir -p .aw-eval
printf '# acme\n' > README.md
cat > .aw-eval/handoff.md <<'Q'
status: diagnosed

## Hypotheses

| # | Hypothesis | Cause-site files | Likelihood | Result |
|---|---|---|---|---|
| 1 | The pro plan hides the export button by design | `plans/pro.py` | high | confirmed |

## Root Cause

The export button is hidden on the pro plan. The standard plan, which is the baseline variant, hides it the same way.
`plans/pro.py` and `plans/standard.py` both set `show_export = False`; the product spec lists it as plan-gated.

## Classification

- Baseline: matches baseline
- Origin: as designed
Q
aw_fixture_repo "initial acme"
