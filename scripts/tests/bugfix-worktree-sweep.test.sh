#!/usr/bin/env bash
# #1992: in an aw-meta lane (AW_META_TEAM and AW_META_LANE set) only meta removes worktrees, so /bugFixOrchestrator's removal steps
# apply outside a meta lane only. The lane path names each evaluated tree to meta as free to remove and leaves the how to meta: no sweep
# flag (`--include-no-pr` sweeps the whole repo, and still keeps a candidate branch with no PR and nothing pushed).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SKILL="$ROOT/skills/bugFixOrchestrator/SKILL.md"
fail=0
bad() { echo "FAIL: $*"; fail=1; }

grep -q 'AW_META_LANE' "$SKILL" || bad "SKILL.md never says how it knows it runs in a meta lane (AW_META_TEAM and AW_META_LANE)"
grep -qi 'free to remove' "$SKILL" || bad "SKILL.md never tells a lane to name its evaluated trees to meta as free to remove"
if grep -q -- '--include-no-pr' "$SKILL"; then bad "SKILL.md names a sweep flag (--include-no-pr); the lane names the trees and meta picks how to remove them"; fi

# Every removal instruction is limited to outside a meta lane, on its own line or the two before it.
while IFS=: read -r n _; do
  from=$(( n > 2 ? n - 2 : 1 ))
  sed -n "${from},${n}p" "$SKILL" | grep -qiE 'outside a meta lane|AW_META_LANE' || bad "SKILL.md:$n removes a worktree without limiting it to outside a meta lane (AW_META_LANE)"
done < <(grep -n 'git worktree remove' "$SKILL" || true)

[ "$fail" -eq 0 ] || exit 1
echo "PASS: bugFixOrchestrator leaves worktree removal to meta inside a meta lane"
