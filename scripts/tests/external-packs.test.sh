#!/usr/bin/env bash
# Tests for scripts/lib/external-packs.sh (fetch_external_pack) and the unpinned ui-sdk pack wiring (#3458).
# Run: bash scripts/tests/external-packs.test.sh
# Everything runs against local git repos through AW_EXTERNAL_GIT_BASE=file://...; no network, no real state dir.
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
contains() { case "$2" in *"$3"*) echo "ok - $1" ;; *) echo "not ok - $1"; echo "  missing: $3"; echo "  in:      $2"; fail=1 ;; esac; }
lacks() { case "$2" in *"$3"*) echo "not ok - $1"; echo "  unexpected: $3"; fail=1 ;; *) echo "ok - $1" ;; esac; }

WORK="$(mktemp -d)"
trap 'find "$WORK" -delete' EXIT
export HOME="$WORK/home"; mkdir -p "$HOME"
export GIT_AUTHOR_NAME=t GIT_AUTHOR_EMAIL=t@example.invalid GIT_COMMITTER_NAME=t GIT_COMMITTER_EMAIL=t@example.invalid
export GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_SYSTEM=/dev/null
unset AW_DRY_RUN
export AW_EXTERNAL_GIT_BASE="file://$WORK/upstream"
export EXTERNAL_DIR="$WORK/external"; mkdir -p "$EXTERNAL_DIR"

# shellcheck source=../lib/external-packs.sh
source "$ROOT/scripts/lib/external-packs.sh" || { echo "not ok - lib sources"; exit 1; }

head_of() { git -C "$1" rev-parse HEAD; }
commit_all() { git -C "$1" add -A; git -C "$1" commit -q -m "$2"; }

# --- 1. ui layout: only the user skill is linked ---
U="$WORK/upstream/vercel-labs/ui.git"; mkdir -p "$U"; git -C "$U" init -q -b main
mkdir -p "$U/packages/core/skills/ui-sdk" "$U/.agents/skills/ui-sdk-write-code"
echo "# ui-sdk" > "$U/packages/core/skills/ui-sdk/SKILL.md"
echo "# contributor" > "$U/.agents/skills/ui-sdk-write-code/SKILL.md"
commit_all "$U" one
AW_EXTERNAL_SKILL_DIRS=""
fetch_external_pack "vercel-labs/ui" "HEAD" "packages/core/skills" >/dev/null 2>&1
contains "ui pack links the user skill" "$AW_EXTERNAL_SKILL_DIRS" "$EXTERNAL_DIR/ui/packages/core/skills/ui-sdk"
lacks "ui pack does not link contributor skills" "$AW_EXTERNAL_SKILL_DIRS" ".agents"

# --- 2. unpinned follows upstream ---
echo more >> "$U/log.txt"; commit_all "$U" two
fetch_external_pack "vercel-labs/ui" "HEAD" "packages/core/skills" >/dev/null 2>&1
check "unpinned pack moves to the new upstream commit" "$(head_of "$EXTERNAL_DIR/ui")" "$(head_of "$U")"

# --- 3. pinned stays ---
P="$WORK/upstream/acme/pinned.git"; mkdir -p "$P/skills/one"; git -C "$P" init -q -b main
echo "# one" > "$P/skills/one/SKILL.md"; commit_all "$P" first
PIN="$(head_of "$P")"
AW_EXTERNAL_SKILL_DIRS=""
fetch_external_pack "acme/pinned" "$PIN" >/dev/null 2>&1
echo x >> "$P/log.txt"; commit_all "$P" second
fetch_external_pack "acme/pinned" "$PIN" >/dev/null 2>&1
check "pinned pack stays at the pin" "$(head_of "$EXTERNAL_DIR/pinned")" "$PIN"
contains "pinned pack links skills/" "$AW_EXTERNAL_SKILL_DIRS" "$EXTERNAL_DIR/pinned/skills/one"

# --- 4. no subdir arg: .claude/skills fallback ---
L="$WORK/upstream/acme/legacy.git"; mkdir -p "$L/.claude/skills/two"; git -C "$L" init -q -b main
echo "# two" > "$L/.claude/skills/two/SKILL.md"; commit_all "$L" first
AW_EXTERNAL_SKILL_DIRS=""
fetch_external_pack "acme/legacy" "HEAD" >/dev/null 2>&1
contains "no subdir arg finds .claude/skills" "$AW_EXTERNAL_SKILL_DIRS" "$EXTERNAL_DIR/legacy/.claude/skills/two"

