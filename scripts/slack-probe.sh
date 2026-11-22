#!/usr/bin/env bash
# scripts/slack-probe.sh — the P0 probe of the Messages page (spec docs/superpowers/specs/2026-11-12-sindri-messages-page-design.md
# §5.5, lines 1 to 8; phase M0a): the owner-run go/no-go probe that proves or kills the spec's Slack guesses (§13). We only ever run
# it against a fake (scripts/tests/slack-probe.test.sh); the owner runs it on their workspace with the probe app of
# docs/sindri/slack-probe-manifest.yaml.
#
# Owner-run commands:
#   scripts/slack-probe.sh                               # read-only: lines 1 to 8, the writes of line 6 skipped
#   scripts/slack-probe.sh --write --channel G0123ABCD   # plus line 6's writes, in that private scratch channel only
#   scripts/slack-probe.sh --manifest-link               # print the prefilled create-app link (line 8), then exit; needs no key
# Usage: scripts/slack-probe.sh [--url URL] [--burst N] [--write --channel ID] [--manifest-link]
#   --url      the Web API base (default https://slack.com/api); anything else must be loopback (the fake)
#   --burst    calls per rate-limit burst in lines 3 and 4 (default 25, 1 to 100); a burst stops at the first 429
#   --write    run line 6's writes; needs --channel
#   --channel  the id of a private channel the owner made with no other member; the write guard refuses any other
# Keys: AW_SLACK_PROBE_KEY, the probe app's User OAuth Token (xoxp-). AW_SLACK_PROBE_APP_KEY, optional, its app-level token (xapp-)
#   for line 5. Keys reach curl through 0600 config files, never an argument, and are never printed. The token goes only to the
#   --url host and to *.slack.com file urls over https (loopback ones when --url is loopback); an upload url never gets it.
# Output: counts, yes/no, error codes, header numbers and host names. Never a channel name, user name, id, topic or message text.
# Write part guards: the channel must be private, made by the token's user, with 1 member. Every message, scheduled message and
#   file it makes is deleted at the end, from the EXIT trap too (a SIGINT included), and the channel is read again to count what is left.
# Exit 0 ok, 1 a probe call failed or the cleanup left something (the "failed:" lines say which), 2 usage, no key or a guard refused.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MANIFEST="$ROOT/docs/sindri/slack-probe-manifest.yaml"
base="https://slack.com/api" burst=25 want_write=0 channel="" manifest_link=0
usage() { echo "usage: $0 [--url URL] [--burst N] [--write --channel ID] [--manifest-link]" >&2; exit 2; }
while [ $# -gt 0 ]; do
  case "$1" in
    --url) base="${2:?--url needs a value}"; shift 2 ;;
    --burst) burst="${2:?--burst needs a number}"; shift 2 ;;
    --write) want_write=1; shift ;;
    --channel) channel="${2:?--channel needs a channel id}"; shift 2 ;;
    --manifest-link) manifest_link=1; shift ;;
    -h|--help) sed -n '2,25p' "$0"; exit 0 ;;
    *) usage ;;
  esac
done

command -v jq >/dev/null || { echo "probe: jq is required" >&2; exit 2; }

# The manifest's user scopes, one per line: the lines under `    user:` that start with `      - `.
manifest_scopes() { awk '/^    user:/{u=1; next} u && /^      - /{print $2; next} u && /^      #/{next} u{exit}' "$MANIFEST"; }

if [ "$manifest_link" -eq 1 ]; then
  [ -f "$MANIFEST" ] || { echo "probe: $MANIFEST is missing" >&2; exit 2; }
  # Comment lines are left out so the link stays short; Slack reads the rest as YAML.
  printf 'https://api.slack.com/apps?new_app=1&manifest_yaml=%s\n' "$(grep -vE '^[[:space:]]*#' "$MANIFEST" | jq -sRr @uri)"
  exit 0
fi

