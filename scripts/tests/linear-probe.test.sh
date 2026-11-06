#!/usr/bin/env bash
# Tests for scripts/linear-probe.sh with a stub curl (a small stateful Linear). Run: bash scripts/tests/linear-probe.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"
FIX="$ROOT/tests/fixtures/linear-probe"
SCRIPT="$ROOT/scripts/linear-probe.sh"
fail=0

check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
has() { # name haystack needle
  if grep -qF -- "$3" <<<"$2"; then echo "ok - $1"; else echo "not ok - $1 (missing: $3)"; sed 's/^/    /' <<<"$2"; fail=1; fi
}

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
RKEY="lin_api_READSECRET123" WKEY="lin_api_WRITESECRET456"
export HOME="$WORK/home" AW_STATE_DIR="$WORK/state"
mkdir -p "$HOME" "$AW_STATE_DIR/sindri-app/profile" "$WORK/bin"
cp "$FIX/curl" "$WORK/bin/"
export STUB_FIX="$FIX" STUB_LOG="$WORK/stub.log" STUB_STATE="$WORK/stub-state" PATH="$WORK/bin:$PATH"
unset AW_SINDRI_HOME AW_LINEAR_PROBE_KEY AW_LINEAR_PROBE_WRITE_KEY STUB_ID_MODE STUB_SHARED STUB_STRAY STUB_STRAY_N STUB_BLOCK_WRITES STUB_FAIL_CREATE_N STUB_FAIL STUB_TEAM_EXTRA STUB_PAGE STUB_ARCHIVE_VISIBLE STUB_SLEEP_OP \
  PROBE_TEST_LINEAR_KEY PROBE_TEST_LINEAR_WRITE_KEY
LOOP="http://127.0.0.1:9/graphql"

