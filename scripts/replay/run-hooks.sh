#!/usr/bin/env bash
# scripts/replay/run-hooks.sh — replays fixture cases through the real hooks (#4257, docs/hooks/replay.md "Replay as data").
#
#   scripts/replay/run-hooks.sh <fixtures-dir|fixture.json>... [--out results.jsonl] [--strict] [--hooks-dir DIR] [--keep DIR]
#
# Per case it makes a fresh temp HOME, AW_STATE_DIR and root, builds the case's world, then pipes each hook event's
# payload on stdin to config/hooks/<script> (Claude) or config/hooks/adapters/<provider>.sh config/hooks/<script>
# (Codex, Cursor). The payload is data: nothing evals it. Its cwd and workspace_roots are set to the case's root (the
# adapters cd there), unless they already point under it. The hook runs under `env -i` with only the case's own paths,
# AW_REPLAY=1, the fake judge, and the Jev URL and proxies on a dead loopback port. PATH is the case's shims (canaries for
# gh curl claude codex cursor-agent ssh, git for push fetch pull clone ls-remote, the fake judge, a clock-aware date),
# then the world's stubs, then /usr/bin:/bin (and jq's own dir). A call to a canary fails its case.
#
# Output: one JSON line per scored event (an expect row), and per event with a finding, to --out (default stdout):
#   {fixture, provider, scenario, verified, event, hook, decision, reasonCode, exit, hookMs, judgeCalls, judgeSource,
#    failopen, expected, status, reason}
# status is pass, fail, notCovered (the case has pendingHook) or unsupported (expect.unsupported). A summary line goes
# to stderr. --strict fails an event whose judge call had no recorded verdict. --hooks-dir replaces config/hooks for the
# canonical scripts (tests); the adapters are always the repo's. --keep DIR keeps each case's dir under DIR.
# Exit: 0 every line passed (notCovered and unsupported count as passed), 1 a line failed, 2 usage or a bad fixture
# (checked for every fixture before any case runs; a fixture under $AW_STATE_DIR/replay/raw/ is refused).
set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
ADAPTERS="$REPO/config/hooks/adapters"
HOOKS="$REPO/config/hooks"
OUT=""
STRICT=0
KEEP=""
INPUTS=()

usage() {
  echo "usage: $0 <fixtures-dir|fixture.json>... [--out results.jsonl] [--strict] [--hooks-dir DIR] [--keep DIR]" >&2
  exit 2
}
die() { echo "replay: $*" >&2; exit 2; }

while [ $# -gt 0 ]; do
  case "$1" in
    --out) [ $# -ge 2 ] || usage; OUT="$2"; shift 2 ;;
    --strict) STRICT=1; shift ;;
    --hooks-dir) [ $# -ge 2 ] || usage; HOOKS="$2"; shift 2 ;;
    --keep) [ $# -ge 2 ] || usage; KEEP="$2"; shift 2 ;;
    -h | --help) usage ;;
    -*) usage ;;
    *) INPUTS+=("$1"); shift ;;
  esac
done
[ "${#INPUTS[@]}" -gt 0 ] || usage
command -v jq > /dev/null 2>&1 || die "jq is not on PATH"
[ -d "$HOOKS" ] || die "no hooks dir $HOOKS"
HOOKS="$(cd "$HOOKS" && pwd)"

REAL_GIT="$(command -v git 2>/dev/null || true)"
REAL_DATE="$(command -v date)"
JQ_DIR="$(dirname "$(command -v jq)")"
BASE_PATH="/usr/bin:/bin"
case ":$BASE_PATH:" in *":$JQ_DIR:"*) ;; *) BASE_PATH="$BASE_PATH:$JQ_DIR" ;; esac
TIMEOUT="$(command -v timeout 2>/dev/null || true)"
DEAD_URL="http://127.0.0.1:9"

# The raw transcripts' dir: never replayed from (they are not sanitized).
RAW_DIR="${AW_STATE_DIR:-$HOME/.agentic-workflow}/replay/raw"
RAW_REAL=""
[ -d "$RAW_DIR" ] && RAW_REAL="$(cd "$RAW_DIR" && pwd -P)"

# --- Collect and check every fixture before running any. ---
FIXTURES=()
for in in "${INPUTS[@]}"; do
  if [ -d "$in" ]; then
    while IFS= read -r f; do FIXTURES+=("$f"); done < <(find "$in" -name '*.json' -type f ! -path '*/verdicts/*' | LC_ALL=C sort)
  elif [ -f "$in" ]; then
    FIXTURES+=("$in")
  else
    die "no such fixture or dir: $in"
  fi