if [ "$want_write" -eq 1 ] && [ -z "$channel" ]; then echo "probe: --write needs --channel ID (a private channel you made, with no one else in it)" >&2; exit 2; fi
if [ "$want_write" -eq 0 ] && [ -n "$channel" ]; then echo "probe: --channel is for --write only" >&2; exit 2; fi
if [ -n "$channel" ]; then case "$channel" in [CG][A-Z0-9][A-Z0-9][A-Z0-9][A-Z0-9][A-Z0-9]*) case "$channel" in *[!A-Z0-9]*) channel_bad=1 ;; esac ;; *) channel_bad=1 ;; esac; fi
[ -z "${channel_bad:-}" ] || { echo "probe: --channel takes a channel id like G0123ABCD (channel details, at the bottom), not a name" >&2; exit 2; }
case "$burst" in ''|*[!0-9]*) usage ;; esac
[ "$burst" -ge 1 ] && [ "$burst" -le 100 ] || { echo "probe: --burst takes 1 to 100" >&2; exit 2; }
command -v curl >/dev/null || { echo "probe: curl is required" >&2; exit 2; }

# url_host URL: the host of an http(s) url, parsed from the authority, not sliced; empty on userinfo or a malformed host or port.
url_host() {
  local authority rest
  authority="${1#*://}"; authority="${authority%%[/?#]*}"
  case "$authority" in *@*|'') return 0 ;; esac
  case "$authority" in
    \[*\]*) rest="${authority#*\]}"
      case "$rest" in '') ;; :*) case "${rest#:}" in ''|*[!0-9]*) return 0 ;; esac ;; *) return 0 ;; esac
      printf '%s' "${authority%%\]*}]" ;;
    *:*:*) return 0 ;;
    *:*) case "${authority#*:}" in ''|*[!0-9]*) return 0 ;; esac; printf '%s' "${authority%%:*}" ;;
    *) printf '%s' "$authority" ;;
  esac
}
is_loopback() { case "$1" in 127.0.0.1|localhost|'[::1]') return 0 ;; esac; return 1; }

base="${base%/}"
base_host="$(url_host "$base")"
loopback=0
case "$base" in
  https://slack.com/api) ;;
  http://*|https://*)
    if [ -n "$base_host" ] && is_loopback "$base_host"; then loopback=1
    else echo "probe: --url must be https://slack.com/api or a loopback address" >&2; exit 2; fi ;;
  *) echo "probe: --url must be an http(s) url" >&2; exit 2 ;;
esac
# file_url_ok URL: a file url the token may go to (https *.slack.com), or a loopback one when --url is loopback.
file_url_ok() {
  local h; h="$(url_host "$1")"
  [ -n "$h" ] || return 1
  case "$1" in https://*) case "$h" in *.slack.com) return 0 ;; esac ;; esac
  [ "$loopback" -eq 1 ] && is_loopback "$h" && case "$1" in http://*|https://*) return 0 ;; esac
  return 1
}

# --- keys ---
key="${AW_SLACK_PROBE_KEY:-}"
app_key="${AW_SLACK_PROBE_APP_KEY:-}"
if [ -z "$key" ]; then
  echo "probe: no Slack key. Read the probe app's User OAuth Token into AW_SLACK_PROBE_KEY (docs/sindri/slack-probe-manifest.yaml, step 6)." >&2
  exit 2
fi
if [ -n "$app_key" ]; then case "$app_key" in xapp-*) ;; *) echo "probe: AW_SLACK_PROBE_APP_KEY must be an app-level token (xapp-)" >&2; exit 2 ;; esac; fi
case "$key" in
  xoxp-*) token_kind=user ;; xoxe.xoxp-*) token_kind=user-rotating ;; xoxb-*) token_kind=bot ;; xapp-*) token_kind=app ;;
  xoxc-*) token_kind=cookie ;; *) token_kind=unknown ;;
esac

work="$(mktemp -d)"; chmod 700 "$work"
cleanup_armed=0 cleanup_done=0
# The one trap: the write part's cleanup (when armed; defined further down) and the temp dir, on any exit, SIGINT and SIGTERM included.
on_exit() { local rc=$?; if declare -F cleanup >/dev/null; then cleanup; fi; rm -rf "$work"; exit "$rc"; }
trap on_exit EXIT
trap 'exit 130' INT TERM

# Keys go to curl through 0600 config files, never an argument.
mkcfg() { (umask 077; printf 'header = "Authorization: Bearer %s"\n' "$2" > "$work/$1.cfg"); }
mkcfg user "$key"
if [ -n "$app_key" ]; then mkcfg app "$app_key"; fi
unset key app_key

