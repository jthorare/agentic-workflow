#!/usr/bin/env bash
# Tests for scripts/slack-probe.sh with a stub curl (a small stateful Slack, tests/fixtures/slack-probe). It never calls slack.com.
# Run: bash scripts/tests/slack-probe.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"
FIX="$ROOT/tests/fixtures/slack-probe"
SCRIPT="$ROOT/scripts/slack-probe.sh"
MANIFEST="$ROOT/docs/sindri/slack-probe-manifest.yaml"
fail=0 passed=0 failed=0

check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; passed=$((passed + 1)); else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1; failed=$((failed + 1))
  fi
}
has() { # name haystack needle
  if grep -qF -- "$3" <<<"$2"; then echo "ok - $1"; passed=$((passed + 1)); else echo "not ok - $1 (missing: $3)"; sed 's/^/    /' <<<"$2"; fail=1; failed=$((failed + 1)); fi
}

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
# The fake tokens are built from parts so the secret scrubber does not read them as real ones.
X=xox A=xap
KEY="${X}p-1111-2222-PROBESECRETKEY" APPKEY="${A}p-1-A0PROBE-3333-APPSECRETKEY" BOTKEY="${X}b-1-BOTSECRET"
mkdir -p "$WORK/bin"
cp "$FIX/curl" "$WORK/bin/"; chmod 755 "$WORK/bin/curl"
export STUB_FIX="$FIX" STUB_LOG="$WORK/stub.log" STUB_STATE="$WORK/stub-state" PATH="$WORK/bin:$PATH"
unset AW_SLACK_PROBE_KEY AW_SLACK_PROBE_APP_KEY STUB_ERR STUB_FAIL STUB_429 STUB_SCOPES_DROP STUB_SCRATCH STUB_NO_METADATA STUB_KEEP STUB_WSS_HOST
LOOP="http://127.0.0.1:9/api"