# --- 4b. a malformed pin keeps the checkout and warns ---
G="$WORK/upstream/acme/garbage.git"; mkdir -p "$G/skills/three"; git -C "$G" init -q -b main
echo "# three" > "$G/skills/three/SKILL.md"; commit_all "$G" first
OLD="$(head_of "$G")"
fetch_external_pack "acme/garbage" "$OLD" >/dev/null 2>&1
echo y >> "$G/log.txt"; commit_all "$G" second
out="$(fetch_external_pack "acme/garbage" "BAD:GARBAGE_PIN" 2>&1)"
check "a garbage pin leaves the clone at its old commit" "$(head_of "$EXTERNAL_DIR/garbage")" "$OLD"
contains "a garbage pin prints the WARN" "$out" "WARN: GARBAGE_PIN in EXTERNAL_PINS.env is not a sha; keeping the current checkout"
rp="$(sed -n '/^  read_pin() {/,/^  }/p' "$ROOT/setup.sh")"
contains "setup.sh read_pin returns BAD:<KEY> for a bad pin" "$rp" 'echo "BAD:$key"'

# --- 4c. ui-sdk lands in every provider's skills dir, the contributor skill in none ---
export TOOLKIT_DIR="$ROOT" AW_DRY_RUN=0
unset AW_STATE_ROOT AW_STATE_DIR
export CLAUDE_DIR="$WORK/pc/claude" CODEX_HOME="$WORK/pc/codex" CURSOR_DIR="$WORK/pc/cursor"
source "$ROOT/providers/lib.sh"
source "$ROOT/providers/claude/install.sh"; source "$ROOT/providers/codex/install.sh"; source "$ROOT/providers/cursor/install.sh"
MANAGED_SKILLS=(); DEPRECATED_SKILLS=()
AW_EXTERNAL_SKILL_DIRS=""
fetch_external_pack "vercel-labs/ui" "HEAD" "packages/core/skills" >/dev/null 2>&1
for pv in claude codex cursor; do
  d="$("${pv}_skills_dir")"
  aw_install_skills_into "$d" </dev/null >/dev/null 2>&1
  check "$pv gets a ui-sdk link" "$(readlink "$d/ui-sdk")" "$EXTERNAL_DIR/ui/packages/core/skills/ui-sdk"
  check "$pv gets no ui-sdk-write-code link" "$([ -e "$d/ui-sdk-write-code" ] || [ -L "$d/ui-sdk-write-code" ] && echo yes || echo no)" "no"
done

# --- 5. static checks ---
setup_sh="$ROOT/setup.sh"
call_line="$(grep -n 'fetch_external_pack "vercel-labs/ui" "HEAD" "packages/core/skills"' "$setup_sh" | head -1 | cut -d: -f1)"
if_line="$(grep -n 'if \[ -f "\$SCRIPT_DIR/EXTERNAL_PINS.env" \]' "$setup_sh" | head -1 | cut -d: -f1)"
fi_line="$(awk -v s="${if_line:-0}" 'NR>s && /^fi$/ {print NR; exit}' "$setup_sh")"
if [ -n "$call_line" ] && [ -n "$if_line" ] && [ -n "$fi_line" ] && { [ "$call_line" -lt "$if_line" ] || [ "$call_line" -gt "$fi_line" ]; }; then
  echo "ok - setup.sh fetches vercel-labs/ui outside the EXTERNAL_PINS.env block"
else
  echo "not ok - setup.sh fetches vercel-labs/ui outside the EXTERNAL_PINS.env block (call=$call_line if=$if_line fi=$fi_line)"; fail=1
fi
for f in EXTERNAL_PINS.env scripts/refresh-external-pins.sh; do
  if grep -qiE 'vercel-labs/ui|UI_PIN|UI_SDK' "$ROOT/$f"; then echo "not ok - $f does not pin vercel-labs/ui"; fail=1
  else echo "ok - $f does not pin vercel-labs/ui"; fi
done

exit "$fail"
