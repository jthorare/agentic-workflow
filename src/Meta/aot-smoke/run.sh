#!/usr/bin/env bash
# NativeAOT smoke for aw-meta (src/Meta/Aw.Meta.Host): publish it with PublishAot for the host RID, fail on any trim/AOT
# (IL2xxx/IL3xxx) warning or a missing bundled e_sqlite3, then run the native binary offline (a bare PATH, no gh, no network)
# against a temp AW_STATE_DIR with a fixture team config, a fixture transcript (one idle peer message, one queued_command
# attachment) and a fixture sessions directory: role-set, inbox-sync, a blocked stop-check, outbox-record on stdin, a clean
# stop-check, the owner flow (a question, owner, owner-answer), log-append, lanes, a merge-check that is refused for want of a
# gate, the version stamp (publish passes SourceRevisionId and AwBuildTime; the OS and RID are normalized), and a usage error. Every call is bounded by `timeout 30`. The normalized output (dates, times, the temp path) must
# equal expected.txt.
# AW_LOG_LEVEL=none on purpose: a log line carries a wall-clock time, and the contract is that stderr stays clean.
# Output goes to artifacts/aot-meta-smoke/ (gitignored). The publish runs on a copy of the files it needs (the root build
# files, src/Aw.Kernel, src/Aw.Kernel.Data, src/Aw.Kernel.Telemetry and src/Redaction, src/Aw.Analyzers (every production
# project loads it), src/Meta): PublishAot with a RID makes restore add the host RID's ILCompiler packs to the lock files,
# so it never runs on the checkout, whose lock files stay RID-free.
set -euo pipefail

# macOS has no `timeout`: GNU coreutils' gtimeout when installed, else perl's alarm (every macOS has perl)
if ! command -v timeout >/dev/null 2>&1; then
  if command -v gtimeout >/dev/null 2>&1; then
    timeout() { gtimeout "$@"; }
  else
    timeout() { local seconds="$1"; shift; perl -e 'alarm shift; exec @ARGV or exit 127' "$seconds" "$@"; }
  fi
fi

here="$(cd "$(dirname "$0")" && pwd)"
out="$here/../../../artifacts/aot-meta-smoke"
mkdir -p "$out"

root="$(cd "$here/../../.." && pwd)"
tree="$out/tree"
[ ! -d "$tree" ] || find "$tree" -delete
mkdir -p "$tree"
git -C "$root" ls-files -z --cached --others --exclude-standard -- global.json nuget.config .editorconfig 'Directory.*' 'BannedSymbols*.txt' CodeMetricsConfig.txt \
  src/Aw.Kernel src/Aw.Kernel.Data src/Aw.Kernel.Telemetry src/Redaction src/Aw.Analyzers src/Meta | (cd "$root" && tar --null -T - -cf -) | tar -xf - -C "$tree"
rid="$(dotnet --info | awk '/^ *RID:/ {print $2; exit}')"
dotnet publish "$tree/src/Meta/Aw.Meta.Host/Aw.Meta.Host.csproj" -c Release -r "$rid" -p:RestoreLockedMode=false \
  -p:SourceRevisionId=aot-smoke -p:AwBuildTime=2026-01-02T03:04:05Z -o "$out/bin" </dev/null >"$out/publish.log" 2>&1 || { tail -20 "$out/publish.log" >&2; exit 1; }
if grep -qE 'IL[23][0-9]{3}' "$out/publish.log"; then
  grep -E 'IL[23][0-9]{3}' "$out/publish.log" >&2
  exit 1
fi
bin="${AW_META_SMOKE_BIN:-$out/bin/aw-meta}"
if [ -z "${AW_META_SMOKE_BIN:-}" ] && ! ls "$out/bin" | grep -qE '^libe_sqlite3\.(dylib|so)$'; then
  echo "aot-meta-smoke: the bundled e_sqlite3 isn't next to the binary" >&2
  exit 1
fi

