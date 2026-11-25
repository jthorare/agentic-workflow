#!/usr/bin/env bash
# #3694: a hook's match test must give the same answer whatever the input size.
# The hooks run `set -o pipefail` and test `printf '%s\n' "$VAR" | grep -q ...`. grep -q quits at its first match; when $VAR is
# bigger than the pipe buffer (64 KB), printf is still writing, dies of SIGPIPE (141) and pipefail makes the whole test false. A deny
# hook then fails open. Each row feeds a hook a needle on line 1 plus >= 200 KB of filler and expects what a small input gets.
# Every hook has a small-input control row (passes today, so a red big row is about size) and one lint row catches new sites.
# Not driven: done-gate.sh's SESSION_ID site (:224, :227): that grep caps the id at 80 characters, so size is not the variable.
# Run: bash config/hooks/tests/hook-pipefail.test.sh
set -uo pipefail

# Temp-dir cleanup (#3262): every mktemp this suite makes is recorded and removed when it exits.
__aw_tmp_reg="$(command mktemp)"
mktemp() { local p; p="$(command mktemp "$@")" || return; printf '%s\n' "$p" >>"$__aw_tmp_reg"; printf '%s\n' "$p"; }
__aw_tmp_clean() { local rc=$? p; while IFS= read -r p; do [ -z "$p" ] || { chmod -R u+rwX "$p" 2>/dev/null || true; rm -rf "$p" || true; }; done <"$__aw_tmp_reg"; rm -f "$__aw_tmp_reg"; exit "$rc"; }
trap __aw_tmp_clean EXIT
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
HOOKS="$ROOT/config/hooks"
fail=0
# The rows set a lane's variables themselves; the suite must not inherit another lane's or a Sindri launch's.
unset TMUX TMUX_PANE AW_META_DIR AW_META_CONFIG AW_META_TEAM AW_META_LANE AW_META_LANE_PID AW_META_LANE_AGENT AW_META_LANE_PROC AW_META_CALLER_PATH AW_SINDRI_CHILD AW_SINDRI_LAUNCH AW_SINDRI_HOME AW_LAUNCHER_CONFIG AW_JUDGE_CHILD AW_HOOK_WD_INNER AW_HOOK_WD_TMP

check() { # <name> <actual> <expected>
  if [ "$2" == "$3" ]; then echo "ok - $1"; else echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1; fi
}

WORK="$(mktemp -d)"
export HOME="$WORK/home"; mkdir -p "$HOME" "$WORK/cwd" "$WORK/bin"
export AW_STATE_DIR="$WORK/state" AW_JUDGE_SESSIONS_DIR="$WORK/sessions"
mkdir -p "$AW_STATE_DIR" "$AW_JUDGE_SESSIONS_DIR"
cd "$WORK/cwd" || exit 2   # not a git repo: block-push-main sees no current branch

# filler <file>: 6000 short lines, about 270 KB (the pipe buffer is 64 KB).
filler() { awk 'BEGIN { for (i = 0; i < 6000; i++) print "filler line number " i " padding padding padding" }' > "$1"; }
# text_big <file> <line1>: line 1 then the filler. text_small: line 1 alone.
text_big() { { printf '%s\n' "$2"; cat "$WORK/filler"; } > "$1"; }
text_small() { printf '%s\n' "$2" > "$1"; }
filler "$WORK/filler"
SIZE="$(wc -c < "$WORK/filler")"
[ "$SIZE" -ge 200000 ] || { echo "not ok - filler is $SIZE bytes, want >= 200000"; exit 1; }

# payload <jq-expr> <textfile>: a hook payload with the text file's content as $t (no argument-size limit).
payload() { jq -nc --rawfile t "$2" "$1"; }
BASH_EXPR='{tool_name:"Bash",tool_input:{command:$t}}'

# run <hook> <payload-file> [env...]: sets rc, out, err.
run() {
  local hook="$1" in="$2"; shift 2
  env "$@" bash "$HOOKS/$hook" < "$in" > "$WORK/out" 2> "$WORK/err"
  rc=$?; out="$(cat "$WORK/out")"; err="$(cat "$WORK/err")"
}

