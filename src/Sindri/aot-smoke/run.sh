#!/usr/bin/env bash
# Publish smoke for sindri (src/Sindri/Aw.Sindri.Host), CI's AOT-job entry for the host (engine spec rev 8 §11, heavy
# lane: "the framework-dependent publish of the Sindri host"). The host is framework-dependent, not NativeAOT: it hosts
# the Temporal worker through Aw.Sindri.Workflows and the Temporal .NET SDK has no trimming support
# (temporalio/sdk-dotnet#286; .agents/rules/csharp.md "AOT scope"). So this publishes the host framework-dependent and
# portable (no runtime identifier, no self-contained runtime) with warnings as errors, checks that the output is
# framework-dependent and carries the Temporal SDK with its native bridge, then drives the published apphost 'sindri'
# (DOTNET_ROOT set from the 'dotnet' on PATH when unset) through the S1 and API P1 journeys against a temp home (serve
# brings up the pinned Temporal dev server, so the temporal CLI must be installed: scripts/binaries-lock.sh fetch
# temporal): doctor with nothing running, start headless with a live profile, a second serve refused by the lease,
# the removed `profile approve` (a human's and a session's both print the removal line, exit 0), status (text and --json's apiVersion), /sindri/mcp's initialize and tools/list as a stateless MCP client, then the 12 tools' calls (sindri_queue, sindri_request filed as session:mcp, sindri_item refused, sindri_decide absent),
# the views rebuild requested by a session and by the human, status's views and health lines, the item verbs (API P3: a human's item add, the queue and the item list, a session's item add, the human's item close, the done list and the queue again), the request verbs (API P4: a session's request, the needs-you list, a session's decide, the list again and the accepted requests), the ledger read (API P5: the human's lists command.handled, a session's doesn't, one item's events, and GET /events/catalog), QUERY /context/subgraph with a JSON body on the published host (POST is 405), the service and job verbs (API P6: the services list as JSON, a manual run of heartbeat followed to its end, a session's run, jobs show and the jobs list, pause and resume with a session's pause, and the services list after each; a manual run of index-sync so the last doctor's library row never depends on its 5-minute slot, #3628), a session's stop, status and doctor again (doctor with HOME on an empty scratch dir, so its
# context:mcp-scope row never reads the real provider files), and a one-node Code workflow (wf create, activate, run, run-show) whose node returns {"sum":3}, run by sindri-code-runner published into the same folder as the host (the sandbox must be able to confine it: a box that can't prints the failed run's error in the 'code-node:' line and the diff fails);
# The normalized output (pids, ports, hashes, times, the host name and the temp path
# masked) must equal expected.txt.
# The apphost, not 'dotnet sindri.dll': start re-launches its own executable as 'serve' (Environment.ProcessPath), which
# under 'dotnet sindri.dll' is the dotnet muxer.
# Callers (API spec §6): a CLI that reads the per-start token calls as a human, whatever its terminal or environment; the run
# unsets the agent variables (as sindri-prove.sh human does: CLAUDECODE, CLAUDE_CODE_ENTRYPOINT, CLAUDE_CODE_EXECPATH,
# CLAUDE_CODE_SESSION_ID, CLAUDE_PID, AW_SINDRI_CHILD, CODEX_SANDBOX, CURSOR_AGENT) and sets AW_SINDRI_PROVER=1 (the 'prover' label);
# a "(human, terminal)" step runs under a pty (scripts/lib/with-tty.sh: CRs and terminal control sequences stripped, stderr
# merged), a "(session)" step moves the token file aside for the call (without_token), with stdin from /dev/null.
# Every invocation of the binary is bounded (30 s, perl's alarm); one that runs over prints 'exit 124'.
# The publish runs on a copy of the files it needs, in a temp dir under $TMPDIR that is deleted on exit, so the
# checkout's lock files and build output are never touched.
# Kept in artifacts/aot-sindri-smoke/ (gitignored): bin/ (the publish output with the apphost), publish.log, actual.txt.
set -euo pipefail
export AW_LOG_LEVEL=none

