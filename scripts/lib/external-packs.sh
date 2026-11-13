#!/usr/bin/env bash
# scripts/lib/external-packs.sh — fetch_external_pack, sourced by setup.sh (and by scripts/tests/external-packs.test.sh).
#
# fetch_external_pack <owner/repo> <pin> [skills-subdir]
#   pin           a commit SHA, or a literal HEAD / empty to follow the upstream default branch on every run;
#                 BAD:<KEY> (setup.sh's read_pin for an unparseable pin) keeps the current checkout and warns
#   skills-subdir optional path inside the clone; when given, only its */SKILL.md dirs are linked.
#                 Without it the pack's skills/ then .claude/skills/ is used.
# Reads EXTERNAL_DIR; appends each skill dir to AW_EXTERNAL_SKILL_DIRS (one per line).
# Clone base is ${AW_EXTERNAL_GIT_BASE:-https://github.com} (tests point it at file://).

# Fallbacks so the lib sources alone; setup.sh's providers/lib.sh versions win when already defined.
declare -F aw_dry >/dev/null 2>&1 || aw_dry() { [ "${AW_DRY_RUN:-0}" = "1" ]; }
declare -F aw_run >/dev/null 2>&1 || aw_run() { if aw_dry; then echo "  [dry-run] $*"; else "$@"; fi; }
: "${AW_EXTERNAL_SKILL_DIRS=}"

fetch_external_pack() {
  local repo="$1"          # e.g. pbakaus/impeccable
  local pin="${2:-HEAD}"   # commit SHA, HEAD to follow upstream, or BAD:<KEY> to keep the checkout
  local subdir="${3:-}"    # optional skills dir inside the clone
  local target
  target="$EXTERNAL_DIR/$(basename "$repo")"

  if aw_dry; then
    echo "  [dry-run] would clone/fetch $repo into $target and check out ${pin}${subdir:+ (skills in $subdir)}"
  else
    if [ ! -d "$target/.git" ]; then
      echo "  $repo: cloning..."
      git clone "${AW_EXTERNAL_GIT_BASE:-https://github.com}/$repo.git" "$target" 2>&1 \
        || { echo "  WARN: failed to clone $repo (non-fatal)"; return 0; }
    fi
    (cd "$target" && git fetch origin --quiet) || true
    if [[ "$pin" == BAD:* ]]; then
      echo "  WARN: ${pin#BAD:} in EXTERNAL_PINS.env is not a sha; keeping the current checkout"
    elif [ -n "$pin" ] && [ "$pin" != "HEAD" ]; then
      (cd "$target" && git checkout "$pin" --quiet) 2>/dev/null \
        || echo "  WARN: pin $pin not found in $repo, using current HEAD"
    else
      # Unpinned: move the checkout to the upstream default branch tip (a bare fetch never moves it).
      (cd "$target" \
        && { git rev-parse --verify --quiet origin/HEAD >/dev/null || git remote set-head origin --auto >/dev/null 2>&1; } \
        && git checkout --quiet --detach origin/HEAD) 2>/dev/null \
        || echo "  WARN: could not move $repo to the upstream default branch, using current checkout"
    fi
  fi

  # Find the skill source directory
  local skills_dir=""
  if [ -n "$subdir" ]; then
    [ -d "$target/$subdir" ] && skills_dir="$target/$subdir"
  elif [ -d "$target/skills" ]; then
    skills_dir="$target/skills"
  elif [ -d "$target/.claude/skills" ]; then
    skills_dir="$target/.claude/skills"
  fi
  if [ -z "$skills_dir" ]; then
    aw_dry || echo "  WARN: no skills directory found in $(basename "$repo")"
    return 0
  fi

  local skill_dir
  for skill_dir in "$skills_dir"/*/; do
    [ -f "$skill_dir/SKILL.md" ] || continue
    AW_EXTERNAL_SKILL_DIRS="$AW_EXTERNAL_SKILL_DIRS${skill_dir%/}
"
  done
  echo "  $repo: ready (${subdir:-$(basename "$skills_dir")} in $target)"
}