# --- 1. block-destructive: a shell -c string is judged unblanked. Line 38's match picks that; SIGPIPE there blanks the quoted command. ---
# The hook runs every rule on every line, so this filler is 40 long lines (240 KB) and not 6000 short ones (that took ~50 s).
awk 'BEGIN { s = sprintf("%6000s", ""); gsub(/ /, "x", s); for (i = 0; i < 40; i++) print s }' > "$WORK/filler-long"
NEEDLE='sh -c "rm -rf /tmp/x"'
for size in small big; do
  if [ $size = big ]; then { printf '%s\n' "$NEEDLE"; cat "$WORK/filler-long"; } > "$WORK/t"; else text_small "$WORK/t" "$NEEDLE"; fi
  payload "$BASH_EXPR" "$WORK/t" > "$WORK/p"
  run block-destructive.sh "$WORK/p"
  check "block-destructive $size: sh -c with a recursive force rm is denied (exit 2)" "$rc" "2"
done

# --- 2. block-push-main: line 118's `grep -E | grep -qv`. Line 1 is a push to main, the rest push to main:feature (the qv
# pattern), so the first grep writes > 64 KB and the second quits on line 1. ---
for size in small big; do
  { printf '%s\n' 'git push origin main'
    if [ $size = big ]; then awk 'BEGIN { for (i = 0; i < 6000; i++) print "git push origin main:feature-" i " # padding padding" }'; fi
  } > "$WORK/t"
  payload "$BASH_EXPR" "$WORK/t" > "$WORK/p"
  run block-push-main.sh "$WORK/p"
  check "block-push-main $size: git push origin main is denied (exit 2)" "$rc" "2"
done

# --- 3. detect-secrets ---
for size in small big; do
  # the key is written in two pieces so the secret scrubber finds no key in this file
  text_$size "$WORK/t" "echo AKIA""ABCDEFGHIJKLMNOP"; payload "$BASH_EXPR" "$WORK/t" > "$WORK/p"
  run detect-secrets.sh "$WORK/p"
  check "detect-secrets $size: an AWS key is denied (exit 2)" "$rc" "2"
done

# A stub judge on PATH: `brief get` says there is no brief (as for a session without one) unless STUB_BRIEF holds JSON; every call is
# logged, and prompt-sort answers a context line.
cat > "$WORK/bin/judge" <<'STUB'
#!/usr/bin/env bash
echo "$*" >> "$STUB_JUDGE_LOG"
case "$1 $2" in
  "brief get") if [ -n "${STUB_BRIEF:-}" ]; then echo "$STUB_BRIEF"; exit 0; fi; echo "no brief for $3" >&2; exit 1 ;;
  "prompt-sort "*) cat > /dev/null; echo '{"context":"SORTED"}'; exit 0 ;;
esac
cat > /dev/null 2>&1; exit 0
STUB
chmod +x "$WORK/bin/judge"
export STUB_JUDGE_LOG="$WORK/judge.log"
SPATH="$WORK/bin:$PATH"

# --- 4. done-gate (:257): a done claim whose line 1 holds evidence, no brief. Evidence found -> exit 0, not found -> exit 2. ---
for size in small big; do
  text_$size "$WORK/t" 'Done. dotnet test: 12 passed'
  jq -nc --rawfile t "$WORK/t" '{session_id:"s-pf",last_assistant_message:$t}' > "$WORK/p"
  : > "$STUB_JUDGE_LOG"
  run done-gate.sh "$WORK/p" PATH="$SPATH"
  check "done-gate $size: a done claim with a test run on line 1 passes (exit 0)" "$rc" "0"
done

# --- 5. done-gate-annotate: the criteria's first 40 characters are on line 1 of the final message. ---
CRIT='ACCEPT-PIPEFAIL-3694 the result shows the thing'
BRIEF="$(jq -nc --arg a "$CRIT" '{acceptanceCriteria:$a}')"
for size in small big; do
  text_$size "$WORK/t" "$CRIT, as asked"
  jq -nc --rawfile t "$WORK/t" '{tool_use_id:"tu-1",tool_response:$t}' > "$WORK/p"
  run done-gate-annotate.sh "$WORK/p" PATH="$SPATH" STUB_BRIEF="$BRIEF"
  check "done-gate-annotate $size: criteria on line 1 read as plausibly met" "$(jq -r '.hookSpecificOutput.additionalContext // empty' <<< "$out" | cut -c1-39)" "Brief acceptance criteria plausibly met"