here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../../.." && pwd)"
# shellcheck source=../../../scripts/lib/with-tty.sh
source "$root/scripts/lib/with-tty.sh"
unset CLAUDECODE CLAUDE_CODE_ENTRYPOINT CLAUDE_CODE_EXECPATH CLAUDE_CODE_SESSION_ID CLAUDE_PID AW_SINDRI_CHILD CODEX_SANDBOX CURSOR_AGENT
export AW_SINDRI_PROVER=1
out="$root/artifacts/aot-sindri-smoke"
mkdir -p "$out"
rm -f "$out/timings.txt" "$out/serve.log"
[ ! -d "$out/bin" ] || find "$out/bin" -delete
alarm=(perl -e 'alarm shift; exec @ARGV or die "exec $ARGV[0]: $!"' 30)   # SIGALRM after 30 s; a command, so with_tty can run it
bounded() { "${alarm[@]}" "$@"; }
bin="$out/bin/sindri"
scratch=""
work=""
# remember_servers records the pids control.json names (serve, and the temporal dev server it started) under $work, appending:
# control.json is replaced by every serve start and gone once serve stops, and cleanup must still reach every server this run
# started. Called right after each serve start; a missing field is skipped.
remember_servers() {
  jq -r '(.pid // empty), (.temporal.pid // empty)' "$AW_SINDRI_HOME/control.json" >>"$work/servers.pids" 2>/dev/null || true
}
# started <pid> prints the process's start time in one form whatever the TZ or LC_TIME of the caller (ps prints local time): the owner
# file and reap_stale's check both use it, so a run under another TZ than the one that wrote the file doesn't look dead.
started() { LC_ALL=C TZ=UTC ps -o lstart= -p "$1" 2>/dev/null | tr -s ' ' | sed 's/^ //;s/ $//'; }
# stop_recorded <pids-file> [<required-substring>] signals every pid listed in the file (SIGTERM, a shared wait of ~3 s, SIGKILL for survivors) only
# while its command line still names sindri or temporal, so a reused pid is left alone. serve and temporal lead their own
# session, so one that leads its own process group, not run.sh's, is signalled as a group to take its children too. cleanup
# and reap_stale share it. With a second argument the command line must also contain it (reap_stale: a pid recorded days ago may now
# be the owner's real sindri or temporal).
stop_recorded() {
  local pid cmd pgid mine targets=() sig live need="${2:-}"
  mine="$(ps -o pgid= -p $$ 2>/dev/null | tr -d ' ')"
  while read -r pid; do
    [ -n "$pid" ] || continue
    if [ -r "/proc/$pid/cmdline" ]; then cmd="$(tr '\0' ' ' <"/proc/$pid/cmdline" 2>/dev/null || true)"
    else cmd="$(ps -p "$pid" -o command= 2>/dev/null || true)"; fi
    case "$cmd" in *sindri*|*temporal*) ;; *) continue ;; esac
    case "$cmd" in *"$need"*) ;; *) continue ;; esac
    pgid="$(ps -o pgid= -p "$pid" 2>/dev/null | tr -d ' ')"
    if [ "$pgid" = "$pid" ] && [ "$pgid" != "$mine" ]; then targets+=("-$pid"); else targets+=("$pid"); fi
  done <"$1"
  for sig in TERM KILL; do
    live=0
    for pid in ${targets[@]+"${targets[@]}"}; do
      # a group target counts as live while its leader is; a zombie leader awaiting its reaper is not running
      kill -0 "${pid#-}" 2>/dev/null && live=1 && kill "-$sig" -- "$pid" 2>/dev/null || true
    done
    [ "$live" = 1 ] && [ "$sig" = TERM ] || break
    for _ in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15; do
      live=0
      for pid in "${targets[@]}"; do kill -0 "${pid#-}" 2>/dev/null && live=1; done
      [ "$live" = 0 ] && break 2
      sleep 0.2
    done
  done
}
# On any exit (a gate cancel sends SIGTERM and SIGKILLs what is left after the step's grace, 45 s by default, so this must be quick), the servers this run started are
# stopped before the scratch tree is deleted: only once this run's home is exported (never the owner's default home).
# `sindri stop` is not called: signalling the recorded pids covers serve and temporal, and a hung `stop` could eat the cancel's grace.
cleaned=0
cleanup() {
  [ -n "$scratch" ] || return 0
  [ "${cleaned:-0}" = 0 ] || return 0
  cleaned=1
  if [ "${AW_SINDRI_HOME:-}" = "$work/home" ]; then
    [ ! -f "$AW_SINDRI_HOME/logs/serve.log" ] || cp "$AW_SINDRI_HOME/logs/serve.log" "$out/serve.log"   # kept for a failing run (#3456)
    [ ! -f "$AW_SINDRI_HOME/control.json" ] || remember_servers   # a start that was never recorded
    [ -f "$work/servers.pids" ] || { find "$scratch" -delete; return 0; }
    stop_recorded "$work/servers.pids"
  fi
  find "$scratch" -delete
}
# A SIGKILLed run (a gate's hard kill) runs no trap, and serve and temporal lead their own sessions, outside run.sh's process tree,
# so the kill never reaches them: they keep running and the scratch dir stays on disk (#2116). The next run reaps them at its start.
# A scratch dir belongs to a live run while its owner file's pid is alive with the recorded start time (a reused pid has another
# start time), and such a dir is never touched, so a concurrent gate's run is safe. No owner file, no reap. Nothing here is fatal.
reap_stale() {
  local dir pid lstart recorded
  for dir in "${TMPDIR:-/tmp}"/aot-sindri-smoke.*; do
    [ -d "$dir" ] || continue
    [ "$dir" != "$scratch" ] || continue
    { { read -r pid && read -r lstart; } <"$dir/owner"; } 2>/dev/null || continue
    [ -n "$pid" ] || continue
    recorded="$(started "$pid" || true)"
    [ "$recorded" != "$lstart" ] || [ -z "$recorded" ] || continue   # alive with the same start time: a live run's dir
    [ ! -f "$dir/run/servers.pids" ] || stop_recorded "$dir/run/servers.pids" aot-sindri-smoke || true
    find "$dir" -delete 2>/dev/null || true
  done
  return 0
}
trap cleanup EXIT
trap 'cleanup; exit 143' TERM
trap 'cleanup; exit 130' INT
trap 'cleanup; exit 129' HUP
# The physical path: macOS's $TMPDIR is under /var, a link to /private/var, and restore given both spellings of one
# project writes its props twice ("…nuget.g.props already exists").
scratch="$(cd "$(mktemp -d "${TMPDIR:-/tmp}/aot-sindri-smoke.XXXXXX")" && pwd -P)"
tree="$scratch/tree"
work="$scratch/run"
printf '%s\n%s\n' "$$" "$(started $$)" >"$scratch/owner"
reap_stale
mkdir -p "$tree" "$work"
# shellcheck source=../../../scripts/lib/sindri-build-inputs.sh
source "$root/scripts/lib/sindri-build-inputs.sh"
git -C "$root" ls-files -z --cached --others --exclude-standard -- "${SINDRI_BUILD_PATHSPECS[@]}" | (cd "$root" && tar --null -T - -cf -) | tar -xf - -C "$tree"
dotnet publish "$tree/src/Sindri/Aw.Sindri.Host/Aw.Sindri.Host.csproj" -c Release --no-self-contained -warnaserror   -o "$out/bin" </dev/null >"$out/publish.log" 2>&1 || { tail -20 "$out/publish.log" >&2; exit 1; }

