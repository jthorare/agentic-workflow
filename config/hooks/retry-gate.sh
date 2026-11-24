#!/usr/bin/env bash
# aw:retry-gate — the retry guard's pre-tool hook (issue 4248; contract docs/hooks/retry-guard.md).
# With no state file for the session it exits 0 at once: no jq, no hash, no network, no model. That is the ordinary
# path, and it runs on every tool call. With a state file, it hashes the call and, when this exact call already failed
# and its prerequisite is unchanged, denies it (exit 2, the one fix on stderr) or adds advice (exit 0, additionalContext):
#   missing-exe   deny while `command -v <exe>` fails; clear once it succeeds
#   missing-path  deny while the path is missing; clear once it exists
#   bad-args      deny (Bash) or advise (other tools); a changed argument is a new hash and passes
#   auth-expired  deny once while the auth file is unchanged and < 10 min passed, then advise; a changed file clears
#   transient     never deny; advise "wait N s" until retryAfter
#   unknown       never deny; advise from the 3rd identical failure
# A Bash command ending in `# aw-retry: <reason>` always passes; the override is recorded with its reason.
# Fails open: an unreadable state file is removed, logged to failopen.log, and the call allowed. Bash 3.2, jq.
[ -n "${AW_JUDGE_CHILD:-}" ] && exit 0
IFS= read -r -d '' INPUT || true
RE_SID='"session_id"[[:space:]]*:[[:space:]]*"([^"]*)"'
[[ $INPUT =~ $RE_SID ]] || exit 0
RG_SID="${BASH_REMATCH[1]}"
[[ $RG_SID =~ ^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$ ]] || exit 0
F="${AW_STATE_DIR:-${HOME:-}/.agentic-workflow}/retry-guard/$RG_SID.json"
[ -f "$F" ] || exit 0
# ---- only a session with a recorded failure gets past this line ----

HOOK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
for lib in "$HOOK_DIR/lib/retry-guard.sh" "$HOOK_DIR/../lib/retry-guard.sh"; do
  [ -f "$lib" ] && { . "$lib"; break; }
done
command -v rg_call > /dev/null 2>&1 || exit 0
command -v jq > /dev/null 2>&1 || { rg_failopen retry-gate jq-missing; exit 0; }
rg_call "$INPUT" || exit 0
rg_hash
rg_now
umask 077

# One read: is the file readable, is it this version, and this call's problem (one line, fields split by \x1f: a tab
# is IFS whitespace, so read would merge the empty fields).
ROW="$(jq -r --arg h "$RG_HASH" --argjson v "$RG_VERSION" --arg p "$RG_POLICY" '
  if type != "object" then error("bad") elif .version != $v or .policy != $p then "old"
  elif (.problems[$h] // null) == null then "none"
  else .problems[$h] | [ "open", .kind, .resource, .exe, (.failures | tostring), (.denies | tostring),
         (.advisories | tostring), ((.wouldDeny // 0) | tostring), (.lastAt | fromdate | tostring), .retryAfter, .authStamp ] | join("\u001f") end
' "$F" 2> /dev/null)" || {
  rm -f "$F"
  rg_failopen retry-gate state-unreadable
  exit 0
}
case "$ROW" in old | none) exit 0 ;; esac
IFS=$'\x1f' read -r _ KIND RES EXE FAILS DENIES ADVS WOULD LAST_EPOCH RETRY_AFTER STAMP <<< "$ROW"

OVR_HINT=""
[ "$RG_TOOL" = Bash ] && OVR_HINT=' To repeat it on purpose, end the command with `# aw-retry: <reason>`.'

# The override: let it through and record it.
if [ -n "$RG_REASON" ]; then
  rg_update "$F" '
    .overrides = ((.overrides // []) + [{hash: $h, kind: $kind, reason: $reason, at: $iso}] | .[-50:])
    | .counters.overrides += 1' --arg h "$RG_HASH" --arg kind "$KIND" --arg reason "$RG_REASON" \
    || rg_failopen retry-gate state-write
  exit 0
fi

ACTION=none MSG=""
case "$KIND" in
  missing-exe)
    if command -v "$RES" > /dev/null 2>&1; then ACTION=clear
    else ACTION=deny; MSG="retry-guard: \`$RES\` is not installed (this exact call failed with \"command not found\"). Install it or use another command.$OVR_HINT"; fi ;;
  missing-path)
    if [ -e "$RES" ]; then ACTION=clear
    else ACTION=deny; MSG="retry-guard: \`$RES\` does not exist (this exact call failed with \"No such file or directory\"). Create it or fix the path.$OVR_HINT"; fi ;;
  bad-args)
    HELP="the tool's help"; [ -n "$EXE" ] && HELP="\`$EXE --help\`"
    MSG="retry-guard: this exact call already failed with an argument error. Change the arguments (check $HELP) before running it again."
    if [ "$RG_TOOL" = Bash ]; then ACTION=deny; MSG="$MSG$OVR_HINT"; else ACTION=advise; fi ;;
  auth-expired)
    FIX="refresh the credentials for $RES"
    [ "$RES" = github.com ] && FIX='run `gh auth login` (or refresh GH_TOKEN)'
    AF="$(rg_auth_file "$RES")"
    NOW_STAMP=""; [ -n "$AF" ] && NOW_STAMP="$(rg_mtime "$AF")"
    if [ -n "$STAMP" ] && [ "$NOW_STAMP" != "$STAMP" ]; then ACTION=clear
    elif [ "$((DENIES + WOULD))" = 0 ] && [ $((RG_NOW - LAST_EPOCH)) -lt 600 ]; then
      ACTION=deny; MSG="retry-guard: the login for $RES looks expired (this exact call failed with an authentication error). To fix it, $FIX, then retry.$OVR_HINT"
    else
      ACTION=advise; MSG="retry-guard: this call failed with an authentication error for $RES. If it fails the same way, $FIX instead of retrying."
    fi ;;
  transient)
    NOW_ISO="$(jq -rn --argjson n "$RG_NOW" '$n | todate')"
    if [ -n "$RETRY_AFTER" ] && [[ $NOW_ISO < $RETRY_AFTER ]]; then
      W=$((30 << (FAILS - 1))); { [ "$FAILS" -gt 5 ] || [ "$W" -gt 300 ]; } && W=300
      ACTION=advise; MSG="retry-guard: this call failed with a network or server error $FAILS time(s). wait $W s before repeating it, or check the service."
    fi ;;
  unknown)
    if [ "$FAILS" -ge 3 ]; then
      ACTION=advise; MSG="retry-guard: this exact call failed $FAILS times the same way. Change something (arguments, inputs, or the approach) before repeating it."
    fi ;;
