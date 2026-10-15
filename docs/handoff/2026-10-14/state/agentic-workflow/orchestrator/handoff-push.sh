#!/usr/bin/env bash
# For each lane worktree: commit uncommitted work as a WIP commit (no hooks), check its files with the denylist,
# then push HEAD to origin handoff/<branch>. Never touches the real PR branches. DRY=1 prints only.
set -u
REPO="${AW_REPO:-$HOME/repos/agentic-workflow}"
export AW_DENYLIST="${AW_DENYLIST:-file:$HOME/.agentic-workflow/denylist.txt}"
cd "$REPO" || exit 1
git worktree list --porcelain | awk '/^worktree /{w=$2} /^branch /{print w, $2}' | sed 's#refs/heads/##' | while read -r w b; do
  case "$w" in "$REPO"|*/orchestrator/gate-*) continue ;; esac
  dirty=$(git -C "$w" status --porcelain | wc -l | tr -d ' ')
  remote_has=0; git -C "$w" fetch -q origin "$b" 2>/dev/null && [ "$(git -C "$w" rev-parse HEAD)" = "$(git -C "$w" rev-parse FETCH_HEAD)" ] && remote_has=1
  if [ "$dirty" = 0 ] && [ "$remote_has" = 1 ]; then echo "SKIP clean+pushed  $b"; continue; fi
  if [ "${DRY:-0}" = 1 ]; then echo "WOULD handoff      $b (dirty=$dirty pushed=$remote_has)"; continue; fi
  if [ "$dirty" != 0 ]; then
    big=$(git -C "$w" status --porcelain -uall | awk '{print $NF}' | while read -r f; do [ -f "$w/$f" ] && [ "$(wc -c < "$w/$f")" -gt 5000000 ] && echo "$f"; done)
    if [ -n "$big" ]; then echo "STOP big files in $b: $big"; continue; fi
    git -C "$w" add -A && git -C "$w" -c user.name=jthorare -c user.email=jthorare@users.noreply.github.com commit -q --no-verify -m "wip(handoff): uncommitted lane work at the machine move, 2026-10-14

Not reviewed or gated; the next lane orchestrator resumes from here.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>" || { echo "FAIL commit $b"; continue; }
    files=$(git -C "$w" diff --name-only HEAD~1 HEAD | while read -r f; do [ -f "$w/$f" ] && echo "$w/$f"; done)
    if [ -n "$files" ]; then out=$(echo "$files" | xargs bash "$REPO/scripts/denylist-check.sh" 2>&1); rc=$?; if [ $rc -ne 0 ]; then echo "STOP denylist $b rc=$rc: $(echo "$out" | tail -1)"; continue; fi; fi
  fi
  git -C "$w" push -q origin "HEAD:refs/heads/handoff/$b" && echo "PUSHED handoff/$b $(git -C "$w" rev-parse --short=8 HEAD) dirty-was=$dirty"
done