# The Code node's runner: published framework-dependent into the same folder, as scripts/install-cs.sh stages it, so the host finds
# 'sindri-code-runner' beside itself (AppContext.BaseDirectory) the way an installed box does. The files both publishes share are the same builds.
dotnet publish "$tree/src/Sindri/Aw.Sindri.CodeRunner/Aw.Sindri.CodeRunner.csproj" -c Release --no-self-contained -warnaserror -o "$out/bin" </dev/null >"$out/publish-code-runner.log" 2>&1 || { tail -20 "$out/publish-code-runner.log" >&2; exit 1; }
[ -e "$out/bin/sindri-code-runner" ] || { echo "aot-sindri-smoke: sindri-code-runner is missing from the publish output" >&2; exit 1; }

# Framework-dependent: the runtimeconfig names the shared framework and no runtime ships beside the host.
pub="$out/bin"
grep -q '"name": "Microsoft.AspNetCore.App"' "$pub/sindri.runtimeconfig.json" || { echo "aot-sindri-smoke: sindri.runtimeconfig.json names no shared framework" >&2; exit 1; }
[ ! -e "$pub/System.Private.CoreLib.dll" ] || { echo "aot-sindri-smoke: the publish is self-contained (System.Private.CoreLib.dll beside the host)" >&2; exit 1; }
# The Temporal SDK and its native core bridge for this platform are in the publish output.
[ -f "$pub/Temporalio.dll" ] || { echo "aot-sindri-smoke: Temporalio.dll is missing from the publish output" >&2; exit 1; }
os="$(uname -s)"; arch="$(uname -m)"
case "$os/$arch" in
  Darwin/arm64) bridge=runtimes/osx-arm64/native/libtemporalio_sdk_core_c_bridge.dylib ;;
  Darwin/x86_64) bridge=runtimes/osx-x64/native/libtemporalio_sdk_core_c_bridge.dylib ;;
  Linux/x86_64) bridge=runtimes/linux-x64/native/libtemporalio_sdk_core_c_bridge.so ;;
  Linux/aarch64) bridge=runtimes/linux-arm64/native/libtemporalio_sdk_core_c_bridge.so ;;
  *) bridge="" ;;