profile() { cp "$FIX/$1" "$AW_STATE_DIR/sindri-app/profile/profile.yaml"; }
run() { # env assignments first, then script args after a literal --; sets $out and $rc; resets the stub's state and log
  local envs=() args=()
  while [ $# -gt 0 ] && [ "$1" != -- ]; do envs+=("$1"); shift; done
  [ "${1:-}" = -- ] && shift
  args=("$@")
  rm -rf "$STUB_STATE"; : > "$STUB_LOG"
  # ${a[@]+"${a[@]}"}: bash 3.2 (macOS) calls an empty array unbound under set -u
  out="$(env ${envs[@]+"${envs[@]}"} bash "$SCRIPT" ${args[@]+"${args[@]}"} 2>&1)"; rc=$?
}
count() { grep -c -- "$1" "$2" || true; }   # count PATTERN FILE
countin() { printf '%s\n' "$2" | grep -c -- "$1" || true; }   # countin PATTERN TEXT

# --- happy read-only: every fact line, zero mutations ---
profile profile-nowrite.yaml
run PROBE_TEST_LINEAR_KEY="$RKEY" -- --url "$LOOP"
check "read-only: exit 0" "$rc" 0
for want in "mutation.issueCreate: yes" "mutation.issueUpdate: yes" "mutation.commentCreate: yes" "mutation.commentUpdate: yes" \
  "mutation.attachmentLinkURL: yes" "mutation.issueArchive: yes" "schema.IssueCreateInput.id: yes" "schema.CommentCreateInput.id: yes" \
  "schema.IssueUpdateInput.conditional: none" "key.scope: read,write" "ratelimit.read.requests_limit: 1500" \
  "ratelimit.read.complexity_limit: 3000000" "ratelimit.read.complexity: 4" \
  "ratelimit.write: skipped (no write key)" "ratelimit.shared_between_keys: unknown" "mention.scanned: 6" "mention.at_name: 2" \
  "mention.profile_url: 1" "mention.other: 1" "paste this into the Ph0 PR"; do has "read-only: $want" "$out" "$want"; done
check "read-only: no mutation sent" "$(count '^MUTATION' "$STUB_LOG")" 0
check "read-only: no cleanup lines" "$(countin '^cleanup' "$out")" 0
check "read key never in output" "$(countin "$RKEY" "$out")" 0
check "Linear text never in output" "$(countin 'SECRET' "$out")" 0
check "read key never a curl argument" "$(count "$RKEY" "$STUB_LOG")" 0

# --- happy write: label made, labelled issues, cleanup archived all, nothing left ---
profile profile.yaml
run PROBE_TEST_LINEAR_KEY="$RKEY" PROBE_TEST_LINEAR_WRITE_KEY="$WKEY" -- --url "$LOOP" --team ENG --write
check "write: exit 0" "$rc" 0
for want in "label: created" "write.allowed: yes" "create.client_id: accepted" "create.repeat_client_id: DUPLICATE_ID" \
  "comment.client_id: accepted" "comment.repeat_client_id: DUPLICATE_ID" "attachment.repeat_url: one" \
  "updatedAt.bumped_by.comment: yes" "updatedAt.bumped_by.attachment: no" "updatedAt.bumped_by.update: yes" \
  "cap.title_300: rejected" "cap.description_70k: ok" "ratelimit.write.requests_limit: 1500" "ratelimit.shared_between_keys: no" \
  "cleanup: archived 1 of 1" "cleanup.left: 0" "archived.hidden_by_default: yes" "pagination.after_cursor: not probed (one issue in the team)" \
  "team_move.old_key_resolves: not probed" "ratelimit.read.remaining_before: 1497 1496" "ratelimit.read.remaining_after: 1496 1495" \
  "ratelimit.write.calls_between: 1 1"; do has "write: $want" "$out" "$want"; done
has "write: an env: write pointer is accepted with a loopback url" "$out" "write.allowed: yes"
check "write: an empty baseline prints no warning" "$(countin '^warning:' "$out")" 0
check "write: no unlabelled create" "$(count UNLABELLED "$STUB_LOG")" 0
check "write: no unlabelled archive" "$(count 'ARCHIVE unlabelled' "$STUB_LOG")" 0
check "write: read key never in output" "$(countin "$RKEY" "$out")" 0
check "write: write key never in output" "$(countin "$WKEY" "$out")" 0
check "write: Linear text never in output" "$(countin 'SECRET' "$out")" 0
check "write: no key in curl args" "$(count "$WKEY" "$STUB_LOG")" 0

# --- client ids ignored, a shared rate budget, a pre-existing labelled issue left alone ---
run PROBE_TEST_LINEAR_KEY="$RKEY" PROBE_TEST_LINEAR_WRITE_KEY="$WKEY" STUB_ID_MODE=ignore STUB_SHARED=1 STUB_STRAY=1 -- --url "$LOOP" --team ENG --write
check "variants: exit 0" "$rc" 0
has "variants: ids ignored" "$out" "create.client_id: ignored"
has "variants: second issue" "$out" "create.repeat_client_id: second-issue"
has "variants: comment ids ignored" "$out" "comment.client_id: ignored"
has "variants: shared budget" "$out" "ratelimit.shared_between_keys: yes"
has "variants: only ours archived" "$out" "cleanup: archived 2 of 2"
has "variants: the pre-existing one is warned about" "$out" "warning: 1 sindri-probe issues existed before this run; left alone"
check "variants: the pre-existing one is never archived" "$(count 'ARCHIVED stray-1' "$STUB_LOG")" 0
has "variants: shared budget raw numbers" "$out" "ratelimit.read.remaining_before: 1497 1495"
has "variants: nothing left" "$out" "cleanup.left: 0"

# --- a full baseline page (250 labelled issues) may be cut off: the probe writes nothing and archives nothing ---
run PROBE_TEST_LINEAR_KEY="$RKEY" PROBE_TEST_LINEAR_WRITE_KEY="$WKEY" STUB_STRAY_N=250 -- --url "$LOOP" --team ENG --write
check "full baseline: exit 1" "$rc" 1
has "full baseline: refused" "$out" "refused: 250 or more sindri-probe issues already exist in the team"
has "full baseline: failed line" "$out" "failed: baseline full (250 or more labelled issues), nothing written"
check "full baseline: nothing archived" "$(count 'ARCHIVED' "$STUB_LOG")" 0

# --- paging ok with two issues in the team; a repeated page fails; an archived issue visible by default ---
run PROBE_TEST_LINEAR_KEY="$RKEY" PROBE_TEST_LINEAR_WRITE_KEY="$WKEY" STUB_TEAM_EXTRA=1 STUB_ARCHIVE_VISIBLE=1 -- --url "$LOOP" --team ENG --write
check "paging: exit 0" "$rc" 0
has "paging: second page follows the first" "$out" "pagination.after_cursor: ok"
has "archive visible: reported" "$out" "archived.hidden_by_default: no"
run PROBE_TEST_LINEAR_KEY="$RKEY" PROBE_TEST_LINEAR_WRITE_KEY="$WKEY" STUB_TEAM_EXTRA=1 STUB_PAGE=repeat -- --url "$LOOP" --team ENG --write
check "paging repeat: exit 1" "$rc" 1
has "paging repeat: failed" "$out" "pagination.after_cursor: failed"
has "paging repeat: cleanup still ran" "$out" "cleanup.left: 0"

# --- SIGINT in the middle of a write run still cleans up (a python launcher: a bash `&` job ignores SIGINT) ---
rm -rf "$STUB_STATE"; : > "$STUB_LOG"
sig="$(PROBE_TEST_LINEAR_KEY="$RKEY" PROBE_TEST_LINEAR_WRITE_KEY="$WKEY" STUB_SLEEP_OP=CommentCreate python3 -I - "$SCRIPT" "$STUB_LOG" "$LOOP" <<'PY'
import os, signal, subprocess, sys, time
script, log, url = sys.argv[1:4]
p = subprocess.Popen(["bash", script, "--url", url, "--team", "ENG", "--write"], stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True,
                     preexec_fn=lambda: signal.signal(signal.SIGINT, signal.SIG_DFL))
end = time.time() + 30
while time.time() < end and "op=mutation CommentCreate" not in open(log).read():
    time.sleep(0.05)
os.kill(p.pid, signal.SIGINT)
out, _ = p.communicate(timeout=60)
print(out, end=""); print("RC=%d" % p.returncode)
PY
)"
has "SIGINT: exit 130" "$sig" "RC=130"
has "SIGINT: cleanup archived the issue" "$sig" "cleanup: archived 1 of 1"
has "SIGINT: nothing left" "$sig" "cleanup.left: 0"