work="$(cd "$(mktemp -d "$out/run.XXXXXX")" && pwd -P)"
trap 'find "$work" -delete' EXIT
state="$work/state"
mkdir -p "$state/meta" "$work/sessions" "$work/repo"
sed "s#@WORK@#$work#g" "$here/fixtures/team.json" >"$state/meta/team.json"
sed "s#@PID@#$$#g" "$here/fixtures/sessions/session.json" >"$work/sessions/session.json"   # this script's pid is alive for the whole run
cp "$here/fixtures/transcript.jsonl" "$work/meta.jsonl"

# AW_TMUX is a fail-closed stub that records its arguments and exits 1: the smoke never reaches the real tmux, whose default
# socket is the live team's server (PATH below still has /usr/bin/tmux).
tmux_stub="$work/tmux-stub"
printf '#!/bin/sh\necho "$@" >> "%s/tmux-stub.log"\nexit 1\n' "$work" >"$tmux_stub"
chmod +x "$tmux_stub"
# Each call gets only these variables: offline, deterministic, no gh on the path, the quiet log level.
meta() { timeout 30 env -i HOME="$work" PATH=/usr/bin:/bin AW_STATE_DIR="$state" AW_CLAUDE_SESSIONS_DIR="$work/sessions" AW_TMUX="$tmux_stub" AW_LOG_LEVEL=none "$bin" "$@"; }
step() { echo "## $*"; meta "$@" || echo "exit $?"; }
stepx() { echo "## $*"; local code=0; meta "$@" || code=$?; echo "exit $code"; }
send() { printf '{"session_id":"s-meta","tool_name":"SendMessage","tool_input":{"to":"%s","message":"%s"}}' "$1" "$2"; }
{
  step role-set --session s-meta --name smoke-meta
  step inbox-sync --session s-meta --transcript "$work/meta.jsonl"
  step inbox-sync --session s-meta --transcript "$work/meta.jsonl"
  stepx stop-check --session s-meta --transcript "$work/meta.jsonl"
  echo "## outbox-record smoke-api"
  send smoke-api "merging #1 next" | meta outbox-record || echo "exit $?"
  echo "## outbox-record smoke-ui"
  send smoke-ui "rebase onto main" | meta outbox-record || echo "exit $?"
  step lanes --announce
  echo "## outbox-record the lane map to smoke-api"
  send smoke-api "$(meta lanes --announce)" | meta outbox-record || echo "exit $?"
  stepx stop-check --session s-meta --transcript "$work/meta.jsonl"
  cat "$here/fixtures/owner-question.jsonl" >>"$work/meta.jsonl"
  step inbox-sync --session s-meta --transcript "$work/meta.jsonl"
  step owner
  step owner-answer 1 --answer sqlite
  step log-append --kind note --text "smoke run"
  step queue-drop 1 --reason "smoke drop" --json
  stepx queue-drop 1 --reason "smoke again"
  step lanes
  step merge-check 1
  step merge-check
  echo "## version --json"
  meta version --json | sed -E -e 's/"os":"[^"]*"/"os":"<os>"/' -e 's/"rid":"[^"]*"/"rid":"<rid>"/' || echo "exit $?"
  step frobnicate
  echo "## log"
  cat "$work/log.md"
  echo "## state files"
  (cd "$state" && find . -type f ! -name '*-wal' ! -name '*-shm' | sort)
} >"$out/raw.txt" 2>&1
sed -E -e "s#$work#<tmp>#g" -e 's/[0-9]{4}-[0-9]{2}-[0-9]{2}/<date>/g' -e 's/<date> [0-9]{2}:[0-9]{2}/<date> <time>/g' "$out/raw.txt" >"$out/actual.txt"
if ! cmp -s "$out/actual.txt" "$here/expected.txt"; then
  diff "$here/expected.txt" "$out/actual.txt" | head -60 >&2
  exit 1
fi
echo "aot-meta-smoke: native binary passed ($(du -h "$bin" | cut -f1) aw-meta)"