esac
[ -z "$bridge" ] || [ -f "$pub/$bridge" ] || { echo "aot-sindri-smoke: the Temporal core bridge $bridge is missing from the publish output" >&2; exit 1; }
[ -x "$bin" ] || { echo "aot-sindri-smoke: the apphost 'sindri' is missing from the publish output" >&2; exit 1; }
if [ -z "${DOTNET_ROOT:-}" ]; then
  DOTNET_ROOT="$(cd "$(dirname "$(perl -MCwd=abs_path -e 'print abs_path(shift)' "$(command -v dotnet)")")" && pwd)"
  export DOTNET_ROOT
fi

export AW_SINDRI_HOME="$work/home" AW_SINDRI_PORT=0 AW_SINDRI_APP_NO_MCP=1   # no automatic MCP registration: it would write a local-scope entry for the temp repo into the real ~/.claude.json
export AW_SINDRI_SKILLS_DIR="$work/no-skills"   # a folder that does not exist: the doctor's skills row says so the same way wherever the smoke runs, not by the checkout or toolkit it happens to find
mkdir -p "$AW_SINDRI_HOME/profile"
: >"$AW_SINDRI_HOME/.prover-scratch"   # the smoke runs as the prover in a home it created (setup-once spec §4 item 6): the marker lets it confirm, as sindri-prove.sh does
mkdir -p "$work/repo"   # the repo 'smoke' the mcp() helper names in X-Sindri-Repo (sindri_request files against the profile's repo)
# A real checkout with one empty commit on main, as a fresh repo on day one: context-map-refresh reads each profile repo's head every
# tick, and a plain folder fails it with SND-CONTEXT-048 (degraded health, #3256). Onboarding builds its code index (0 files, the graph
# layer "nothing to index yet") and the first map (0 nodes). The fixed dates keep the commit's sha, which the doctor prints, the same every run.
git -C "$work/repo" init -q -b main
GIT_AUTHOR_DATE='2026-01-01T00:00:00Z' GIT_COMMITTER_DATE='2026-01-01T00:00:00Z' \
  git -C "$work/repo" -c core.hooksPath=/dev/null -c user.name=smoke -c user.email=smoke@localhost -c commit.gpgsign=false commit -q --allow-empty -m smoke
