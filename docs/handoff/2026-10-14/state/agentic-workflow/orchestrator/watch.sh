#!/usr/bin/env bash
# Emits one line per change in open PRs or lane branches; silent otherwise.
REPO=/Users/thor/repos/agentic-workflow
STATE=/Users/thor/.agentic-workflow/orchestrator/watch-state
mkdir -p "$STATE"; cd "$REPO" || exit 1
snap() {
  gh pr list --state open --limit 50 --json number,title,isDraft,mergeable,headRefOid,reviewDecision \
    --jq '.[] | "#\(.number) draft=\(.isDraft) \(.mergeable) head=\(.headRefOid[0:8]) \(.title[0:50])"' 2>/dev/null | sort
  gh pr list --state merged --limit 10 --json number,title --jq '.[] | "merged #\(.number) \(.title[0:50])"' 2>/dev/null | sort
  git fetch -q --prune origin 2>/dev/null
  git for-each-ref --format='branch %(refname:short) %(objectname:short)' 'refs/remotes/origin/feat/sindri-*' 'refs/remotes/origin/feat/ctx-*' | sort
}
snap > "$STATE/prev"
while true; do
  sleep 120
  snap > "$STATE/cur" || continue
  [ -s "$STATE/cur" ] || continue
  # Lines that are new or changed, reduced to one event per PR/branch.
  comm -13 "$STATE/prev" "$STATE/cur" | sed 's/^/CHANGE /'
  comm -23 "$STATE/prev" "$STATE/cur" | grep -E '^#' | awk '{print "GONE " $1}' | while read l; do
    n=$(echo "$l" | awk '{print $2}'); grep -q "^$n " "$STATE/cur" || echo "$l (closed or merged)"; done
  mv "$STATE/cur" "$STATE/prev"
  free=$(df -g /System/Volumes/Data | awk 'NR==2{print $4}')
  if [ "$free" -lt 5 ] && [ ! -f "$STATE/disklow" ]; then echo "DISK LOW: $free GB free"; touch "$STATE/disklow"; fi
  if [ "$free" -ge 8 ] && [ -f "$STATE/disklow" ]; then echo "DISK OK again: $free GB free"; rm -f "$STATE/disklow"; fi
done