lines=()
: > "$work/failures"
emit() { lines+=("$1: $2"); printf '%s: %s\n' "$1" "$2"; }
section() { lines+=("# line $1"); printf '# line %s\n' "$1"; }
# failed WHAT: a file, not an array, so a call inside $(...) is kept too
failed() { printf '%s\n' "$1" >> "$work/failures"; printf 'failed: %s\n' "$1" >&2; }

# api CFG NAME METHOD [field=value ...] -> $work/NAME.json (body), $work/NAME.h (headers).
# Returns 0 ok:true, 3 ok:false (an error code: a finding), 4 HTTP 429, 1 a transport failure or an answer that is not JSON.
api() {
  local cfg="$1" name="$2" method="$3" code kv args=()
  shift 3
  for kv in "$@"; do args+=(--data-urlencode "$kv"); done
  code="$(curl -sS -K "$work/$cfg.cfg" -X POST --max-time 30 -D "$work/$name.h" -o "$work/$name.json" -w '%{http_code}' \
    ${args[@]+"${args[@]}"} "$base/$method" 2>/dev/null)" || return 1
  [ "$code" = 429 ] && return 4
  jq -e type "$work/$name.json" >/dev/null 2>&1 || return 1
  case "$code" in 2*) ;; *) return 1 ;; esac
  if jq -e '.ok == true' "$work/$name.json" >/dev/null 2>&1; then return 0; fi
  return 3
}
# errcode NAME: Slack's error code, reduced to [a-z0-9_] (never a message).
errcode() { jq -rj '.error // "error" | tostring' "$work/$1.json" 2>/dev/null | tr -c 'a-z0-9_' '_' | cut -c1-40; }
hdr() { tr -d '\r' < "$1" | grep -i "^$2:" | head -n1 | cut -d: -f2- | sed 's/^ *//'; }   # FILE NAME -> that header's value
yn() { if [ "$(jq -r "$2" "$work/$1.json" 2>/dev/null)" = true ]; then echo yes; else echo no; fi; }   # NAME JQ-BOOLEAN
# result NAME RC METHOD: "yes", "no (code)", "rate limited" or "failed" (recorded as a failure)
result() {
  case "$2" in
    0) echo yes ;; 3) echo "no ($(errcode "$1"))" ;; 4) echo "rate limited" ;;
    *) failed "$3"; echo failed ;;
  esac
}
# burst LABEL METHOD field=value...: up to $burst calls, stopping at the first 429; prints the counts and the Retry-After.
burst() {
  local label="$1" method="$2" i ok=0 first=none retry=none rc
  shift 2
  for i in $(seq 1 "$burst"); do
    api user "Burst" "$method" "$@"; rc=$?
    if [ "$rc" -eq 4 ]; then first="$i"; retry="$(hdr "$work/Burst.h" retry-after | tr -cd '0-9')"; retry="${retry:-none}"; break; fi
    if [ "$rc" -eq 1 ]; then failed "$method (burst)"; break; fi
    [ "$rc" -eq 0 ] && ok=$((ok + 1))
  done
  emit "$label.burst.calls" "$burst"
  emit "$label.burst.ok" "$ok"
  emit "$label.burst.first_429_at" "$first"
  emit "$label.retry_after" "$retry"
}

# --- line 1: token kind and granted scopes ---
section "1: token kind and granted scopes (auth.test, x-oauth-scopes)"
emit token.kind "$token_kind"
api user Auth auth.test; rc=$?
auth_ok=0 user_id=""
case "$rc" in
  0) emit auth.ok yes; auth_ok=1; user_id="$(jq -r '.user_id // empty' "$work/Auth.json")"
     emit auth.bot_id "$(if jq -e '.bot_id' "$work/Auth.json" >/dev/null 2>&1; then echo present; else echo absent; fi)" ;;
  3) emit auth.ok "no ($(errcode Auth))"; failed "auth.test" ;;
  4) emit auth.ok "rate limited"; failed "auth.test" ;;
  *) emit auth.ok failed; failed "auth.test" ;;
esac
missing=0
if [ "$auth_ok" -eq 1 ]; then
  granted="$(hdr "$work/Auth.h" x-oauth-scopes | tr -d ' ' | tr ',' '\n' | grep -v '^$')"
  wanted="$(manifest_scopes)"
  emit scopes.wanted "$(grep -c . <<<"$wanted")"
  emit scopes.granted "$(grep -c . <<<"$granted" || true)"
  while IFS= read -r s; do
    if grep -qxF -- "$s" <<<"$granted"; then emit "scope.$s" yes; else emit "scope.$s" no; missing=$((missing + 1)); fi
  done <<<"$wanted"
  emit scopes.missing "$missing"
  emit scopes.extra "$(grep -vxF -f <(printf '%s\n' "$wanted") <<<"$granted" | grep -c . || true)"
