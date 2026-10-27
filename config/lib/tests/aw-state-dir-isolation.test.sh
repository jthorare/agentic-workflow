#!/usr/bin/env bash
# Every hook and install script must derive its state defaults from
# AW_STATE_DIR (falling back to ~/.agentic-workflow only when it's unset),
# so a sandbox run that sets ONLY AW_STATE_DIR (plus CLAUDE_SETTINGS_FILE and
# CLAUDE_HOOKS_DIR for installs) never writes into the user's real
# ~/.agentic-workflow. Existing specific overrides (AW_OUTBOX_DIR, etc.)
# still win when set — that isn't touched here.
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../../.." && pwd)"

test_hooks_write_only_under_aw_state_dir() {
  local home state settings hooks_dir
  home="$(mktemp -d)"
  state="$(mktemp -d)/state"
  settings="$(mktemp -d)/settings.json"
  hooks_dir="$(mktemp -d)/hooks"
  echo '{}' > "$settings"

  # A HOME with a canary ~/.agentic-workflow that must stay untouched.
  mkdir -p "$home/.agentic-workflow"
  echo "canary" > "$home/.agentic-workflow/DO_NOT_TOUCH"

  local env=(HOME="$home" AW_STATE_DIR="$state" CLAUDE_SETTINGS_FILE="$settings" CLAUDE_HOOKS_DIR="$hooks_dir")

  # Run every state-touching hook once, feeding harmless/empty stdin so each
  # exits fast on its fail-open path rather than blocking on a real gate.
  for hook in context-guard external-write-guard probe-log turn-origin prompt-sort \
              record-teammate-name subagent-start-map send-gate outbox-flush done-gate; do
    local script="$ROOT/config/hooks/$hook.sh"
    [ -f "$script" ] || { echo "FAIL: missing hook script $script"; exit 1; }
    echo '{}' | env "${env[@]}" bash "$script" > /dev/null 2>&1 || true
  done

  [ -f "$home/.agentic-workflow/DO_NOT_TOUCH" ] || { echo "FAIL: canary file disappeared"; exit 1; }
  local extra
  extra="$(find "$home/.agentic-workflow" -newer "$home/.agentic-workflow/DO_NOT_TOUCH" 2>/dev/null || true)"
  [ -z "$extra" ] || { echo "FAIL: hooks wrote into HOME/.agentic-workflow: $extra"; exit 1; }

  echo "PASS: test_hooks_write_only_under_aw_state_dir"
}

test_installers_write_only_under_aw_state_dir() {
  local home state settings hooks_dir
  home="$(mktemp -d)"
  state="$(mktemp -d)/state"
  settings="$(mktemp -d)/settings.json"
  hooks_dir="$(mktemp -d)/hooks"
  echo '{}' > "$settings"
  mkdir -p "$home/.agentic-workflow"
  echo "canary" > "$home/.agentic-workflow/DO_NOT_TOUCH"

  local bin launch_agents
  bin="$(mktemp -d)/bin"
  launch_agents="$(mktemp -d)/LaunchAgents"
  local env=(HOME="$home" AW_STATE_DIR="$state" CLAUDE_SETTINGS_FILE="$settings" CLAUDE_HOOKS_DIR="$hooks_dir" \
             CLAUDE_LOCAL_BIN="$bin" AW_LAUNCH_AGENTS_DIR="$launch_agents" AW_SKIP_LAUNCHD=1 AW_JUDGE_BIN=/usr/bin/true)

  for installer in install-judge-hooks install-context-guard install-wake-gating install-scope-gate install-done-gate install-external-write-guard; do
    env "${env[@]}" bash "$ROOT/scripts/$installer.sh" > /dev/null 2>&1 || true
  done

  [ -d "$state" ] || { echo "FAIL: expected state to land under AW_STATE_DIR ($state)"; exit 1; }
  local extra
  extra="$(find "$home/.agentic-workflow" -newer "$home/.agentic-workflow/DO_NOT_TOUCH" 2>/dev/null || true)"
  [ -z "$extra" ] || { echo "FAIL: installers wrote into HOME/.agentic-workflow: $extra"; exit 1; }
  [ ! -e "$home/.local" ] || { echo "FAIL: an installer wrote into HOME/.local despite CLAUDE_LOCAL_BIN"; exit 1; }

  echo "PASS: test_installers_write_only_under_aw_state_dir"
}

