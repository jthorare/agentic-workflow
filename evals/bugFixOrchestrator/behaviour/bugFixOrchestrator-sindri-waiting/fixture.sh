#!/usr/bin/env bash
# Sindri block case: the shared fixture (two levels up, when the skill has one), then the two files the staged Result block reads
# (scripts/skill-evals.sh, rewrite_stage) so the run sees AW_RESULT_SKILL and AW_RESULT_FILE as a Sindri Step would.
set -eu
export AW_GIT_MODE=full # the setup commits; the agent runs after this process, still readonly (as aw_fixture_repo)
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
mkdir -p .aw-eval .aw-state
printf '%s\n' bugFixOrchestrator > .aw-eval/result-skill
printf '%s\n' "$PWD/.aw-state/sindri-result.json" > .aw-eval/result-file
# A tiny repo with one bug, so the skill has a checkout to stand in. Offline, no remote; the stub bugfix-state answers 0.
cat > calc.py <<'P'
def average(xs):
    return sum(xs) / (len(xs) + 1)
P
printf '# calc\n' > README.md
git init -q -b main
git add -A
git -c user.name=eval -c user.email=eval@example.com commit -q -m "initial calc"
git checkout -q -b sindri/inst-1/1/work
