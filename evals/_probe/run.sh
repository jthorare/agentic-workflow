#!/usr/bin/env bash
# Phase 0a probe (#1107, spec 2026-10-25-skill-revamp-evals-design.md §8 step 0): stage the probe plugin outside any git
# repo and run its one case, so we see what the eval's agent shell can write and which variables reach it.
# Usage: run.sh <inherit|allowlist> <out-dir>
#   inherit    the caller's whole environment plus a planted fake token, to see what reaches the agent by default
#   allowlist  env -i with PATH, TERM, LANG, HOME and the AW_* values (spec §5.4), to see that claude keeps its login
set -euo pipefail
mode="${1:-}" out="${2:-}"
[ -n "$out" ] || { echo "usage: run.sh <inherit|allowlist> <out-dir>" >&2; exit 2; }
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
stage="${AW_STATE_DIR:-$HOME/.agentic-workflow}/skill-evals/cache/probe-$mode"
mkdir -p "$stage" "$out"
rsync -a --delete "$here/plugin/" "$stage/"
cd "$stage"
args=(claude plugin eval . --trust-plugin --no-publish --ablation none --runs 1 --model sonnet
      --allow-tools Bash Write --max-cost-usd 2 --keep-temp --json "$out/result.json" --report "$out/report.html")
aw=(AW_TOOLKIT="$stage" AW_STATE_DIR=.aw-state AW_PREAMBLE=off AW_GIT_MODE=readonly AW_RESULT=1 AW_PROBE_RUN="$mode")
case "$mode" in
  inherit)   env "${aw[@]}" AW_PROBE_TOKEN=probe-not-a-real-token "${args[@]}" ;;
  allowlist) env -i PATH="$PATH" TERM="${TERM:-dumb}" LANG="${LANG:-C.UTF-8}" HOME="$HOME" "${aw[@]}" "${args[@]}" ;;
  *) echo "usage: run.sh <inherit|allowlist> <out-dir>" >&2; exit 2 ;;
esac