# Broader guard: run every installer under the FULL sandbox envelope (every
# override this file exercises) with HOME pointed at a fresh, otherwise-empty
# temp dir, and separately snapshot the REAL, ambient $HOME (the one this
# test process actually started with) before and after. Catches both "this
# script ignores its HOME override" (fake HOME stops being empty) and "this
# script resolves some path against the ambient environment instead of the
# HOME it was given" (real HOME changes even though we passed a different
# HOME= to the child).
test_full_envelope_leaves_both_the_empty_home_and_the_real_home_untouched() {
  local fake_home state settings hooks_dir bin launch_agents real_home
  fake_home="$(mktemp -d)"
  state="$(mktemp -d)/state"
  settings="$(mktemp -d)/settings.json"
  hooks_dir="$(mktemp -d)/hooks"
  bin="$(mktemp -d)/bin"
  launch_agents="$(mktemp -d)/LaunchAgents"
  echo '{}' > "$settings"
  real_home="$HOME"

  # A full recursive diff of $HOME is impractical (permission-denied noise,
  # and the user's own concurrent activity touches files there constantly) — so
  # scope the "real HOME untouched" assertion to the specific locations this
  # fix's env overrides are meant to redirect away from: exactly the set
  # found by grepping every install script/hook for a $HOME-resolving write.
  # ~/.agentic-workflow is deliberately not watched: other sessions on the same box write there all the time, which
  # made this assertion flaky. The fake HOME stays empty below, which proves nothing was written to $HOME/.agentic-workflow.
  local real_targets=("$real_home/.local/bin" "$real_home/.claude/hooks" "$real_home/.claude/settings.json" \
                       "$real_home/Library/LaunchAgents")
  # mtime, size and name per file: GNU stat (Linux) takes -c, BSD stat (macOS) takes -f; on Linux `stat -f` is a
  # different (filesystem) command that fails, which under pipefail killed this test silently.
  local STAT_FILES
  if stat -c '%Y' / >/dev/null 2>&1; then STAT_FILES=(stat -c '%Y %s %n'); else STAT_FILES=(stat -f '%m %z %N'); fi
  local before after
  before="$(mktemp)"; after="$(mktemp)"
  : > "$before"
  for t in "${real_targets[@]}"; do
    if [ -e "$t" ]; then find "$t" -type f -exec "${STAT_FILES[@]}" {} + 2>/dev/null; fi   # `[ -e ] && …` as the loop's last command failed the pipeline (pipefail) when the last target is absent, as ~/Library/LaunchAgents is on Linux
  done | sort >> "$before"

  local env=(HOME="$fake_home" AW_STATE_DIR="$state" CLAUDE_SETTINGS_FILE="$settings" CLAUDE_HOOKS_DIR="$hooks_dir" \
             CLAUDE_LOCAL_BIN="$bin" AW_LAUNCH_AGENTS_DIR="$launch_agents" AW_SKIP_LAUNCHD=1 AW_JUDGE_BIN=/usr/bin/true)
  for installer in install-judge-hooks install-context-guard install-wake-gating install-scope-gate install-done-gate install-external-write-guard; do
    env "${env[@]}" bash "$ROOT/scripts/$installer.sh" > /dev/null 2>&1 || true
  done

  : > "$after"
  for t in "${real_targets[@]}"; do
    if [ -e "$t" ]; then find "$t" -type f -exec "${STAT_FILES[@]}" {} + 2>/dev/null; fi   # `[ -e ] && …` as the loop's last command failed the pipeline (pipefail) when the last target is absent, as ~/Library/LaunchAgents is on Linux
  done | sort >> "$after"
  diff "$before" "$after" > /dev/null \
    || { echo "FAIL: the real HOME's install-target locations changed during a fully-sandboxed installer run:"; diff "$before" "$after"; exit 1; }

  # .npm is npm's own cache/log dir, always keyed off $HOME regardless of
  # anything this fix controls — not part of the AW_STATE_DIR contract, so it's
  # excluded here rather than papered over as untouched.
  local leftover
  leftover="$(ls -A "$fake_home" 2>/dev/null | grep -v '^\.npm$' || true)"
  [ -z "$leftover" ] \
    || { echo "FAIL: expected the fake, otherwise-empty HOME to stay empty (besides npm's own cache), found: $leftover"; exit 1; }

  echo "PASS: test_full_envelope_leaves_both_the_empty_home_and_the_real_home_untouched"
}

test_hooks_write_only_under_aw_state_dir
test_installers_write_only_under_aw_state_dir
test_full_envelope_leaves_both_the_empty_home_and_the_real_home_untouched
echo "All aw-state-dir-isolation tests passed."
