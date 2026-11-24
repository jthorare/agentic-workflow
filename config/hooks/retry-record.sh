#!/usr/bin/env bash
# aw:retry-record — the retry guard's post-tool hook (issue 4248; contract docs/hooks/retry-guard.md).
# PostToolUseFailure (Claude Code; the Codex and Cursor adapters map their failures to it): classify the error text with a
# fixed table and record the problem in $AW_STATE_DIR/retry-guard/<session>.json. PostToolUse (a success): clear the
# problem of that same call, if the session has a state file; with no file it exits at once, so a success, polling
# included, is never recorded.
# Advisory: prints nothing and always exits 0. Stores the call hash, the kind and the resource, never the tool input or
# output. Fails open (a failopen.log line) when the state can't be written. Bash 3.2, jq.
[ -n "${AW_JUDGE_CHILD:-}" ] && exit 0
IFS= read -r -d '' INPUT || true
[ -n "$INPUT" ] || exit 0
RE_SID='"session_id"[[:space:]]*:[[:space:]]*"([^"]*)"'
[[ $INPUT =~ $RE_SID ]] || exit 0
RG_SID="${BASH_REMATCH[1]}"
[[ $RG_SID =~ ^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$ ]] || exit 0
RG_DIR="${AW_STATE_DIR:-${HOME:-}/.agentic-workflow}/retry-guard"
[ "$RG_DIR" != "/.agentic-workflow/retry-guard" ] || exit 0
F="$RG_DIR/$RG_SID.json"
# A failure: Claude's and Cursor's PostToolUseFailure, or a Codex PostToolUse the adapter marked aw_tool_failed.
RE_EVENT='"hook_event_name"[[:space:]]*:[[:space:]]*"PostToolUseFailure"'
RE_FAILED='"aw_tool_failed"[[:space:]]*:[[:space:]]*true'
FAILED=0
if [[ $INPUT =~ $RE_EVENT ]] || [[ $INPUT =~ $RE_FAILED ]]; then FAILED=1; fi
if [ "$FAILED" = 0 ]; then
  [ -f "$F" ] || exit 0   # a success with no problems open: the ordinary path
fi

HOOK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
for lib in "$HOOK_DIR/lib/retry-guard.sh" "$HOOK_DIR/../lib/retry-guard.sh"; do
  [ -f "$lib" ] && { . "$lib"; break; }
done
command -v rg_call > /dev/null 2>&1 || exit 0
command -v jq > /dev/null 2>&1 || { rg_failopen retry-record jq-missing; exit 0; }
rg_call "$INPUT" || { rg_failopen retry-record input-unparsable; exit 0; }
rg_hash
rg_now
umask 077

if [ "$FAILED" = 0 ]; then
  # A success: drop this call's problem.
  jq -e --arg h "$RG_HASH" '.problems[$h] != null' "$F" > /dev/null 2>&1 || exit 0
  rg_update "$F" '
    if .problems[$h] == null then . else
      ((.problems[$h].denies + .problems[$h].advisories) > 0) as $seen
      | del(.problems[$h])
      | .counters.cleared += 1
      | if $seen then .counters.clearedAfterChange += 1 else . end
    end' --arg h "$RG_HASH" || rg_failopen retry-record state-write
  exit 0
fi

[ "$RG_INTR" = "true" ] && exit 0   # an interrupt is not a failure of the call