done
[ "${#FIXTURES[@]}" -gt 0 ] || die "no fixtures found in: ${INPUTS[*]}"

# verdicts_path <fixture>: the case's verdicts file (relative to tests/fixtures/replay/<provider>/), or empty.
verdicts_path() {
  local rel
  rel="$(jq -r '.judge.verdicts // empty' "$1")"
  [ -n "$rel" ] || return 0
  printf '%s/%s\n' "$(cd "$(dirname "$1")/.." && pwd)" "$rel"
}

for f in "${FIXTURES[@]}"; do
  real="$(cd "$(dirname "$f")" && pwd -P)/$(basename "$f")"
  if [ -n "$RAW_REAL" ]; then
    case "$real" in "$RAW_REAL"/*) die "$f: refused, it is under $RAW_DIR (raw transcripts are never replayed)" ;; esac
  fi
  jq -e '.schema == "aw-replay-fixture/1" and (.provider | IN("claude", "codex", "cursor"))
         and (.events | type == "array") and (.expect | type == "array")' "$f" > /dev/null 2>&1 \
    || die "$f: not an aw-replay-fixture/1 case (schemas/replay-fixture.schema.json)"
  while IFS= read -r s; do
    case "$s" in [a-z0-9]*.sh) ;; *) die "$f: bad script name '$s'" ;; esac
    case "$s" in */*) die "$f: bad script name '$s'" ;; esac
    [ -f "$HOOKS/$s" ] || die "$f: no hook script $HOOKS/$s"
  done < <(jq -r '.events[] | select(has("hook")) | .script // ""' "$f")
  v="$(verdicts_path "$f")"
  [ -z "$v" ] || [ -f "$v" ] || die "$f: no verdicts file $v"
done

if [ -n "$OUT" ]; then
  : > "$OUT" || die "cannot write $OUT"
  emit() { printf '%s\n' "$1" >> "$OUT"; }
else
  emit() { printf '%s\n' "$1"; }
fi
if [ -n "$KEEP" ]; then mkdir -p "$KEEP" || die "cannot make $KEEP"; KEEP="$(cd "$KEEP" && pwd)"; fi

CASE=""
cleanup() { [ -z "$CASE" ] || [ -n "$KEEP" ] || rm -rf -- "$CASE"; }
trap cleanup EXIT

now_ms() {
  if [ -n "${EPOCHREALTIME:-}" ]; then local t="${EPOCHREALTIME/[.,]/}"; printf '%s\n' "$((t / 1000))"
  else printf '%s\n' "$(($("$REAL_DATE" +%s) * 1000))"; fi
}
lines() { [ -f "$1" ] && wc -l < "$1" | tr -d ' ' || echo 0; }

# make_shims: the canaries, git, judge and date, all first on PATH.
make_shims() {
  local n
  for n in gh curl claude codex cursor-agent ssh; do
    printf '#!/bin/sh\nprintf "%%s %%s\\n" "%s" "$*" >> "%s/CANARY"\nexit 99\n' "$n" "$ROOT" > "$SHIMS/$n"
  done
  cat > "$SHIMS/git" << EOF
#!/bin/sh
for a in "\$@"; do
  case "\$a" in push|fetch|pull|clone|ls-remote) printf '%s %s\n' git "\$*" >> "$ROOT/CANARY"; exit 99 ;; esac
done
[ -n "$REAL_GIT" ] || exit 127
exec "$REAL_GIT" "\$@"
EOF
  printf '#!/bin/sh\nexec "%s" "%s/fake-judge.sh" "$@"\n' "$BASH" "$HERE" > "$SHIMS/judge"
  # date: the fake clock. An offset only applies to a plain `date [+FORMAT]`; -d, -r and -s pass through.
  cat > "$SHIMS/date" << EOF