# --- client ids refused ---
run PROBE_TEST_LINEAR_KEY="$RKEY" PROBE_TEST_LINEAR_WRITE_KEY="$WKEY" STUB_ID_MODE=refuse -- --url "$LOOP" --team ENG --write
check "refuse: exit 0" "$rc" 0
has "refuse: client id refused" "$out" "create.client_id: refused"
has "refuse: still cleaned" "$out" "cleanup.left: 0"

# --- a workspace that blocks write keys ---
run PROBE_TEST_LINEAR_KEY="$RKEY" PROBE_TEST_LINEAR_WRITE_KEY="$WKEY" STUB_BLOCK_WRITES=1 -- --url "$LOOP" --team ENG --write
check "blocked: exit 0" "$rc" 0
has "blocked: says so" "$out" "write.allowed: no (FORBIDDEN)"

# --- failure mid-write: the second create dies, exit 1, cleanup still archives ---
run PROBE_TEST_LINEAR_KEY="$RKEY" PROBE_TEST_LINEAR_WRITE_KEY="$WKEY" STUB_FAIL_CREATE_N=2 -- --url "$LOOP" --team ENG --write
check "mid-write failure: exit 1" "$rc" 1
has "mid-write failure: names the call" "$out" "failed: repeat issue create"
has "mid-write failure: cleanup archived" "$out" "cleanup: archived 1 of 1"
has "mid-write failure: nothing left" "$out" "cleanup.left: 0"
check "mid-write failure: read key not printed" "$(countin "$RKEY" "$out")" 0
check "mid-write failure: write key not printed" "$(countin "$WKEY" "$out")" 0

# --- a failed read call: exit 1, names it ---
run PROBE_TEST_LINEAR_KEY="$RKEY" STUB_FAIL=Comments -- --url "$LOOP"
check "failed read: exit 1" "$rc" 1
has "failed read: names it" "$out" "failed: mention scan"

# --- guards ---
run -- --url "$LOOP"
check "no key: exit 2" "$rc" 2
has "no key: says so" "$out" "no Linear key"
run PROBE_TEST_LINEAR_KEY="$RKEY" PROBE_TEST_LINEAR_WRITE_KEY="$WKEY" -- --url "$LOOP" --write
check "--write without --team: exit 2" "$rc" 2
run PROBE_TEST_LINEAR_KEY="$RKEY" PROBE_TEST_LINEAR_WRITE_KEY="$WKEY" -- --team ENG --write
check "env: write pointer with the default url: exit 2" "$rc" 2
has "env: write pointer: says why" "$out" "keychain: or op:"
check "env: write pointer: nothing sent" "$(count curl "$STUB_LOG")" 0
for u in "http://[::1]:9/graphql" "http://localhost:9/graphql" "http://127.0.0.1/graphql"; do
  run PROBE_TEST_LINEAR_KEY="$RKEY" -- --url "$u"
  check "loopback $u accepted: exit 0" "$rc" 0
done
AT="@"   # built apart so the secret scrubber does not read the userinfo url as a credential
for u in "http://127.0.0.1:80${AT}evil.com/graphql" "http://127.0.0.1.evil.com/graphql" "http://[::1]x/graphql" "http://127.0.0.1:/graphql" "http://[::1]:9x/graphql" "http://::1/graphql"; do
  run PROBE_TEST_LINEAR_KEY="$RKEY" -- --url "$u"
  check "url $u refused: exit 2" "$rc" 2
  check "url $u refused: nothing sent" "$(count curl "$STUB_LOG")" 0
done
run PROBE_TEST_LINEAR_KEY="$RKEY" -- --url https://example.com/graphql
check "non-loopback url: exit 2" "$rc" 2
run PROBE_TEST_LINEAR_KEY="$RKEY" -- --url http://api.linear.app.evil.test/graphql
check "lookalike url: exit 2" "$rc" 2
run PROBE_TEST_LINEAR_KEY="$RKEY" PROBE_TEST_LINEAR_WRITE_KEY="$RKEY" -- --url "$LOOP" --team ENG --write
check "write key equal to read key: exit 2" "$rc" 2
profile profile-nowrite.yaml
run PROBE_TEST_LINEAR_KEY="$RKEY" -- --url "$LOOP" --team ENG --write
check "--write with no write key: exit 2" "$rc" 2
run PROBE_TEST_LINEAR_KEY="$RKEY" -- --nope
check "bad flag: exit 2" "$rc" 2

if [ "$fail" -eq 0 ]; then echo "all ok"; else echo "FAILED"; exit 1; fi