esac

# An override marker with no reason is not an override: say so in the deny.
if [ "$ACTION" = deny ] && [ "$RG_OVR_MARK" = true ]; then
  MSG="$MSG An \`# aw-retry:\` override needs a reason after the colon (\`# aw-retry: <why this repeat is needed>\`); this one is empty, so the call is still denied."
fi

# Advisory by default (#4257): a would-be deny prints the same text as advice, exits 0 and counts counters.wouldDeny.
# AW_POLICY_RETRY_GUARD_1_MODE=block turns the deny on; unset or any other value is advisory.
MODE=advisory
# TODO(#4257): policy_mode retry-guard/1 (config/lib/policy.sh) replaces this read.
[ "${AW_POLICY_RETRY_GUARD_1_MODE:-}" = block ] && MODE=block
[ "$ACTION" = deny ] && [ "$MODE" != block ] && ACTION=would-deny

case "$ACTION" in
  none) exit 0 ;;
  would-deny)
    rg_update "$F" '
      if .problems[$h] == null then . else
        .problems[$h].wouldDeny = ((.problems[$h].wouldDeny // 0) + 1)
        | .counters.wouldDeny = ((.counters.wouldDeny // 0) + 1)
      end' --arg h "$RG_HASH" || rg_failopen retry-gate state-write
    jq -nc --arg m "$MSG" '{hookSpecificOutput: {hookEventName: "PreToolUse", additionalContext: $m}}'
    exit 0 ;;
  clear)
    rg_update "$F" '
      if .problems[$h] == null then . else
        ((.problems[$h].denies + .problems[$h].advisories + (.problems[$h].wouldDeny // 0)) > 0) as $seen
        | del(.problems[$h]) | .counters.cleared += 1
        | if $seen then .counters.clearedAfterChange += 1 else . end
      end' --arg h "$RG_HASH" || rg_failopen retry-gate state-write
    exit 0 ;;
  deny)
    rg_update "$F" 'if .problems[$h] == null then . else .problems[$h].denies += 1 | .counters.denies += 1 end' \
      --arg h "$RG_HASH" || rg_failopen retry-gate state-write
    printf '%s\n' "$MSG" >&2
    exit 2 ;;
  advise)
    rg_update "$F" 'if .problems[$h] == null then . else .problems[$h].advisories += 1 | .counters.advisories += 1 end' \
      --arg h "$RG_HASH" || rg_failopen retry-gate state-write
    jq -nc --arg m "$MSG" '{hookSpecificOutput: {hookEventName: "PreToolUse", additionalContext: $m}}'
    exit 0 ;;
esac
exit 0