#!/bin/sh
off=\$(cat "$CASE/clock" 2>/dev/null || echo 0)
for a in "\$@"; do case "\$a" in -d*|--date*|-r*|--reference*|-s*|--set*|-f*|--file*) exec "$REAL_DATE" "\$@" ;; esac; done
[ "\$off" = 0 ] && exec "$REAL_DATE" "\$@"
t=\$(( \$("$REAL_DATE" +%s) + off ))
if "$REAL_DATE" --version >/dev/null 2>&1; then exec "$REAL_DATE" -d "@\$t" "\$@"; else exec "$REAL_DATE" -r "\$t" "\$@"; fi
EOF
  chmod +x "$SHIMS"/*
}

# apply_world <world json>: stubs on PATH, empty files under the root, auth file mtimes under HOME.
apply_world() {
  local w="$1" c p host mt
  while IFS= read -r c; do
    case "$c" in "" | */* | .*) continue ;; esac
    printf '#!/bin/sh\nexit 0\n' > "$STUBS/$c" && chmod +x "$STUBS/$c"
  done < <(printf '%s' "$w" | jq -r '.commandsOnPath[]? // empty')
  while IFS= read -r p; do
    case "$p" in __ROOT__/*) ;; *) continue ;; esac
    case "/$p/" in */../*) continue ;; esac
    p="$ROOT/${p#__ROOT__/}"
    mkdir -p "$(dirname "$p")" && { [ -e "$p" ] || : > "$p"; }
  done < <(printf '%s' "$w" | jq -r '.files[]? // empty')
  while IFS=$'\t' read -r host mt; do
    [ -n "$host" ] || continue
    case "$host" in */* | .*) continue ;; esac
    case "$host" in github.com) p="$CASE_HOME/.config/gh/hosts.yml" ;; *) p="$CASE_HOME/.config/aw-replay/auth/$host" ;; esac
    mkdir -p "$(dirname "$p")" && { [ -e "$p" ] || : > "$p"; }
    touch -d "@$mt" "$p" 2>/dev/null || touch -t "$("$REAL_DATE" -r "$mt" +%Y%m%d%H%M.%S)" "$p"
  done < <(printf '%s' "$w" | jq -r '.authFileMtime // {} | to_entries[] | "\(.key)\t\(.value)"')
}