run() { # env assignments first, then script args after a literal --; sets $out and $rc; resets the stub's state and log
  local envs=() args=()
  while [ $# -gt 0 ] && [ "$1" != -- ]; do envs+=("$1"); shift; done
  [ "${1:-}" = -- ] && shift
  args=("$@")
  rm -rf "$STUB_STATE"; : > "$STUB_LOG"
  out="$(env ${envs[@]+"${envs[@]}"} bash "$SCRIPT" ${args[@]+"${args[@]}"} 2>&1)"; rc=$?
}
count() { grep -c -- "$1" "$2" || true; }        # count PATTERN FILE
countin() { printf '%s\n' "$2" | grep -c -- "$1" || true; }   # countin PATTERN TEXT
writes() { grep -cE 'method=(chat\.(postMessage|update|delete|scheduleMessage|deleteScheduledMessage)|reactions\.(add|remove)|conversations\.mark|files\.(getUploadURLExternal|completeUploadExternal|delete)|UPLOAD) ' "$STUB_LOG" || true; }

check "slack-probe.sh parses" "$(bash -n "$SCRIPT" 2>&1; echo $?)" 0

# --- Probe_prints_counts_and_never_a_name: the read-only run prints every line's facts as counts and yes/no, and no name or text ---
run AW_SLACK_PROBE_KEY="$KEY" AW_SLACK_PROBE_APP_KEY="$APPKEY" -- --url "$LOOP" --burst 5
check "Probe_prints_counts_and_never_a_name: exit 0" "$rc" 0
for want in "token.kind: user" "auth.ok: yes" "scopes.wanted: 25" "scopes.granted: 25" "scopes.missing: 0" "scope.im:history: yes" \
  "scope.pins:write: yes" "conversations.public: 2" "conversations.private: 1" "conversations.mpim: 1" "conversations.im: 3" \
  "conversations.more_pages: no" "read.im: yes" "read.mpim: yes" "read.channel: yes" "read.thread: yes (3 messages)" \
  "info.channel.last_read: yes" "info.channel.unread_count: no" "info.im.last_read: yes" "info.im.unread_count: yes" \
  "history.limit_1000.returned: 3" "history.limit_1000.has_more: no" "history.burst.calls: 5" "history.burst.ok: 5" \
  "history.burst.first_429_at: none" "history.retry_after: none" "history.ratelimit_headers: none" \
  "replies.burst.calls: 5" "replies.burst.first_429_at: none" \
  "search.ok: yes" "search.to_me.total: 7" "search.to_me.newest_first: yes" "search.burst.first_429_at: none" \
  "socket.user_token: no (not_allowed_token_type)" "socket.app_token: yes" "socket.url_host_ok: yes" \
  "emoji.count: 3" "emoji.hosts: emoji.slack-edge.com" "write: skipped (no --write)" \
  "chat.command.exists: yes (invalid_arguments)" "block_action.user_token: not probed" \
  "approval.scopes_missing: 0" "manifest.link: check by hand" "paste this into the M0c issue"; do
  has "Probe_prints_counts_and_never_a_name: $want" "$out" "$want"
done
check "Probe_prints_counts_and_never_a_name: no channel name, user name or text" "$(countin 'SECRET' "$out")" 0
check "Probe_prints_counts_and_never_a_name: no conversation or user id" "$(printf '%s\n' "$out" | grep -cE '\b[CDGTU]0[A-Z0-9]+' || true)" 0
check "Probe_prints_counts_and_never_a_name: the key is never printed" "$(countin "$KEY" "$out")" 0
check "Probe_prints_counts_and_never_a_name: the app key is never printed" "$(countin "$APPKEY" "$out")" 0
check "Probe_prints_counts_and_never_a_name: the key is never a curl argument" "$(count "PROBESECRETKEY" "$STUB_LOG")" 0
check "Probe_prints_counts_and_never_a_name: the app key is never a curl argument" "$(count "APPSECRETKEY" "$STUB_LOG")" 0
check "Probe_prints_counts_and_never_a_name: no write call" "$(writes)" 0
check "Probe_prints_counts_and_never_a_name: no conversations.mark" "$(count 'method=conversations.mark' "$STUB_LOG")" 0
check "Probe_prints_counts_and_never_a_name: every line has a header" "$(countin '^# line [1-8]:' "$out")" 8

# --- a missing scope and no app key ---
run AW_SLACK_PROBE_KEY="$KEY" STUB_SCOPES_DROP=im:history -- --url "$LOOP" --burst 2
check "missing scope: exit 0" "$rc" 0
has "missing scope: counted" "$out" "scopes.missing: 1"
has "missing scope: named" "$out" "scope.im:history: no"
has "missing scope: line 8" "$out" "approval.scopes_missing: 1"
has "no app key: line 5 says so" "$out" "socket.app_token: skipped (no AW_SLACK_PROBE_APP_KEY)"

# --- the key ---
run -- --url "$LOOP"
check "missing key: exit 2" "$rc" 2
has "missing key: says so" "$out" "no Slack key"
check "missing key: nothing sent" "$(count 'curl ran' "$STUB_LOG")" 0
run AW_SLACK_PROBE_KEY="$KEY" AW_SLACK_PROBE_APP_KEY="$KEY" -- --url "$LOOP"
check "an app key that is not xapp-: exit 2" "$rc" 2
check "an app key that is not xapp-: nothing sent" "$(count 'curl ran' "$STUB_LOG")" 0
check "an app key that is not xapp-: key not printed" "$(countin "$KEY" "$out")" 0
run AW_SLACK_PROBE_KEY="$BOTKEY" -- --url "$LOOP" --burst 1
check "a bot token: exit 0" "$rc" 0
has "a bot token: named by its kind" "$out" "token.kind: bot"
check "a bot token: never printed" "$(countin BOTSECRET "$out")" 0

# --- --write needs --channel ---
run AW_SLACK_PROBE_KEY="$KEY" -- --url "$LOOP" --write
check "--write without --channel: exit 2" "$rc" 2
has "--write without --channel: says so" "$out" "--write needs --channel"
check "--write without --channel: nothing sent" "$(count 'curl ran' "$STUB_LOG")" 0
run AW_SLACK_PROBE_KEY="$KEY" -- --url "$LOOP" --channel G0PROBE
check "--channel without --write: exit 2" "$rc" 2
run AW_SLACK_PROBE_KEY="$KEY" -- --url "$LOOP" --write --channel '#general'
check "--channel takes an id: exit 2" "$rc" 2
check "--channel takes an id: nothing sent" "$(count 'curl ran' "$STUB_LOG")" 0

# --- 429 and error answers ---
run AW_SLACK_PROBE_KEY="$KEY" STUB_429=conversations.history:7 -- --url "$LOOP" --burst 10
check "429: exit 0 (a 429 is a finding)" "$rc" 0
has "429: the burst stops at the first one" "$out" "history.burst.first_429_at: 4"
has "429: Retry-After read" "$out" "history.retry_after: 30"
has "429: ok calls counted" "$out" "history.burst.ok: 3"
run AW_SLACK_PROBE_KEY="$KEY" STUB_ERR=search.messages:not_allowed_token_type -- --url "$LOOP" --burst 2
check "an error answer: exit 0 (an error code is a finding)" "$rc" 0
has "an error answer: the code" "$out" "search.ok: no (not_allowed_token_type)"
has "an error answer: no burst on a refused method" "$out" "search.burst: skipped (search.messages refused)"
run AW_SLACK_PROBE_KEY="$KEY" STUB_ERR=auth.test:invalid_auth -- --url "$LOOP"
check "invalid_auth: exit 1" "$rc" 1
has "invalid_auth: the code" "$out" "auth.ok: no (invalid_auth)"
has "invalid_auth: the rest skipped" "$out" "line 2: skipped (auth.test failed)"
check "invalid_auth: one call only" "$(count 'curl ran' "$STUB_LOG")" 1
run AW_SLACK_PROBE_KEY="$KEY" STUB_FAIL=emoji.list -- --url "$LOOP" --burst 1
check "a transport failure: exit 1" "$rc" 1
has "a transport failure: names the call" "$out" "failed: emoji.list"
has "a transport failure: the paste block still prints" "$out" "paste this into the M0c issue"

# --- the write part in the scratch channel ---
run AW_SLACK_PROBE_KEY="$KEY" -- --url "$LOOP" --burst 1 --write --channel G0PROBE
check "write: exit 0" "$rc" 0
for want in "write.channel: private, mine, 1 member" "post.ok: yes" "post.metadata_kept: yes" "update.ok: yes" "react.ok: yes" \
  "react.repeat: already_reacted" "react.remove.ok: yes" "mark.ok: yes" "schedule.ok: yes" "schedule.121_days: time_too_far" \
  "upload.url_host_ok: yes" "upload.ok: yes" "file.url_private_host_ok: yes" "file.fetch_with_token: 200" \
  "file.fetch_without_token: 302" "cleanup.messages: deleted 1 of 1" "cleanup.scheduled: deleted 1 of 1" \
  "cleanup.files: deleted 1 of 1" "cleanup.left: 0"; do has "write: $want" "$out" "$want"; done
check "write: nothing written outside the scratch channel" "$(count 'WRITE OUTSIDE' "$STUB_LOG")" 0
check "write: the upload url never gets the token" "$(count 'UPLOAD with token' "$STUB_LOG")" 0
check "write: no name or text" "$(countin 'SECRET' "$out")" 0
check "write: the key is never printed" "$(countin "$KEY" "$out")" 0
check "write: the key is never a curl argument" "$(count "PROBESECRETKEY" "$STUB_LOG")" 0
run AW_SLACK_PROBE_KEY="$KEY" STUB_NO_METADATA=1 -- --url "$LOOP" --burst 1 --write --channel G0PROBE
has "write: metadata dropped is reported" "$out" "post.metadata_kept: no"
run AW_SLACK_PROBE_KEY="$KEY" STUB_KEEP=1 -- --url "$LOOP" --burst 1 --write --channel G0PROBE
check "write: a message left behind: exit 1" "$rc" 1
has "write: a message left behind: counted" "$out" "cleanup.left: 1"
run AW_SLACK_PROBE_KEY="$KEY" STUB_FAIL=chat.update -- --url "$LOOP" --burst 1 --write --channel G0PROBE
check "write: a failure mid-write: exit 1" "$rc" 1
has "write: a failure mid-write: names it" "$out" "failed: chat.update"
has "write: a failure mid-write: cleanup still ran" "$out" "cleanup.left: 0"
for s in public not-mine members; do
  run AW_SLACK_PROBE_KEY="$KEY" STUB_SCRATCH="$s" -- --url "$LOOP" --write --channel G0PROBE
  check "write guard ($s): exit 2" "$rc" 2
  has "write guard ($s): refused" "$out" "refused: --channel must be a private channel you made, with no other member"
  check "write guard ($s): nothing written" "$(writes)" 0
done

# --- the url guard ---
for u in "http://localhost:9/api" "http://[::1]:9/api"; do
  run AW_SLACK_PROBE_KEY="$KEY" -- --url "$u" --burst 1
  check "loopback $u accepted: exit 0" "$rc" 0
done
AT="@"   # built apart so the secret scrubber does not read the userinfo url as a credential
for u in "https://example.com/api" "http://slack.com/api" "https://slack.com.evil.test/api" "http://127.0.0.1:80${AT}evil.com/api" \
  "http://127.0.0.1.evil.com/api" "http://[::1]x/api" "ftp://127.0.0.1/api"; do
  run AW_SLACK_PROBE_KEY="$KEY" -- --url "$u"
  check "url $u refused: exit 2" "$rc" 2
  check "url $u refused: nothing sent" "$(count 'curl ran' "$STUB_LOG")" 0
done
run AW_SLACK_PROBE_KEY="$KEY" -- --url "$LOOP" --burst 0
check "--burst 0: exit 2" "$rc" 2
run AW_SLACK_PROBE_KEY="$KEY" -- --nope
check "bad flag: exit 2" "$rc" 2

# --- the manifest: every scope of §5.1, rotation off, and the prefilled link ---
want_scopes="channels:read groups:read im:read mpim:read channels:history groups:history im:history mpim:history users:read emoji:read search:read chat:write reactions:read reactions:write channels:write groups:write im:write mpim:write files:read files:write users.profile:write dnd:write dnd:read pins:read pins:write"
got_scopes="$(awk '/^    user:/{u=1; next} u && /^      - /{print $2; next} u && /^      #/{next} u{exit}' "$MANIFEST" | paste -sd' ' -)"
check "manifest: the §5.1 scopes, in order" "$got_scopes" "$want_scopes"
has "manifest: token rotation off" "$(cat "$MANIFEST")" "token_rotation_enabled: false"
check "manifest: no bot scopes" "$(grep -c '^    bot:' "$MANIFEST" || true)" 0
run -- --manifest-link
check "--manifest-link: exit 0 with no key" "$rc" 0
has "--manifest-link: Slack's create-from-manifest link" "$out" "https://api.slack.com/apps?new_app=1&manifest_yaml="
has "--manifest-link: holds the scopes" "$out" "chat%3Awrite"
check "--manifest-link: comments left out" "$(countin 'By-hand' "$out")" 0
check "--manifest-link: nothing sent" "$(count 'curl ran' "$STUB_LOG")" 0

echo "$passed passed, 0 skipped, $failed failed"
if [ "$fail" -eq 0 ]; then echo "all ok"; else echo "FAILED"; exit 1; fi