fi

# The write guard comes before any other call: a refused channel ends the run with nothing written.
if [ "$auth_ok" -eq 1 ] && [ "$want_write" -eq 1 ]; then
  api user Scratch conversations.info "channel=$channel" include_num_members=true; rc=$?
  if [ "$rc" -ne 0 ] || ! jq -e --arg u "$user_id" '.channel | (.is_private == true) and (.is_im != true) and (.is_mpim != true)
      and (.creator == $u) and (.num_members == 1)' "$work/Scratch.json" >/dev/null 2>&1; then
    echo "refused: --channel must be a private channel you made, with no other member (conversations.info: $(result Scratch "$rc" conversations.info))" >&2
    exit 2
  fi
fi

if [ "$auth_ok" -ne 1 ]; then
  for n in 2 3 4 5 6 7 8; do emit "line $n" "skipped (auth.test failed)"; done
else

# --- line 2: DMs, group DMs, threads; does last_read give a channel unread count? ---
section "2: read DMs, group DMs, threads; last_read and unread_count (users.conversations, conversations.info)"
im="" mpim="" chan="" thread_chan="" thread_ts="" hot=""
api user Convs users.conversations types=public_channel,private_channel,mpim,im limit=200 exclude_archived=true; rc=$?
if [ "$rc" -eq 0 ]; then
  emit conversations.public "$(jq '[.channels[]? | select((.is_im != true) and (.is_mpim != true) and (.is_private != true))] | length' "$work/Convs.json")"
  emit conversations.private "$(jq '[.channels[]? | select((.is_im != true) and (.is_mpim != true) and (.is_private == true))] | length' "$work/Convs.json")"
  emit conversations.mpim "$(jq '[.channels[]? | select(.is_mpim == true)] | length' "$work/Convs.json")"
  emit conversations.im "$(jq '[.channels[]? | select(.is_im == true)] | length' "$work/Convs.json")"
  emit conversations.more_pages "$(if [ -n "$(jq -r '.response_metadata.next_cursor // ""' "$work/Convs.json")" ]; then echo yes; else echo no; fi)"
  im="$(jq -r '[.channels[]? | select(.is_im == true) | .id] | first // empty' "$work/Convs.json")"
  mpim="$(jq -r '[.channels[]? | select(.is_mpim == true) | .id] | first // empty' "$work/Convs.json")"
  chan="$(jq -r '[.channels[]? | select((.is_im != true) and (.is_mpim != true)) | .id] | first // empty' "$work/Convs.json")"
else emit conversations.list "$(result Convs "$rc" users.conversations)"; fi
for k in im mpim channel; do
  case "$k" in im) id="$im" ;; mpim) id="$mpim" ;; *) id="$chan" ;; esac
  if [ -z "$id" ]; then emit "read.$k" "not probed (none)"; continue; fi
  api user "Hist_$k" conversations.history "channel=$id" limit=50; rc=$?
  emit "read.$k" "$(result "Hist_$k" "$rc" "conversations.history ($k)")"
  [ "$rc" -eq 0 ] || continue
  [ -n "$hot" ] || hot="$id"
  if [ -z "$thread_ts" ]; then
    thread_ts="$(jq -r '[.messages[]? | select((.reply_count // 0) > 0) | .thread_ts // .ts] | first // empty' "$work/Hist_$k.json")"
    [ -n "$thread_ts" ] && thread_chan="$id"
  fi
done
if [ -n "$thread_ts" ]; then
  api user Replies conversations.replies "channel=$thread_chan" "ts=$thread_ts" limit=10; rc=$?
  r="$(result Replies "$rc" conversations.replies)"
  [ "$r" = yes ] && r="yes ($(jq '[.messages[]?] | length' "$work/Replies.json") messages)"
  emit read.thread "$r"