# write_stdin <event json> <file>: the hook's stdin for one event.
write_stdin() {
  local ev="$1" in="$2" suffix=""
  [ "$RESETS" -eq 0 ] || suffix="-r$RESETS"
  if printf '%s' "$ev" | jq -e 'has("payload")' > /dev/null; then
    printf '%s' "$ev" | jq -jc --arg root "$ROOT" --arg suf "$suffix" '
      (.padBytes // 0) as $pad
      | .payload
      | walk(if type == "string" then gsub("__ROOT__"; $root) else . end)
      | if (.cwd | type) == "string" and ((.cwd | startswith($root)) | not) then .cwd = $root else . end
      | if (.workspace_roots | type) == "array" then .workspace_roots |= map(if type == "string" and startswith($root) then . else $root end) else . end
      | if $suf != "" then
          (if (.session_id | type) == "string" then .session_id += $suf else . end)
          | (if (.conversation_id | type) == "string" then .conversation_id += $suf else . end)
        else . end
      | if $pad > 0 then del(._pad) else . end' > "$in"
    pad="$(printf '%s' "$ev" | jq -r '.padBytes // 0')"
    if [ "$pad" -gt 0 ]; then
      # ._pad = N x characters, appended as text: jq builds a 2 MB string slowly.
      local body sep=","
      body="$(cat "$in")"; body="${body%\}}"
      [ "$body" != "{" ] || sep=""
      { printf '%s%s"_pad":"' "$body" "$sep"; head -c "$pad" /dev/zero | tr '\0' x; printf '"}'; } > "$in"
    fi
  elif printf '%s' "$ev" | jq -e 'has("payloadRaw")' > /dev/null; then
    printf '%s' "$ev" | jq -j --arg root "$ROOT" '.payloadRaw | gsub("__ROOT__"; $root)' > "$in"
  else
    printf '%s' "$ev" | jq -r '.payloadBase64' | { base64 -d 2>/dev/null || base64 -D; } > "$in"
  fi
}

# classify <provider> <rc> <stdout file> <stderr file> <failopen grew 0|1>: prints "<decision>\t<reasonCode or empty>".
classify() {
  local rc="$2" out="$3" err="$4" fo="$5" json verdict code text=""
  json="$(jq -c 'select(type == "object")' "$out" 2>/dev/null | tail -n 1)"
  code="$(printf '%s' "$json" | jq -r '.hookSpecificOutput.reasonCode // .reasonCode // empty' 2>/dev/null)"
  if [ "$rc" -eq 2 ]; then printf 'deny\t%s\n' "$code"; return; fi
  if [ "$rc" -ne 0 ]; then printf 'abstain\t%s\n' "$code"; return; fi
  verdict="$(printf '%s' "${json:-null}" | jq -r '
    if . == null then "none"
    elif (.hookSpecificOutput.permissionDecision // "") == "deny" or (.decision // "") == "block"
      or (.permission // "") == "deny" or .continue == false or ((.followup_message // "") != "") then "deny"
    elif (.hookSpecificOutput.permissionDecision // "") == "ask" or (.permission // "") == "ask"
      or ([.hookSpecificOutput.additionalContext, .systemMessage, .user_message, .agent_message, .additional_context, .reason]
          | map(select(type == "string" and . != "")) | length > 0) then "advise"
    else "none" end' 2>/dev/null)"
  if [ "$verdict" = "deny" ]; then printf 'deny\t%s\n' "$code"; return; fi
  if [ "$fo" = 1 ]; then printf 'abstain\t%s\n' "$code"; return; fi
  if [ "$verdict" = "advise" ]; then printf 'advise\t%s\n' "$code"; return; fi
  [ -s "$err" ] && text=y
  [ -z "$json" ] && [ -n "$(tr -d '[:space:]' < "$out")" ] && text=y
  if [ -n "$text" ]; then printf 'advise\t%s\n' "$code"; else printf 'allow\t%s\n' "$code"; fi
}

N_CASES=0 N_LINES=0 N_PASS=0 N_FAIL=0 N_NC=0 N_UNS=0

for f in "${FIXTURES[@]}"; do
  N_CASES=$((N_CASES + 1))
  fixture="$(jq -r '.id' "$f")"; provider="$(jq -r '.provider' "$f")"
  scenario="$(jq -r '.scenario' "$f")"; verified="$(jq -r '.verified' "$f")"
  pending="$(jq -r '.pendingHook // empty' "$f")"
  if [ -n "$KEEP" ]; then CASE="$(mktemp -d "$KEEP/case.XXXXXX")"; else CASE="$(mktemp -d "${TMPDIR:-/tmp}/aw-replay.XXXXXX")"; fi
  [ -n "$CASE" ] || die "cannot make a case dir"
  ROOT="$CASE/root" CASE_HOME="$CASE/home" STATE="$CASE/state" STUBS="$CASE/stubs" SHIMS="$CASE/shims"
  mkdir -p "$ROOT" "$CASE_HOME" "$STATE" "$STUBS" "$SHIMS" "$CASE/tmp" "$CASE/io"
  echo 0 > "$CASE/clock"
  CALLS="$CASE/judge-calls.jsonl"; : > "$CALLS"
  FAILOPEN="$STATE/logs/failopen.log"
  VERDICTS="$(verdicts_path "$f")"
  make_shims
  apply_world "$(jq -c '.world // {}' "$f")"
  RESETS=0

  while IFS= read -r ev; do
    n="$(printf '%s' "$ev" | jq -r '.n')"
    if printf '%s' "$ev" | jq -e 'has("reset")' > /dev/null; then
      case "$(printf '%s' "$ev" | jq -r '.reset')" in
        session) RESETS=$((RESETS + 1)) ;;
        corrupt-state)
          find "$STATE" -type f ! -path "$STATE/logs/*" | while IFS= read -r s; do printf 'not json {' > "$s"; done ;;
      esac
      continue
    fi
    if printf '%s' "$ev" | jq -e 'has("world")' > /dev/null; then
      apply_world "$(printf '%s' "$ev" | jq -c '.world')"
      continue
    fi
    hook="$(printf '%s' "$ev" | jq -r '.hook')"; script="$(printf '%s' "$ev" | jq -r '.script')"
    adv="$(printf '%s' "$ev" | jq -r '.advanceClock // 0')"
    [ "$adv" = 0 ] || echo $(( $(cat "$CASE/clock") + adv )) > "$CASE/clock"
    in="$CASE/io/in.$n" o="$CASE/io/out.$n" e="$CASE/io/err.$n"
    write_stdin "$ev" "$in"
    if [ "$provider" = claude ]; then cmd=("$BASH" "$HOOKS/$script"); else cmd=("$BASH" "$ADAPTERS/$provider.sh" "$HOOKS/$script"); fi
    [ -z "$TIMEOUT" ] || cmd=("$TIMEOUT" 30 "${cmd[@]}")
    fo0="$(lines "$FAILOPEN")" jc0="$(lines "$CALLS")" cn0="$(lines "$ROOT/CANARY")"
    t0="$(now_ms)"
    (
      cd "$ROOT" || exit 125
      exec env -i HOME="$CASE_HOME" AW_STATE_DIR="$STATE" PATH="$SHIMS:$STUBS:$BASE_PATH" TMPDIR="$CASE/tmp" \
        LANG="${LANG:-C}" TERM=dumb AW_REPLAY=1 AW_REPLAY_VERDICTS="$VERDICTS" AW_REPLAY_CALLS="$CALLS" \
        AW_JUDGE_CS_BIN="$HERE/fake-judge.sh" AW_JUDGE_JEV_URL="$DEAD_URL" \
        http_proxy="$DEAD_URL" https_proxy="$DEAD_URL" HTTP_PROXY="$DEAD_URL" HTTPS_PROXY="$DEAD_URL" ALL_PROXY="$DEAD_URL" \
        "${cmd[@]}"
    ) < "$in" > "$o" 2> "$e"
    rc=$?
    ms=$(( $(now_ms) - t0 ))
    fo=0; [ "$(lines "$FAILOPEN")" -gt "$fo0" ] && fo=1
    calls=$(( $(lines "$CALLS") - jc0 ))
    missing="$(tail -n "+$((jc0 + 1))" "$CALLS" | jq -s '[.[] | select(.found == false)] | length' 2>/dev/null || echo 0)"
    canary=""; [ "$(lines "$ROOT/CANARY")" -gt "$cn0" ] && canary="$(tail -n "+$((cn0 + 1))" "$ROOT/CANARY" | head -n 1)"
    IFS=$'\t' read -r decision code < <(classify "$provider" "$rc" "$o" "$e" "$fo")
    [ "$rc" -ne 124 ] || [ -z "$TIMEOUT" ] || code="${code:-timeout}"
    if [ "$calls" -eq 0 ]; then jsrc=none; elif [ "$missing" -gt 0 ]; then jsrc=missing; else jsrc=recorded; fi
    exp="$(jq -c --argjson n "$n" '[.expect[] | select(.event == $n)][0] // null' "$f")"
    [ "$exp" != null ] || [ -n "$canary" ] || continue

    status=pass reason=""
    xd="$(printf '%s' "$exp" | jq -r '.decision // empty')"
    xcode="$(printf '%s' "$exp" | jq -r '.reasonCode // empty')"
    xtext="$(printf '%s' "$exp" | jq -r '.textContains // empty')"
    xuns="$(printf '%s' "$exp" | jq -r '.unsupported // false')"
    if [ -n "$canary" ]; then status=fail reason="canary: $canary"
    elif [ "$STRICT" = 1 ] && [ "$missing" -gt 0 ]; then status=fail reason="verdict missing: $missing judge call(s) had no recorded answer"
    elif [ -n "$pending" ]; then status=notCovered reason="pending hook: $pending"
    elif [ "$xuns" = true ]; then
      case "$decision" in allow | advise) status=unsupported ;; *) status=fail reason="unsupported event got $decision" ;; esac
    elif [ "$decision" != "$xd" ]; then status=fail reason="expected $xd, got $decision"
    elif [ -n "$xcode" ] && [ "$code" != "$xcode" ]; then status=fail reason="expected reasonCode $xcode, got ${code:-none}"
    elif [ -n "$xtext" ] && ! { cat "$e" "$o"; } | grep -qF -- "$xtext"; then status=fail reason="text lacks \"$xtext\""
    fi
    case $status in pass) N_PASS=$((N_PASS + 1)) ;; fail) N_FAIL=$((N_FAIL + 1)) ;; notCovered) N_NC=$((N_NC + 1)) ;; unsupported) N_UNS=$((N_UNS + 1)) ;; esac
    N_LINES=$((N_LINES + 1))
    emit "$(jq -nc --arg fixture "$fixture" --arg provider "$provider" --arg scenario "$scenario" --arg verified "$verified" \
      --argjson event "$n" --arg hook "$hook" --arg decision "$decision" --arg code "$code" --argjson exit "$rc" \
      --argjson ms "$ms" --argjson calls "$calls" --arg jsrc "$jsrc" --argjson fo "$([ "$fo" = 1 ] && echo true || echo false)" \
      --arg expected "$xd" --arg status "$status" --arg reason "$reason" '
      {fixture: $fixture, provider: $provider, scenario: $scenario, verified: $verified, event: $event, hook: $hook,
       decision: $decision, reasonCode: (if $code == "" then null else $code end), exit: $exit, hookMs: $ms,
       judgeCalls: $calls, judgeSource: $jsrc, failopen: $fo, expected: (if $expected == "" then null else $expected end),
       status: $status, reason: $reason}')"
  done < <(jq -c '.events | sort_by(.n) | .[]' "$f")

  cleanup
  CASE=""
done

printf 'replay: %s cases, %s events, %s pass, %s fail, %s notCovered, %s unsupported\n' \
  "$N_CASES" "$N_LINES" "$N_PASS" "$N_FAIL" "$N_NC" "$N_UNS" >&2
[ "$N_FAIL" -eq 0 ] || exit 1
exit 0
