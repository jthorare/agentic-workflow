#!/usr/bin/env bash
# Stage the pilot plugin outside any git repo, then print the staged path.
# Why: `claude plugin eval` refuses a plugin whose git dir registers many linked worktrees
# ("a git directory of the plugin registers more linked worktrees than can be screened").
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
stage="${1:-${AW_EVAL_STAGE:-/tmp/aw-pilot-stage}}"
mkdir -p "$stage"
rsync -a --delete --exclude results/ "$here/.claude-plugin/" "$stage/.claude-plugin/"
rsync -aL --delete "$here/../skills/" "$stage/skills/"
rsync -a --delete --exclude results/ "$here/evals/" "$stage/evals/"
rsync -a --delete "$here/stubs/" "$stage/stubs/"
mkdir -p "$stage/evals/_lib"
rsync -a --delete "$here/../config/voice/" "$stage/evals/_lib/voice/"
echo "$stage"
