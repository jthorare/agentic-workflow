#!/usr/bin/env bash
# Sindri block case: the shared fixture (two levels up, when the skill has one), then the two files the staged Result block reads
# (scripts/skill-evals.sh, rewrite_stage) so the run sees AW_RESULT_SKILL and AW_RESULT_FILE as a Sindri Step would.
set -eu
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
bash "$here/../../fixture.sh" || exit 1
mkdir -p .aw-eval .aw-state
printf '%s\n' addressReview > .aw-eval/result-skill
printf '%s\n' "$PWD/.aw-state/sindri-result.json" > .aw-eval/result-file