else emit read.thread "not probed (no thread in the newest 50 messages)"; fi
for k in channel im; do
  case "$k" in im) id="$im" ;; *) id="$chan" ;; esac
  if [ -z "$id" ]; then emit "info.$k" "not probed (none)"; continue; fi
  api user "Info_$k" conversations.info "channel=$id"; rc=$?
  if [ "$rc" -eq 0 ]; then
    emit "info.$k.last_read" "$(yn "Info_$k" '.channel | has("last_read")')"
    emit "info.$k.unread_count" "$(yn "Info_$k" '.channel | has("unread_count")')"
  else emit "info.$k" "$(result "Info_$k" "$rc" "conversations.info ($k)")"; fi
done

# --- line 3: history and replies tier ---
section "3: history and replies tier (limit ceiling, x-rate-limit headers, Retry-After after a burst)"
if [ -n "$hot" ]; then
  api user Ceiling conversations.history "channel=$hot" limit=1000; rc=$?
  if [ "$rc" -eq 0 ]; then
    emit history.limit_1000.returned "$(jq '[.messages[]?] | length' "$work/Ceiling.json")"
    emit history.limit_1000.has_more "$(yn Ceiling '.has_more')"
    rl="$(tr -d '\r' < "$work/Ceiling.h" | grep -iE '^x-rate-?limit' | while IFS=: read -r n v; do
      printf '%s=%s ' "$(tr 'A-Z' 'a-z' <<<"$n" | tr -cd 'a-z0-9-')" "$(tr -cd '0-9' <<<"$v")"; done)"
    emit history.ratelimit_headers "${rl:-none}"
  else emit history.limit_1000 "$(result Ceiling "$rc" "conversations.history (limit)")"; fi
  burst history conversations.history "channel=$hot" limit=1
else emit history "not probed (no readable conversation)"; fi
if [ -n "$thread_ts" ]; then burst replies conversations.replies "channel=$thread_chan" "ts=$thread_ts" limit=1
else emit replies "not probed (no thread)"; fi

# --- line 4: search ---
section "4: search.messages ok, error code, tier; to:me as a change detector"
api user Search search.messages query=to:me sort=timestamp sort_dir=desc count=2; rc=$?
emit search.ok "$(result Search "$rc" search.messages)"
if [ "$rc" -eq 0 ]; then
  emit search.to_me.total "$(jq '.messages.paging.total // .messages.total // 0' "$work/Search.json")"
  emit search.to_me.newest_first "$(jq -r '[.messages.matches[]?.ts | tonumber] as $t
    | if ($t | length) < 2 then "unknown" elif $t[0] >= $t[1] then "yes" else "no" end' "$work/Search.json")"
  burst search search.messages query=to:me sort=timestamp count=1
else emit search.burst "skipped (search.messages refused)"; fi

# --- line 5: Socket Mode ---
section "5: apps.connections.open with the user token and the xapp- token"
api user SockUser apps.connections.open; rc=$?
emit socket.user_token "$(result SockUser "$rc" "apps.connections.open (user token)")"
if [ -f "$work/app.cfg" ]; then
  api app SockApp apps.connections.open; rc=$?
  emit socket.app_token "$(result SockApp "$rc" "apps.connections.open (app token)")"
  if [ "$rc" -eq 0 ]; then
    wss="$(jq -r '.url // ""' "$work/SockApp.json")"; h="$(url_host "$wss")"
    case "$wss" in wss://*) case "$h" in *.slack.com) emit socket.url_host_ok yes ;; *) emit socket.url_host_ok no ;; esac ;; *) emit socket.url_host_ok no ;; esac
  fi
else emit socket.app_token "skipped (no AW_SLACK_PROBE_APP_KEY)"; fi
emit socket.user_events "check by hand (manifest step 2: did Slack accept user_events?)"

# --- line 6: emoji hosts (read-only), then the writes in the scratch channel ---
section "6: emoji and file hosts; with --write: post with metadata, read back, update, react, mark, schedule, upload, delete"
api user Emoji emoji.list; rc=$?
if [ "$rc" -eq 0 ]; then
  emit emoji.count "$(jq '.emoji // {} | length' "$work/Emoji.json")"
  hosts="$(jq -r '.emoji // {} | .[] | select(startswith("https://"))' "$work/Emoji.json" | while IFS= read -r u; do url_host "$u"; echo; done \
    | grep -E '^[a-z0-9.-]+$' | sort -u | paste -sd, -)"
  emit emoji.hosts "${hosts:-none}"
else emit emoji "$(result Emoji "$rc" emoji.list)"; fi

