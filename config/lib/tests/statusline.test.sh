#!/usr/bin/env bash
# Tests for config/statusline.sh's Live column. Temp HOME / AW_STATE_DIR; fake `scorer` and
# `judge` on PATH. The statusline must read a cached snapshot, refresh it in the background,
# and never block on scorer or judge.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
SCRIPT="$ROOT/config/statusline.sh"
PASS=0
FAIL=0

ok() { PASS=$((PASS + 1)); echo "  PASS: $1"; }
bad() { FAIL=$((FAIL + 1)); echo "  FAIL: $1"; }
assert_contains() { if grep -qF -- "$2" <<<"$1"; then ok "$3"; else bad "$3 (missing: $2)"; echo "$1" | sed 's/^/      | /'; fi; }
assert_not_contains() { if grep -qF -- "$2" <<<"$1"; then bad "$3 (found: $2)"; else ok "$3"; fi; }

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
export HOME="$TMP/home"
export AW_STATE_DIR="$TMP/state"
mkdir -p "$HOME/.claude" "$TMP/bin" "$AW_STATE_DIR/scorer/live"
STRICT_PATH="/usr/bin:/bin:/opt/homebrew/bin:/usr/local/bin"
SID="sess-123"
CACHE="$AW_STATE_DIR/scorer/live/$SID.statusline.json"

# scorer: logs its argv, sleeps SCORER_SLEEP seconds, prints a snapshot.
cat >"$TMP/bin/scorer" <<'EOF'
#!/usr/bin/env bash
echo "$*" >>"$FAKE_LOG"
sleep "${SCORER_SLEEP:-0}"
echo '{"v":1,"usage":{"calls": 500,"callsOver200k":7},"judge":{"state":"ok","calls":4}}'
EOF
cat >"$TMP/bin/judge" <<'EOF'
#!/usr/bin/env bash
echo '{"status":"ok","failures24h":0}'
EOF
chmod +x "$TMP/bin/scorer" "$TMP/bin/judge"
export FAKE_LOG="$TMP/scorer.log"
: >"$FAKE_LOG"

now() { date +%s; }
write_cache() { # at calls over judgeCalls judge
  mkdir -p "$(dirname "$CACHE")"
  printf '{"at":%s,"calls":%s,"over200k":%s,"judgeCalls":%s,"judge":"%s","failures":0}\n' "$1" "$2" "$3" "$4" "$5" >"$CACHE"
}

input() { # session id
  jq -n --arg sid "$1" '{session_id:$sid, model:{display_name:"Claude Opus 5"}, workspace:{current_dir:"/tmp"},
    context_window:{used_percentage:70}, cost:{total_cost_usd:98.41, total_duration_ms:600000, total_api_duration_ms:300000,
    total_lines_added:10, total_lines_removed:2}, rate_limits:{five_hour:{used_percentage:40}, seven_day:{used_percentage:20}}}'
}

run() { # cols session-id [PATH]
  echo "$2" >/dev/null
  echo "$1" >"$HOME/.claude/terminal_width"
  input "$2" | AW_STATUSLINE_TTY="${AW_STATUSLINE_TTY-}" PATH="${3:-$TMP/bin:$STRICT_PATH}" bash "$SCRIPT"
}

echo "statusline Live column tests"

# --- width source: own terminal first; per-tty files, no shared state between windows ---
# Stub tty size lookup: "ttysA" is 200 columns, "ttysB" is 90, anything else has no size.
export TTY_COLS_STUB='case "$1" in ttysA) echo 200;; ttysB) echo 90;; esac'
mkdir -p "$HOME/.claude/terminal_width.d"
wrun() { # tty [debug]  (SID-based so the Live refresh stays quiet)
  input "$SID" | AW_STATUSLINE_DEBUG="${2:-}" AW_STATUSLINE_TTY="$1" AW_STATUSLINE_TTY_COLS_CMD="$TTY_COLS_STUB" \
    PATH="$TMP/bin:$STRICT_PATH" bash "$SCRIPT" 2>&1
}
echo 40 >"$HOME/.claude/terminal_width"          # stale legacy global
echo 40 >"$HOME/.claude/terminal_width.d/ttysA"  # stale per-tty file
w=$(wrun ttysA 1)
assert_contains "$w" "width: 200 (source: ancestor-tty)" "own tty width wins over stale files"
assert_contains "$w" "Live" "stale narrow files ignored: Live shown"
echo 120 >"$HOME/.claude/terminal_width.d/ttysZ"
w=$(wrun ttysZ 1)
assert_contains "$w" "width: 120 (source: terminal_width.d)" "tty without size: falls back to its own terminal_width.d file"
rm "$HOME/.claude/terminal_width.d/ttysZ"
w=$(wrun ttysZ 1)
assert_not_contains "$w" "terminal_width-legacy" "tty with no size and no file never reads the global file"
echo 120 >"$HOME/.claude/terminal_width"
w=$(wrun "" 1)
assert_contains "$w" "width: 120 (source: terminal_width-legacy)" "no ancestor tty at all: legacy global file"
assert_not_contains "$(wrun ttysA)" "statusline width:" "debug output is off by default"