printf 'schemaVersion: 1\nuser: smoke\nrepos:\n  - { name: smoke, path: %s }\nworkflow:\n  agent:\n    target: claude-code\nservices:\n  heartbeat: { every: 5s }\n' "$work/repo" >"$AW_SINDRI_HOME/profile/profile.yaml"
# Red hook for #3405 (test only): AW_SMOKE_MAP_EVERY=300s names context-map-refresh in the profile with that cadence, so the first tick (before the
# code index exists, so quiet) is followed by the next one 300 s later. The subgraph read lands ~150 s after serve starts, so the next tick must come later than that to reproduce the race. Unset, the profile is unchanged.
if [ -n "${AW_SMOKE_MAP_EVERY:-}" ]; then
  printf '  context-map-refresh: { every: %s }\n' "$AW_SMOKE_MAP_EVERY" >>"$AW_SINDRI_HOME/profile/profile.yaml"
fi

run() { # the human, no terminal
  echo "## sindri $*"
  local rc=0 t0=$EPOCHREALTIME call="$*"
  bounded "$bin" "$@" </dev/null 2>&1 || rc=$?
  exit_line "$rc"
}
human() { # the human at a terminal; stdin passed through (the typed confirmation)
  echo "## sindri $* (human, terminal)"
  local rc=0 t0=$EPOCHREALTIME call="$*"
  with_tty "${alarm[@]}" "$bin" "$@" || rc=$?
  exit_line "$rc"
}
source "$here/helpers.sh"
doctor() { # doctor reads the providers' user-wide MCP files: point it at an empty scratch home, never the real one
  HOME="$work/fakehome" CLAUDE_CONFIG_DIR="" CODEX_HOME="" run doctor
}
mcp() { # $1 = JSON-RPC method, $2 = params: one stateless /sindri/mcp call as an MCP client sends it (no Origin)
  local url
  url="$(jq -r '.url' "$AW_SINDRI_HOME/control.json")"
  curl -s -m 30 -X POST "${url%/}/sindri/mcp" -H 'Content-Type: application/json' -H 'Accept: application/json, text/event-stream' \
    -H 'X-Sindri-Repo: smoke' -d "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"$1\",\"params\":$2}" | sed -n 's/^data: //p'
}
settle() { # the projector has caught up and isn't rebuilding (it polls once a second): status's views line is then stable.
  # Bounded by wall time, not a count, so a loaded box gets the same 60 s; a timeout prints a named line into the transcript
  # (so the diff says why) instead of falling through to a snapshot taken mid-rebuild.
  local deadline=$((SECONDS + 60))
  while ((SECONDS < deadline)); do
    jq -e '.views | (.cursor == .ledgerHead and (.rebuilding | not))' <<<"$(bounded "$bin" status --json </dev/null 2>/dev/null)" >/dev/null 2>&1 && return 0
    sleep 0.25
  done
  echo "## settle: the views were still rebuilding or behind after 60 s"
}
items_in() { # $1 = state, $2 = count, $3 = seconds to wait (default 60): the items view lists that many in that state (the projector polls once a second);
  # bounded by wall time like settle, with a named line on a timeout
  local limit="${3:-60}"; local deadline=$((SECONDS + limit))
  while ((SECONDS < deadline)); do
    jq -e --argjson n "$2" '.items | length == $n' <<<"$(bounded "$bin" items --state "$1" --json </dev/null 2>/dev/null)" >/dev/null 2>&1 && return 0
    sleep 0.25
  done
  echo "## items_in: the items view didn't list $2 '$1' item(s) after ${limit} s"
}
code_node_run() { # a one-node Code workflow (manual trigger, then code returning {"sum":3}) made, activated and run through the CLI; the node's
  # result is the one line printed. The run is polled to its end by wall time like settle; a failed run (a sandbox that can't confine the
  # runner is SND-CODE-005) prints the status and the node's error in that line, so the diff says why. A verb that fails prints its own code-node line, so the diff fails.
  # activate is the owner's (workflows page spec §8): this plain call reads the per-start token, so it calls as the human and is not refused; a session is (SND-AUTH-002)
  echo "## sindri wf create, activate and run a one-node Code workflow (code-smoke), then wf run-show --json | jq (the node's result)"
  local spec="$work/code-smoke.json" run_id="" status="" shown="" deadline=$((SECONDS + 60))
  cat >"$spec" <<'JSON'
  {
    "version": 1,
    "name": "Code smoke",
    "nodes": [
      { "id": "start", "type": "manual", "config": {} },
      { "id": "sum", "type": "code", "config": { "code": "return new JsonObject { [\"sum\"] = 1 + 2 };" } }
    ],
    "edges": [ { "from": "start", "to": "sum" } ]
  }
JSON
  bounded "$bin" wf create code-smoke --file "$spec" </dev/null >/dev/null 2>&1 || { echo "code-node: wf create failed"; return 0; }
  bounded "$bin" wf activate code-smoke </dev/null >/dev/null 2>&1 || { echo "code-node: wf activate failed"; return 0; }
  run_id="$(bounded "$bin" wf run code-smoke --json </dev/null 2>/dev/null | jq -r '.run // empty')"
  [ -n "$run_id" ] || { echo "code-node: wf run started no run"; return 0; }
  while ((SECONDS < deadline)); do
    shown="$(bounded "$bin" wf run-show "$run_id" --json </dev/null 2>/dev/null || true)"
    status="$(jq -r '.status // empty' <<<"$shown" 2>/dev/null || true)"
    case "$status" in succeeded|failed|cancelled) break ;; esac
    sleep 0.5
  done
  if [ "$status" = succeeded ]; then
    printf 'code-node: %s\n' "$(jq -c '[.nodes[] | select(.node == "sum")][0].output.result' <<<"$shown")"
  else
    printf 'code-node: %s %s\n' "${status:-no status after 60 s}" "$(jq -r '[.error, ([.nodes[] | select(.node == "sum")][0].error)] | map(select(. != null)) | join("; ")' <<<"$shown" 2>/dev/null)"
  fi
}
session_tty() { # a CLI that can't read the token, at a terminal (the server decides)
  echo "## sindri $* (session, terminal)"
  local rc=0 t0=$EPOCHREALTIME call="$*"
  without_token with_tty "${alarm[@]}" "$bin" "$@" || rc=$?
  exit_line "$rc"
}
{
  doctor
  AW_LOG_LEVEL=information human --headless </dev/null   # serve logs at information into logs/serve.log (#3456)
  remember_servers
  run serve
  remember_servers
  human profile approve </dev/null   # removed: edits apply on save (exit 0)
  session_tty profile approve </dev/null
  settle
  run status
  echo "## sindri status --json | jq '{running, apiVersion}'"
  json="$({ bounded "$bin" status --json </dev/null 2>&1 || true; })"
  jq -c '{running, apiVersion}' <<<"$json" 2>/dev/null || printf '%s\n' "$json"
  echo "## /sindri/mcp initialize, then tools/list (stateless)"
  mcp initialize '{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"smoke","version":"1"}}' | jq -c '.result.serverInfo.name'
  mcp tools/list '{}' | jq -c '[.result.tools[].name]'
  echo "## /sindri/mcp sindri_queue, sindri_request (files as session:mcp), sindri_item (not an id), sindri_decide (no such tool)"
  mcp tools/call '{"name":"sindri_queue","arguments":{}}' | jq -c '.result | {isError, groups: [.structuredContent.groups[].group]}'
  mcp tools/call '{"name":"sindri_request","arguments":{"kind":"note","text":"smoke"}}' | jq -c '.result | {isError, request: .structuredContent.requestId}'
  mcp tools/call '{"name":"sindri_item","arguments":{"itemId":"nope"}}' | jq -c '.result | {isError, code: .structuredContent.code}'
  mcp tools/call '{"name":"sindri_decide","arguments":{}}' | jq -c '.result | {isError, code: .structuredContent.code}'
  session views rebuild
  human views rebuild </dev/null
  settle
  run status
  items_in new 1 120   # the setup service's one hello-sindri run files "Hello from Sindri" (spec decision 19); wait for it so the lists' order is fixed
  human item add "Smoke item" </dev/null
  items_in new 2
  run queue
  run items --state new
  session item add "Filed by a session"
  item="$(bounded "$bin" items --json </dev/null | jq -r '.items[] | select(.title == "Smoke item") | .id')"
  human item close "$item" </dev/null
  items_in done 1
  run items --state done
  run queue
  session request note "Smoke request"
  settle
  run needs-you
  decision="$(bounded "$bin" needs-you --json </dev/null | jq -r '[.rows[] | select(.title == "Smoke request")][0].subject.id')"
  session decide "$decision" accept
  settle
  run needs-you
  run requests --state accepted
  echo "## sindri ledger --json --after 0 | jq (human: command.handled is listed)"
  with_tty "${alarm[@]}" "$bin" ledger --json --after 0 </dev/null | jq -c '[.events[].type] | any(. == "command.handled")'
  echo "## sindri ledger --json --after 0 | jq (session: public types only)"
  without_token bounded "$bin" ledger --json --after 0 </dev/null | jq -c '[.events[].type] | any(. == "command.handled")'
  echo "## sindri ledger --item <closed item> --json | jq (the item's events)"
  with_tty "${alarm[@]}" "$bin" ledger --item "$item" --type 'item.*' --json </dev/null | jq -c '[.events[].type]'
  echo "## GET /sindri/events/catalog | jq (public types, their invalidates)"
  curl -s -m 30 "$(jq -r '.url' "$AW_SINDRI_HOME/control.json" | sed 's|/$||')/sindri/events/catalog" -H 'X-Sindri-Repo: smoke' \
    | jq -c '{commandHandled: any(.types[]; .type == "command.handled"), itemCreated: (.types[] | select(.type == "item.created") | .invalidates)}'
  # The smoke runs context-map-refresh itself so the map doesn't wait for the cadence (#3405). If that run comes before the code index exists it is
  # a quiet tick, and the scheduler's next tick builds the map; the poll below (cap 120 s) covers both. human_run_service retries past SND-JOB-409,
  # which the scheduler returns when its own context-map-refresh tick is in flight.
  human_run_service context-map-refresh >/dev/null 2>&1 || true
  map_deadline=$((SECONDS + 120))
  until jq -e '[.jobs[] | select(.kind == "RefreshContextMap")] | length > 0' <<<"$(bounded "$bin" jobs --state succeeded --json </dev/null 2>/dev/null)" >/dev/null 2>&1; do
    if ((SECONDS >= map_deadline)); then echo "## map wait: no RefreshContextMap job had succeeded after 120 s"; break; fi
    sleep 1
  done
  # A new current map makes a later tick queue RelinkPinAliases, once that tick has nothing else to do (#3462). Run one more tick by hand and
  # wait (cap 120 s) for the relink to succeed, so the jobs list below always holds its row instead of only when a scheduler tick won the race.
  human_run_service context-map-refresh >/dev/null 2>&1 || true
  relink_deadline=$((SECONDS + 120))
  until jq -e '[.jobs[] | select(.kind == "RelinkPinAliases")] | length > 0' <<<"$(bounded "$bin" jobs --state succeeded --json </dev/null 2>/dev/null)" >/dev/null 2>&1; do
    if ((SECONDS >= relink_deadline)); then echo "## relink wait: no RelinkPinAliases job had succeeded after 120 s"; break; fi
    sleep 1
  done
  echo "## QUERY /sindri/context/subgraph with a JSON body on the published host (the HTTP QUERY method; POST is not a route)"
  base="$(jq -r '.url' "$AW_SINDRI_HOME/control.json" | sed 's|/$||')"
  curl -s -m 30 -X QUERY "$base/sindri/context/subgraph" -H 'X-Sindri-Repo: smoke' -H 'Content-Type: application/json' -d '{"nodes":["feature:smoke.none"]}' \
    -w '\nhttp=%{http_code}\n' | jq -R -c 'if startswith("http=") then . else (try (fromjson | {title, status}) catch .) end'
  curl -s -m 30 -o /dev/null -w 'post http=%{http_code}\n' -X POST "$base/sindri/context/subgraph" -H 'X-Sindri-Repo: smoke' -H 'Content-Type: application/json' -d '{"nodes":["feature:smoke.none"]}'
  # Red hook for #3628 (test only): AW_SMOKE_WAIT_TICKS=1 waits 330 s here, past the first slot of context-sync-gaps (1 m) and index-sync (5 m), so both built-ins
  # have ticked on the profile's default context settings before the jobs list and the last doctor. Unset, the smoke runs as before.
  if [ -n "${AW_SMOKE_WAIT_TICKS:-}" ]; then sleep 330; fi
  echo "## sindri services --json | jq (name, every, paused, job)"
  bounded "$bin" services --json </dev/null | jq -c '[.services[] | {name, every, paused, job}]'
  manual_service_runs heartbeat
  # The newest heartbeat run, not jobs[0]: a SyncSources job from context-sync-gaps can land after the manual runs (#3628).
  job="$(bounded "$bin" jobs --json </dev/null | jq -r '[.jobs[] | select(.kind == "RunService" and .service == "heartbeat")][0].id')"
  run jobs show "$job"
  # The smoke's own context-map-refresh run (#3405) reads differently depending on which tick wins ("refresh JOB started" or a quiet tick's text),
  # so its row is left out of the list. So is the SyncSources row (#3628): the profile has no context section, so context-sync-gaps runs on the
  # default settings and starts that sync on whichever of its 1-minute ticks sees a feed that has not read yet. Same shape as `run`: header, output, exit line.
  echo "## sindri jobs --state succeeded"
  jobs_rc=0
  jobs_out="$(bounded "$bin" jobs --state succeeded </dev/null 2>&1)" || jobs_rc=$?
  { grep -v -e 'RunService  context-map-refresh' -e 'SyncSources  sync' <<<"$jobs_out" || true; }
  exit_line "$jobs_rc"
  # The library's first read is on index-sync's 5-minute slot, so the last doctor's library row says "first read hasn't finished" or "0 design docs"
  # by timing (#3628). Run the tick by hand, after the jobs list so its RunService row is not in it, so the row always reads "after". Before the stop: doctor reads the stored state.
  human_run_service index-sync >/dev/null 2>&1 || true
  human services pause heartbeat </dev/null
  session services pause heartbeat
  settle
  run services
  human services resume heartbeat </dev/null
  settle
  run services
  code_node_run
  session stop
  run status
  doctor
} >"$out/actual.raw"

bash "$here/normalize.sh" "$work" "$root" <"$out/actual.raw" >"$out/actual.txt"
diff -u "$here/expected.txt" "$out/actual.txt"
echo "aot-sindri-smoke: ok (framework-dependent publish, $(du -sh "$pub" | cut -f1))"