posted=() sched=() file_id=""
# cleanup: delete every message, scheduled message and file this run made, then read the channel again. Runs once, from the main
# flow or the EXIT trap.
cleanup() {
  [ "$cleanup_armed" -eq 1 ] && [ "$cleanup_done" -eq 0 ] || return 0
  cleanup_done=1
  local ts id n=0 m=0 left
  for ts in ${posted[@]+"${posted[@]}"}; do
    m=$((m + 1)); api user Del chat.delete "channel=$channel" "ts=$ts" && n=$((n + 1)) || failed "cleanup chat.delete"
  done
  emit cleanup.messages "deleted $n of $m"
  n=0 m=0
  for id in ${sched[@]+"${sched[@]}"}; do
    m=$((m + 1)); api user DelSched chat.deleteScheduledMessage "channel=$channel" "scheduled_message_id=$id" && n=$((n + 1)) || failed "cleanup chat.deleteScheduledMessage"
  done
  emit cleanup.scheduled "deleted $n of $m"
  n=0 m=0
  if [ -n "$file_id" ]; then m=1; api user DelFile files.delete "file=$file_id" && n=1 || failed "cleanup files.delete"; fi
  emit cleanup.files "deleted $n of $m"
  if [ "${#posted[@]}" -gt 0 ]; then
    if api user Left conversations.history "channel=$channel" "oldest=${posted[0]}" inclusive=true limit=100; then
      left="$(jq -r '.messages[]?.ts' "$work/Left.json" | grep -cxF -f <(printf '%s\n' "${posted[@]}") || true)"
      emit cleanup.left "$left"
      [ "$left" -eq 0 ] || failed "cleanup left $left message(s)"
    else emit cleanup.left unknown; failed "cleanup re-read"; fi
  else emit cleanup.left 0; fi
}