done

# --- 6. meta-lane-gate: Stop with a reply marked on line 1. A marked reply adds no "no line starting REPLY TO META" notice. ---
STUBMETA="$WORK/aw-meta"
cat > "$STUBMETA" <<'STUB'
#!/usr/bin/env bash
cat > /dev/null 2>&1
[ "$1" = lane-stop-check ] && { echo "OWED-LIST"; exit 1; }
exit 0
STUB
chmod +x "$STUBMETA"
for size in small big; do
  text_$size "$WORK/t" 'REPLY TO META: all good'
  jq -nc --rawfile t "$WORK/t" '{hook_event_name:"Stop",session_id:"s-pf",stop_hook_active:false,last_assistant_message:$t}' > "$WORK/p"
  # this shell plays meta-lane.sh: the hook's parent is AW_META_LANE_PID, so it is the lane's own agent
  AW_META_BIN="$STUBMETA" AW_META_DIR="$WORK/meta-$size" AW_META_TEAM=t1 AW_META_LANE=api AW_META_LANE_PID=$$ AW_META_LANE_AGENT=codex AW_META_LANE_PROC=codex \
    bash "$HOOKS/meta-lane-gate.sh" < "$WORK/p" > "$WORK/out" 2> "$WORK/err"; rc=$?
  check "meta-lane-gate $size: a reply marked on line 1 is not called unmarked" "$rc:$(cat "$WORK/err")" "2:OWED-LIST"
done

# --- 7. prompt-sort: a slash command (line 1) exits before the judge is called. ---
for size in small big; do
  text_$size "$WORK/t" '/bugFixOrchestrator FRN-1'
  jq -nc --rawfile t "$WORK/t" '{session_id:"s-pf",prompt:$t}' > "$WORK/p"
  : > "$STUB_JUDGE_LOG"
  run prompt-sort.sh "$WORK/p" PATH="$SPATH"
  check "prompt-sort $size: a slash command never reaches the judge" "$rc:$(cat "$STUB_JUDGE_LOG"):$out" "0::"
done

# --- 8. turn-origin: a teammate message does not clear auto_continued_at. ---
for size in small big; do
  text_$size "$WORK/t" '<teammate-message from="x">hi</teammate-message>'
  jq -nc --rawfile t "$WORK/t" '{session_id:"s-to",prompt:$t}' > "$WORK/p"
  printf '{"auto_continued_at":"2026-01-01T00:00:00Z"}\n' > "$AW_JUDGE_SESSIONS_DIR/s-to.json"
  run turn-origin.sh "$WORK/p"
  check "turn-origin $size: a teammate message keeps auto_continued_at" "$(jq -r '.auto_continued_at // "cleared"' "$AW_JUDGE_SESSIONS_DIR/s-to.json")" "2026-01-01T00:00:00Z"
done

# --- 9. lint: no hook pipes printf straight into a grep whose flags hold q (also `printf | grep | grep -q`). ---
HITS="$(grep -nE 'printf[^|]*\|[[:space:]]*grep[[:space:]]+(-[A-Za-z]*q|--quiet|--silent)|printf[^|]*\|[[:space:]]*grep[^|]*\|[[:space:]]*grep[[:space:]]+(-[A-Za-z]*q|--quiet|--silent)' "$HOOKS"/*.sh | sed "s#^$HOOKS/##")"
check "lint: no config/hooks/*.sh pipes printf into grep -q (use grep -q ... <<<\"\$X\")" "$(printf '%s' "$HITS" | grep -c . || true)" "0"
[ -z "$HITS" ] || printf '%s\n' "$HITS" | sed 's/^/  hit: /' | cut -c1-160

exit "$fail"