# The error text (at most 8 KiB) and a file_path / path argument, for tools that name the file.
DETAIL="$(printf '%s' "$INPUT" | jq -r '
  ((.tool_input // {}) | if type == "object" then (.file_path // .path // "") else "" end | tostring | gsub("[\u0000-\u001f]"; " ")),
  (.error // "" | tostring | .[0:8192])' 2> /dev/null)" || DETAIL=""
ARG_PATH="${DETAIL%%$'\n'*}"
ERR_TEXT="${DETAIL#*$'\n'}"
[ "$ERR_TEXT" != "$DETAIL" ] || ERR_TEXT=""

# --- the classifier (docs/hooks/retry-guard.md "What the gate does"); the first kind that matches wins -------------------
KIND="" RES=""
shopt -s nocasematch
RE_NF1='([A-Za-z0-9._+/-]+): command not found'
RE_NF2='command not found: ([A-Za-z0-9._+/-]+)'
RE_127='exit code:? 127([^0-9]|$)'
RE_AUTH='(http 401([^0-9]|$)|401 unauthorized|status 401([^0-9]|$)|bad credentials|token (has )?expired|gh auth login|authentication required|not logged in|unauthorized)'
RE_BAD='(unknown flag|unknown option|unknown command|invalid option|unrecognized option|usage:|invalid argument)'
RE_TRANSIENT='(timed out|timeout|could not resolve host|temporary failure in name resolution|connection reset|connection refused|http[^0-9]{0,8}5[0-9][0-9]([^0-9]|$)|(^|[^0-9])50[234]([^0-9]|$)|rate limit)'
if [[ $ERR_TEXT =~ $RE_NF1 ]] || [[ $ERR_TEXT =~ $RE_NF2 ]]; then
  KIND=missing-exe; RES="${BASH_REMATCH[1]##*/}"
elif [[ $ERR_TEXT =~ $RE_127 ]] && [ -n "$RG_EXE" ]; then
  KIND=missing-exe; RES="$RG_EXE"
elif [[ $ERR_TEXT =~ $RE_AUTH ]]; then
  KIND=auth-expired
  RE_HOST='https?://([A-Za-z0-9.-]+)'
  if [[ $ERR_TEXT == *github.com* ]] || [ "$RG_EXE" = gh ]; then RES=github.com
  elif [[ $ERR_TEXT =~ $RE_HOST ]] || [[ $RG_CMD =~ $RE_HOST ]]; then RES="${BASH_REMATCH[1]}"
  else RES=unknown-host; fi
  RES="${RES#api.}"
elif [[ $ERR_TEXT =~ $RE_BAD ]]; then
  KIND=bad-args
fi
if [ -z "$KIND" ]; then
  shopt -u nocasematch
  RE_P1="cannot access '([^']+)': No such file or directory"
  RE_P2="'([^']+)': No such file or directory"
  RE_P3='([^[:space:]:'"'"'"]+): No such file or directory'
  P=""
  if [[ $ERR_TEXT =~ $RE_P1 ]] || [[ $ERR_TEXT =~ $RE_P2 ]] || [[ $ERR_TEXT =~ $RE_P3 ]]; then P="${BASH_REMATCH[1]}"
  elif [[ $ERR_TEXT == *"File does not exist"* ]] && [ -n "$ARG_PATH" ]; then P="$ARG_PATH"; fi
  if [ -n "$P" ]; then
    case "$P" in /*) ;; *) P="${P#./}"; [ -n "$RG_CWD" ] && P="${RG_CWD%/}/$P" ;; esac
    KIND=missing-path; RES="$P"
  fi
  shopt -s nocasematch
fi
if [ -z "$KIND" ]; then
  if [[ $ERR_TEXT =~ $RE_TRANSIENT ]]; then KIND=transient; else KIND=unknown; fi
fi
shopt -u nocasematch
case "$KIND" in bad-args | transient | unknown) RES="$RG_HASH" ;; esac
RES="${RES//[$'\001'-$'\037']/ }"; RES="${RES:0:256}"
[ -n "$RES" ] || RES="$RG_HASH"

# The last failure's error text, as classified (8 KiB at most), hashed: the judge tells "the same failure again" from "a
# poll that gets new state each time" by it. The text itself is never stored.
ERR_HASH="$(rg_text_hash "$ERR_TEXT")"

STAMP=""
if [ "$KIND" = auth-expired ]; then
  AF="$(rg_auth_file "$RES")"
  [ -n "$AF" ] && STAMP="$(rg_mtime "$AF")"
fi

mkdir -p "$RG_DIR" 2> /dev/null && chmod 700 "$RG_DIR" 2> /dev/null || { rg_failopen retry-record state-dir; exit 0; }
rg_update "$F" '
  (.problems[$h] // null) as $old
  | (if $old != null and $old.kind == $kind then $old.failures + 1 else 1 end) as $n
  | .problems[$h] = {
      key: ($sid + "|" + $kind + "|" + $res + "|" + $p),
      kind: $kind, resource: $res, tool: $tool, exe: $exe,
      failures: $n,
      denies: (if $old != null and $old.kind == $kind then $old.denies else 0 end),
      advisories: (if $old != null and $old.kind == $kind then $old.advisories else 0 end),
      firstAt: (if $old != null and $old.kind == $kind then $old.firstAt else $iso end),
      lastAt: $iso,
      retryAfter: (if $kind == "transient"
                   then ($now + ([30 * pow(2; $n - 1), 300] | min | floor)) | todate else "" end),
      authStamp: $stamp,
      errorHash: $eh,
      errorHashes: ((if $old != null and $old.kind == $kind then ($old.errorHashes // []) else [] end)
                    | if index([$eh]) then . else (. + [$eh])[-16:] end),
      distinctErrors: ((if $old != null and $old.kind == $kind then ($old.distinctErrors // 0) else 0 end)
                       + (if $old != null and $old.kind == $kind and (($old.errorHashes // []) | index([$eh])) then 0 else 1 end))
    }
  | .counters.failures += 1' \
  --arg h "$RG_HASH" --arg kind "$KIND" --arg res "$RES" --arg tool "$RG_TOOL" --arg exe "$RG_EXE" --arg stamp "$STAMP" --arg eh "$ERR_HASH" \
  || rg_failopen retry-record state-write
exit 0