write_part() {
  local rc act ts r now body url
  emit write.channel "private, mine, 1 member"
  act="$(od -An -N8 -tx1 /dev/urandom | tr -d ' \n')"
  cleanup_armed=1
  api user Post chat.postMessage "channel=$channel" "text=sindri-probe ${act:0:8}" \
    "metadata=$(jq -nc --arg a "$act" '{event_type:"sindri_probe",event_payload:{act_id:$a}}')"; rc=$?
  emit post.ok "$(result Post "$rc" chat.postMessage)"
  [ "$rc" -eq 0 ] || return
  ts="$(jq -r '.ts // empty' "$work/Post.json")"
  [ -n "$ts" ] || { failed "chat.postMessage (no ts)"; return; }
  posted+=("$ts")
  api user Back conversations.history "channel=$channel" "latest=$ts" inclusive=true limit=1 include_all_metadata=true; rc=$?
  if [ "$rc" -eq 0 ]; then
    emit post.metadata_kept "$(if [ "$(jq -r '.messages[0].metadata.event_payload.act_id // ""' "$work/Back.json")" = "$act" ]; then echo yes; else echo no; fi)"
  else emit post.metadata_kept "$(result Back "$rc" "conversations.history (read back)")"; fi
  api user Update chat.update "channel=$channel" "ts=$ts" "text=sindri-probe ${act:0:8} edited"; rc=$?
  emit update.ok "$(result Update "$rc" chat.update)"
  api user React reactions.add "channel=$channel" "timestamp=$ts" name=eyes; rc=$?
  emit react.ok "$(result React "$rc" reactions.add)"
  api user React2 reactions.add "channel=$channel" "timestamp=$ts" name=eyes; rc=$?
  r="$(result React2 "$rc" "reactions.add (again)")"; case "$r" in "no ("*) r="${r#no (}"; r="${r%)}" ;; yes) r="no error" ;; esac
  emit react.repeat "$r"
  api user Unreact reactions.remove "channel=$channel" "timestamp=$ts" name=eyes; rc=$?
  emit react.remove.ok "$(result Unreact "$rc" reactions.remove)"
  api user Mark conversations.mark "channel=$channel" "ts=$ts"; rc=$?
  emit mark.ok "$(result Mark "$rc" conversations.mark)"
  now="$(date +%s)"
  api user Sched chat.scheduleMessage "channel=$channel" "text=sindri-probe ${act:0:8} scheduled" "post_at=$((now + 3600))"; rc=$?
  emit schedule.ok "$(result Sched "$rc" chat.scheduleMessage)"
  if [ "$rc" -eq 0 ]; then sched+=("$(jq -r '.scheduled_message_id' "$work/Sched.json")"); fi
  api user Sched121 chat.scheduleMessage "channel=$channel" "text=sindri-probe ${act:0:8} far" "post_at=$((now + 121 * 86400))"; rc=$?
  r="$(result Sched121 "$rc" "chat.scheduleMessage (121 days)")"
  if [ "$rc" -eq 0 ]; then sched+=("$(jq -r '.scheduled_message_id' "$work/Sched121.json")"); r=accepted; fi
  case "$r" in "no ("*) r="${r#no (}"; r="${r%)}" ;; esac
  emit schedule.121_days "$r"

  # upload: the two-step external upload; the upload url gets the bytes and never the token
  body="$work/upload.txt"; printf 'sindri-probe %s\n' "${act:0:8}" > "$body"
  api user UpUrl files.getUploadURLExternal filename=sindri-probe.txt "length=$(wc -c < "$body" | tr -d ' ')"; rc=$?
  if [ "$rc" -ne 0 ]; then emit upload.ok "$(result UpUrl "$rc" files.getUploadURLExternal)"; return; fi
  url="$(jq -r '.upload_url // ""' "$work/UpUrl.json")"; file_id="$(jq -r '.file_id // ""' "$work/UpUrl.json")"
  if ! file_url_ok "$url"; then emit upload.url_host_ok no; file_id=""; return; fi
  emit upload.url_host_ok yes
  r="$(curl -sS -X POST --max-time 30 -o "$work/Up.out" -w '%{http_code}' --data-binary @"$body" "$url" 2>/dev/null)" || { failed "upload post"; file_id=""; return; }
  case "$r" in 2*) ;; *) emit upload.ok "no (http $(tr -cd '0-9' <<<"$r"))"; file_id=""; return ;; esac
  api user UpDone files.completeUploadExternal "files=$(jq -nc --arg id "$file_id" '[{id:$id,title:"sindri-probe"}]')" "channel_id=$channel"; rc=$?
  emit upload.ok "$(result UpDone "$rc" files.completeUploadExternal)"
  [ "$rc" -eq 0 ] || return
  api user FileInfo files.info "file=$file_id"; rc=$?
  [ "$rc" -eq 0 ] || { emit file.info "$(result FileInfo "$rc" files.info)"; return; }
  url="$(jq -r '.file.url_private // ""' "$work/FileInfo.json")"
  if ! file_url_ok "$url"; then emit file.url_private_host_ok no; return; fi
  emit file.url_private_host_ok yes
  r="$(curl -sS -K "$work/user.cfg" --max-time 30 -o /dev/null -w '%{http_code}' "$url" 2>/dev/null)" || r=000
  emit file.fetch_with_token "$(tr -cd '0-9' <<<"$r")"
  r="$(curl -sS --max-time 30 -o /dev/null -w '%{http_code}' "$url" 2>/dev/null)" || r=000
  emit file.fetch_without_token "$(tr -cd '0-9' <<<"$r")"
}
if [ "$want_write" -eq 1 ]; then write_part; cleanup; else emit write "skipped (no --write)"; fi

# --- line 7: slash commands and block actions ---
section "7: chat.command with a user token; block actions"
api user Command chat.command; rc=$?
case "$rc" in
  0) emit chat.command.exists yes ;;
  3) c="$(errcode Command)"; if [ "$c" = unknown_method ]; then emit chat.command.exists "no (unknown_method)"; else emit chat.command.exists "yes ($c)"; fi ;;
  *) emit chat.command.exists "$(result Command "$rc" chat.command)" ;;
esac
emit block_action.user_token "not probed (no Web API method sends a block action)"

# --- line 8: workspace approval and the manifest link ---
section "8: workspace approval per scope; the prefilled manifest link"
emit approval.scopes_missing "$missing"
emit manifest.link "check by hand (scripts/slack-probe.sh --manifest-link, manifest step 1)"
fi

echo
echo "paste this into the M0c issue"
echo '```'
printf '%s\n' ${lines[@]+"${lines[@]}"}   # bash 3.2 calls an empty array unbound under set -u
echo '```'
if [ -s "$work/failures" ]; then
  printf 'probe: %d call(s) failed: %s\n' "$(grep -c . "$work/failures")" "$(paste -sd';' - < "$work/failures")" >&2
  exit 1
fi
exit 0
