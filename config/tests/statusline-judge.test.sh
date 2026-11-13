#!/usr/bin/env bash
# Tests for the judge segment in config/statusline.sh.
set -euo pipefail

# Temp-dir cleanup (#3262): every mktemp this suite makes is recorded and removed when it exits.
__aw_tmp_reg="$(command mktemp)"
mktemp() { local p; p="$(command mktemp "$@")" || return; printf '%s\n' "$p" >>"$__aw_tmp_reg"; printf '%s\n' "$p"; }
__aw_tmp_clean() { local p; while IFS= read -r p; do [ -z "$p" ] || { chmod -R u+rwX "$p" 2>/dev/null; rm -rf "$p"; }; done <"$__aw_tmp_reg"; rm -f "$__aw_tmp_reg"; }
trap __aw_tmp_clean EXIT
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}

FAKE_BIN="$(mktemp -d)"
cat > "$FAKE_BIN/judge" <<'EOF'
#!/usr/bin/env bash
[ "$1" = "health" ] || exit 1
cat "$JUDGE_HEALTH_FIXTURE"
EOF
chmod +x "$FAKE_BIN/judge"
export PATH="$FAKE_BIN:$PATH"
# statusline.sh has no --source-only guard; pull just the judge_segment()
# function body out of the file rather than sourcing the whole script (which
# would read stdin and exit). bash 3.2 on macOS doesn't reliably `source` a
# process-substitution pipe, so write it to a real temp file first.
FUNC_FILE="$(mktemp)"
sed -n '/^judge_segment()/,/^}/p' "$DIR/statusline.sh" > "$FUNC_FILE"
source "$FUNC_FILE"

export JUDGE_HEALTH_FIXTURE="$(mktemp)"
echo '{"status":"ok","failures24h":0}' > "$JUDGE_HEALTH_FIXTURE"
check "ok status" "$(judge_segment)" "judge ✓"

echo '{"status":"degraded","failures24h":4}' > "$JUDGE_HEALTH_FIXTURE"
check "degraded status shows the count" "$(judge_segment)" "judge ⚠ 4 failures"

rm -f "$FAKE_BIN/judge"
check "judge missing from PATH shows down" "$(judge_segment)" "judge ✗ down"

exit $fail