# --- two concurrent sessions, different ttys, each renders its own tier ---
echo 200 >"$HOME/.claude/terminal_width.d/ttysB"   # a file for B must not leak into A or vice versa
wrun ttysA >"$TMP/outA" & wrun ttysB >"$TMP/outB" &
wait
outA=$(cat "$TMP/outA"); outB=$(cat "$TMP/outB")
assert_contains "$outA" "Live" "session A (200 cols) has the Live column"
assert_not_contains "$outB" "Live" "session B (90 cols) is narrow, even with a 200 file for its tty"
echo 50 >"$HOME/.claude/terminal_width.d/ttysA"
outA2=$(wrun ttysA); outB2=$(wrun ttysB)
[ "$outA2" = "$outA" ] && [ "$outB2" = "$outB" ] && ok "writes to one tty's file change neither session" || bad "cross-session leak"
: >"$FAKE_LOG"

# --- shell-integration template must not write from tool shells / non-tty ---
SI="$TMP/si.sh"
sed -n "/^ *cat > \"\$si_tmp\" << 'SHELL_EOF'/,/^SHELL_EOF/p" "$ROOT/providers/claude/install.sh" | sed '1d;$d' >"$SI"
[ -s "$SI" ] && ok "shell integration template extracted" || bad "shell integration template not found"
rm -f "$HOME"/.claude/terminal_width "$HOME"/.claude/terminal_width.d/* "$HOME"/.claude/shell_pid.d/* 2>/dev/null
rmdir "$HOME/.claude/terminal_width.d" "$HOME/.claude/shell_pid.d" 2>/dev/null
CLAUDECODE=1 COLUMNS=97 bash -i -c ". $SI" >/dev/null 2>&1 </dev/null
COLUMNS=97 bash -c ". $SI" >/dev/null 2>&1 </dev/null
COLUMNS=97 bash -i -c ". $SI" >/dev/null 2>&1 </dev/null
[ ! -e "$HOME/.claude/terminal_width.d" ] && [ ! -e "$HOME/.claude/terminal_width" ] \
  && ok "shell integration: CLAUDECODE / non-interactive / no tty write nothing" || bad "shell integration wrote without a tty or under CLAUDECODE"
if command -v python3 >/dev/null 2>&1; then
  # Real pty, interactive shell: writes only terminal_width.d/<tty>, never the global file.
  ptyrun() { env "$@" python3 - "$SI" <<'PY' >/dev/null 2>&1 || true
import os, pty, sys
pid, fd = pty.fork()
if pid == 0:
    os.execvp("bash", ["bash", "-i", "-c", ". " + sys.argv[1]])
try:
    while os.read(fd, 1024):
        pass
except OSError:
    pass
os.waitpid(pid, 0)
PY
  }
  ptyrun CLAUDECODE=1
  [ ! -e "$HOME/.claude/terminal_width.d" ] && ok "shell integration: interactive pty under CLAUDECODE=1 writes nothing" || bad "pty + CLAUDECODE=1 wrote a width file"
  unset CLAUDECODE; ptyrun COLUMNS=150
  n=$(ls "$HOME/.claude/terminal_width.d" 2>/dev/null | wc -l | tr -d ' ')
  [ "$n" = 1 ] && [ ! -e "$HOME/.claude/terminal_width" ] && [ "$(cat "$HOME"/.claude/terminal_width.d/* 2>/dev/null)" = 150 ] \
    && ok "shell integration: interactive pty writes terminal_width.d/<tty> only" || bad "interactive pty: $n per-tty files"
fi

# --- WIDE and MEDIUM show Live from a fresh cache; NARROW hides it ---
write_cache "$(now)" 1289 351 12 ok
wide="$(run 200 "$SID")"
assert_contains "$wide" "Live" "WIDE: header has Live"
assert_contains "$wide" "1289 calls · 351 >200k · judge ✓ 12" "WIDE: Live value from cache"
assert_contains "$wide" "Lines" "WIDE: Lines still shown"
medium="$(run 145 "$SID")"
assert_contains "$medium" "1289 calls · 351 >200k · judge ✓ 12" "MEDIUM: Live value from cache"
assert_not_contains "$medium" "Lines" "MEDIUM: no Lines"
narrow="$(run 90 "$SID")"
assert_not_contains "$narrow" "Live" "NARROW: no Live header"
assert_not_contains "$narrow" "calls" "NARROW: no Live value"
assert_contains "$narrow" "Context" "NARROW: existing columns kept"
[ ! -s "$FAKE_LOG" ] && ok "fresh cache starts no refresh" || bad "fresh cache started a refresh: $(cat "$FAKE_LOG")"

# --- existing columns unchanged (the line is the old line plus the Live suffix) ---
line1="$(sed -n 1p <<<"$wide")"
assert_contains "$line1" "5h Usage  │ 7d Usage  │ Context         │ Model      │ Branch          │ Cost    │ Time    │ Cache │ API  │ Lines    " "WIDE: existing header unchanged"
line2="$(sed -n 2p <<<"$wide")"
assert_contains "$line2" "40% " "WIDE: 5h usage"
assert_contains "$line2" '$98.41' "WIDE: cost"
assert_contains "$line2" "+10 -2" "WIDE: lines"
assert_contains "$line2" "50%" "WIDE: api"
assert_contains "$line2" "Opus 5" "WIDE: model"

# --- judge glyphs ---
write_cache "$(now)" 5 0 3 degraded
assert_contains "$(run 200 "$SID")" "5 calls · 0 >200k · judge ⚠ 3" "degraded judge shows the warning glyph"
write_cache "$(now)" 5 0 3 down
assert_contains "$(run 200 "$SID")" "judge ✗ 3" "down judge shows the cross glyph"

# --- stale cache: output is immediate, refresh runs in the background ---
write_cache "$(( $(now) - 120 ))" 1289 351 12 ok
: >"$FAKE_LOG"
start=$(now)
stale="$(SCORER_SLEEP=3 run 200 "$SID")"
elapsed=$(( $(now) - start ))
assert_contains "$stale" "1289 calls · 351 >200k · judge ✓ 12" "stale: prints the cached value"
[ "$elapsed" -le 1 ] && ok "stale: statusline returned in ${elapsed}s (did not wait for the 3s scorer)" || bad "stale: statusline blocked ${elapsed}s"
deadline=$(( $(now) + 10 ))
while [ "$(now)" -lt "$deadline" ] && ! grep -q '"calls": 500' "$CACHE" 2>/dev/null; do sleep 0.2; done
if grep -q '"calls": 500' "$CACHE"; then ok "stale: background refresh rewrote the cache"; else bad "stale: cache never refreshed"; fi
assert_contains "$(cat "$FAKE_LOG")" "live --session $SID --cwd /tmp --json" "refresh passes --session and --cwd"
assert_contains "$(run 200 "$SID")" "500 calls · 7 >200k · judge ✓ 4" "next render shows the refreshed numbers"
[ ! -d "$CACHE.lock" ] && ok "refresh lock released" || bad "refresh lock left behind"

# --- no cache yet: shows --, then a background refresh fills it ---
rm "$CACHE"
: >"$FAKE_LOG"
first="$(run 200 "$SID")"
assert_contains "$(sed -n 2p <<<"$first")" "│ --" "no cache: Live shows --"
deadline=$(( $(now) + 10 ))
while [ "$(now)" -lt "$deadline" ] && [ ! -f "$CACHE" ]; do sleep 0.2; done
[ -f "$CACHE" ] && ok "no cache: background refresh created it" || bad "no cache: never created"

# --- missing scorer: -- and no refresh ---
mkdir -p "$TMP/emptybin"
ln -sf "$(command -v jq)" "$TMP/emptybin/jq"
rm "$CACHE"
: >"$FAKE_LOG"
nos="$(run 200 "$SID" "$TMP/emptybin:$STRICT_PATH")"
assert_contains "$(sed -n 2p <<<"$nos")" "│ --" "missing scorer: Live shows --"
assert_not_contains "$nos" "calls" "missing scorer: no counts"
sleep 1
[ ! -f "$CACHE" ] && ok "missing scorer: no cache written" || bad "missing scorer: cache appeared"

# --- no scorer, judge installed: the judge segment still shows, from a cached `judge health` ---
mkdir -p "$TMP/judgeonly"
ln -sf "$(command -v jq)" "$TMP/judgeonly/jq"
cp "$TMP/bin/judge" "$TMP/judgeonly/judge"
JCACHE="$AW_STATE_DIR/statusline/judge-health.json"
rm -f "$JCACHE"
jo="$(run 200 "$SID" "$TMP/judgeonly:$STRICT_PATH")"
assert_contains "$(sed -n 2p <<<"$jo")" "│ --" "judge only, no cache: Live shows --"
deadline=$(( $(now) + 10 ))
while [ "$(now)" -lt "$deadline" ] && [ ! -f "$JCACHE" ]; do sleep 0.2; done
[ -f "$JCACHE" ] && ok "judge only: background refresh wrote the judge cache" || bad "judge only: no judge cache"
assert_contains "$(run 200 "$SID" "$TMP/judgeonly:$STRICT_PATH")" "judge ✓" "judge only: next render shows the judge segment"
assert_not_contains "$(run 200 "$SID" "$TMP/judgeonly:$STRICT_PATH")" "calls" "judge only: no scorer counts"
printf '{"at":%s,"judge":"degraded","failures":3}\n' "$(now)" >"$JCACHE"
assert_contains "$(run 200 "$SID" "$TMP/judgeonly:$STRICT_PATH")" "judge ⚠" "judge only: degraded glyph from the cache"
[ ! -f "$CACHE" ] && ok "judge only: no scorer cache written" || bad "judge only: scorer cache appeared"

# --- negative caching: a failing scorer is not re-run on every render ---
mkdir -p "$TMP/failbin"
printf '#!/usr/bin/env bash\necho "$*" >>"$FAKE_LOG"\nexit 1\n' >"$TMP/failbin/scorer"
cp "$TMP/bin/judge" "$TMP/failbin/judge"
chmod +x "$TMP/failbin/scorer" "$TMP/failbin/judge"
rm "$CACHE" 2>/dev/null; : >"$FAKE_LOG"
run 200 "$SID" "$TMP/failbin:$STRICT_PATH" >/dev/null
deadline=$(( $(now) + 10 ))
while [ "$(now)" -lt "$deadline" ] && [ ! -f "$CACHE" ]; do sleep 0.2; done
sleep 0.5
[ -f "$CACHE" ] && ok "failed refresh writes a stub cache" || bad "no stub cache after failure"
fail2="$(run 200 "$SID" "$TMP/failbin:$STRICT_PATH")"
sleep 1
assert_contains "$(sed -n 2p <<<"$fail2")" "│ --" "stub renders --"
[ "$(wc -l <"$FAKE_LOG" | tr -d ' ')" = 1 ] && ok "second render within TTL does not call scorer again" || bad "scorer invoked $(wc -l <"$FAKE_LOG") times"

# --- non-numeric failures24h still renders ---
printf '#!/usr/bin/env bash\necho '"'"'{"status":"degraded","failures24h":"lots"}'"'"'\n' >"$TMP/bin/judge"
rm "$CACHE"
run 200 "$SID" >/dev/null
deadline=$(( $(now) + 10 ))
while [ "$(now)" -lt "$deadline" ] && ! grep -q '"calls": 500' "$CACHE" 2>/dev/null; do sleep 0.2; done
assert_contains "$(run 200 "$SID")" "500 calls · 7 >200k · judge ⚠ 4" "non-numeric failures24h: still renders"
printf '#!/usr/bin/env bash\necho '"'"'{"status":"ok","failures24h":0}'"'"'\n' >"$TMP/bin/judge"

# --- long values are cut to the column width ---
write_cache "$(now)" 12345 3512 123 degraded
long="$(sed -n 2p <<<"$(run 200 "$SID")")"
assert_contains "$long" "12345 calls · 3512 >200k · judge ⚠ " "long value drops the judge count first"
assert_not_contains "$long" "judge ⚠ 123" "long value does not overflow"
assert_not_contains "$long" "…" "dropping the count needs no cut"
write_cache "$(now)" 12345678 987654 123 degraded
assert_contains "$(sed -n 2p <<<"$(run 200 "$SID")")" "12345678 c · 987654 >200k · judge ⚠ " "then calls shortens to c"
write_cache "$(now)" 123456789 9876543 123 degraded
cut="$(sed -n 2p <<<"$(run 200 "$SID")")"
assert_contains "$cut" "…" "only then is the value cut"
echo "$cut" | iconv -f UTF-8 -t UTF-8 >/dev/null 2>&1 && ok "cut value is valid UTF-8" || bad "cut value is invalid UTF-8"

# --- multibyte: widths are characters, under the C locale and a UTF-8 one ---
vw() { printf '%s' "$1" | LC_ALL=C tr -d '\200-\277' | wc -c | tr -d ' '; }
for loc in C en_US.UTF-8; do
  write_cache "$(now)" 1346 370 6 degraded
  out="$(LC_ALL=$loc run 200 "$SID")"
  echo "$out" | iconv -f UTF-8 -t UTF-8 >/dev/null 2>&1 && ok "[$loc] output is valid UTF-8" || bad "[$loc] invalid UTF-8"
  assert_contains "$out" "1346 calls · 370 >200k · judge ⚠ 6" "[$loc] full value renders untruncated"
  h="$(sed 's/\x1b\[[0-9;]*m//g' <<<"$out" | sed -n 1p)"; v="$(sed 's/\x1b\[[0-9;]*m//g' <<<"$out" | sed -n 2p)"
  lh="${h##*│ }"; lv="${v##*│ }"
  [ "$(vw "$lh")" = "$(vw "$lv")" ] && ok "[$loc] Live header and value have the same visible width" || bad "[$loc] widths differ: $(vw "$lh") vs $(vw "$lv")"
  [ "$(vw "$h")" = "$(vw "$v")" ] && ok "[$loc] whole header and value rows align" || bad "[$loc] rows differ: $(vw "$h") vs $(vw "$v")"
  write_cache "$(now)" 123456789 9876543 123 degraded
  echo "$(LC_ALL=$loc run 200 "$SID")" | iconv -f UTF-8 -t UTF-8 >/dev/null 2>&1 && ok "[$loc] cut value is valid UTF-8" || bad "[$loc] cut invalid UTF-8"
done

# --- first render has no numbers, the next one does ---
rm "$CACHE" 2>/dev/null
first="$(LC_ALL=C run 200 "$SID")"
assert_contains "$(sed -n 2p <<<"$first")" "│ --" "first render without cache shows --"
deadline=$(( $(now) + 10 ))
while [ "$(now)" -lt "$deadline" ] && ! grep -q '"calls": 500' "$CACHE" 2>/dev/null; do sleep 0.2; done
assert_contains "$(LC_ALL=C run 200 "$SID")" "500 calls · 7 >200k · judge ✓ 4" "next render shows numbers"

# --- hostile session ids never reach a path or the scorer ---
: >"$FAKE_LOG"
for hostile in '../../evil' 'a/b' '$(touch pwned)' 'x y' ''; do
  out="$(run 200 "$hostile")"
  assert_contains "$(sed -n 2p <<<"$out")" "│ --" "hostile id '$hostile': Live shows --"
done
sleep 1
[ ! -s "$FAKE_LOG" ] && ok "hostile ids: scorer never called" || bad "hostile ids reached scorer: $(cat "$FAKE_LOG")"
[ -z "$(find "$TMP" -name 'evil*' -o -name pwned -o -name 'a' 2>/dev/null)" ] && ok "hostile ids: nothing written outside the cache dir" || bad "hostile id wrote a file"

# --- empty stdin still prints the placeholder header, Live included at WIDE ---
echo 200 >"$HOME/.claude/terminal_width"
empty="$(echo '' | AW_STATUSLINE_TTY= PATH="$TMP/bin:$STRICT_PATH" bash "$SCRIPT")"
assert_contains "$empty" "Lines     │ Live" "fallback WIDE: Live header present"
echo 90 >"$HOME/.claude/terminal_width"
empty="$(echo '' | AW_STATUSLINE_TTY= PATH="$TMP/bin:$STRICT_PATH" bash "$SCRIPT")"
assert_not_contains "$empty" "Live" "fallback NARROW: no Live"

# --- the 5h and 7d reset times render (GNU date on Linux, BSD date on macOS) ---
echo 200 >"$HOME/.claude/terminal_width"
reset_out="$(jq -n '{session_id:"sess-reset", model:{display_name:"M"}, workspace:{current_dir:"/tmp"},
  rate_limits:{five_hour:{used_percentage:40, resets_at:1700000000}, seven_day:{used_percentage:20, resets_at:1700000000}}}' \
  | TZ=UTC LC_ALL=C AW_STATUSLINE_TTY= PATH="$TMP/bin:$STRICT_PATH" bash "$SCRIPT")"
assert_contains "$reset_out" "40% 10pm" "5h reset time shows as a 12-hour clock"
assert_contains "$reset_out" "20% Tue" "7d reset time shows as a weekday"

echo ""
echo "statusline: $PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
